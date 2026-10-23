using System.Text;
using PFWolf.Assets;
using PFWolf.Loaders;

namespace PFWolf.Managers;

/// <summary>A raw data mod's files, written but not yet saved: texts by path in the mod, and the data files to copy in</summary>
/// <param name="Texts">The YAML files and the report, by their path in the mod ("gamepack-info.yaml", "mapdefs/walls.yaml")</param>
/// <param name="DataFiles">The mod's data files (full paths), copied to the mod's root under the same names</param>
public sealed record RawDataModFiles(IReadOnlyDictionary<string, string> Texts, IReadOnlyList<string> DataFiles, string Report)
{
    /// <summary>Files that aren't text, by their path in the mod (the palette)</summary>
    public IReadOnlyDictionary<string, byte[]> Binaries { get; init; } = new Dictionary<string, byte[]>();
}

/// <summary>A palette for a raw data mod (the one its exe had), read from a palette file</summary>
/// <param name="FileName">The file it was read from, for the report</param>
/// <param name="How">How it was read ("JASC-PAL", "raw, 6-bit VGA values…")</param>
public sealed record RawDataModPalette(string FileName, PaletteColor[] Colors, string How)
{
    /// <summary>Reads a palette file (PaletteFile.Read: raw, .act, JASC-PAL, GIMP); throws InvalidDataException or IOException</summary>
    public static RawDataModPalette FromFile(string path)
    {
        var (colors, how) = PaletteFile.Read(File.ReadAllBytes(path));
        return new RawDataModPalette(Path.GetFileName(path), colors, how);
    }
}

/// <summary>
/// Writes a raw data mod from its matched files (RawDataModGenerator.Match): a gamepack-info.yaml
/// adding its own game built on the base game, with a file-pack of its data files (as shipped,
/// with md5s); a raw-data-map.yaml giving only the parts whose names differ from the base game's,
/// new chunks marked for the modder to name; alias.yaml when VGAGRAPH's chunk numbers moved;
/// mapdefs walls for wall ids the base game doesn't have; a modinfo.yaml; and a report.
/// </summary>
public static partial class RawDataModWriter
{
    /// <summary>The report's file name in the mod</summary>
    public const string ReportFileName = "raw-data-mod.txt";

    /// <summary>The comment a raw-data-map line gets for a chunk to name</summary>
    public const string NameThis = "new: name this";

    /// <param name="gameId">The mod's game's id (its pack id: settings, saves and --game go by it)</param>
    /// <param name="title">The game's title</param>
    /// <param name="previousRawDataMap">The mod's raw-data-map.yaml from the last time (run again after
    /// the files were edited): a new or changed chunk whose [hash] it gives keeps the name it has there</param>
    /// <param name="palette">The mod's own palette (its exe's), written as palettes/NAME.pal for the
    /// base game's game-palette; null keeps the base game's</param>
    public static RawDataModFiles Write(RawDataMatch match, string gameId, string title, string? previousRawDataMap = null,
        RawDataModPalette? palette = null)
    {
        var kept = previousRawDataMap == null ? [] : KeptNames(previousRawDataMap);
        match = WithKeptNames(match, kept);
        var gone = kept.Where(entry => !match.Sections.Any(section => section.Chunks.Any(chunk => Short(chunk.Hash) == entry.Key)))
            .Select(entry => entry.Value).ToList();

        var texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["modinfo.yaml"] = ModInfo(gameId, title, match),
            ["gamepack-info.yaml"] = GamePackInfo(match, gameId, title),
        };

        if (RawDataMap(match) is { } rawDataMap)
            texts["raw-data-map.yaml"] = rawDataMap;
        if (Alias(match) is { } alias)
            texts["alias.yaml"] = alias;
        var (walls, wallIds) = Walls(match);
        if (walls != null)
            texts["mapdefs/walls.yaml"] = walls;

        var binaries = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        if (palette != null)
            binaries[PaletteFile.EntryPath(match.GamePalette)] = PaletteFile.ToRaw(palette.Colors);

