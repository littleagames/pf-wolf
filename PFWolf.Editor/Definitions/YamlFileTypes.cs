using PFWolf.Assets;
using PFWolf.Assets.Sounds;
using PFWolf.Loaders;
using YamlDotNet.Core;

namespace PFWolf.Editor.Definitions;

/// <summary>
/// What each YAML file in pfwolf.pk3 or a mod is read as, by its path there (as
/// PfWolfPk3Loader.LoadEntry reads it), so a file can be checked as the game will read it
/// </summary>
public static class YamlFileTypes
{
    /// <summary>Files a mod keeps at its root that pfwolf.pk3 keeps in gamepacks/{pack}/</summary>
    private static readonly HashSet<string> ModRootGamePackFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "game-info.yaml", "colors.yaml", "fonts.yaml", "hud-messages.yaml", "statusbar.yaml", "intermission.yaml", "alias.yaml", "raw-data-map.yaml",
        "movies.yaml", "factions.yaml", "translations.yaml",
    };

    public static bool IsYaml(string entryPath)
        => entryPath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || entryPath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The path a file has as pfwolf.pk3's would (a mod's actordefs/x.yaml is
    /// actordefs/{pack}/x.yaml, its game-info.yaml gamepacks/{pack}/game-info.yaml)
    /// </summary>
    public static string LoadedPath(string entryPath, bool isMod, string packId = "pack")
    {
        var path = entryPath.Replace('\\', '/');
        if (!isMod)
            return path;

        var slash = path.IndexOf('/');
        if (slash < 0)
            return ModRootGamePackFiles.Contains(path) ? $"gamepacks/{packId}/{path.ToLowerInvariant()}" : path;

        var folder = path[..(slash + 1)].ToLowerInvariant();
        var rest = path[(slash + 1)..];
        return folder switch
        {
            "language/" => $"gamepacks/{packId}/language/{rest}",
            "actordefs/" or "mapdefs/" or "animdefs/" => $"{folder}{packId}/{rest}",
            _ => path,
        };
    }

    /// <summary>
    /// Where a mod keeps its own of one of pfwolf.pk3's files, to lay over it (gamepacks/wolf3d/colors.yaml
    /// is a mod's colors.yaml, actordefs/wolf3d/guards.yaml its actordefs/guards.yaml), or null with
    /// <paramref name="why"/> for a file a mod can't change
    /// </summary>
    public static string? ModPathFor(string baseEntryPath, out string why)
    {
        why = "";
        var path = baseEntryPath.Replace('\\', '/');
        var parts = path.Split('/');
        var name = parts[^1].ToLowerInvariant();

        if (path.StartsWith("menudefs/", StringComparison.OrdinalIgnoreCase))
            return $"menudefs/{parts[^1]}";         // merged over the menu by its items' and components' ids (MenuAsset.MergeYaml)
        else if (path.Equals("gamepacks/gamepack-info.yaml", StringComparison.OrdinalIgnoreCase))
            why = "A mod can't change pfwolf.pk3's games; its own gamepack-info.yaml adds games of its own (the Game packs tab's New…)";
        else if (path.StartsWith("gamepacks/", StringComparison.OrdinalIgnoreCase))
        {
            // gamepacks/{pack}/language/en-us.yaml, gamepacks/{pack}/colors.yaml
            if (parts.Length == 4 && parts[2].Equals("language", StringComparison.OrdinalIgnoreCase))
                return $"language/{parts[3]}";
            if (parts.Length == 3 && ModRootGamePackFiles.Contains(name))
                return name;
            why = $"Mods can't carry a game pack's {parts[^1]} yet";
        }
        else if (path.StartsWith("actordefs/", StringComparison.OrdinalIgnoreCase)
                 || path.StartsWith("mapdefs/", StringComparison.OrdinalIgnoreCase)
                 || path.StartsWith("animdefs/", StringComparison.OrdinalIgnoreCase))
            return $"{parts[0].ToLowerInvariant()}/{parts[^1]}";      // a pack's own, or the shared ones: flat in a mod
        else if (path.StartsWith("language/", StringComparison.OrdinalIgnoreCase) && parts.Length == 2)
            return path;
        else if (path.StartsWith("sounds/", StringComparison.OrdinalIgnoreCase) && name.Contains("sound-seq"))
            return path;
        else
            why = "The game doesn't read this file from a mod";
        return null;
    }

    /// <summary>
    /// A mod's new file laid over one of pfwolf.pk3's: empty but for a note saying how it works,
    /// or the whole of pfwolf.pk3's to trim down to what changes
    /// </summary>
    public static string ModCopyText(string baseEntryPath, string baseText, bool whole)
    {
        var newline = baseText.Contains("\r\n") ? "\r\n" : "\n";
        var note = whole
            ? $"# A copy of pfwolf.pk3's {baseEntryPath}, laid over it key by key. Take out what you{newline}"
              + $"# don't change: what's left out keeps pfwolf.pk3's (and `key: !remove` takes one out).{newline}"
            : $"# Laid over pfwolf.pk3's {baseEntryPath}, key by key: give only what you change, and{newline}"
              + $"# what's left out keeps pfwolf.pk3's (`key: !remove` takes one out).{newline}";
        return whole ? note + newline + baseText.TrimStart('﻿') : note;
    }

    /// <summary>What the file holds, in a word or two ("actor classes", "colors"), or null for one the game doesn't read</summary>
    public static string? Describe(string entryPath, bool isMod)
    {
        var path = LoadedPath(entryPath, isMod);
        var name = Path.GetFileName(path).ToLowerInvariant();
        if (isMod && !path.Contains('/'))
        {
            return name switch
            {
                "gamepack-info.yaml" => "games the mod adds",
                "modinfo.yaml" => "about the mod",
                _ => null,
            };
        }
        if (path.StartsWith("gamepacks/gamepack-info", StringComparison.OrdinalIgnoreCase))
            return "game packs";
        if (path.StartsWith("gamepacks/", StringComparison.OrdinalIgnoreCase))
        {
            if (path.Contains("/language/"))
                return "language strings";
            return name switch
            {
                "game-info.yaml" => "game info",
                "alias.yaml" => "aliases",
                "raw-data-map.yaml" => "data file names",
                "colors.yaml" => "colors",
                "fonts.yaml" => "fonts",
                "hud-messages.yaml" => "message styles",
                "factions.yaml" => "factions",
                "translations.yaml" => "color translations",
                "statusbar.yaml" => "status bar",
                "sound-seq.yaml" => "sounds",
                "intermission.yaml" => "intermission",
                "presenter.yaml" => "presenter",
                "movies.yaml" => "movies",
                "elevator.yaml" => "elevator",
                _ => null,
            };
        }
        if (path.StartsWith("language/", StringComparison.OrdinalIgnoreCase))
            return "language strings";
        if (path.StartsWith("actordefs/", StringComparison.OrdinalIgnoreCase))
            return "actor classes";
        if (path.StartsWith("mapdefs/", StringComparison.OrdinalIgnoreCase))
            return "map definitions";
        if (path.StartsWith("animdefs/", StringComparison.OrdinalIgnoreCase))
            return "animations";
        if (path.StartsWith("menudefs/", StringComparison.OrdinalIgnoreCase))
            return "menu";
        if (path.StartsWith("sounds/", StringComparison.OrdinalIgnoreCase) && name.Contains("sound-seq"))
            return "sounds";
        return null;
    }

    /// <summary>The definitions kind a file holds, or null for any other</summary>
    public static DefinitionKind? KindOf(string entryPath, bool isMod)
    {
        if (isMod && entryPath.Equals("gamepack-info.yaml", StringComparison.OrdinalIgnoreCase))
            return DefinitionKind.GamePackInfo;
        var path = LoadedPath(entryPath, isMod);
        if (path.StartsWith("gamepacks/gamepack-info", StringComparison.OrdinalIgnoreCase))
            return DefinitionKind.GamePackInfo;
        if (path.StartsWith("gamepacks/", StringComparison.OrdinalIgnoreCase) && path.Contains("game-info"))
            return DefinitionKind.GameInfo;
        if (path.StartsWith("gamepacks/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/factions.yaml", StringComparison.OrdinalIgnoreCase))
            return DefinitionKind.Factions;
        if (path.StartsWith("actordefs/", StringComparison.OrdinalIgnoreCase))
            return DefinitionKind.Actors;
        if (path.StartsWith("mapdefs/", StringComparison.OrdinalIgnoreCase))
            return DefinitionKind.MapDefs;
        if (path.StartsWith("animdefs/", StringComparison.OrdinalIgnoreCase))
            return DefinitionKind.Animations;
        return null;
    }

    /// <summary>
    /// What's wrong with a file's text, as YAML or as what the game reads it as (its !remove and
    /// !replace tags left off, as the loader reads a file on its own), or null when it reads
    /// </summary>
    public static string? Check(string entryPath, bool isMod, string text)
    {
        text = text.TrimStart('﻿');
        string yaml;
        try
        {
            var stream = new YamlDotNet.RepresentationModel.YamlStream();
            stream.Load(new StringReader(text));
            yaml = YamlTree.Parse(text) is { } tree && YamlTree.HasMergeTags(tree) ? YamlTree.ToText(YamlTree.WithoutMergeTags(tree)) : text;
        }
        catch (YamlException e)
        {
            return DefinitionKinds.Describe(e);
        }

        if (Reader(entryPath, isMod) is not { } read)
            return null;
        try
        {
            read(yaml);
            return null;
        }
        catch (YamlException e)
        {
            return DefinitionKinds.Describe(e);
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException or ArgumentException or InvalidCastException
                                      or NullReferenceException or KeyNotFoundException)
        {
            return e.Message;
        }
    }

    // How the game reads the file (the loader's own types), or null for one it doesn't
    private static Action<string>? Reader(string entryPath, bool isMod)
    {
        var path = LoadedPath(entryPath, isMod);
        var name = Path.GetFileName(path).ToLowerInvariant();
        if (isMod && !path.Contains('/'))
        {
            return name switch
            {
                "gamepack-info.yaml" => yaml => YamlDataEntryLoader.DeserializeQuietly<Dictionary<string, GamePack>>(yaml),
                "modinfo.yaml" => yaml => YamlDataEntryLoader.DeserializeQuietly<ModInfo>(yaml),
                _ => null,
            };
        }

        if (KindOf(entryPath, isMod) is { } kind)
            return yaml => DefinitionKinds.Read(kind, yaml);

        if (path.StartsWith("language/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("gamepacks/", StringComparison.OrdinalIgnoreCase) && path.Contains("/language/"))
            return yaml => YamlDataEntryLoader.DeserializeQuietly<Dictionary<string, string>>(yaml);
        if (path.StartsWith("menudefs/", StringComparison.OrdinalIgnoreCase))
            return yaml => YamlDataEntryLoader.DeserializeQuietly<MenuAsset>(yaml);
        if (path.StartsWith("sounds/", StringComparison.OrdinalIgnoreCase) && name.Contains("sound-seq"))
            return yaml => YamlDataEntryLoader.DeserializeQuietly<SoundSequenceAsset>(yaml);
        if (!path.StartsWith("gamepacks/", StringComparison.OrdinalIgnoreCase))
            return null;

        return name switch
        {
            "alias.yaml" => yaml => YamlDataEntryLoader.DeserializeQuietly<AliasAsset>(yaml),
            "raw-data-map.yaml" => yaml => YamlDataEntryLoader.DeserializeQuietly<RawDataMapAsset>(yaml),
            "colors.yaml" => yaml => YamlDataEntryLoader.DeserializeQuietly<Dictionary<string, string>>(yaml),
            "fonts.yaml" => yaml => YamlDataEntryLoader.DeserializeQuietly<Dictionary<string, FontDefinition>>(yaml),
            "hud-messages.yaml" => yaml => YamlDataEntryLoader.DeserializeQuietly<Dictionary<string, HudMessageStyleDefinition>>(yaml),
            "translations.yaml" => yaml => YamlDataEntryLoader.DeserializeQuietly<Dictionary<string, List<string>>>(yaml),
            "statusbar.yaml" => yaml => YamlDataEntryLoader.DeserializeQuietly<Dictionary<string, StatusBarElement>>(yaml),
            "sound-seq.yaml" => yaml => YamlDataEntryLoader.DeserializeQuietly<SoundSequenceAsset>(yaml),
            "intermission.yaml" => yaml => YamlDataEntryLoader.DeserializeQuietly<IntermissionAsset>(yaml),
            "presenter.yaml" => yaml => YamlDataEntryLoader.DeserializeQuietly<PresenterAsset>(yaml),
            "movies.yaml" => yaml => YamlDataEntryLoader.DeserializeQuietly<Dictionary<string, MovieInfo>>(yaml),
            "elevator.yaml" => yaml => YamlDataEntryLoader.DeserializeQuietly<ElevatorAsset>(yaml),
            _ => null,
        };
    }

    /// <summary>The line (from 1) a key path's key is on in a file's text, or 0 when it isn't there</summary>
    public static int LineOf(string text, IReadOnlyList<string> path)
    {
        try
        {
            var node = (YamlDotNet.RepresentationModel.YamlNode?)YamlText.ParseRoot(text.TrimStart('﻿'));
            int line = 0;
            foreach (var name in path)
            {
                if (node is not YamlDotNet.RepresentationModel.YamlMappingNode mapping || YamlNodes.FindKey(mapping, name) is not { } key)
                    break;
                line = (int)key.Start.Line;
                node = mapping.Children[key];
            }
            return line;
        }
        catch (Exception e) when (e is YamlException or InvalidDataException)
        {
            return 0;
        }
    }
}
