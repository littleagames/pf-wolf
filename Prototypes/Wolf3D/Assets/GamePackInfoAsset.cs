namespace Wolf3D.Assets;

internal record GamePackInfoAsset : Asset
{
    public GamePackInfoAsset(Dictionary<string, GamePack> gamePacks)
    {
        GamePacks = gamePacks;
    }

    public Dictionary<string, GamePack> GamePacks { get; init; } = [];

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
    // "file-pack"
    public FilePack? FilePack { get; init; }
    // "starting-scene"
    public string? StartingScene { get; init; }
    // "game-pack-asset-reference"
    public string? GamePackAssetReference { get; init; }
}