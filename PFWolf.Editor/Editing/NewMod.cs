using System.Text;
using System.Text.RegularExpressions;
using PFWolf.Managers;

namespace PFWolf.Editor.Editing;

/// <summary>What the New mod dialog asks for</summary>
/// <param name="Path">The pk3 to make</param>
/// <param name="Name">The mod's name (modinfo.yaml), and a stand-alone game's title</param>
/// <param name="GameId">A stand-alone game's id: its key in gamepack-info.yaml, which --game names it by</param>
/// <param name="BasePack">For a mod of one of PFWolf's games, that game's pack ("wolf3d"); null for a stand-alone game</param>
public sealed record NewModRequest(string Path, string Name, string GameId, string? BasePack)
{
    public bool IsStandalone => BasePack == null;
}

/// <summary>
/// A new mod as a pk3. A stand-alone game plays as it's made: it starts as a copy of examples/mods/
/// standalone-demo (its fonts, menu pictures, colors, sounds, player, walls, doors and one level,
/// embedded in the editor), with its own gamepack-info.yaml (built on `standalone`), modinfo.yaml
/// and game-info.yaml, and its own copy of Wolf3D's palette (palettes/wolfpal.pal) when one's
/// given. A mod of one of PFWolf's games is just its modinfo.yaml, naming that game.
/// </summary>
public static partial class NewMod
{
    /// <summary>The level a stand-alone game starts with</summary>
    public const string FirstMap = "MAP01";

    /// <summary>
    /// A stand-alone game's id (and its pk3's file name) made from its name: lower case, one dash
    /// for each run of spaces, anything else left out ("Bob's  Deep Sea!" -> "bobs-deep-sea")
    /// </summary>
    public static string IdFrom(string name)
    {
        var kept = NotIdCharacters().Replace(name.ToLowerInvariant(), "");
        return Gaps().Replace(kept, "-").Trim('-');
    }

    /// <summary>What's wrong with a stand-alone game's id, or null when it will do</summary>
    public static string? CheckGameId(string id)
    {
        if (!GameId().IsMatch(id))
            return "A game's id is lower case letters, digits and dashes (it's what --game names it by).";
        if (GameTypes.KnownGamePackIds.Concat(Enum.GetValues<GameType>().Select(GameTypes.GetReleaseId))
                .Append("standalone").Contains(id, StringComparer.OrdinalIgnoreCase))
            return $"{id} is one of PFWolf's own games; pick another id.";
        return null;
    }

    /// <summary>The palette a stand-alone game starts with: Wolf3D's, which standalone's game-palette names</summary>
    public const string DefaultPalette = "wolfpal";

    /// <summary>The palette's file in a pk3 (pfwolf.pk3's, and the new game's copy)</summary>
    public static string PaletteEntryPath => $"palettes/{DefaultPalette}.pal";