        var report = Report(match, gameId, title, texts.Keys.Concat(binaries.Keys), wallIds, gone, palette);
        texts[ReportFileName] = report;
        return new RawDataModFiles(texts, match.ModFiles.Files.ToList(), report) { Binaries = binaries };
    }

    /// <summary>
    /// The names a raw-data-map.yaml this wrote gives its new and changed chunks, by the [hash] at
    /// the end of their lines (as the modder may have renamed them since)
    /// </summary>
    public static Dictionary<string, string> KeptNames(string rawDataMap)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Text.RegularExpressions.Match line in KeptLine().Matches(rawDataMap))
            names.TryAdd(line.Groups["hash"].Value, Unquote(line.Groups["name"].Value));
        return names;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^\s*-\s*(?<name>""(?:[^""\\]|\\.)*""|[^\s#]+)\s*#[^\n\[]*\[(?<hash>[0-9A-Fa-f]+)\]", System.Text.RegularExpressions.RegexOptions.Multiline)]
    private static partial System.Text.RegularExpressions.Regex KeptLine();

    /// <summary>The names a raw-data-map.yaml this wrote still marks for the modder to name</summary>
    public static List<string> NamesToGive(string rawDataMap)
        => ListLine().Matches(rawDataMap)
            .Where(line => line.Groups["comment"].Value.Contains(NameThis, StringComparison.Ordinal))
            .Select(line => Unquote(line.Groups["name"].Value))
            .ToList();

    /// <summary>
    /// raw-data-map.yaml with a chunk's name changed (its line no longer marked to name); null
    /// when no line of it names <paramref name="oldName"/>
    /// </summary>
    public static string? RenameChunk(string rawDataMap, string oldName, string newName)
    {
        var renamed = false;
        var text = ListLine().Replace(rawDataMap, line =>
        {
            if (renamed || !Unquote(line.Groups["name"].Value).Equals(oldName, StringComparison.OrdinalIgnoreCase))
                return line.Value;
            renamed = true;
            var comment = line.Groups["comment"].Value.Replace(NameThis, "new", StringComparison.Ordinal);
            return $"{line.Groups["lead"].Value}{Quote(newName)}{(comment.Length > 0 ? "  " + comment : "")}";
        });
        return renamed ? text : null;
    }

    /// <summary>
    /// A definitions file (mapdefs walls.yaml) with every value that is exactly <paramref name="oldName"/>
    /// ("North: WALL0098") changed to <paramref name="newName"/>
    /// </summary>
    public static string RenameValues(string yaml, string oldName, string newName)
        => System.Text.RegularExpressions.Regex.Replace(yaml,
            $@"(?<=:[ \t]*)""?{System.Text.RegularExpressions.Regex.Escape(oldName)}""?(?=[ \t]*(#.*)?\r?$)",
            Quote(newName), System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    [System.Text.RegularExpressions.GeneratedRegex(@"^(?<lead>[ \t]*-[ \t]*)(?<name>""(?:[^""\\\r\n]|\\.)*""|[^\s#]+)[ \t]*(?<comment>#[^\r\n]*)?(?=\r?$)", System.Text.RegularExpressions.RegexOptions.Multiline)]
    private static partial System.Text.RegularExpressions.Regex ListLine();

    private static string Unquote(string name)
        => name.StartsWith('"') && name.EndsWith('"') && name.Length >= 2
            ? name[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\")
            : name;

    // The new and changed chunks named as the last raw-data-map named them, where that name isn't taken
    private static RawDataMatch WithKeptNames(RawDataMatch match, Dictionary<string, string> kept)
    {
        if (kept.Count == 0)
            return match;

        var sections = match.Sections.Select(section =>
        {
            var used = section.Chunks.Select(chunk => chunk.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var chunks = section.Chunks.Select(chunk =>
            {
                if (chunk.Match is not (ChunkMatch.New or ChunkMatch.Changed) || !kept.TryGetValue(Short(chunk.Hash), out var name)
                    || name.Equals(chunk.Name, StringComparison.OrdinalIgnoreCase) || used.Contains(name))
                    return chunk;
                used.Add(name);
                return chunk with { Name = name };
            }).ToList();
            return section with { Chunks = chunks };
        }).ToList();
        return match with { Sections = sections };
    }

    /// <summary>
    /// Saves the mod into <paramref name="modFolder"/>: its texts, and its data files when they're
    /// somewhere else. Files already there are left alone unless <paramref name="overwrite"/>; the
    /// ones that were are returned (nothing is written when any is and overwrite is off).
    /// </summary>
    public static List<string> Save(RawDataModFiles files, string modFolder, bool overwrite)
    {
        modFolder = Path.GetFullPath(modFolder);
        var copies = files.DataFiles.Where(file => !SamePath(Path.GetDirectoryName(Path.GetFullPath(file))!, modFolder)).ToList();
        string Target(string path) => Path.Combine(modFolder, path.Replace('/', Path.DirectorySeparatorChar));
        var clashes = files.Texts.Keys.Concat(files.Binaries.Keys).Select(Target)
            .Concat(copies.Select(file => Path.Combine(modFolder, Path.GetFileName(file))))
            .Where(File.Exists)
            .ToList();
        if (clashes.Count > 0 && !overwrite)
            return clashes;

        Directory.CreateDirectory(modFolder);
        foreach (var (path, text) in files.Texts)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Target(path))!);
            File.WriteAllText(Target(path), text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        foreach (var (path, data) in files.Binaries)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Target(path))!);
            File.WriteAllBytes(Target(path), data);
        }
        foreach (var file in copies)
            File.Copy(file, Path.Combine(modFolder, Path.GetFileName(file)), overwrite: true);
        return clashes;
    }

    /// <summary>A mod game id from a title or folder name: lower case, letters, digits and dashes ("My Mod!" -> "my-mod")</summary>
    public static string GameIdFrom(string name)
    {
        var id = new StringBuilder();
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
                id.Append(c);
            else if (id.Length > 0 && id[^1] != '-')
                id.Append('-');
        }
        var result = id.ToString().Trim('-');
        return result.Length == 0 ? "raw-data-mod" : result;
    }

    private static bool SamePath(string a, string b)
        => Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar).Equals(Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

    private static string ModInfo(string gameId, string title, RawDataMatch match)
        => $"""
            # Made by PFWolf's raw data mod generator from data files built on {match.BaseReleaseId}
            name: {Quote(title)}
            id: {gameId}
            game-packs: [{gameId}]

            """;

    private static string GamePackInfo(RawDataMatch match, string gameId, string title)
    {
        var text = new StringBuilder();
        text.AppendLine($"# Made by PFWolf's raw data mod generator: the game this mod adds, built on {match.BasePackId}, playing");
        text.AppendLine("# the data files in this folder (looked for here first, then in the game folder). An md5 that no");
        text.AppendLine("# longer matches its file means the file was edited again: run the generator again.");
        text.AppendLine($"{gameId}:");
        text.AppendLine($"  title: {Quote(title)}");
        text.AppendLine($"  base-pack: {match.BasePackId}");
        text.AppendLine($"  game-palette: {match.GamePalette}");
        text.AppendLine("  file-pack:");
        text.AppendLine($"    description: {Quote(title)}");
        text.AppendLine("    file-loaders:");

        string Ref(string? modFile, FileReference? baseFile)
            => modFile != null ? $"{{ file: {Quote(Path.GetFileName(modFile))}, md5: {GamePackInfoAsset.Md5Of(modFile)} }}"
                : $"{{ file: {Quote(baseFile?.File ?? "")} }}";
        FileLoaderDetails? Base(string loader)
            => match.BaseFilePack.FileLoaders.FirstOrDefault(kvp => kvp.Key.Equals(loader, StringComparison.OrdinalIgnoreCase)).Value;

        var files = match.ModFiles;
        text.AppendLine("      Wolf3DAudioFileLoader:");
        text.AppendLine($"        header: {Ref(files.AudioHed, Base("Wolf3DAudioFileLoader")?.Header)}");
        text.AppendLine($"        data: {Ref(files.AudioT, Base("Wolf3DAudioFileLoader")?.Data)}");
        text.AppendLine("      Wolf3DMapFileLoader:");
        text.AppendLine($"        header: {Ref(files.MapHead, Base("Wolf3DMapFileLoader")?.Header)}");
        text.AppendLine($"        data: {Ref(files.GameMaps, Base("Wolf3DMapFileLoader")?.Data)}");
        text.AppendLine("      Wolf3DVgaFileLoader:");
        text.AppendLine($"        dict: {Ref(files.VgaDict, Base("Wolf3DVgaFileLoader")?.Dict)}");
        text.AppendLine($"        header: {Ref(files.VgaHead, Base("Wolf3DVgaFileLoader")?.Header)}");
        text.AppendLine($"        data: {Ref(files.VgaGraph, Base("Wolf3DVgaFileLoader")?.Data)}");
        text.AppendLine($"        font-count: {match.FontCount}");
        text.AppendLine("      Wolf3DVswapFileLoader:");
        text.AppendLine($"        data: {Ref(files.Vswap, Base("Wolf3DVswapFileLoader")?.Data)}");

        // The base game's movies (Blake Stone's), the mod's own copies when it has them
        if (Base("JamMovieFileLoader")?.Movies is { Count: > 0 } movies)
        {
            text.AppendLine("      JamMovieFileLoader:");
            text.AppendLine("        movies:");
            foreach (var (name, movie) in movies)
            {
                var own = movie.File is { } file && File.Exists(Path.Combine(match.DataFolder, file));
                text.AppendLine($"          {name}: {{ file: {Quote(movie.File ?? "")} }}{(own ? "" : "   # the game folder's")}");
            }
        }

        return text.ToString();
    }

    /// <summary>The raw-data-map.yaml, giving only the lists whose names differ from the base game's; null when none do</summary>
    private static string? RawDataMap(RawDataMatch match)
    {
        var lists = new List<(string Key, List<(string Name, string? Comment)> Lines)>();
        void List(string key, params ChunkSection[] parts)
        {
            var matches = parts.Select(part => match[part]).ToList();
            if (matches.All(part => part == null || !part.Differs))
                return;

            var lines = new List<(string, string?)>();
            if (key == "graphics")
                lines.Add((match.FirstGraphicName, null));
            foreach (var (part, section) in parts.Zip(matches))
                lines.AddRange(Lines(section, part));
            lists.Add((key, lines));
        }

        List("walls", ChunkSection.Walls);
        List("sprites", ChunkSection.Sprites);
        List("digitized-audio", ChunkSection.DigitizedSounds);
        List("audio", ChunkSection.PcSounds, ChunkSection.AdLibSounds, ChunkSection.DigiSounds);
        List("music", ChunkSection.Music);
        List("graphics", ChunkSection.Fonts, ChunkSection.Pics, ChunkSection.Tile8, ChunkSection.Externs);
        List("maps", ChunkSection.Maps);
        if (lists.Count == 0)
            return null;

        var text = new StringBuilder();
        text.AppendLine($"# Made by PFWolf's raw data mod generator: names for this mod's data files, in each file's order.");
        text.AppendLine($"# Only the lists whose names differ from {match.BasePackId}'s are here; the rest are {match.BasePackId}'s.");
        text.AppendLine($"#   \"{NameThis}\": a chunk {match.BasePackId} doesn't have. Give it a name (mapdefs, actordefs and");
        text.AppendLine("#     the rest use these names), or keep the made-up one; the editor's art browser can rename it.");
        text.AppendLine($"#   \"changed\": differs from {match.BasePackId}'s chunk in its place, and keeps that one's name.");
        text.AppendLine($"#   \"moved\": the same as {match.BasePackId}'s chunk of that name, somewhere else in the file.");
        text.AppendLine("# The [hash] at the end lets the generator keep your names when it's run again.");
        foreach (var (key, lines) in lists)
        {
            text.AppendLine($"{key}:");
            foreach (var (name, comment) in lines)
                text.AppendLine(comment == null ? $"    - {Quote(name)}" : $"    - {Quote(name)}  # {comment}");
        }
        return text.ToString();
    }

    // A part's names in order: the chunks' names, then the base game's past the last chunk (empty places)
    private static IEnumerable<(string Name, string? Comment)> Lines(SectionMatch? section, ChunkSection part)
    {
        if (section == null)
            yield break;
        foreach (var chunk in section.Chunks)
            yield return (chunk.Name, Comment(chunk, part));
        for (int i = section.Chunks.Count; i < section.BaseNames.Count; i++)
            yield return (section.BaseNames[i], null);
    }

    /// <summary>Whether a chunk still has the made-up name it was given, for the modder to change</summary>
    public static bool NeedsName(NamedChunk chunk, ChunkSection section)
        => chunk.Match == ChunkMatch.New && chunk.Name.Equals(DataFileChunks.PlaceholderName(section, chunk.Index), StringComparison.OrdinalIgnoreCase);

    private static string? Comment(NamedChunk chunk, ChunkSection section) => chunk.Match switch
    {
        ChunkMatch.New when !NeedsName(chunk, section) => $"new [{Short(chunk.Hash)}]",
        ChunkMatch.New => $"{NameThis} [{Short(chunk.Hash)}]",
        ChunkMatch.Changed => $"changed [{Short(chunk.Hash)}]",
        ChunkMatch.Moved => "moved",
        _ => null,
    };

    /// <summary>The hash as raw-data-map.yaml's comments give it</summary>
    public static string Short(string? hash) => hash == null ? "" : hash[..Math.Min(16, hash.Length)];

    /// <summary>alias.yaml with its chunk numbers moved to where their pictures now are; null when none moved</summary>
    private static string? Alias(RawDataMatch match)
    {
        if (match.BaseAlias is not { ArtExtern.Count: > 0 } alias || match[ChunkSection.Pics] is not { } pics || !GraphicsDiffer(match))
            return null;

        var graphics = GraphicsNames(match);
        var text = new StringBuilder();
        text.AppendLine("# Made by PFWolf's raw data mod generator: the graphics the help and end texts' ^G and ^T commands");
        text.AppendLine("# draw, by chunk number in this mod's VGAGRAPH (its place in raw-data-map's graphics list).");
        text.AppendLine("art-extern:");
        var moved = false;
        foreach (var (number, name) in alias.ArtExtern.OrderBy(entry => entry.Key))
        {
            var index = graphics.FindIndex(graphic => graphic.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                text.AppendLine($"  # {number}: {Quote(name)}   (not in this mod's VGAGRAPH)");
                moved = true;
                continue;
            }
            moved |= index != number;
            text.AppendLine($"  {index}: {Quote(name)}");
        }
        return moved ? text.ToString() : null;
    }

    private static bool GraphicsDiffer(RawDataMatch match)
        => new[] { ChunkSection.Fonts, ChunkSection.Pics, ChunkSection.Tile8, ChunkSection.Externs }.Any(part => match[part]?.Differs == true);

    // The mod's graphics list as raw-data-map gives it (the base game's when the mod has no VGAGRAPH)
    private static List<string> GraphicsNames(RawDataMatch match)
    {
        var names = new List<string> { match.FirstGraphicName };
        foreach (var part in new[] { ChunkSection.Fonts, ChunkSection.Pics, ChunkSection.Tile8, ChunkSection.Externs })
            names.AddRange(Lines(match[part], part).Select(line => line.Name));
        return names;
    }

    /// <summary>
    /// mapdefs walls for the wall ids the mod's VSWAP has pages for that the base game's mapdefs
    /// don't list (tile n shows pages 2n-2 and 2n-1, before the door pages); null when there are none
    /// </summary>
    private static (string? Text, List<int> Ids) Walls(RawDataMatch match)
    {
        if (match[ChunkSection.Walls] is not { } walls || match.BaseMapDefs is not { } defs)
            return (null, []);

        var doorPages = RawDataModGenerator.DoorPageCount(walls.BaseNames, defs);
        var wallPages = walls.Chunks.Count - doorPages;
        var floors = defs.Floors;
        bool Taken(int id) => defs.Walls.ContainsKey(id) || defs.Doors.ContainsKey(id)
            || floors.AreaStart is { } start && floors.AreaCount is { } count && id >= start && id < start + count
            || id == floors.Ambush || id == floors.SecretExit
            || floors.Triggers?.ContainsKey(id) == true;

        var ids = new List<int>();
        var text = new StringBuilder();
        for (int id = 1; 2 * id <= wallPages && id <= MapConstants.MaxWallId; id++)
        {
            var (light, dark) = (walls.Chunks[2 * id - 2], walls.Chunks[2 * id - 1]);
            if (Taken(id) || light.Match is not (ChunkMatch.New or ChunkMatch.Moved or ChunkMatch.Changed) && dark.Match is not (ChunkMatch.New or ChunkMatch.Moved or ChunkMatch.Changed))
                continue;
            if (ids.Count == 0)
            {
                text.AppendLine("# Made by PFWolf's raw data mod generator: the wall ids this mod's VSWAP has pages for that");
                text.AppendLine($"# {match.BasePackId} doesn't use. Tile n shows wall pages 2n-2 (north and south) and 2n-1 (east and west).");
                text.AppendLine("walls:");
            }
            ids.Add(id);
            text.AppendLine($"    '{id}':");
            text.AppendLine($"        North: {Quote(light.Name)}");
            text.AppendLine($"        South: {Quote(light.Name)}");
            text.AppendLine($"        East: {Quote(dark.Name)}");
            text.AppendLine($"        West: {Quote(dark.Name)}");
        }
        return ids.Count == 0 ? (null, ids) : (text.ToString(), ids);
    }

    private static string Report(RawDataMatch match, string gameId, string title, IEnumerable<string> written, List<int> wallIds, List<string> gone,
        RawDataModPalette? palette)
    {
        var text = new StringBuilder();
        text.AppendLine($"{title} ({gameId}): a raw data mod built on {match.BasePackId} ({match.BaseReleaseId})");
        text.AppendLine();
        text.AppendLine("Data files:");
        foreach (var file in match.ModFiles.Files)
            text.AppendLine($"  {Path.GetFileName(file)}");
        text.AppendLine(match.BaseFilesFound
            ? $"Compared with {match.BasePackId}'s own data files in the game folder."
            : $"{match.BasePackId}'s own data files weren't in the game folder, so chunks are named by their place alone (\"by place\").");
        text.AppendLine();
        text.AppendLine("Part             places  same  moved  changed  by place  new  empty");
        foreach (var section in match.Sections)
            text.AppendLine($"{section.Section,-16} {section.Chunks.Count,6}  {section.CountOf(ChunkMatch.Same),4}  {section.CountOf(ChunkMatch.Moved),5}  "
                + $"{section.CountOf(ChunkMatch.Changed),7}  {section.CountOf(ChunkMatch.ByPosition),8}  {section.CountOf(ChunkMatch.New),3}  {section.CountOf(ChunkMatch.Empty),5}");
        text.AppendLine();

        var toName = match.Sections.SelectMany(section => section.Chunks.Where(chunk => NeedsName(chunk, section.Section))).Select(chunk => chunk.Name).ToList();
        if (toName.Count > 0)
            text.AppendLine($"To name in raw-data-map.yaml ({toName.Count}): {string.Join(", ", toName)}");
        if (gone.Count > 0)
            text.AppendLine($"No longer in the data files (named last time): {string.Join(", ", gone)}");
        if (match[ChunkSection.Maps] is { } maps)
        {
            var changed = maps.Chunks.Where(chunk => chunk.Match is ChunkMatch.Changed or ChunkMatch.New).Select(chunk => chunk.Name).ToList();
            if (changed.Count > 0)
                text.AppendLine($"Levels that differ from {match.BasePackId}'s: {string.Join(", ", changed)}");
        }
        if (wallIds.Count > 0)
            text.AppendLine($"New wall ids in mapdefs/walls.yaml: {string.Join(", ", wallIds)}");
        if (palette != null)
            text.AppendLine($"Palette: {PaletteFile.EntryPath(match.GamePalette)} (in place of {match.BasePackId}'s {match.GamePalette}), from {palette.FileName} ({palette.How})");
        text.AppendLine();
        text.AppendLine("Written: " + string.Join(", ", written.Order(StringComparer.OrdinalIgnoreCase)));
        text.AppendLine();
        text.AppendLine("Not in the data files, so still the base game's until you change them in game-info.yaml or actordefs: "
            + (palette == null ? "the palette (a palette file given here puts the mod's own in), " : "")
            + "level music, par times, ceiling colors, episode and level names, and any new enemies, items or wall "
            + "behaviour the mod's exe had.");
        return text.ToString();
    }

    // A YAML scalar, quoted when it needs to be
    private static string Quote(string value)
        => value.Length > 0 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.' or ' ') && !value.StartsWith(' ') && !value.EndsWith(' ')
           && !bool.TryParse(value, out _) && !double.TryParse(value, out _) && value is not ("null" or "yes" or "no" or "on" or "off" or "~")
            ? value
            : "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
