namespace PFWolf.Constants;

public static class MapConstants
{
    public const int TileShift = 16;
    public const long Global1 = (1L << 16);
    public const long TileGlobal = Global1;

    public const int MapShift = 6;
    public const int MapSize = (1 << MapShift);
    public const int MapArea = MapSize * MapSize;

    /// <summary>Wall ids on plane 0 are 1 to this; the game keeps its WallId and DoorNumber flags above them</summary>
    public const int MaxWallId = 255;

    /// <summary>Planes in a GAMEMAPS level: walls, objects and ECWolf's flats</summary>
    public const int MapPlanes = 3;

    /// <summary>
    /// Planes every loaded level has: walls, objects, flats (ECWolf's floor and ceiling, see
    /// <see cref="FlatPlane"/>), WallId heights, tags and light zones. A GAMEMAPS level has none
    /// of the last three, so they're empty.
    /// </summary>
    public const int LevelPlanes = 6;

    /// <summary>
    /// ECWolf's floor and ceiling plane: the low byte of a tile's value picks its floor flat and
    /// the high byte its ceiling flat, through the mapdefs flats table
    /// </summary>
    public const int FlatPlane = 2;

    /// <summary>Each tile's WallId height in stories, 0 where the map's default applies</summary>
    public const int HeightPlane = 3;

    /// <summary>
    /// Each tile's tag, 0 for none: a switch acts on the doors, walls and actors that share its tag
    /// </summary>
    public const int TagPlane = 4;

    /// <summary>Each tile's light zone, 0 for none (the map's light)</summary>
    public const int ZonePlane = 5;

    /// <summary>
    /// A WallId tile's patch (mapdefs patches, 1 to 255; 0 for none) is the high byte of its plane 0
    /// value, the WallId id the low byte: plane 0 value = WallId | patch &lt;&lt; PatchShift
    /// </summary>
    public const int PatchShift = 8;

    /// <summary>The plane 0 bits that hold the tile's own code (WallId, DoorNumber, floor), below its patch</summary>
    public const int Plane0CodeMask = 0xff;
}
