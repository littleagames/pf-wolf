using System.IO.Compression;

namespace Wolf3D.Loaders;

/// <summary>
/// A pk3 (zip) file. The archive is reopened for each read, so it isn't held open while the game runs.
/// </summary>
internal class Pk3AssetSource : IAssetSource
{
    private readonly string _filePath;

    // Entry paths with '/' to the names stored in the zip, which some Windows tools write with '\'
    private readonly Dictionary<string, string> _entryNames = [];

    public Pk3AssetSource(string filePath)
    {
        _filePath = filePath;

        using ZipArchive archive = ZipFile.OpenRead(filePath);
        foreach (var entry in archive.Entries.Where(entry => entry.Length > 0 && entry.IsEncrypted == false))
            _entryNames.TryAdd(entry.FullName.Replace('\\', '/'), entry.FullName);

        EntryPaths = _entryNames.Keys.ToList();
    }

    public string Name => Path.GetFileName(_filePath);

    public IReadOnlyList<string> EntryPaths { get; }

    public MemoryStream Open(string entryPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(_filePath);
        var entry = (_entryNames.TryGetValue(entryPath, out var entryName) ? archive.GetEntry(entryName) : null)
            ?? throw new FileNotFoundException($"Entry {entryPath} not found in {_filePath}");

        using var stream = entry.Open();
        var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;
        return ms;
    }
}
