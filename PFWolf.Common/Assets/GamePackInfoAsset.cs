namespace PFWolf.Assets;

/// <summary>
/// A game-packs list, as menudefs and mods give it: which packs something belongs in
/// </summary>
public static class GamePackList
{
    /// <summary>
    /// Whether a game-packs list takes in the running pack: when the list is empty or unset, when
    /// it names the pack, or when it names one of the packs it's built on (its base-pack, that
    /// one's base-pack, and so on). The nearest of those the list names decides, and "!pack"
    /// leaves one out: [wolf3d] is for Wolf3D and every pack built on it, [wolf3d, "!spear"] is
    /// for them apart from Spear and the packs built on Spear, and [wolf3d, "!spear", spear-demo]
    /// takes Spear's demo back in.
    /// </summary>
    /// <param name="basePackIds">The packs the running one is built on, nearest first</param>
    public static bool Includes(IReadOnlyCollection<string>? gamePacks, string gamePackId, IReadOnlyList<string> basePackIds)
    {
        if (gamePacks == null || gamePacks.Count == 0)
            return true;

        foreach (var pack in basePackIds.Prepend(gamePackId))
        {
            if (gamePacks.Contains(pack, StringComparer.OrdinalIgnoreCase))
                return true;
            if (gamePacks.Contains("!" + pack, StringComparer.OrdinalIgnoreCase))
                return false;
        }

        return false;
    }

    /// <summary>A list entry's pack name, without the "!" that leaves a pack out</summary>
    public static string PackName(string entry) => entry.TrimStart('!');
}

public record GamePackInfoAsset : Asset
{
    public GamePackInfoAsset(Dictionary<string, GamePack> gamePacks)
    {
        GamePacks = gamePacks;
    }

    public Dictionary<string, GamePack> GamePacks { get; init; } = [];

    /// <summary>
    /// Palette asset name (game-palette) of a game release entry, e.g. "wolf3d-apogee" -> "wolfpal"
    /// </summary>
    public string GetGamePalette(string releaseId)
    {
        var gamePack = GetGamePack(releaseId);
        if (string.IsNullOrWhiteSpace(gamePack.GamePalette))
            throw new KeyNotFoundException($"'{releaseId}' in gamepacks/gamepack-info.yaml has no game-palette");

        return gamePack.GamePalette;
    }

    /// <summary>
    /// A data file a release's file-pack gives one of its loaders,
    /// e.g. ("wolf3d-apogee", "Wolf3DAudioFileLoader", d => d.Data) -> "audiot.wl6"
    /// </summary>
    public string GetDataFile(string releaseId, string loaderName, Func<FileLoaderDetails, FileReference?> selectFile)
    {
        var file = selectFile(GetFileLoader(releaseId, loaderName))?.File;
        if (string.IsNullOrWhiteSpace(file))
            throw new KeyNotFoundException($"'{releaseId}' in gamepacks/gamepack-info.yaml is missing a file name for {loaderName}");

        return file;
    }

