using PFWolf.Assets;
using PFWolf.Loaders;

namespace PFWolf.Managers;

/// <summary>How a raw data mod's chunk got its name</summary>
public enum ChunkMatch
{
    /// <summary>The same as the base game's chunk in the same place</summary>
    Same,
    /// <summary>The same as one of the base game's chunks, in another place: that one's name</summary>
    Moved,
    /// <summary>Different from the base game's chunk in its place, whose name it keeps (a replaced texture)</summary>
    Changed,
    /// <summary>In the place of a named base chunk, with the base game's files not there to compare: named by its place</summary>
    ByPosition,
    /// <summary>Past the base game's chunks (or its place's name went to a moved one): a placeholder name to fill in</summary>
    New,
    /// <summary>A place with no data: named as the base game names it, or a placeholder past its names</summary>
    Empty,
}

/// <summary>A raw data mod's chunk, with the name it gets and why</summary>
/// <param name="BaseIndex">The base game's chunk it's the same as or takes the name of; null for a new one</param>
public sealed record NamedChunk(int Index, string Name, ChunkMatch Match, string? Hash, int? BaseIndex);

/// <summary>One part of a raw data mod's files, named, and the base game's names for it</summary>
public sealed record SectionMatch(ChunkSection Section, IReadOnlyList<string> BaseNames, IReadOnlyList<NamedChunk> Chunks)
{
    /// <summary>Whether the names differ from the base game's, so the mod's raw-data-map has to give them</summary>
    public bool Differs => Chunks.Count > BaseNames.Count
        || Chunks.Any(chunk => !chunk.Name.Equals(BaseNames[chunk.Index], StringComparison.OrdinalIgnoreCase));

    public int CountOf(ChunkMatch match) => Chunks.Count(chunk => chunk.Match == match);
}

/// <summary>A base game a raw data mod's files may be built on, and how well they match it</summary>
/// <param name="Share">Of the mod's chunks, the share the same as one of the base game's (0 to 1); null with the base game's files not there</param>
/// <param name="NamesMatched">How many of the mod's data files have the base game's own file names (vswap.wl6)</param>
/// <param name="Sniffed">How many were told by what's in them, not by their names (FOXTROT1.WL6): worth checking</param>
public sealed record BaseCandidate(string PackId, string ReleaseId, string Title, double? Share, int NamesMatched, DataFileSet ModFiles, int Sniffed = 0);

/// <summary>
/// The raw data mod generator (docs/dev/raw-data-mods-plan.md): reads a folder of data files
/// edited by WDC or ChaosEdit, compares each chunk with the base game's, and names it. A chunk the
/// base game has keeps that name wherever it now is, one in a named place keeps the place's name
/// (a replaced texture), and the rest get placeholders for the modder to fill in.
/// </summary>
public static class RawDataModGenerator
{
    /// <summary>The data file loaders' file kinds, by the names a release's files have without their extension</summary>
    private static readonly (DataFileKind Kind, string Loader, string Part, string[] Stems)[] FileKinds =
    [
        (DataFileKind.Vswap, "Wolf3DVswapFileLoader", "data", ["VSWAP"]),
        (DataFileKind.VgaDict, "Wolf3DVgaFileLoader", "dict", ["VGADICT"]),
        (DataFileKind.VgaHead, "Wolf3DVgaFileLoader", "header", ["VGAHEAD"]),
        (DataFileKind.VgaGraph, "Wolf3DVgaFileLoader", "data", ["VGAGRAPH"]),
        (DataFileKind.AudioHed, "Wolf3DAudioFileLoader", "header", ["AUDIOHED"]),
        (DataFileKind.AudioT, "Wolf3DAudioFileLoader", "data", ["AUDIOT"]),
        (DataFileKind.MapHead, "Wolf3DMapFileLoader", "header", ["MAPHEAD"]),
        (DataFileKind.GameMaps, "Wolf3DMapFileLoader", "data", ["GAMEMAPS", "MAPTEMP"]),
    ];

