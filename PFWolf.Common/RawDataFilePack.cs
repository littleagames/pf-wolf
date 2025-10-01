using PFWolf.Common.Assets;
using PFWolf.Common.Loaders;

namespace PFWolf.Common;

public record DataPackFile(string File, string Md5);

public record DataPackFileLoader(Type FileLoader, params string[] Files);


public abstract class RawDataFilePack
{
    public Guid Id = Guid.NewGuid();

    public abstract string PackName { get; }

    public abstract string PackDescription { get; }

    // TODO: Introduce this when you add more game palettes into pfwolf.pk3
    //public abstract string DefaultPaletteAssetName { get; }

    protected abstract List<DataPackFile> Files { get; }
    protected abstract List<DataPackFileLoader> FileLoaders { get; }

    public string? FindHashByFile(string file)
    {
        var result = Files.FirstOrDefault(x => x.File == file);
        return result?.Md5;
    }

    public string? FindFileByHash(string hash)
    {
        var result = Files.FirstOrDefault(x => x.Md5 == hash);
        return result?.File;
    }

    public bool Validate(List<string> files)
    {
        return Files.All(x => files.Any(f => f.Equals(x.File, StringComparison.InvariantCultureIgnoreCase)));
    }

    public IEnumerable<DataFile> GetDataFiles(List<DataFile> foundFiles)
    {
        var dataFiles = new List<DataFile>();
        foreach (var dataPackFile in Files)
        {
            var gameFile = foundFiles.FirstOrDefault(x => x.Md5.Equals(dataPackFile.Md5));
            if (gameFile == null)
            {
                throw new InvalidDataException($"Missing file: {dataPackFile.File} from found files.");
            }

            yield return gameFile;
        }
    }

    public T GetLoader<T>(string directory) where T : BaseFileLoader
    {
        var loader = FileLoaders.FirstOrDefault(x => x.FileLoader == typeof(T));
        return (T)InstantiateLoader(directory, loader);
    }

    private BaseFileLoader InstantiateLoader(string directory, DataPackFileLoader? loader)
    {
        if (loader == null)
            throw new InvalidOperationException($"Failed to instantiate loader.");

        try
        {
            List<object?> argsList = [directory];
            argsList.AddRange(loader.Files);
            BaseFileLoader fileLoader = Activator.CreateInstance(loader.FileLoader, argsList.ToArray()) as BaseFileLoader;
            if (fileLoader == null)
            {
                throw new InvalidOperationException($"File Loader {loader.GetType().Name} could not be instantiated.");
            }

            return fileLoader;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"File Loader {loader.GetType().Name} could not be instantiated.", ex);
        }
    }

    public Dictionary<string, Asset> LoadAssets(string directory, GamePackAssetReference assetReferenceMap)
    {
        foreach (var loader in FileLoaders.Where(x => !typeof(BaseFileLoader).IsAssignableFrom(x.FileLoader)))
        {
            throw new InvalidOperationException($"Invalid FileLoader specified: {loader.GetType().Name}");
        }

        Dictionary<string, Asset> assets = new Dictionary<string, Asset>();
        foreach (var loader in FileLoaders)
        {
            var fileLoader = InstantiateLoader(directory, loader);

            var loadedAssets = fileLoader.Load(assetReferenceMap);
            foreach (var asset in loadedAssets)
                assets.Add(asset.Key, asset.Value);
        }

        return assets;
    }
}