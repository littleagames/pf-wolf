using PFWolf.Common.Assets;
using PFWolf.Common.Loaders;
using System.IO.Compression;

namespace PFWolf.Common;

public interface IAssetManager
{
    Task LoadPackage(string directory, string fileName);
}

public class AssetManager : IAssetManager
{
    private Dictionary<string, Asset> _assets = [];

    public Task LoadPackage(string directory, string fileName)
    {
        var pk3FileFullPath = Path.Combine(directory, fileName);
        using ZipArchive archive = ZipFile.OpenRead(pk3FileFullPath);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            try
            { 
                if (entry.Length == 0)
                    continue;

                if (entry.FullName.StartsWith("palettes/"))
                {
                    if (entry.Length == 768) // 3 bytes per color, 256 colors
                    {
                       // AddAsset(new AssetReference<Palette>(entry.Name, pk3FileFullPath, AssetType.Palette, () => Pk3DataFileLoader.Load(pk3FileFullPath, entry.FullName))); 
                        // pk3datafileloader(filepath, assetpathinfile, Pallette)
                        // So in this case it will convert to Palette when loaded

                        // This is an example of the asset loaded, but just in "Raw" data until it is mapped to a proper "Palette"
                        //_assets.Add(new RawAsset<Palette>(entry.Name, entry.Open()));

                        // This would straight be a common data model of palette
                        //_assets.Add(new Palette
                        //{
                        //    Name = entry.Name
                        //});
                        // add "loading" strategy and path to assets (isLoaded=false)
                        // this allows for loaded when needed
                    }
                    //assets.Add(new PaletteAsset
                    //{
                    //    Name = CleanName(entry.Name),
                    //    RawData = rawData
                    //});
                    continue;
                }
            }
            catch (Exception ex)
            {
                // Log the error, but continue processing other entries
                Console.WriteLine($"Error processing entry {entry.FullName}: {ex.Message}");
            }
        }

        // How should I handle this?
        // Should I use a <T> to use a specific FileLoader?
        // Then maybe a gamepack will just call this method in the file loader, and just put all of the things in the asset manager
        throw new NotImplementedException();
    }

    public T Load<T>(string assetName) where T : Asset
    {
        if (!_assets.TryGetValue(assetName, out var asset))
        {
            throw new KeyNotFoundException($"Asset with name {assetName} not found.");
        }

        if (asset is not T typedAsset)
        {
            throw new InvalidOperationException($"Asset with name {assetName} is not of type {typeof(T).Name}.");
        }

        return Activator.CreateInstance<T>();
    }

    private void AddAsset(Asset asset)
    {
        if (_assets.ContainsKey(asset.Name))
        {
            throw new InvalidOperationException($"Asset with name {asset.Name} already exists.");
        }

        _assets.Add(asset.Name, asset);
    }
}