    /// <summary>
    /// The default palette's file from the game folder's pfwolf.pk3, for a new stand-alone game to
    /// carry its own copy of; null when it can't be read
    /// </summary>
    public static byte[]? ReadDefaultPalette(string gameFolder)
    {
        try
        {
            var source = new PFWolf.Loaders.Pk3AssetSource(System.IO.Path.Combine(gameFolder, AssetManager.BasePk3FileName));
            return source.EntryPaths.Contains(PaletteEntryPath) ? source.Open(PaletteEntryPath).ToArray() : null;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Makes the mod's pk3 (which mustn't be there already), and its folder if need be. A
    /// stand-alone game gets <paramref name="palette"/> (a palette file's bytes) as its own copy
    /// of the default palette, when there is one.
    /// </summary>
    public static void Create(NewModRequest request, byte[]? palette = null)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(request.Path))!);
        ModFiles.CreateArchive(request.Path, Files(request, palette));
    }

    /// <summary>What's wrong with where the new pk3 is to go, or null when it will do</summary>
    public static string? CheckPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "Say where the pk3 goes.";
        if (!path.EndsWith(".pk3", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return "The mod is a .pk3 (or .zip) file.";
        if (!System.IO.Path.IsPathRooted(path))
            return "Give the pk3's full path.";
        if (File.Exists(path) || Directory.Exists(path))
            return $"{System.IO.Path.GetFileName(path)} is there already; pick another name.";
        return null;
    }

    /// <summary>
    /// The files the new mod starts with, by their path in it; a stand-alone game's include
    /// <paramref name="palette"/> as palettes/wolfpal.pal when it's given
    /// </summary>
    public static List<(string EntryPath, byte[] Data)> Files(NewModRequest request, byte[]? palette = null)
    {
        var name = Quote(request.Name);
        var files = new List<(string, string)>();

        if (!request.IsStandalone)
        {
            files.Add(("modinfo.yaml",
                $"name: {name}\n" +
                "version: \"1.0\"\n" +
                $"description: \"A mod for {request.BasePack}\"\n" +
                "# The games it loads in (and the games built on them)\n" +
                $"game-packs: [{request.BasePack}]\n"));
            return files.Select(file => (file.Item1, Encoding.UTF8.GetBytes(file.Item2))).ToList();
        }

        files.Add(("modinfo.yaml",
            $"name: {name}\n" +
            "version: \"1.0\"\n" +
            "description: \"A stand-alone game\"\n"));
        files.Add(("gamepack-info.yaml",
            "# The game this mod is. Built on standalone, it needs no other game's data files, and starts\n" +
            "# from none of their definitions: its actordefs/, mapdefs/, fonts.yaml, colors.yaml and the\n" +
            "# rest all come from this mod. --game names it by its id.\n" +
            $"{request.GameId}:\n" +
            $"  title: {name}\n" +
            "  base-pack: standalone\n" +
            "  # The palette its pictures are matched to and drawn in: Wolf3D's, whose copy is in this\n" +
            "  # mod's palettes/ (the editor's palette browser edits it there)\n" +
            $"  game-palette: {DefaultPalette}\n"));
        files.Add(("game-info.yaml",
            "# The whole game-info: a stand-alone game has none to start from. See pfwolf.pk3's\n" +
            "# gamepacks/wolf3d/game-info.yaml for what each setting does.\n" +
            "default-map:\n" +
            "  ceiling-color: \"#303848\"\n" +
            "  floor-color: \"#5A5048\"\n" +
            "  # Dim, so the lamps' light shows\n" +
            "  shading:\n" +
            "    light: 150\n" +
            "    fade-start: 2\n" +
            "    fade-end: 18\n" +
            "    max-fade: 70\n" +
            "\n" +
            "# The startup info is printed in the signon picture's dark panel\n" +
            "signon:\n" +
            "  pic: SDSIGNON\n" +
            "  press-a-key: true\n" +
            "  text-area: { x: 18, y: 46, width: 284, height: 134 }\n" +
            "\n" +
            "# Until a key goes to the menu: the title, then the high scores\n" +
            "title-pics:\n" +
            "  - SDTITLE\n" +
            "title-loop:\n" +
            "  - title: true\n" +
            "    seconds: 8\n" +
            "  - high-scores: true\n" +
            "    seconds: 6\n" +
            "\n" +
            "# No music until it has some (an empty name keeps the shared menus from asking for Wolf3D's)\n" +
            "menu-music: \"\"\n" +
            "\n" +
            "# The high scores in the game's own fonts, with no pictures, and the table a new player starts with\n" +
            "high-scores:\n" +
            "  pic: \"\"\n" +
            "  headers: []\n" +
            "  labels:\n" +
            "    - { text: \"High scores\", x: 92, y: 30, font: LargeFont, color: READHCOLOR }\n" +
            "    - { text: \"Name\", x: 32, y: 62, color: READCOLOR }\n" +
            "    - { text: \"Floor\", x: 148, y: 62, color: READCOLOR }\n" +
            "    - { text: \"Score\", x: 228, y: 62, color: READCOLOR }\n" +
            "  show-episode: false\n" +
            "  defaults:\n" +
            "    - { name: \"Player\", score: 5000 }\n" +
            "    - { name: \"Player\", score: 4000 }\n" +
            "    - { name: \"Player\", score: 3000 }\n" +
            "    - { name: \"Player\", score: 2000 }\n" +
            "    - { name: \"Player\", score: 1000 }\n" +
            "\n" +
            "skills:\n" +
            "  NORMAL:\n" +
            "    name: \"Normal\"\n" +
            "\n" +
            "episodes:\n" +
            "  EP01:\n" +
            $"    name: {name}\n" +
            $"    start-map: {FirstMap}\n" +
            "\n" +
            "maps:\n" +
            $"  {FirstMap}:\n" +
            "    name: \"Level 1\"\n" +
            "    floor-number: 1\n"));

        var result = files.Select(file => (file.Item1, Encoding.UTF8.GetBytes(file.Item2))).ToList();
        result.AddRange(StarterFiles());
        if (palette != null)
            result.Add((PaletteEntryPath, palette));
        return result;
    }

    private const string StarterPrefix = "StandaloneStarter/";

    /// <summary>
    /// The example game's files a stand-alone game starts with, by their path in the mod (the
    /// editor's embedded copy of examples/mods/standalone-demo, but for the files Files writes)
    /// </summary>
    public static IEnumerable<(string EntryPath, byte[] Data)> StarterFiles()
    {
        var assembly = typeof(NewMod).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames().Where(name => name.StartsWith(StarterPrefix, StringComparison.Ordinal)).Order())
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var data = new MemoryStream();
            stream.CopyTo(data);
            yield return (resource[StarterPrefix.Length..].Replace('\\', '/'), data.ToArray());
        }
    }

    private static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    // What an id can't hold, apart from the spaces and dashes that become its dashes
    [GeneratedRegex(@"[^a-z0-9\s-]+")]
    private static partial Regex NotIdCharacters();

    [GeneratedRegex(@"[\s-]+")]
    private static partial Regex Gaps();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex GameId();
}