    /// <summary>
    /// The games the files in <paramref name="dataFolder"/> may be built on, best first: PFWolf's own
    /// releases whose data files have the same names (VSWAP.*...), ranked by how many of the mod's
    /// chunks are the same as theirs (when their files are in the game folder), then by how many
    /// file names match theirs exactly. The working folder must be the game folder (pfwolf.pk3).
    /// </summary>
    /// <param name="files">Which file is which, as the modder said (Which file is which…); null:
    /// by their names, else by what's in them</param>
    public static List<BaseCandidate> DetectBase(string dataFolder, DataFileSet? files = null)
    {
        var info = ReadGamePackInfo();
        var modChunks = new Dictionary<int, DataFileChunks?>();
        var candidates = new List<BaseCandidate>();
        foreach (var type in Enum.GetValues<GameType>())
        {
            var release = GameTypes.GetReleaseId(type);
            if (!info.GamePacks.TryGetValue(release, out var pack) || info.GetFilePack(release) is not { } filePack)
                continue;

            var sniffed = 0;
            var modFiles = files ?? FindDataFiles(dataFolder, filePack, out sniffed);
            if (!modFiles.Files.Any())
                continue;
            var namesMatched = NamesMatched(modFiles, filePack);

            double? share = null;
            var baseFiles = BaseDataFiles(filePack);
            if (baseFiles.Files.Any())
            {
                var fontCount = FontCount(info, release);
                if (!modChunks.TryGetValue(fontCount, out var mod))
                    modChunks[fontCount] = mod = TryRead(modFiles, fontCount);
                var stock = TryRead(baseFiles, fontCount);
                if (mod != null && stock != null)
                {
                    var all = mod.Sections.SelectMany(section => mod[section]).ToList();
                    var hashes = stock.Sections.SelectMany(section => stock[section]).Select(chunk => chunk.Hash).ToHashSet();
                    share = all.Count == 0 ? 0 : all.Count(chunk => hashes.Contains(chunk.Hash)) / (double)all.Count;
                }
            }

            candidates.Add(new BaseCandidate(GameTypes.GetGamePackId(type), release,
                filePack.Description ?? pack.Title ?? release, share, namesMatched, modFiles, sniffed));
        }

        return candidates
            .OrderByDescending(candidate => candidate.Share ?? -1)
            .ThenByDescending(candidate => candidate.NamesMatched)
            .ToList();
    }

    /// <summary>
    /// The mod's data files a release's file-pack would read, by its file names in
    /// <paramref name="dataFolder"/>, else the same names with another extension (VSWAP.MOD), else
    /// (FOXTROT1.WL6) by what's in them (DataFileSniffer); null for one the folder doesn't have
    /// (the game folder's is played)
    /// </summary>
    /// <param name="sniffed">How many were told by what's in them, not by their names: the modder should check those</param>
    public static DataFileSet FindDataFiles(string dataFolder, FilePack filePack, out int sniffed)
    {
        var files = Directory.Exists(dataFolder) ? Directory.GetFiles(dataFolder) : [];
        var found = new DataFileSet();
        foreach (var (kind, loader, part, stems) in FileKinds)
        {
            var name = FileName(filePack, loader, part);
            var match = files.FirstOrDefault(file => name != null && Path.GetFileName(file).Equals(name, StringComparison.OrdinalIgnoreCase))
                        ?? files.FirstOrDefault(file => stems.Contains(Path.GetFileNameWithoutExtension(file), StringComparer.OrdinalIgnoreCase));
            found = found.With(kind, match);
        }

        // The kinds no file is named for, from the files no kind took
        sniffed = 0;
        var left = files.Except(found.Files, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var (kind, file) in DataFileSniffer.Guess(left))
        {
            if (found[kind] != null)
                continue;
            found = found.With(kind, file);
            sniffed++;
        }
        return found;
    }

    /// <summary>The files a folder of data files has, as Which file is which… lists them: by name, with their sizes</summary>
    public static List<(string File, long Size)> FolderFiles(string dataFolder)
        => Directory.Exists(dataFolder)
            ? Directory.GetFiles(dataFolder).Order(StringComparer.OrdinalIgnoreCase).Select(file => (file, new FileInfo(file).Length)).ToList()
            : [];