    /// <summary>A release's file-pack entry for one of its loaders, or null when it has none</summary>
    public FileLoaderDetails? FindFileLoader(string releaseId, string loaderName)
        => (GetGamePack(releaseId).FilePack?.FileLoaders ?? [])
            .FirstOrDefault(kvp => kvp.Key.Equals(loaderName, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>A release's file-pack entry for one of its loaders</summary>
    public FileLoaderDetails GetFileLoader(string releaseId, string loaderName)
    {
        var fileLoaders = GetGamePack(releaseId).FilePack?.FileLoaders ?? [];
        return fileLoaders.FirstOrDefault(kvp => kvp.Key.Equals(loaderName, StringComparison.OrdinalIgnoreCase)).Value
            ?? throw new KeyNotFoundException($"'{releaseId}' in gamepacks/gamepack-info.yaml has no file-pack entry for {loaderName}");
    }

    /// <summary>
    /// The release's data files that aren't in the game folder, its movies apart (it can go
    /// without them) and the levels' pair too (a pk3 can have the levels)
    /// </summary>
    public List<string> FindMissingDataFiles(string releaseId)
        => DataFiles(releaseId, includeMaps: false)
            .Select(file => file.File!)
            .Where(file => !File.Exists(file))
            .ToList();

    /// <summary>
    /// The release's data files in the game folder whose md5 isn't the one gamepack-info gives:
    /// another version of the game's files
    /// </summary>
    public List<string> FindMismatchedDataFiles(string releaseId)
        => DataFiles(releaseId, includeMaps: true)
            .Where(file => !string.IsNullOrWhiteSpace(file.Md5) && File.Exists(file.File))
            .Where(file => !Md5Of(file.File!).Equals(file.Md5!.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(file => file.File!)
            .ToList();

    /// <summary>
    /// Whether two releases' data files have the same names, so only their md5s tell them apart
    /// (wolf3d's GT files and wolf3d-apogee's)
    /// </summary>
    public bool HasSameDataFileNames(string releaseId, string otherReleaseId)
    {
        static HashSet<string> Names(IEnumerable<FileReference> files)
            => files.Select(file => file.File!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Names(DataFiles(releaseId, includeMaps: true)).SetEquals(Names(DataFiles(otherReleaseId, includeMaps: true)));
    }

    private IEnumerable<FileReference> DataFiles(string releaseId, bool includeMaps)
        => (GetGamePack(releaseId).FilePack?.FileLoaders ?? [])
            .Where(kvp => includeMaps || !kvp.Key.Equals("Wolf3DMapFileLoader", StringComparison.OrdinalIgnoreCase))
            .SelectMany(kvp => new[] { kvp.Value.Header, kvp.Value.Data, kvp.Value.Dict })
            .Where(file => !string.IsNullOrWhiteSpace(file?.File))
            .Select(file => file!);

    private static string Md5Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(System.Security.Cryptography.MD5.HashData(stream));
    }

    /// <summary>
    /// The packs a release is built on, nearest first: its base-pack, then that pack's base-pack
    /// (the release of the pack's name gives it), and so on (spear-demo -> [spear, wolf3d])
    /// </summary>
    public List<string> GetBasePackChain(string releaseId)
    {
        var chain = new List<string>();
        var basePack = GamePacks.TryGetValue(releaseId, out var gamePack) ? gamePack.BasePack : null;
        while (!string.IsNullOrWhiteSpace(basePack)
               && !basePack.Equals(releaseId, StringComparison.OrdinalIgnoreCase)
               && !chain.Contains(basePack, StringComparer.OrdinalIgnoreCase))
        {
            chain.Add(basePack);
            basePack = GamePacks.TryGetValue(basePack, out var next) ? next.BasePack : null;
        }

        return chain;
    }

    public GamePack GetGamePack(string releaseId)
        => GamePacks.TryGetValue(releaseId, out var gamePack)
            ? gamePack
            : throw new KeyNotFoundException($"No '{releaseId}' entry in gamepacks/gamepack-info.yaml");
}

public record FileReference
{
    public string? File { get; init; }
    public string? Md5 { get; init; }
}

public record FileLoaderDetails
{
    public FileReference? Header { get; init; }
    public FileReference? Data { get; init; }
    public FileReference? Dict { get; init; }
    public string? Map { get; init; }
    // "font-count": Wolf3DVgaFileLoader's number of font chunks after STRUCTPIC, which the file
    // itself doesn't record (2 when left out, as in Wolf3D and Spear; Blake Stone has 5)
    public int? FontCount { get; init; }
    // JamMovieFileLoader's movie files, by the name the game plays them by (Blake Stone's
    // IntroMovie: ianim.bs6)
    public Dictionary<string, FileReference>? Movies { get; init; }
}

public record FilePack
{
    public string? Description { get; init; }
    // "strict-md5": data files whose md5 isn't the one given are refused, for a release whose
    // other versions are laid out differently (the shareware: v1.0-1.2 aren't v1.4's layout)
    public bool StrictMd5 { get; init; }
    // Each item is a mapping from loader-type name -> details, preserving the YAML shape:
    public Dictionary<string, FileLoaderDetails> FileLoaders { get; init; } = [];
}

public record GamePack
{
    public string? Title { get; init; }
    // "game-info"
    public string? GameInfo { get; init; }
    // "game-palette"
    public string? GamePalette { get; init; }
    // "base-pack": game pack whose actordefs/mapdefs/gamepacks files this one starts from,
    // overriding them with its own; that pack's own base-pack comes before it, and so on.
    // (Every file in mapdefs/{pack}/ is loaded; there's no list.)
    public string? BasePack { get; init; }
    // "file-pack"
    public FilePack? FilePack { get; init; }
    // "starting-scene"
    public string? StartingScene { get; init; }
    // "game-pack-asset-reference"
    public string? GamePackAssetReference { get; init; }
}