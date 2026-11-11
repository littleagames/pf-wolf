namespace PFWolf.Shading;

/// <summary>
/// THE LIGHT GRID: where the light falls on a level, shared by the game's renderer and the
/// editor's 3D view. Each tile has a base light, a level and a tint: its zone's, else the
/// level's. On top of that the level is split into light cells, LightCells a tile each way, and
/// each cell has light added to it (CellAdd, in light levels) by lights moving about, or set by
/// a set light (CellSet, CellSetWeight). Light is looked up at a point: the base light of a tile
/// (the open tile a wall is seen from, the tile a floor pixel or a sprite is on) plus the cell
/// the point is in. Tiles are indexed as the level indexes them (y × width + x).
/// </summary>
public sealed class LightGrid
{
    public const int LightCellShift = 3;
    public const int LightCells = 1 << LightCellShift;                          // cells a tile, each way
    public const int CellGlobalShift = MapConstants.TileShift - LightCellShift; // global units to cells
    public const int MaxLightLevel = ShadeMath.MaxLightLevel;

    /// <summary>Cells across and down the level: LightCells a tile each way (<see cref="SizeTo"/>)</summary>
    public int GridWidth { get; private set; } = MapConstants.ClassicMapSize << LightCellShift;
    public int GridHeight { get; private set; } = MapConstants.ClassicMapSize << LightCellShift;

    /// <summary>Each tile's base light: a light level, and a tint (an index in <see cref="Tints"/>)</summary>
    public byte[] TileLevel = new byte[MapConstants.ClassicMapArea], TileTint = new byte[MapConstants.ClassicMapArea];

    /// <summary>
    /// Light levels added to each cell, indexed cy × GridWidth + cx; the most of them any one
    /// light added, and that light's tint (0: none, the tile's own)
    /// </summary>
    public byte[] CellAdd = new byte[MapConstants.ClassicMapArea << (2 * LightCellShift)];
    public byte[] CellStrong = new byte[MapConstants.ClassicMapArea << (2 * LightCellShift)], CellTint = new byte[MapConstants.ClassicMapArea << (2 * LightCellShift)];

    /// <summary>
    /// What a set light (light.mode: set) moves each cell's base light by, up or down, toward
    /// its own; and how much of the way (0-255) it went, so where set lights overlap the nearest wins
    /// </summary>
    public sbyte[] CellSet = new sbyte[MapConstants.ClassicMapArea << (2 * LightCellShift)];
    public byte[] CellSetWeight = new byte[MapConstants.ClassicMapArea << (2 * LightCellShift)];

    /// <summary>The tints in use (0 is none, white), as zones and lights need them</summary>
    public readonly List<(byte R, byte G, byte B)> Tints = [];

    /// <summary>The cell (x, y in cells) in CellAdd, CellStrong, CellTint, CellSet and CellSetWeight</summary>
    public int CellIndex(int cx, int cy) => cy * GridWidth + cx;

    /// <summary>Sizes the tiles' and cells' lights to a level of this many tiles, if it's another size (cleared); true when it was</summary>
    public bool SizeTo(int width, int height)
    {
        int area = width * height, gridWidth = width << LightCellShift, gridHeight = height << LightCellShift;
        if (TileLevel.Length == area && gridWidth == GridWidth && gridHeight == GridHeight)
            return false;

        GridWidth = gridWidth;
        GridHeight = gridHeight;
        TileLevel = new byte[area];
        TileTint = new byte[area];
        CellAdd = new byte[gridWidth * gridHeight];
        CellStrong = new byte[gridWidth * gridHeight];
        CellTint = new byte[gridWidth * gridHeight];
        CellSet = new sbyte[gridWidth * gridHeight];
        CellSetWeight = new byte[gridWidth * gridHeight];
        return true;
    }

    /// <summary>A tint's index in <see cref="Tints"/>, added if it's new; more tints than a tile can name are untinted (0).</summary>
    public byte TintIndex((byte R, byte G, byte B) tint)
    {
        int index = Tints.IndexOf(tint);
        if (index >= 0)
            return (byte)index;
        if (Tints.Count > byte.MaxValue)
            return 0;
        Tints.Add(tint);
        return (byte)(Tints.Count - 1);
    }

    /// <summary>
    /// The light at a point (global units) on a tile (an index; 0 or more): the tile's base level
    /// plus the light added to (or set on) the point's cell, from 0 to full, and the cell's tint
    /// if a light tinted it, else the tile's
    /// </summary>
    public (int Level, int Tint) LightAt(int tile, long gx, long gy)
    {
        int level = TileLevel[tile], tint = TileTint[tile];
        long cx = gx >> CellGlobalShift, cy = gy >> CellGlobalShift;
        if ((ulong)cx < (ulong)GridWidth && (ulong)cy < (ulong)GridHeight)
        {
            long cell = cy * GridWidth + cx;
            level += CellAdd[cell] + CellSet[cell];
            if (level < 0)
                level = 0;
            if (CellTint[cell] != 0)
                tint = CellTint[cell];
        }
        return (level > MaxLightLevel ? MaxLightLevel : level, tint);
    }
}
