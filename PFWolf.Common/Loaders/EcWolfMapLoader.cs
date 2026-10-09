using System.Text;
using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Loaders;

/// <summary>
/// ECWolf's binary maps, as a pk3 keeps them in maps/NAME.wad: a WAD holding a map marker lump,
/// then PLANES, which is WDC's map export format:
///   char[6] "WDC3.1", int32 number of maps (1), int16 number of planes, int16 name length,
///   char[name length] name, int16 width, int16 height, then each plane's width x height
///   uint16s, uncompressed, all little-endian.
/// The planes are the same as GAMEMAPS': walls, objects, then ECWolf's floor and ceiling
/// flats; a fourth holds PFWolf's wall heights (ECWolf reads it as its info plane), a fifth
/// PFWolf's tags and a sixth its light zones. A map
/// written in ECWolf's text format (TEXTMAP, UWMF) isn't read yet.
/// </summary>
public static class EcWolfMapLoader
{
    private const string Magic = WdcMapFile.Magic;
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

        WdcMap map;
        try
        {
            // One map per lump
            map = WdcMapFile.Read(mapLump.Data)[0];
        }
        catch (InvalidDataException e)
        {
            throw new InvalidDataException($"its PLANES lump: {e.Message}");
        }
        return ToMapAsset(map);
    }

    /// <summary>
    /// A WDC map as a level: the fourth plane (ECWolf's info plane) holds PFWolf's wall heights,
    /// the fifth its tags and the sixth its light zones; planes past them are left out, and
    /// missing ones are empty
    /// </summary>
    public static MapAsset ToMapAsset(WdcMap map)
    {
        if (map.Width != MapConstants.MAPSIZE || map.Height != MapConstants.MAPSIZE)
            throw new InvalidDataException($"it's {map.Width}x{map.Height}; PFWolf maps are {MapConstants.MAPSIZE}x{MapConstants.MAPSIZE}");

        var mapData = new ushort[MapConstants.LEVELPLANES][];
        for (var plane = 0; plane < MapConstants.LEVELPLANES; plane++)
            mapData[plane] = plane < map.Planes.Length ? (ushort[])map.Planes[plane].Clone() : new ushort[MapConstants.MAPAREA];

        return new MapAsset
        {
            Width = (ushort)map.Width,
            Height = (ushort)map.Height,
            Name = map.Name,
            MapData = mapData,
        };
    }

    /// <summary>A map as maps/NAME.wad, with <paramref name="markerName"/> as its marker lump</summary>
    public static byte[] Save(MapAsset map, string markerName)
    {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
        {
            // Empty planes at the end (zones, tags, then heights) are left off, so ECWolf doesn't read
            // an empty height plane as an info plane. One in use keeps those before it.
            var planes = map.MapData.ToList();
            while (planes.Count > MapConstants.MAPPLANES && planes[^1].All(tile => tile == 0))
                planes.RemoveAt(planes.Count - 1);

            bw.Write(Encoding.ASCII.GetBytes(Magic));
            bw.Write(1);
            bw.Write((ushort)planes.Count);
            bw.Write((ushort)SavedNameLength);

            var name = new byte[SavedNameLength];
            var mapName = (map.Name ?? "").Split('\0')[0];
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
