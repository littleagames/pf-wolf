using System.Text;
using Wolf3D.Assets;
using Wolf3D.Managers;

namespace Wolf3D.Loaders;

/// <summary>
/// ECWolf's binary maps, as a pk3 keeps them in maps/NAME.wad: a WAD holding a map marker lump,
/// then PLANES, which is WDC's map export format:
///   char[6] "WDC3.1", int32 number of maps (1), int16 number of planes, int16 name length,
///   char[name length] name, int16 width, int16 height, then each plane's width x height
///   uint16s, uncompressed, all little-endian.
/// The planes are the same as GAMEMAPS': walls, objects, then ECWolf's floor and ceiling
/// flats; a fourth holds PFWolf's wall heights (ECWolf reads it as its info plane). A map
/// written in ECWolf's text format (TEXTMAP, UWMF) isn't read yet.
/// </summary>
internal static class EcWolfMapLoader
{
    private const string Magic = "WDC3.1";
    private const string PlanesLump = "PLANES";
    private const int SavedNameLength = 16;

    public static MapAsset Load(byte[] wad)
    {
        var lumps = WadFile.Read(wad);
        if (lumps.Count < 2)
            throw new InvalidDataException("it needs a map marker lump followed by a PLANES lump");

        var mapLump = lumps[1];
        if (mapLump.Name.Equals("TEXTMAP", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("it's a UWMF (TEXTMAP) map, which PFWolf can't read yet");
        if (!mapLump.Name.Equals(PlanesLump, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"its map lump is {mapLump.Name}, not PLANES");

        using var br = new BinaryReader(new MemoryStream(mapLump.Data));
        try
        {
            if (Encoding.ASCII.GetString(br.ReadBytes(Magic.Length)) != Magic)
                throw new InvalidDataException($"its PLANES lump doesn't start with {Magic}");

            br.ReadInt32();     // number of maps: one per lump
            int planes = br.ReadUInt16();
            int nameLength = br.ReadUInt16();
            var name = Encoding.ASCII.GetString(br.ReadBytes(nameLength));
            var nul = name.IndexOf('\0');
            if (nul >= 0)
                name = name.Substring(0, nul);

            int width = br.ReadUInt16(), height = br.ReadUInt16();
            if (width != MapManager.MAPSIZE || height != MapManager.MAPSIZE)
                throw new InvalidDataException($"it's {width}x{height}; PFWolf maps are {MapManager.MAPSIZE}x{MapManager.MAPSIZE}");

            // The fourth plane (ECWolf's info plane) holds PFWolf's wall heights; planes past it
            // are left out, and missing ones are empty
            var mapData = new ushort[MapManager.LEVELPLANES][];
            for (var plane = 0; plane < MapManager.LEVELPLANES; plane++)
            {
                mapData[plane] = new ushort[MapManager.MAPAREA];
                if (plane >= planes)
                    continue;

                for (var i = 0; i < MapManager.MAPAREA; i++)
                    mapData[plane][i] = br.ReadUInt16();
            }

            return new MapAsset
            {
                Width = (ushort)width,
                Height = (ushort)height,
                Name = name,
                MapData = mapData,
            };
        }
        catch (EndOfStreamException)
        {
            throw new InvalidDataException("its PLANES lump ends too soon");
        }
    }

    /// <summary>A map as maps/NAME.wad, with <paramref name="markerName"/> as its marker lump</summary>
    public static byte[] Save(MapAsset map, string markerName)
    {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
        {
            // An empty height plane is left off, so ECWolf doesn't read it as an info plane
            var planes = map.MapData.ToList();
            if (planes.Count > MapManager.MAPPLANES && planes[^1].All(tile => tile == 0))
                planes.RemoveAt(planes.Count - 1);

            bw.Write(Encoding.ASCII.GetBytes(Magic));
            bw.Write(1);
            bw.Write((ushort)planes.Count);
            bw.Write((ushort)SavedNameLength);

            var name = new byte[SavedNameLength];
            var mapName = (map.Name ?? "").TrimEnd('\0');
            Encoding.ASCII.GetBytes(mapName, 0, Math.Min(mapName.Length, SavedNameLength), name, 0);
            bw.Write(name);

            bw.Write(map.Width);
            bw.Write(map.Height);
            foreach (var plane in planes)
                foreach (var tile in plane)
                    bw.Write(tile);
        }

        return WadFile.Write([new WadFile.Lump(markerName, []), new WadFile.Lump(PlanesLump, ms.ToArray())]);
    }
}
