using System.IO.Compression;

namespace PFWolf.Common.DataLoaders;

public class Pk3EntryLoader
{
    public static MemoryStream Open(string filePath, string entryPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(filePath);
        var entry = archive.GetEntry(entryPath);
        if (entry == null)
            throw new FileNotFoundException($"Entry {entryPath} not found in {filePath}");

        return Load(entry);
    }

    public static MemoryStream Load(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms;
    }
}
