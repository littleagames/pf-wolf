using System.Text;

namespace PFWolf.Loaders;

/// <summary>One level of a WDC map file, with as many planes as the file has</summary>
public sealed record WdcMap(string Name, int Width, int Height, ushort[][] Planes);

/// <summary>
/// WDC's map export format (a .map file), which ECWolf also keeps as a binary map's PLANES lump:
///   char[6] "WDC3.1", int32 number of maps, int16 number of planes, int16 name length,
///   then for each map: char[name length] name, int16 width, int16 height, then each plane's
///   width x height uint16s, uncompressed, all little-endian.
/// </summary>
public static class WdcMapFile
{
    public const string Magic = "WDC3.1";
    private const string PlanesLump = "PLANES";

    /// <summary>The levels in a WDC map file (or a PLANES lump)</summary>
    public static IReadOnlyList<WdcMap> Read(byte[] data)
    {
        using var br = new BinaryReader(new MemoryStream(data));
        try
        {
            if (data.Length < Magic.Length || Encoding.ASCII.GetString(br.ReadBytes(Magic.Length)) != Magic)
                throw new InvalidDataException($"it doesn't start with {Magic}");

            int count = br.ReadInt32();
            int planes = br.ReadUInt16();
            int nameLength = br.ReadUInt16();
            if (count < 1)
                throw new InvalidDataException("it holds no maps");

            var maps = new List<WdcMap>(count);
            for (int m = 0; m < count; m++)
            {
                var name = Encoding.ASCII.GetString(br.ReadBytes(nameLength));
                var nul = name.IndexOf('\0');
                if (nul >= 0)
                    name = name.Substring(0, nul);

                int width = br.ReadUInt16(), height = br.ReadUInt16();
                var mapData = new ushort[planes][];
                for (int plane = 0; plane < planes; plane++)
                {
                    mapData[plane] = new ushort[width * height];
                    for (int i = 0; i < mapData[plane].Length; i++)
                        mapData[plane][i] = br.ReadUInt16();
                }
                maps.Add(new WdcMap(name, width, height, mapData));
            }
            return maps;
        }
        catch (EndOfStreamException)
        {
            throw new InvalidDataException("it ends too soon");
        }
    }

    /// <summary>
    /// The levels in a file that's either a WDC map file or an ECWolf binary map (a WAD holding
    /// a map marker lump, then PLANES)
    /// </summary>
    public static IReadOnlyList<WdcMap> ReadFile(byte[] data)
    {
        var magic = data.Length >= 4 ? Encoding.ASCII.GetString(data, 0, 4) : "";
        if (magic is not ("IWAD" or "PWAD"))
            return Read(data);

        var planes = WadFile.Read(data).FirstOrDefault(lump => lump.Name.Equals(PlanesLump, StringComparison.OrdinalIgnoreCase))
                     ?? throw new InvalidDataException("it's a WAD without a PLANES lump (a UWMF map isn't read yet)");
        return Read(planes.Data);
    }
}
