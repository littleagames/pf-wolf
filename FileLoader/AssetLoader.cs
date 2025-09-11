using CSharpFunctionalExtensions;
using PFWolf.Common;
using PFWolf.Common.Assets;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileLoader;

internal class AssetLoader
{
    Dictionary<string, Asset> _assets = new Dictionary<string, Asset>();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="packFilePaths">File paths of pk3 files, in order</param>
    internal Result LoadAvailableGamePacks(List<string> packFilePaths, Maybe<string> selectedGamePack)
    {
        if (selectedGamePack.HasNoValue)
        {
            // choose default gamepack
            return Result.Failure("Cannot determine default game pack at this time.");
        }

        // Only load in definition types, but use the same loader that processes all other types, but just filter down the types we need
        foreach (var gamePackPath in packFilePaths)
        {
            using ZipArchive archive = ZipFile.OpenRead(gamePackPath);
            var gamePackInfo = archive.Entries.
                Single(entry => entry.Length > 0 && entry.IsEncrypted == false
                && entry.FullName.StartsWith("gamepacks/gamepack-info"));

            if (gamePackInfo != null)
            {
                var name = gamePackInfo.Name;
                var size = gamePackInfo.Length;
                var fileNameOnly = Path.GetFileNameWithoutExtension(Path.GetFileName(gamePackInfo.FullName));

                var gamePackDefinition = new GamePackDefinitions(fileNameOnly, gamePackInfo.Open());

                if (_assets.TryGetValue(fileNameOnly, out var foundAsset))
                {
                    if (foundAsset is not GamePackDefinitions existingGamePackDefinition)
                    {
                        return Result.Failure($"Asset with name '{fileNameOnly}' is not a GamePackDefinitions asset.");
                    }

                    foreach (var newPackDefinition in gamePackDefinition.GamePacks)
                    {
                        // TODO: Add or overwrite?
                        existingGamePackDefinition.GamePacks.TryAdd(newPackDefinition.Key, newPackDefinition.Value);
                    }
                }
                else
                {
                    _assets.Add(fileNameOnly, gamePackDefinition);
                }

                //var mapDefinitionAsset = new MapDefinition(); // Only one, merge all data into this asset
                // TODO: Load in all map definitions, with game packs overwriting previous values
                foreach (var gamePack in gamePackDefinition.GamePacks.Values)
                {
                    if (gamePack.MapDefinitions.Count == 0)
                        continue;

                    var mapDefinitions = archive.Entries
                        .Where(x => x.Length > 0
                        && gamePack.MapDefinitions.Any(md => GetAssetReadyName(md).IndexOf(GetAssetReadyName(x.FullName), StringComparison.InvariantCultureIgnoreCase) >= 0))
                        .ToList();

                    // TODO: AssetReference<MapDefintions>
                    // Should have file path and entry path, as well as the Pk3AssetLoader
                    
                }

                if (!gamePackDefinition.GamePacks.TryGetValue(selectedGamePack.Value, out var gamePackDefinitionDataModel))
                {
                    continue;
                    //return Result.Failure($"Game pack '{selectedGamePack.Value}' not found in '{gamePackPath}'.");
                }

                //var mapDefinitions = archive.Entries.FirstOrDefault(x => x.FullName == gamePackDefinitionDataModel.MapDefinitions);

                //var assetMapping = archive.Entries.FirstOrDefault(x => x.FullName == gamePackDefinitionDataModel.GamePackAssetMapping);

                // Did we fill in the "Base pack" data?

                // TODO: This will only load if the game pack exists?

                // TODO: Add map definitions (Asset reference?)
                // TODO: Add game pack assets mapping (Asset reference?)
                //var mapDefinitions = archive.Entries.FirstOrDefault(x => x.FullName == gamePackDefinition.MapDefinitions);
                //var gamePackAssetMapping = archive.Entries.FirstOrDefault(x => x.FullName == gamePackDefinition.GamePackAssetMapping);

            }
        }

        Console.WriteLine($"Assets: {_assets.Count}");
        return Result.Success();
    }

    private static string GetAssetReadyName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return string.Empty;

        var stripExtension = fullName.LastIndexOf(".");

        if (stripExtension >= 0)
            fullName = fullName.Substring(0, stripExtension);

        return fullName.Replace('\\', '/').Trim().ToLowerInvariant();

        //var path = Path.GetDirectoryName(fullName);
        //var fileName = Path.GetFileNameWithoutExtension(fullName);
        //var assetReadyPath = Path.Combine(path, fileName);
        //return assetReadyPath;
    }

    public Result<T> Get<T>(string assetName) where T : Asset
    {
        if (_assets.TryGetValue(assetName, out var foundAsset))
        {
            if (foundAsset is not T typedAsset)
            {
                return Result.Failure<T>($"Asset with name '{assetName}' is not of type {typeof(T).Name}.");
            }

            return Result.Success(typedAsset);
        }
        else
        {
            return Result.Failure<T>($"Asset with name '{assetName}' not found.");
        }
    }
}
