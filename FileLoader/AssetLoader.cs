using PFWolf.Common;
using PFWolf.Common.Assets;
using System;
using System.Collections.Generic;
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
    internal void LoadAvailableGamePacks(List<string> packFilePaths)
    {
        // Only load in definition types, but use the same loader that processes all other types, but just filter down the types we need
        foreach(var gamePackPath in packFilePaths)
        {
            using ZipArchive archive = ZipFile.OpenRead(gamePackPath);
            foreach (ZipArchiveEntry entry in archive.Entries.Where(entry => entry.Length > 0 && entry.IsEncrypted == false))
            {
                if (entry.FullName.StartsWith("gamepacks/gamepack-info"))
                {
                    var name = entry.Name;
                    var size = entry.Length;
                    var fileNameOnly = Path.GetFileNameWithoutExtension(Path.GetFileName(entry.FullName));

                    var gamePackDefinition = new GamePackDefinitions(fileNameOnly, entry.Open());

                    if (_assets.TryGetValue(fileNameOnly, out var foundAsset))
                    {
                        if (foundAsset is not GamePackDefinitions existingGamePackDefinition)
                        {
                            throw new InvalidOperationException($"Asset with name '{fileNameOnly}' is not a GamePackDefinitions asset.");
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
                    // TODO: Add map definitions (Asset reference?)
                    // TODO: Add game pack assets mapping (Asset reference?)
                    //var mapDefinitions = archive.Entries.FirstOrDefault(x => x.FullName == gamePackDefinition.MapDefinitions);
                    //var gamePackAssetMapping = archive.Entries.FirstOrDefault(x => x.FullName == gamePackDefinition.GamePackAssetMapping);
                    continue;
                }
            }
        }
    }
}
