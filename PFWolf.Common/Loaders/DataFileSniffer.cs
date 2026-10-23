using System.Text;

namespace PFWolf.Loaders;

/// <summary>What a classic game's data file is, whatever it's named</summary>
public enum DataFileKind
{
    Vswap,
    VgaDict,
    VgaHead,
    VgaGraph,
    AudioHed,
    AudioT,
    MapHead,
    GameMaps,
}

/// <summary>
/// Tells which data file is which by what's in it, for a raw data mod whose files aren't named
/// VSWAP, GAMEMAPS... (FOXTROT1.WL6): GAMEMAPS starts with "TED5v1.0"; MAPHEAD with an RLEW tag
/// and level offsets; VSWAP with a page table that fits the file; VGADICT is 255 Huffman nodes
/// (1020-1024 bytes); VGAHEAD and AUDIOHED are rising offset tables (3 and 4 bytes) whose last
/// offset is their data file's size, which pairs them with VGAGRAPH and AUDIOT.
/// </summary>
public static class DataFileSniffer
{
    /// <summary>The kinds in the order people know them</summary>
    public static IReadOnlyList<DataFileKind> Kinds { get; } = Enum.GetValues<DataFileKind>();

    /// <summary>The file's usual name without its extension (VSWAP, GAMEMAPS...)</summary>
    public static string StemOf(DataFileKind kind) => kind.ToString().ToUpperInvariant();

    /// <summary>
    /// Which file of <paramref name="files"/> each kind looks like by its contents, each file used
    /// once; a kind none looks like is left out
    /// </summary>
    public static Dictionary<DataFileKind, string> Guess(IEnumerable<string> files)
    {
        var guesses = new Dictionary<DataFileKind, string>();
        var sizes = files.Where(File.Exists).ToDictionary(file => file, file => new FileInfo(file).Length, StringComparer.OrdinalIgnoreCase);
        void Take(DataFileKind kind, string? file)
        {
            if (file != null && !guesses.ContainsKey(kind) && !guesses.ContainsValue(file))
                guesses[kind] = file;
        }

        foreach (var file in sizes.Keys)
        {
            var head = ReadStart(file, 4096);
            if (LooksLikeGameMaps(head))
                Take(DataFileKind.GameMaps, file);
            else if (LooksLikeMapHead(head, sizes[file]))
                Take(DataFileKind.MapHead, file);
            else if (LooksLikeVswap(head, sizes[file]))
                Take(DataFileKind.Vswap, file);
            else if (sizes[file] is >= 1020 and <= 1024 && !LooksLikeOffsets(file, 4, sizes, out _) && !LooksLikeOffsets(file, 3, sizes, out _))
                Take(DataFileKind.VgaDict, file);
        }

        // Offset tables, by the data file whose size their last offset is
        foreach (var file in sizes.Keys.Where(file => !guesses.ContainsValue(file)))
        {
            if (LooksLikeOffsets(file, 4, sizes, out var audioT))
            {
                Take(DataFileKind.AudioHed, file);
                Take(DataFileKind.AudioT, audioT);
            }
            else if (LooksLikeOffsets(file, 3, sizes, out var vgaGraph))
            {
                Take(DataFileKind.VgaHead, file);
                Take(DataFileKind.VgaGraph, vgaGraph);
            }
        }

        return guesses;
    }

    private static bool LooksLikeGameMaps(byte[] head)
        => head.Length >= 8 && Encoding.ASCII.GetString(head, 0, 8) == "TED5v1.0";

    // The RLEW tag, then up to 100 offsets: none past what GAMEMAPS could hold, at least one level
    private static bool LooksLikeMapHead(byte[] head, long size)
    {
        if (size < 2 + 4 || size > 64 * 1024 || BitConverter.ToUInt16(head, 0) != 0xABCD)
            return false;
        int levels = 0;
        for (int i = 0; i < 100 && 2 + i * 4 + 4 <= head.Length; i++)
        {
            var offset = BitConverter.ToInt32(head, 2 + i * 4);
            if (offset > 0)
                levels++;
            else if (offset < -1)
                return false;
        }
        return levels > 0;
    }

    // Page count, sprite start and sound start in order, then a page table that fits the file
    private static bool LooksLikeVswap(byte[] head, long size)
    {
        if (head.Length < 6)
            return false;
        int pages = BitConverter.ToUInt16(head, 0), sprites = BitConverter.ToUInt16(head, 2), sounds = BitConverter.ToUInt16(head, 4);
        if (pages < 2 || sprites > sounds || sounds > pages || 6 + pages * 6 > size || head.Length < 6 + 4)
            return false;
        var first = BitConverter.ToUInt32(head, 6);
        return first >= 6 + pages * 6 && first < size;
    }

    // A table of rising offsets (sparse ones aside) from 0, whose last is another file's size
    private static bool LooksLikeOffsets(string file, int width, Dictionary<string, long> sizes, out string? dataFile)
    {
        dataFile = null;
        var size = sizes[file];
        if (size < width * 2 || size % width != 0 || size > 1024 * 1024)
            return false;
        var data = File.ReadAllBytes(file);
        long previous = -1, last = 0;
        for (int i = 0; i < data.Length; i += width)
        {
            long value = width == 4 ? BitConverter.ToUInt32(data, i) : data[i] | (data[i + 1] << 8) | (data[i + 2] << 16);
            if (width == 3 && value == 0xFFFFFF)
                continue;
            if (previous < 0 ? value != 0 : value < previous)
                return false;
            previous = last = value;
        }
        var match = sizes.FirstOrDefault(other => other.Key != file && other.Value == last).Key;
        dataFile = match;
        return match != null;
    }

    private static byte[] ReadStart(string file, int count)
    {
        using var stream = File.OpenRead(file);
        var buffer = new byte[Math.Min(count, stream.Length)];
        stream.ReadExactly(buffer);
        return buffer;
    }
}
