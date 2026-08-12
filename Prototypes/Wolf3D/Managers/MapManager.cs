using Wolf3D.Assets;
using Wolf3D.Mappers;

namespace Wolf3D.Managers;

public class MapDataConstants
{
    public const int ICONARROWS = 90;
    public const int PUSHABLETILE = 98;
    public const int EXITTILE = 99;          // at end of castle
    public const int AREATILE = 107;         // first of NUMAREAS floor tiles
    public const int NUMAREAS = 37;
    public const int ELEVATORTILE = 21;
    public const int AMBUSHTILE = 106;
    public const int ALTELEVATORTILE = 107;
}
struct maptype
{
    public int[] planestart;
    public UInt16[] planelength;
    public UInt16 width;
    public UInt16 height;
    public char[] name;

    public maptype()
    {
        planestart = new Int32[MapManager.MAPPLANES];
        planelength = new UInt16[MapManager.MAPPLANES];
        name = new char[16];
    }
}

internal class MapManager
{
    internal const int MAPSHIFT = 6;
    internal const int MAPSIZE = (1 << MAPSHIFT);
    internal const int MAPAREA = MAPSIZE * MAPSIZE;

    public const int NUMMAPS = 60;
    public const int MAPPLANES = 3;

    private readonly Lazy<AssetManager> assetManager;

    private UInt16[][] mapsegs = new ushort[MAPPLANES][];
    private maptype[] mapheaderseg = new maptype[NUMMAPS];


    public MapManager(Lazy<AssetManager> assetManager)
    {
        this.assetManager = assetManager;
    }

    internal ushort mapwidth, mapheight;
    internal byte[,] tilemap;
    internal bool[,] spotvis;
    internal Actor?[,] actorat;

    public ushort GetTile(int x, int y, int plane)
    {
        return mapsegs[0][y * mapwidth + x];
    }

    public void LoadMap(string mapName)
    {
        var mapAsset = assetManager.Value.Find<MapAsset>(mapName);
        if (mapAsset == null)
            throw new Exception($"Map not found {mapName}");

        mapwidth = mapAsset.Width;
        mapheight = mapAsset.Height;
        mapsegs = mapAsset.MapData;

#if USE_FEATUREFLAGS
    const int MXX = MAPSIZE - 1;
    
    // Read feature flags data from map corners and overwrite corners with adjacent tiles
    ffDataTopLeft     = MAPSPOT(0,   0,   0); MAPSPOT(0,   0,   0) = MAPSPOT(1,       0,       0);
    ffDataTopRight    = MAPSPOT(MXX, 0,   0); MAPSPOT(MXX, 0,   0) = MAPSPOT(MXX,     1,       0);
    ffDataBottomRight = MAPSPOT(MXX, MXX, 0); MAPSPOT(MXX, MXX, 0) = MAPSPOT(MXX - 1, MXX,     0);
    ffDataBottomLeft  = MAPSPOT(0,   MXX, 0); MAPSPOT(0,   MXX, 0) = MAPSPOT(0,       MXX - 1, 0);
#endif

        tilemap = new byte[MAPSIZE, MAPSIZE]; // wall values only
        spotvis = new bool[MAPSIZE, MAPSIZE];
        actorat = new Actor?[MAPSIZE, MAPSIZE];

        for (int y = 0; y < mapheight; y++)
        {
            for (int x = 0; x < mapwidth; x++)
            {
                int tile = MAPSPOT(x, y, 0);
                if (tile < MapDataConstants.AMBUSHTILE)
                {
                    // solid wall
                    tilemap[x, y] = (byte)tile;
                    actorat[x, y] = new Wall(tile);// (uint)tile;
                }
                else
                {
                    // area floor
                    tilemap[x, y] = 0;
                    actorat[x, y] = null;
                }
            }
        }
    }

    internal static bool VALIDAREA(int x) => (x) >= MapDataConstants.AREATILE && (x) < (MapDataConstants.AREATILE + MapDataConstants.NUMAREAS);

    internal static bool ISPOINTER(Actor? check)
    {
        return check is objstruct;
    }


    internal int MAPSPOT(int x, int y, int plane) => (mapsegs[(plane)][((y) << MAPSHIFT) + (x)]);
    internal void SetMapSpot(int x, int y, int plane, ushort value)
    {
        (mapsegs[(plane)][((y) << MAPSHIFT) + (x)]) = value;
    }
}
