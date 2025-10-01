namespace PFWolf.Common.Assets;

/// <summary>
/// Provides a reference to an asset in an unloaded with intention to load it
/// </summary>
public record AssetReference<T> : Asset where T : Asset
{
    public Func<T> Load { get; init; } = null!;

    public AssetReference(AssetType assetType, Func<T> load)
    {
        Type = assetType;
        Load = load;
    }
}