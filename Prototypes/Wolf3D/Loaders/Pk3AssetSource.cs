using System.IO.Compression;

namespace Wolf3D.Loaders;

/// <summary>
/// A pk3 (zip) file. The archive is reopened for each read, so it isn't held open while the game runs.
/// </summary>
internal class Pk3AssetSource : IAssetSource
{
    private readonly string _filePath;

    public Pk3AssetSource(string filePath)
    {
        _filePath = filePath;

        using ZipArchive archive = ZipFile.OpenRead(filePath);
        EntryPaths = archive.Entries
            .Where(entry => entry.Length > 0 && entry.IsEncrypted == false)
            .Select(entry => entry.FullName)
            .ToList();
    }

    public string Name => Path.GetFileName(_filePath);

    public IReadOnlyList<string> EntryPaths { get; }

    public MemoryStream Open(string entryPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(_filePath);
        var entry = archive.GetEntry(entryPath)
            ?? throw new FileNotFoundException($"Entry {entryPath} not found in {_filePath}");

        using var stream = entry.Open();
        var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;
        return ms;
    }
}
