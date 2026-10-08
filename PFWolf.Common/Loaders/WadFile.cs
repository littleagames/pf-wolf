using System.Text;

namespace PFWolf.Loaders;

/// <summary>
/// A Doom-style WAD file: "IWAD" or "PWAD", the number of lumps and where their directory
/// starts (int32 each), then the directory: each lump's offset and size (int32) and its name
/// (8 ASCII characters, padded with zeros). ECWolf keeps a map in one (maps/NAME.wad).
/// </summary>
public static class WadFile
{
    public sealed record Lump(string Name, byte[] Data);

    private const int HeaderSize = 12;
    private const int DirectoryEntrySize = 16;
    private const int NameLength = 8;

    public static List<Lump> Read(byte[] wad)
    {
        if (wad.Length < HeaderSize)
            throw new InvalidDataException("it's too short to be a WAD file");

        var magic = Encoding.ASCII.GetString(wad, 0, 4);
        if (magic != "IWAD" && magic != "PWAD")
            throw new InvalidDataException("it isn't a WAD file");

        var count = BitConverter.ToInt32(wad, 4);
        var directory = BitConverter.ToInt32(wad, 8);
        if (count < 0 || directory < HeaderSize || (long)directory + (long)count * DirectoryEntrySize > wad.Length)
            throw new InvalidDataException("its lump directory is damaged");

        var lumps = new List<Lump>(count);
        for (var i = 0; i < count; i++)
        {
            var entry = directory + i * DirectoryEntrySize;
            var offset = BitConverter.ToInt32(wad, entry);
            var size = BitConverter.ToInt32(wad, entry + 4);
            var name = Encoding.ASCII.GetString(wad, entry + 8, NameLength).TrimEnd('\0');
            if (size < 0 || offset < 0 || (long)offset + size > wad.Length)
                throw new InvalidDataException($"its lump {name} runs past the end of the file");

            lumps.Add(new Lump(name, wad.AsSpan(offset, size).ToArray()));
        }

        return lumps;
    }

    /// <summary>A PWAD holding these lumps, in order</summary>
    public static byte[] Write(IReadOnlyList<Lump> lumps)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        var dataSize = lumps.Sum(lump => lump.Data.Length);
        bw.Write(Encoding.ASCII.GetBytes("PWAD"));
        bw.Write(lumps.Count);
        bw.Write(HeaderSize + dataSize);

        foreach (var lump in lumps)
            bw.Write(lump.Data);

        var offset = HeaderSize;
        foreach (var lump in lumps)
        {
            bw.Write(offset);
            bw.Write(lump.Data.Length);
            var name = new byte[NameLength];
            Encoding.ASCII.GetBytes(lump.Name.ToUpperInvariant(), 0, Math.Min(lump.Name.Length, NameLength), name, 0);
            bw.Write(name);
            offset += lump.Data.Length;
        }

        bw.Flush();
        return ms.ToArray();
    }
}
