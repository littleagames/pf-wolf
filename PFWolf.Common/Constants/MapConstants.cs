namespace PFWolf.Constants;

public static class MapConstants
{
    public const int TileShift = 16;
    public const long Global1 = (1L << 16);
    public const long TileGlobal = Global1;

    /// <summary>
    /// Wolf3D's map size, each way: what GAMEMAPS levels are and what a new level starts as. A
    /// level can be any width and height from <see cref="MinMapSize"/> to <see cref="MaxMapSize"/>.
    /// </summary>
    public const int ClassicMapSize = 64;
    public const int ClassicMapArea = ClassicMapSize * ClassicMapSize;

    /// <summary>The smallest a level can be each way: a wall round one open tile</summary>
    public const int MinMapSize = 3;

    /// <summary>The largest a level can be each way: an actor's tile coordinates are bytes</summary>
    public const int MaxMapSize = 256;

    /// <summary>The most doors a level can have (the game keeps a door's number below its wall bits)</summary>
    public const int MaxDoors = 256;

    /// <summary>Why a level of this size can't be loaded, or null when it can</summary>
    public static string? CheckMapSize(int width, int height) =>
        width is >= MinMapSize and <= MaxMapSize && height is >= MinMapSize and <= MaxMapSize
            ? null
            : $"it's {width}x{height}; PFWolf levels are {MinMapSize} to {MaxMapSize} tiles each way";

    /// <summary>Wall ids on plane 0 are 1 to this; the game keeps its wall and door flags above them</summary>
    public const int MaxWallId = 255;

    /// <summary>Planes in a GAMEMAPS level: walls, objects and ECWolf's flats</summary>
    public const int MapPlanes = 3;

    /// <summary>
    /// Planes every loaded level has: walls, objects, flats (ECWolf's floor and ceiling, see
    /// <see cref="FlatPlane"/>), wall heights, tags and light zones. A GAMEMAPS level has none
    /// of the last three, so they're empty.
    /// </summary>
    public const int LevelPlanes = 6;

    /// <summary>
    /// ECWolf's floor and ceiling plane: the low byte of a tile's value picks its floor flat and
    /// the high byte its ceiling flat, through the mapdefs flats table
    /// </summary>
    public const int FlatPlane = 2;

    /// <summary>Each tile's wall height in stories, 0 where the map's default applies</summary>
    public const int HeightPlane = 3;

    /// <summary>
    /// Each tile's tag, 0 for none: a switch acts on the doors, walls and actors that share its tag
    /// </summary>
    public const int TagPlane = 4;

    /// <summary>Each tile's light zone, 0 for none (the map's light)</summary>
    public const int ZonePlane = 5;

    /// <summary>
    /// A wall tile's patch (mapdefs patches, 1 to 255; 0 for none) is the high byte of its plane 0
    /// value, the wall id the low byte: plane 0 value = wall | patch &lt;&lt; PatchShift
    /// </summary>
    public const int PatchShift = 8;

    /// <summary>The plane 0 bits that hold the tile's own code (wall, door, floor), below its patch</summary>
    public const int Plane0CodeMask = 0xff;
}
