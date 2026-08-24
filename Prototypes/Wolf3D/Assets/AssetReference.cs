namespace Wolf3D.Assets;

/// <summary>
/// Provides a reference to an asset in an unloaded with intention to load it
/// </summary>
internal record AssetReference<T> : Asset where T : Asset
{
    public Func<T> Load { get; init; } = null!;

    public AssetReference(Func<T> load)
    {
        Load = load;
    }

    public override void Merge(Asset other)
    {
        // For now, do nothing
    }
}