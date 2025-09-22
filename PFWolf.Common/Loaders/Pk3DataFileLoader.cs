using System.IO.Compression;

namespace PFWolf.Common.Loaders;

public class Pk3DataFileLoader
{
    public static byte[] Load(string filePath, string entryPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(filePath);
        var entry = archive.GetEntry(entryPath);
        if (entry == null)
            throw new FileNotFoundException($"Entry {entryPath} not found in {filePath}");
        using var stream = entry.Open();
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
}
