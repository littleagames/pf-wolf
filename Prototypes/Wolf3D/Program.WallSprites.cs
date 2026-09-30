using Wolf3D.Constants;

namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                        WALL SPRITES (DIRECTIONAL 3D SPRITES)

    A static with `flags: [WALLSPRITE]` in actordefs is drawn as a flat panel standing in its
    tile, like Wolf4SDL's directional 3D sprites, instead of a billboard -- fences, gates and
    grilles. Its map `angles` (actor.Dir) is the way the front of the panel faces: 0/180 face
    east/west (a north-south panel), 90/270 face north/south (an east-west panel), and the
    four diagonals stand the panel corner to corner. The back shows the sprite mirrored.

    The `wallsprite.offset` property moves the panel off the tile centre toward its front, in
    texels (64 to a tile): 32 puts an axis-aligned panel on the tile edge. The panel is always
    clipped to its own tile. Coordinates are 16.16 global fixed point.

    =============================================================================
    */

    internal const string WallSpriteFlag = "WALLSPRITE";
    internal const string WallSpriteOffsetProperty = "wallsprite.offset";

    /// <summary>
    /// A panel's ends: (X1, Y1) is the sprite's left edge seen from the front, (X2, Y2) its right
    /// edge, so the sprite's columns run from end 1 to end 2.
    /// </summary>
    internal readonly record struct WallSpriteSpan(double X1, double Y1, double X2, double Y2)
    {
        public double Length => Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));
    }

    internal static bool IsWallSprite(Entities.Actors.Actor actor) =>
        actor.Flags.Contains(WallSpriteFlag, StringComparer.OrdinalIgnoreCase);

    /// <summary>Which way a facing's panel runs across the tile, for messages.</summary>
    internal static string WallSpriteAxis(objdirtypes dir) => dir switch
    {
        objdirtypes.east or objdirtypes.west => "north-south",
        objdirtypes.north or objdirtypes.south => "east-west",
        objdirtypes.northeast or objdirtypes.southwest => "northwest-southeast",
        objdirtypes.northwest or objdirtypes.southeast => "northeast-southwest",
        _ => "none",
    };

    /// <summary>
    /// Where a wall sprite's panel stands, or null if the actor isn't one, has no facing, or its
    /// offset pushes the panel out of its tile.
    /// </summary>
    internal static WallSpriteSpan? GetWallSpriteSpan(Entities.Actors.Actor actor)
    {
        if (!IsWallSprite(actor) || actor.Dir == objdirtypes.nodir)
            return null;

        //
        // the front's normal (y grows south) and the direction the sprite's columns run, which
        // is the right hand of someone looking at the front
        //
        double angle = (int)actor.Dir * Math.PI / 4;
        double nx = Math.Round(Math.Cos(angle), 12), ny = -Math.Round(Math.Sin(angle), 12);
        double dx = ny, dy = -nx;

        double offset = actor.Properties.TryGetValue(WallSpriteOffsetProperty, out var value)
            ? Convert.ToDouble(value) * MapConstants.TILEGLOBAL / 64
            : 0;

        double ox = (double)actor.TileX * MapConstants.TILEGLOBAL;
        double oy = (double)actor.TileY * MapConstants.TILEGLOBAL;
        double px = ox + MapConstants.TILEGLOBAL / 2 + nx * offset;
        double py = oy + MapConstants.TILEGLOBAL / 2 + ny * offset;

        //
        // clip the panel's line to the tile (Liang-Barsky, unbounded in both directions)
        //
        double t0 = double.NegativeInfinity, t1 = double.PositiveInfinity;
        bool Clip(double p, double q)
        {
            if (p == 0)
                return q >= 0;
            double r = q / p;
            if (p < 0)
                t0 = Math.Max(t0, r);
            else
                t1 = Math.Min(t1, r);
            return t0 < t1;
        }

        if (!Clip(-dx, px - ox) || !Clip(dx, ox + MapConstants.TILEGLOBAL - px)
            || !Clip(-dy, py - oy) || !Clip(dy, oy + MapConstants.TILEGLOBAL - py))
            return null;

        return new WallSpriteSpan(px + t0 * dx, py + t0 * dy, px + t1 * dx, py + t1 * dy);
    }
}
