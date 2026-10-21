using PFWolf.Assets;
using PFWolf.Assets.Sounds;
using PFWolf.Loaders;

namespace PFWolf.Commands;

/// <summary>
/// What the game is made of, and hearing it: mods, assetinfo, anims, exportmap, playsound and
/// changemus
/// </summary>
internal sealed class AssetCommands(ConsoleManager console, AssetManager assets, AudioManager audio, GameEngineManager engine)
{
    internal void Register()
    {
        console.Register("mods", "Lists the mods loaded over pfwolf.pk3, in load order, anything wrong with them, and where the mod catalog is.", "mods", ModsCommand);
        console.Register("assetinfo", "Shows where an asset came from: each file that added, replaced or merged into it, in order.",
            "assetinfo <name>", AssetInfoCommand, complete: (_, i) => i == 0 ? assets.AssetNames : []);
        console.Register("anims", "Lists the animations (animdefs): what they animate, how long they run, and whether they loop; with a name, its frames too.",
            "anims [name]", AnimsCommand, complete: (_, i) => i == 0 ? assets.Animations.Keys : []);
        console.Register("playsound", "Plays a sound: a sound-seq name (doors/open), or a sound file's or digitized sound's name.",
            "playsound <name>", PlaySoundCommand);
        console.Register("changemus", "Plays a song, from a pk3's music/ folder or the game's own; with no name, shows what's playing. A song that ends stops instead of looping with 'once'.",
            "changemus [name] [once]", ChangeMusCommand, complete: (_, i) => i == 1 ? ["once"] : []);
        console.Register("exportmap", "Writes a level, or all of them, as ECWolf maps (NAME.wad) for a mod's maps/ folder: binary planes, or UWMF text with 'uwmf'; to the exports folder, or the folder given.",
            "exportmap <MAP##|all> [folder] [uwmf]", ExportMapCommand,
            complete: (_, i) => i == 0 ? ["all", .. engine.GetGameInfo().Maps.Keys] : i == 2 ? ["uwmf"] : []);
    }

    private void ModsCommand(string[] args)
    {
        var mods = assets.LoadedMods;
        if (mods.Count == 0)
            console.Print("No mods loaded");

        for (var i = 0; i < mods.Count; i++)
        {
            var mod = mods[i];
            var version = string.IsNullOrWhiteSpace(mod.Info.Version) ? "" : $" {mod.Info.Version}";
            var author = string.IsNullOrWhiteSpace(mod.Info.Author) ? "" : $" by {mod.Info.Author}";
            console.Print($"{i + 1}. {mod.DisplayName}{version}{author} ({mod.FullPath})");
        }

        foreach (var warning in assets.ModWarnings)
            console.Print(warning);
        if (Ui.ModCatalog.Url is { } catalog)
            console.Print($"More mods: {catalog}");
    }

    private void AssetInfoCommand(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: assetinfo <name>");

        var found = false;
        foreach (var (type, name, origins) in assets.FindAssetOrigins(args[0]))
        {
            found = true;
            console.Print($"{name} ({type})");
            foreach (var origin in origins)
            {
                console.Print(origin.Action == AssetOrigin.LeftOut
                    ? $"  left out, couldn't be loaded: {origin.Source}: {origin.Path}"
                    : $"  {origin.Action} by {origin.Source}: {origin.Path}");
            }
        }

        if (!found)
            console.Print($"No asset named {args[0]}");
    }

    private void AnimsCommand(string[] args)
    {
        var animations = assets.Animations
            .Where(a => args.Length == 0 || a.Key.Equals(args[0], StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (animations.Count == 0)
        {
            console.Print(args.Length == 0 ? "There are no animations (animdefs)" : $"No animation named {args[0]}");
            return;
        }

        foreach (var (name, kinds) in animations)
        {
            var first = kinds[0];
            var kindNames = string.Join(", ", kinds.Select(k => k.FrameType == typeof(TextureAsset) ? "texture" : "picture"));
            console.Print($"{name} ({kindNames}): {first.TotalTics} tics, {(first.Loops ? "loops" : "plays once")}");
            if (args.Length == 0)
                continue;

            // The frames are the same names whatever the kind
            var frames = first switch
            {
                AnimatedAsset<TextureAsset> texture => texture.Frames.Select(f => (f.Name, f.Tics)),
                AnimatedAsset<GraphicAsset> picture => picture.Frames.Select(f => (f.Name, f.Tics)),
                _ => [],
            };
            foreach (var (frame, tics) in frames)
                console.Print($"  {frame}: {tics} tics");
        }
    }

    private void ExportMapCommand(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: exportmap <MAP##|all> [folder] [uwmf]");

        var mapNames = args[0].Equals("all", StringComparison.OrdinalIgnoreCase)
            ? engine.GetGameInfo().Maps.Keys.ToList()
            : [args[0]];
        // "uwmf" last, or in place of the folder
        bool uwmf = args.Length > 1 && args[^1].Equals("uwmf", StringComparison.OrdinalIgnoreCase);
        var folderArgs = uwmf ? args[1..^1] : args[1..];
        var folder = folderArgs.Length > 0 ? folderArgs[0] : engine.ConfigDirectories.ExportsDirectory;
        Directory.CreateDirectory(folder);
        var mapdefs = assets.FindInGamePackIfAny<MapObjectTranslationAsset>("mapdefs") ?? new MapObjectTranslationAsset();

        var written = 0;
        foreach (var mapName in mapNames)
        {
            // The level as loaded, before a game changes it
            var map = assets.Find<MapAsset>(mapName);
            if (map == null)
            {
                console.Print($"No level named {mapName}");
                continue;
            }

            if (!uwmf && (map.LocalDefs != null || map.ExtraThings.Count > 0))
                console.Print($"{mapName}: its own walls, doors, flats or extra things don't fit in binary planes; export it with 'uwmf' to keep them");
            var file = uwmf
                ? EcWolfMapLoader.SaveUwmf(map, mapName.ToUpperInvariant(), mapdefs, warning => console.Print($"{mapName}: {warning}"))
                : EcWolfMapLoader.Save(map, mapName);
            File.WriteAllBytes(Path.Combine(folder, $"{mapName.ToUpperInvariant()}.wad"), file);
            written++;
        }

        console.Print($"Wrote {written} level{(written == 1 ? "" : "s")} to {Path.GetFullPath(folder)}");
    }

    private void PlaySoundCommand(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: playsound <name>");

        audio.Play(args[0]);
        console.Print(audio.IsPlaying(args[0])
            ? $"Playing {args[0]}"
            : $"{args[0]} didn't play: there's no such sound, or the sound devices it has are off");
    }

    private void ChangeMusCommand(string[] args)
    {
        if (args.Length == 0)
        {
            var playing = audio.CurrentMusicTrack;
            console.Print(string.IsNullOrEmpty(playing) ? "No music" : $"Music: {playing}{(audio.IsMusicPlaying ? "" : " (not playing)")}");
            return;
        }

        var name = args[0];
        if (!audio.HasMusic(name))
            throw new ArgumentException($"There's no song named {name}");

        audio.PlayMusic(name, loop: !(args.Length > 1 && args[1].Equals("once", StringComparison.OrdinalIgnoreCase)));
        var kind = assets.Exists<MusicFileAsset>(name) ? "music file" : "IMF";
        console.Print(audio.MusicEnabled ? $"Playing {name} ({kind})" : $"{name} ({kind}) plays when music is switched on");
    }
}