    // How many of the mod's files have the release's own names (vswap.wl6)
    private static int NamesMatched(DataFileSet files, FilePack filePack)
        => FileKinds.Count(kind => files[kind.Kind] is { } file && FileName(filePack, kind.Loader, kind.Part) is { } name
                                   && Path.GetFileName(file).Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Names every chunk of the mod's files against the base game's: its raw-data-map, its doors
    /// (the last wall pages), and its own data files in the game folder when they're there. The
    /// working folder must be the game folder.
    /// </summary>
    /// <param name="files">Which file is which, as the modder said; null: FindDataFiles</param>
    public static RawDataMatch Match(string dataFolder, string baseReleaseId, DataFileSet? files = null)
    {
        var info = ReadGamePackInfo();
        var filePack = info.GetFilePack(baseReleaseId)
            ?? throw new ArgumentException($"'{baseReleaseId}' has no data files to build a raw data mod on");
        var packId = PackIdOf(baseReleaseId);

        var defs = new AssetManager();
        defs.Load(packId, baseReleaseId, [], definitionsOnly: true);
        var rawDataMap = defs.FindInGamePackIfAny<RawDataMapAsset>("raw-data-map") ?? new RawDataMapAsset();
        var mapDefs = defs.FindInGamePackIfAny<MapObjectTranslationAsset>("mapdefs");

        var fontCount = FontCount(info, baseReleaseId);
        var modFiles = files ?? FindDataFiles(dataFolder, filePack, out _);
        var mod = DataFileChunks.Read(modFiles, fontCount);
        var baseFiles = BaseDataFiles(filePack);
        var stock = TryRead(baseFiles, fontCount);

        var baseNames = SplitNames(rawDataMap, fontCount);
        var sections = new List<SectionMatch>();
        foreach (var (section, names) in baseNames)
        {
            if (!HasFileFor(modFiles, section))
                continue;
            var tail = section == ChunkSection.Walls ? DoorPageCount(names, mapDefs) : 0;
            var chunks = MatchSection(section, names, stock?[section], mod[section], mod.Count(section), tail);
            sections.Add(new SectionMatch(section, names, chunks));
        }

        return new RawDataMatch(packId, baseReleaseId, info.GetGamePack(baseReleaseId), filePack, modFiles,
            stock != null, sections, rawDataMap.Graphics.FirstOrDefault() ?? "STRUCTPIC", fontCount)
        {
            DataFolder = Path.GetFullPath(dataFolder),
            GamePalette = info.GetGamePalette(baseReleaseId),
            BaseAlias = defs.FindInGamePackIfAny<AliasAsset>("alias"),
            BaseMapDefs = mapDefs,
        };
    }

    /// <summary>
    /// Names one part's chunks. The last <paramref name="tailNames"/> base names go to the last
    /// places, wherever those now are (Wolf3D's doors: the exe takes the last 8 wall pages). Then a
    /// chunk the same as a base chunk takes its name (the one in its own place first); then a
    /// chunk in a place the base game names takes that name, unless it went to a moved chunk;
    /// anything else gets a placeholder (DataFileChunks.PlaceholderName).
    /// </summary>
    /// <param name="baseChunks">The base game's chunks, to compare with; null when its files aren't there</param>
    /// <param name="count">How many places the mod's part has (one past its last chunk with data)</param>
    public static List<NamedChunk> MatchSection(ChunkSection section, IReadOnlyList<string> baseNames,
        IReadOnlyList<DataChunk>? baseChunks, IReadOnlyList<DataChunk> modChunks, int count, int tailNames = 0)
    {
        var modHash = modChunks.ToDictionary(chunk => chunk.Index, chunk => chunk.Hash);
        var baseHash = baseChunks?.ToDictionary(chunk => chunk.Index, chunk => chunk.Hash);
        var byHash = (baseChunks ?? []).GroupBy(chunk => chunk.Hash).ToDictionary(group => group.Key, group => group.Select(chunk => chunk.Index).ToList());
        var named = new NamedChunk?[count];
        var used = new HashSet<int>();

        NamedChunk Take(int index, int baseIndex)
        {
            used.Add(baseIndex);
            var hash = modHash.GetValueOrDefault(index);
            var match = hash == null ? ChunkMatch.Empty
                : baseHash == null || !baseHash.TryGetValue(baseIndex, out var stockHash) ? ChunkMatch.ByPosition
                : stockHash != hash ? ChunkMatch.Changed
                : index == baseIndex ? ChunkMatch.Same
                : ChunkMatch.Moved;
            return new NamedChunk(index, baseNames[baseIndex], match, hash, baseIndex);
        }

        // The tail (doors), when the part grew or shrank: the last places take the last names
        tailNames = Math.Min(tailNames, Math.Min(count, baseNames.Count));
        if (count != baseNames.Count)
            for (int k = 0; k < tailNames; k++)
                named[count - tailNames + k] = Take(count - tailNames + k, baseNames.Count - tailNames + k);

        // The same as a base chunk: its name, the one in its own place first
        foreach (var chunk in modChunks)
        {
            if (chunk.Index >= count || named[chunk.Index] != null || !byHash.TryGetValue(chunk.Hash, out var indices))
                continue;
            var free = indices.Where(index => index < baseNames.Count && !used.Contains(index)).ToList();
            if (free.Count > 0)
                named[chunk.Index] = Take(chunk.Index, free.Contains(chunk.Index) ? chunk.Index : free[0]);
        }

        // A named place: its name, unless a moved chunk took it; else a placeholder
        for (int i = 0; i < count; i++)
        {
            if (named[i] != null)
                continue;
            if (i < baseNames.Count && !used.Contains(i) && !(count != baseNames.Count && i >= baseNames.Count - tailNames))
                named[i] = Take(i, i);
            else
            {
                var hash = modHash.GetValueOrDefault(i);
                named[i] = new NamedChunk(i, DataFileChunks.PlaceholderName(section, i), hash == null ? ChunkMatch.Empty : ChunkMatch.New, hash, null);
            }
        }

        return named.Select(chunk => chunk!).ToList();
    }

    /// <summary>
    /// raw-data-map's lists by part: graphics split into fonts, pics, Tile8 and externs around its
    /// Tile8 name, and AUDIOT's sounds into its PC (PC*), AdLib (AL*) and digitized (DS*) names
    /// </summary>
    public static List<(ChunkSection Section, IReadOnlyList<string> Names)> SplitNames(RawDataMapAsset map, int fontCount)
    {
        var graphics = map.Graphics;
        var tile8 = graphics.FindIndex(name => name.Equals("Tile8", StringComparison.OrdinalIgnoreCase));
        var fontsEnd = Math.Min(graphics.Count, 1 + fontCount);
        var picsEnd = tile8 >= 0 ? tile8 : graphics.Count;
        List<string> Sounds(string prefix) => map.Audio.Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

        return
        [
            (ChunkSection.Walls, map.Walls),
            (ChunkSection.Sprites, map.Sprites),
            (ChunkSection.DigitizedSounds, map.DigitizedAudio),
            (ChunkSection.Fonts, graphics.Skip(1).Take(fontsEnd - 1).ToList()),
            (ChunkSection.Pics, graphics.Skip(fontsEnd).Take(Math.Max(0, picsEnd - fontsEnd)).ToList()),
            (ChunkSection.Tile8, tile8 >= 0 ? [graphics[tile8]] : ["Tile8"]),
            (ChunkSection.Externs, tile8 >= 0 ? graphics.Skip(tile8 + 1).ToList() : []),
            (ChunkSection.PcSounds, Sounds("PC")),
            (ChunkSection.AdLibSounds, Sounds("AL")),
            (ChunkSection.DigiSounds, Sounds("DS")),
            (ChunkSection.Music, map.Music),
            (ChunkSection.Maps, map.Maps),
        ];
    }

    /// <summary>
    /// How many of the last wall names are door faces (the exe's DOORWALL: the last wall pages, 8
    /// in Wolf3D and Spear), counted back from the end while doors.yaml uses them
    /// </summary>
    public static int DoorPageCount(IReadOnlyList<string> wallNames, MapObjectTranslationAsset? mapDefs)
    {
        var doorFaces = (mapDefs?.Doors.Values ?? Enumerable.Empty<MapTextureTranslation>())
            .SelectMany(door => new[] { door.North, door.South, door.East, door.West })
            .Where(face => !string.IsNullOrEmpty(face))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var count = 0;
        while (count < wallNames.Count && doorFaces.Contains(wallNames[wallNames.Count - 1 - count]))
            count++;
        return count;
    }

    /// <summary>
    /// The palette file a folder of data files comes with (a .pal, .act or .gpl that reads as a
    /// palette), when it has just one; null otherwise
    /// </summary>
    public static string? FindPaletteFile(string dataFolder)
    {
        if (!Directory.Exists(dataFolder))
            return null;
        var palettes = Directory.GetFiles(dataFolder).Where(PaletteFile.HasPaletteExtension).Where(file =>
        {
            try
            {
                PaletteFile.Read(File.ReadAllBytes(file));
                return true;
            }
            catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }).ToList();
        return palettes.Count == 1 ? palettes[0] : null;
    }

    /// <summary>
    /// A BaseCandidate's share as a person reads it: "100%" only when every chunk is the same
    /// ("over 99%" for two new walls out of 600, which would round up to it)
    /// </summary>
    public static string ShareText(double share)
        => share >= 1 ? "100%" : share >= 0.99 ? "over 99%" : $"{Math.Floor(share * 100)}%";

    /// <summary>PFWolf's own pack id of a release ("blake-aog" -> "blake")</summary>
    public static string PackIdOf(string releaseId)
        => Enum.GetValues<GameType>().Where(type => GameTypes.GetReleaseId(type) == releaseId).Select(GameTypes.GetGamePackId).FirstOrDefault()
           ?? releaseId;

    private static bool HasFileFor(DataFileSet files, ChunkSection section) => section switch
    {
        ChunkSection.Walls or ChunkSection.Sprites or ChunkSection.DigitizedSounds => files.Vswap != null,
        ChunkSection.Fonts or ChunkSection.Pics or ChunkSection.Tile8 or ChunkSection.Externs => files.VgaGraph != null,
        ChunkSection.PcSounds or ChunkSection.AdLibSounds or ChunkSection.DigiSounds or ChunkSection.Music => files.AudioT != null,
        ChunkSection.Maps => files.GameMaps != null,
        _ => false,
    };

    private static GamePackInfoAsset ReadGamePackInfo()
        => PfWolfPk3Loader.ReadGamePackInfo(new Pk3AssetSource(AssetManager.BasePk3FileName))
           ?? throw new KeyNotFoundException($"gamepacks/gamepack-info.yaml is missing from {AssetManager.BasePk3FileName}");

    private static int FontCount(GamePackInfoAsset info, string releaseId)
        => info.FindFileLoader(releaseId, "Wolf3DVgaFileLoader")?.FontCount ?? 2;

    private static string? FileName(FilePack filePack, string loader, string part)
    {
        var details = filePack.FileLoaders.FirstOrDefault(kvp => kvp.Key.Equals(loader, StringComparison.OrdinalIgnoreCase)).Value;
        return part switch
        {
            "header" => details?.Header?.File,
            "dict" => details?.Dict?.File,
            _ => details?.Data?.File,
        };
    }

    // The base game's own data files in the game folder (the working folder), those that are there
    private static DataFileSet BaseDataFiles(FilePack filePack)
    {
        string? There(string loader, string part) => FileName(filePack, loader, part) is { } name && File.Exists(name) ? Path.GetFullPath(name) : null;
        return new DataFileSet(
            There("Wolf3DVswapFileLoader", "data"),
            There("Wolf3DVgaFileLoader", "dict"), There("Wolf3DVgaFileLoader", "header"), There("Wolf3DVgaFileLoader", "data"),
            There("Wolf3DAudioFileLoader", "header"), There("Wolf3DAudioFileLoader", "data"),
            There("Wolf3DMapFileLoader", "header"), There("Wolf3DMapFileLoader", "data"));
    }

    // The base game's files can be another release's that won't read with these settings
    private static DataFileChunks? TryRead(DataFileSet files, int fontCount)
    {
        if (!files.Files.Any())
            return null;
        try
        {
            return DataFileChunks.Read(files, fontCount);
        }
        catch (DataFileReadException)
        {
            return null;
        }
    }
}

/// <summary>A raw data mod's files named against a base game: what RawDataModGenerator.Write writes the mod from</summary>
/// <param name="BaseFilesFound">Whether the base game's data files were there to compare with (else names go by place alone)</param>
/// <param name="FirstGraphicName">raw-data-map's name for VGAGRAPH's first chunk (STRUCTPIC)</param>
public sealed record RawDataMatch(string BasePackId, string BaseReleaseId, GamePack BasePack, FilePack BaseFilePack, DataFileSet ModFiles,
    bool BaseFilesFound, IReadOnlyList<SectionMatch> Sections, string FirstGraphicName, int FontCount)
{
    public SectionMatch? this[ChunkSection section] => Sections.FirstOrDefault(match => match.Section == section);

    /// <summary>The folder the mod's data files were read from</summary>
    public string DataFolder { get; init; } = "";

    /// <summary>The base game's game-palette</summary>
    public string GamePalette { get; init; } = "";

    /// <summary>The base game's alias.yaml, whose chunk numbers move when pics are put in</summary>
    public AliasAsset? BaseAlias { get; init; }

    /// <summary>The base game's mapdefs: its wall ids, doors and floor codes</summary>
    public MapObjectTranslationAsset? BaseMapDefs { get; init; }
}
