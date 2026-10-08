namespace PFWolf.Constants;

public static class MapConstants
{
    public const int TILESHIFT = 16;
    public const long GLOBAL1 = (1L << 16);
    public const long TILEGLOBAL = GLOBAL1;

    public const int MAPSHIFT = 6;
    public const int MAPSIZE = (1 << MAPSHIFT);
    public const int MAPAREA = MAPSIZE * MAPSIZE;

    /// <summary>Wall ids on plane 0 are 1 to this; the game keeps its wall and door flags above them</summary>
    public const int MAXWALLID = 63;

    /// <summary>Planes in a GAMEMAPS level: walls, objects and ECWolf's flats</summary>
    public const int MAPPLANES = 3;

    /// <summary>
    /// Planes every loaded level has: walls, objects, flats (ECWolf's floor and ceiling, see
    /// <see cref="FLATPLANE"/>), wall heights, tags and light zones. A GAMEMAPS level has none
    /// of the last three, so they're empty.
    /// </summary>
    public const int LEVELPLANES = 6;

    /// <summary>
    /// ECWolf's floor and ceiling plane: the low byte of a tile's value picks its floor flat and
    /// the high byte its ceiling flat, through the mapdefs flats table
    /// </summary>
    public const int FLATPLANE = 2;

    /// <summary>Each tile's wall height in stories, 0 where the map's default applies</summary>
    public const int HEIGHTPLANE = 3;

    /// <summary>
    /// Each tile's tag, 0 for none: a switch acts on the doors, walls and actors that share its tag
    /// </summary>
    public const int TAGPLANE = 4;

    /// <summary>Each tile's light zone, 0 for none (the map's light)</summary>
    public const int ZONEPLANE = 5;
}
