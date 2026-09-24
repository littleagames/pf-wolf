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
        var fileLoaders = GetGamePack(releaseId).FilePack?.FileLoaders ?? [];
        var details = fileLoaders.FirstOrDefault(kvp => kvp.Key.Equals(loaderName, StringComparison.OrdinalIgnoreCase)).Value
            ?? throw new KeyNotFoundException($"'{releaseId}' in gamepacks/gamepack-info.yaml has no file-pack entry for {loaderName}");

        var file = selectFile(details)?.File;
        if (string.IsNullOrWhiteSpace(file))
            throw new KeyNotFoundException($"'{releaseId}' in gamepacks/gamepack-info.yaml is missing a file name for {loaderName}");

        return file;
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
    // "map-definitions"
    public List<string>? MapDefinitions { get; init; }
    // "game-palette"
    public string? GamePalette { get; init; }
    // "base-pack": game pack whose actordefs/mapdefs/gamepacks files this one starts from,
    // overriding them with its own
    public string? BasePack { get; init; }
    // "file-pack"
    public FilePack? FilePack { get; init; }
    // "starting-scene"
    public string? StartingScene { get; init; }
    // "game-pack-asset-reference"
    public string? GamePackAssetReference { get; init; }
}