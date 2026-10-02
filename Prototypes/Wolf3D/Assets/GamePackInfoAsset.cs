namespace Wolf3D.Assets;

internal record GamePackInfoAsset : Asset
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

    public GamePack GetGamePack(string releaseId)
        => GamePacks.TryGetValue(releaseId, out var gamePack)
            ? gamePack
            : throw new KeyNotFoundException($"No '{releaseId}' entry in gamepacks/gamepack-info.yaml");

    public override void Merge(Asset other)
    {
        // TODO: Overwrite or merge the data
    }
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
    // overriding them with its own. (Every file in mapdefs/{pack}/ is loaded; there's no list.)
    public string? BasePack { get; init; }
    // "file-pack"
    public FilePack? FilePack { get; init; }
    // "starting-scene"
    public string? StartingScene { get; init; }
    // "game-pack-asset-reference"
    public string? GamePackAssetReference { get; init; }
}