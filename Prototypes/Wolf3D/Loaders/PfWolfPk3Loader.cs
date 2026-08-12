using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;
using Wolf3D.Assets;

namespace Wolf3D.Loaders;

internal class PfWolfPk3Loader
{
    private Dictionary<string, Asset> _assets = [];

    public PfWolfPk3Loader(string pk3File)
    {

        using ZipArchive archive = ZipFile.OpenRead(pk3File);

        foreach (ZipArchiveEntry entry in archive.Entries.Where(entry => entry.Length > 0 && entry.IsEncrypted == false))
        {
            var assetName = GetAssetReadyName(entry.Name);
            if (entry.FullName.StartsWith("graphics/"))
            {
                // 1) Validate file is a valid graphic to load
                // 2) Load asset reference to pack, and what type it is
                // TODO: distinguish between PNG and other formats by using a "try load" for each data type of a graphic
                // Then I can use this same loader for wolf3d file formats as well
                //AddReference(assetName, () => GraphicDataLoader.Load(Pk3EntryLoader.Open(pk3FileFullPath, entry.FullName), sourcePalette: Load<Palette>("wolfpal")));
                continue;
            }

            if (entry.FullName.StartsWith("palettes/"))
            {
                AddReference(assetName, () => PaletteDataLoader.Load(Pk3EntryLoader.Open(pk3File, entry.FullName)));
                continue;
            }
        }
    }

    private void AddReference<T>(string assetName, Func<T> assetLoader) where T : Asset
    {
        AddAsset(assetName, new AssetReference<T>(assetLoader));
    }

    private void AddAsset(string assetName, Asset asset, bool overwrite = true)
    {
        var key = GetKey(assetName, GetAssetTypeName(asset));

        if (_assets.ContainsKey(key))
        {
            if (!overwrite)
                return;
            _assets[key] = asset;
        }
        else
        {
            _assets.Add(key, asset);
        }
    }

    private static string GetKey(string assetName, string assetType)
        => assetName.ToLowerInvariant(); //$"{assetType}:{assetName}".ToLowerInvariant();

    private static string GetAssetTypeName(Asset asset)
    {
        var type = asset.GetType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AssetReference<>))
        {
            var genericType = type.GetGenericArguments()[0];
            return genericType.Name;
        }
        return type.Name;
    }

    public Dictionary<string, Asset> GetAssets()
    {
        var loadedAssets = new Dictionary<string, Asset>();
        foreach (var asset in _assets)
        {
            var assetValue = asset.Value;
            var assetType = assetValue.GetType();
            if (assetType.IsGenericType && assetType.GetGenericTypeDefinition() == typeof(AssetReference<>))
            {
                // Use reflection to call Load() on AssetReference<T>
                var loadMethod = assetType.GetProperty("Load")?.GetValue(assetValue) as Delegate;
                if (loadMethod != null)
                {
                    var loadedAsset = loadMethod.DynamicInvoke();
                    _assets[asset.Key] = (Asset)loadedAsset; // Replace reference with loaded asset
                    loadedAssets.Add(asset.Key, (Asset)loadedAsset);
                }
            }
            else
            {
                loadedAssets.Add(asset.Key, assetValue);
            }
        }

        return loadedAssets;
    }

    /// <summary>
    /// Loads an asset into memory, if already loaded, it simply returns the asset.
    /// If the asset is a reference, it will load the asset using the provided loader function
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="assetName"></param>
    /// <param name="assetType"></param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException"></exception>
    public T Load<T>(string assetName) where T : Asset
    {
        string assetType = typeof(T).Name;

        if (string.IsNullOrWhiteSpace(assetName))
        {
            throw new ArgumentException($"Asset name cannot be empty. Asset Type: {assetType}", nameof(assetName));
        }

        var key = GetKey(assetName, assetType);

        // TODO: Determine if this should just return null if not found, or throw
        if (!_assets.TryGetValue(key, out var asset))
        {
            throw new KeyNotFoundException($"Asset with name {assetName} not found.");
        }

        if (asset is AssetReference<T>)
        {
            var typedAsset = (AssetReference<T>)asset;
            var loadedAsset = typedAsset.Load();
            _assets[key] = loadedAsset; // Replace reference with loaded asset
            return loadedAsset;
        }

        return (T)asset;
    }

    private static string GetAssetReadyName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return string.Empty;

        var stripExtension = fullName.LastIndexOf('.');

        if (stripExtension >= 0)
            fullName = fullName.Substring(0, stripExtension);

        return fullName.Replace('\\', '/').Trim().ToLowerInvariant();
    }

}
