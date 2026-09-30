using Wolf3D.Assets;
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

    The panel blocks along its line: the player and projectiles can stand or fly on either
    side of it in its tile, and it blocks sight and hitscan unless it has the
    WALLSPRITE.SHOOTTHROUGH flag (a grille or chain-link fence), and projectiles unless it has
    WALLSPRITE.PROJECTILETHROUGH. Enemies keep out of its whole tile (WallSpriteBlocker in
    actorat), as they do a diagonal wall's.

    =============================================================================
    */

    internal const string WallSpriteFlag = "WALLSPRITE";
    internal const string WallSpriteShootThroughFlag = "WALLSPRITE.SHOOTTHROUGH";
    internal const string WallSpriteProjectileThroughFlag = "WALLSPRITE.PROJECTILETHROUGH";
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

    /*
    =============================================================================

                                WALL SPRITE DRAWING

    Each screen column's view ray (the one the wall trace casts) is met with the panel, which
    gives the sprite column that shows there and how far away it is -- measured along the
    view, as CalcHeight measures walls, so a panel and a wall at the same spot are the same
    height. The texture is pinned to the panel, so from behind it comes out mirrored.

    =============================================================================
    */

    // The direction of each column's ray, as fractions of a unit (y grows south)
    static double[] wallspriteraydx = [], wallspriteraydy = [];
    static int wallspriteraysangle = -1;

    static void SetupWallSpriteRays()
    {
        if (wallspriteraydx.Length == viewwidth && wallspriteraysangle == midangle)
            return;

        if (wallspriteraydx.Length != viewwidth)
        {
            wallspriteraydx = new double[viewwidth];
            wallspriteraydy = new double[viewwidth];
        }

        const double TORADIANS = 2 * Math.PI / FINEANGLES;
        for (int x = 0; x < viewwidth; x++)
        {
            double angle = (midangle + pixelangle[x]) * TORADIANS;
            wallspriteraydx[x] = Math.Cos(angle);
            wallspriteraydy[x] = -Math.Sin(angle);
        }
        wallspriteraysangle = midangle;
    }

    /// <summary>How far (x, y) is from the view point along the view direction, as CalcHeight's nx.</summary>
    static double ViewDepth(double x, double y) =>
        ((x - viewx) * viewcos - (y - viewy) * viewsin) / 65536.0;

    /// <summary>A post's height (as wallheight) at a view depth, kept short of DrawScaleds' 32000 "drawn" mark.</summary>
    static short HeightAtDepth(double nx) =>
        (short)Math.Min(heightnumerator * 256.0 / Math.Max(nx, MINDIST), 31999);

    /// <summary>
    /// Fills in a wall sprite's vislist entry: its height is the panel's middle's, for sorting
    /// it among the other sprites. False if the panel is wholly behind the view.
    /// </summary>
    internal static bool TransformWallSprite(Entities.Actors.Actor actor, visobj_t vis)
    {
        if (GetWallSpriteSpan(actor) is not { } span)
            return false;

        if (ViewDepth(span.X1, span.Y1) < MINDIST && ViewDepth(span.X2, span.Y2) < MINDIST)
            return false;

        vis.viewheight = HeightAtDepth(ViewDepth((span.X1 + span.X2) / 2, (span.Y1 + span.Y2) / 2));
        vis.wallsprite = actor;
        return true;
    }

    /// <summary>Whether a wall sprite's tile, or one beside it (a panel on a tile edge shows from there), was seen.</summary>
    internal static bool WallSpriteSeen(Entities.Actors.Actor actor)
    {
        int x = actor.TileX, y = actor.TileY;
        return _mapManager.spotvis[x, y]
            || _mapManager.spotvis[x + 1, y] || _mapManager.spotvis[x - 1, y]
            || _mapManager.spotvis[x, y + 1] || _mapManager.spotvis[x, y - 1];
    }

    internal static void ScaleWallSprite(visobj_t vis)
    {
        if (vis.wallsprite == null || GetWallSpriteSpan(vis.wallsprite) is not { } span)
            return;

        var spriteAsset = _assetManager.Find<SpriteAsset>(vis.shapenum);
        if (spriteAsset == null)
            return;

        SetupWallSpriteRays();

        //
        // the panel from its left end, relative to the view point
        //
        double ax = span.X1 - viewx, ay = span.Y1 - viewy;
        double ex = span.X2 - span.X1, ey = span.Y2 - span.Y1;
        double cos = viewcos / 65536.0, sin = viewsin / 65536.0;

        for (int x = 0; x < viewwidth; x++)
        {
            double dx = wallspriteraydx[x], dy = wallspriteraydy[x];

            //
            // where the ray (s along it) meets the panel (t along it, 0 at its left end)
            //
            double det = ex * dy - ey * dx;
            if (Math.Abs(det) < 1e-9)
                continue;                                   // seen edge on

            double t = (dx * ay - dy * ax) / det;
            if (t < 0 || t >= 1)
                continue;

            double s = (ex * ay - ey * ax) / det;
            if (s <= 0)
                continue;                                   // behind the view

            short height = HeightAtDepth(s * (dx * cos - dy * sin));
            if (wallheight[x] >= height)
                continue;                                   // a wall is in front

            int half = height >> 3;
            if (half == 0)
                continue;

            int column = Math.Min((int)(t * spriteAsset.Width), spriteAsset.Width - 1);
            ScaleColumn((short)x, (short)(centery - half), MathUtils.FixedDiv(half, TEXTURESIZE / 2), spriteAsset, column,
                vis.bright ? noshade : TileLight(vis.tilex, vis.tiley), vis.bright ? 0 : ShadeOffsetForHeight(height));
        }
    }

    /*
    =============================================================================

                            WALL SPRITE COLLISION AND SIGHT

    =============================================================================
    */

    // The panel the player last ran into in TryMove, for ClipMove to slide along
    static WallSpriteSpan? wallspriteblock;

    // Where ClipMove's move starts: a panel the player already overlaps there (one spawned on
    // top of them) doesn't block, so they can always step off it
    static int wallspritefromx, wallspritefromy;

    /// <summary>
    /// Whether a box of half-width <paramref name="size"/> centred on (x, y) crosses the panel.
    /// Touching it along the box's edge doesn't count, so the player can slide along one.
    /// </summary>
    internal static bool BoxHitsWallSprite(WallSpriteSpan span, long x, long y, long size)
    {
        double dx = span.X2 - span.X1, dy = span.Y2 - span.Y1;

        //
        // clip the panel to the box (Liang-Barsky)
        //
        double t0 = 0, t1 = 1;
        bool Clip(double p, double q)
        {
            if (p == 0)
                return q > 0;
            double r = q / p;
            if (p < 0)
                t0 = Math.Max(t0, r);
            else
                t1 = Math.Min(t1, r);
            return t0 < t1;
        }

        return Clip(-dx, span.X1 - (x - size)) && Clip(dx, x + size - span.X1)
            && Clip(-dy, span.Y1 - (y - size)) && Clip(dy, y + size - span.Y1);
    }

    /// <summary>Whether the line from (x1, y1) to (x2, y2) crosses the panel.</summary>
    internal static bool LineHitsWallSprite(WallSpriteSpan span, long x1, long y1, long x2, long y2)
    {
        static double Cross(double ax, double ay, double bx, double by) => ax * by - ay * bx;

        double ex = span.X2 - span.X1, ey = span.Y2 - span.Y1;
        double d1 = Cross(ex, ey, x1 - span.X1, y1 - span.Y1);
        double d2 = Cross(ex, ey, x2 - span.X1, y2 - span.Y1);
        if (d1 > 0 && d2 > 0 || d1 < 0 && d2 < 0 || d1 == 0 && d2 == 0)
            return false;                               // both ends on one side, or along it

        double lx = x2 - x1, ly = y2 - y1;
        double d3 = Cross(lx, ly, span.X1 - x1, span.Y1 - y1);
        double d4 = Cross(lx, ly, span.X2 - x1, span.Y2 - y1);
        return !(d3 > 0 && d4 > 0 || d3 < 0 && d4 < 0);
    }

    /// <summary>
    /// The first panel standing in tiles xl..xh, yl..yh that a box of half-width
    /// <paramref name="size"/> centred on (x, y) crosses. For the player, a panel the box
    /// already crossed where ClipMove's move began is skipped; otherwise it's a projectile's box,
    /// which WALLSPRITE.PROJECTILETHROUGH panels let by.
    /// </summary>
    internal static WallSpriteSpan? WallSpriteHitByBox(int xl, int yl, int xh, int yh, long x, long y, long size, bool player)
    {
        foreach (var actor in _mapManager.GetActors())
        {
            if (actor.TileX < xl || actor.TileX > xh || actor.TileY < yl || actor.TileY > yh)
                continue;
            if (!IsWallSprite(actor) || GetWallSpriteSpan(actor) is not { } span)
                continue;
            if (!player && actor.Flags.Contains(WallSpriteProjectileThroughFlag, StringComparer.OrdinalIgnoreCase))
                continue;
            if (!BoxHitsWallSprite(span, x, y, size))
                continue;
            if (player && BoxHitsWallSprite(span, wallspritefromx, wallspritefromy, size))
                continue;
            return span;
        }
        return null;
    }

    /// <summary>Whether a panel that isn't WALLSPRITE.SHOOTTHROUGH stands across the line (sight, hitscan).</summary>
    internal static bool WallSpriteBlocksLine(long x1, long y1, long x2, long y2)
    {
        long xl = Math.Min(x1, x2) >> MapConstants.TILESHIFT, xh = Math.Max(x1, x2) >> MapConstants.TILESHIFT;
        long yl = Math.Min(y1, y2) >> MapConstants.TILESHIFT, yh = Math.Max(y1, y2) >> MapConstants.TILESHIFT;

        foreach (var actor in _mapManager.GetActors())
        {
            if (actor.TileX < xl || actor.TileX > xh || actor.TileY < yl || actor.TileY > yh)
                continue;
            if (!IsWallSprite(actor) || actor.Flags.Contains(WallSpriteShootThroughFlag, StringComparer.OrdinalIgnoreCase))
                continue;
            if (GetWallSpriteSpan(actor) is { } span && LineHitsWallSprite(span, x1, y1, x2, y2))
                return true;
        }
        return false;
    }
}
