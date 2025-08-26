namespace PFWolf.Common.Assets;

/// <summary>
/// Provides a reference to an asset in an unloaded with intention to load it
/// </summary>
public record AssetReference<T> : Asset where T : Asset
{
    public string Path { get; init; } = null!;

    public AssetReference(string name, string path)
    {
        Name = name;
        Path = path;
    }
}