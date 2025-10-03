using PFWolf.Common.Assets;

namespace PFWolf.Common.FileLoaders;
public abstract class BaseFileLoader
{
    protected readonly string Directory;

    protected BaseFileLoader(string directory)
    {
        Directory = directory;
    }

    public abstract List<KeyValuePair<string, Asset>> Load(GamePackAssetReference assetReferenceMap);
}
