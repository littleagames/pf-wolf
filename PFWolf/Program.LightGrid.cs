using PFWolf.Constants;
using PFWolf.Managers;

namespace PFWolf;

internal partial class Program
{
    /*
    =============================================================================

                                LIGHT GRID

    Where the light falls. Each tile has a base light, a level and a tint: its zone's, else the
    level's. On top of that the level is split into light cells, LIGHTCELLS a tile each way,
    and each cell has light added to it (celladd, in light levels) by lights moving about.
    Light is looked up at a point: the base light of a tile (the open tile a wall is seen
    from, the tile a floor pixel or a sprite is on) plus the cell the point is in.

    =============================================================================
    */

    internal const int LIGHTCELLSHIFT = 3;
    internal const int LIGHTCELLS = 1 << LIGHTCELLSHIFT;                    // cells a tile, each way
    const int CELLGLOBALSHIFT = MapConstants.TILESHIFT - LIGHTCELLSHIFT;    // global units to cells
    internal const int GRIDSHIFT = MapManager.MAPSHIFT + LIGHTCELLSHIFT;
    internal const int GRIDSIZE = 1 << GRIDSHIFT;                           // cells across the map
    const int MAXLIGHTLEVEL = LIGHTLEVELS - 1;

    // Each tile's base light, indexed (y << MAPSHIFT) + x like the flats: a light level, and a
    // tint (an index in tints)
    static readonly byte[] tilelevel = new byte[MapManager.MAPAREA], tiletint = new byte[MapManager.MAPAREA];

    // Light levels added to each cell, indexed (y << GRIDSHIFT) + x in cells; the most of them any
    // one light added, and that light's tint (0: none, the tile's own)
    static readonly byte[] celladd = new byte[GRIDSIZE * GRIDSIZE];
    static readonly byte[] cellstrong = new byte[GRIDSIZE * GRIDSIZE], celltint = new byte[GRIDSIZE * GRIDSIZE];

    // The tints in use (0 is none), and each one's light rows by level, made as they're needed
    static readonly List<(byte R, byte G, byte B)> tints = [];
    static readonly List<byte[]?[]> tintrows = [];

    static int ambientlevel;            // the level's own light level (tint 0)
    static bool griddirty = true;       // the tiles' base lights need building again
    static int gridversion;             // the MapManager.ZonesVersion they were built for
    static bool haszones;               // some tile's base light isn't the level's

    static readonly (byte, byte, byte) NoTint = (255, 255, 255);

    /// <summary>A tint's index in tints, added if it's new.</summary>
    static byte TintIndex((byte R, byte G, byte B) tint)
    {
        int index = tints.IndexOf(tint);
        if (index >= 0)
            return (byte)index;
        if (tints.Count > byte.MaxValue)
            return 0;                   // more tints than a tile can name: untinted
        tints.Add(tint);
        tintrows.Add(new byte[LIGHTLEVELS][]);
        return (byte)(tints.Count - 1);
    }

    /// <summary>Forgets the light rows (they're for the old fade), leaving the tints with only none.</summary>
    static void ResetLightRows()
    {
        tints.Clear();
        tintrows.Clear();
        colortints.Clear();
        // cells tinted from the old tints mustn't name new ones
        Array.Clear(celltint);
        TintIndex(NoTint);
    }

    // A tint's light row for a light level
    static byte[] RowOf(int tint, int level) => tintrows[tint][level] ??= LevelRow(level, tints[tint]);

    /// <summary>The index of a tile in tilelevel and tiletint, or -1 off the map.</summary>
    static int TileIndex(int x, int y) =>
        (uint)x < MapManager.MAPSIZE && (uint)y < MapManager.MAPSIZE ? (y << MapManager.MAPSHIFT) + x : -1;

    /// <summary>
    /// The light row at a point (global units): the base light of a tile (a TileIndex; off the
    /// map, the level's) plus the light added to the point's cell.
    /// </summary>
    static byte[] LightAt(int tile, long gx, long gy)
    {
        if (!shading)
            return noshade;
        if (tile < 0)
            return lightrow;

        int level = tilelevel[tile], tint = tiletint[tile];
        long cx = gx >> CELLGLOBALSHIFT, cy = gy >> CELLGLOBALSHIFT;
        if ((ulong)cx < GRIDSIZE && (ulong)cy < GRIDSIZE)
        {
            long cell = (cy << GRIDSHIFT) + cx;
            level += celladd[cell];
            if (celltint[cell] != 0)
                tint = celltint[cell];
        }
        return RowOf(tint, level > MAXLIGHTLEVEL ? MAXLIGHTLEVEL : level);
    }

    /// <summary>The middle of a tile, in global units: where something placed on the tile stands.</summary>
    static (int X, int Y) TileCenter(int x, int y) =>
        ((x << MapConstants.TILESHIFT) + (int)MapConstants.TILEGLOBAL / 2, (y << MapConstants.TILESHIFT) + (int)MapConstants.TILEGLOBAL / 2);

    /// <summary>Brings the tiles' base lights up to date with the zone plane and the zones; called before each frame.</summary>
    static void UpdateLightGrid()
    {
        if (!griddirty && gridversion == _mapManager.ZonesVersion)
            return;
        griddirty = false;
        gridversion = _mapManager.ZonesVersion;

        haszones = false;
        for (int y = 0; y < MapManager.MAPSIZE; y++)
            for (int x = 0; x < MapManager.MAPSIZE; x++)
            {
                int i = (y << MapManager.MAPSHIFT) + x;
                tilelevel[i] = (byte)ambientlevel;
                tiletint[i] = 0;
                if (x < _mapManager.mapwidth && y < _mapManager.mapheight
                    && _mapManager.GetZone(x, y) is var id and not 0
                    && levelzones.TryGetValue(id, out var zone) && zone.RowLevel >= 0)
                {
                    tilelevel[i] = (byte)zone.RowLevel;
                    tiletint[i] = zone.RowTint;
                    haszones |= zone.RowLevel != ambientlevel || zone.RowTint != 0;
                }
            }
    }
}
