using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf;

internal partial class Program
{
    // Wall sprites' geometry (Rendering/WallSprites.cs) and their blocking on the level
    // (Rendering/WallSprites.cs WallSpriteBlocking); drawing them is below
    static WallSpriteBlocking wallSpriteBlocking = null!;       // made in InitServices

    internal const string WallSpriteFlag = WallSprites.WallSpriteFlag;
    internal static bool IsWallSprite(Entities.Actors.Actor actor) => WallSprites.IsWallSprite(actor);
    internal static string WallSpriteAxis(objdirtypes dir) => WallSprites.WallSpriteAxis(dir);
    internal static WallSpriteSpan? GetWallSpriteSpan(Entities.Actors.Actor actor) => WallSprites.GetWallSpriteSpan(actor);

    static int wallspritefromx { get => wallSpriteBlocking.wallspritefromx; set => wallSpriteBlocking.wallspritefromx = value; }
    static int wallspritefromy { get => wallSpriteBlocking.wallspritefromy; set => wallSpriteBlocking.wallspritefromy = value; }
    internal static WallSpriteSpan? WallSpriteHitByBox(int xl, int yl, int xh, int yh, long x, long y, long size, bool player) =>
        wallSpriteBlocking.WallSpriteHitByBox(xl, yl, xh, yh, x, y, size, player);
    internal static bool WallSpriteBlocksLine(long x1, long y1, long x2, long y2) => wallSpriteBlocking.WallSpriteBlocksLine(x1, y1, x2, y2);
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

            // Stretched across the panel, so only its offset's height counts: that row on the floor
            int column = Math.Min((int)(t * spriteAsset.Width), spriteAsset.Width - 1);
            int top = FloorRow(half) - TexelsToPixels(spriteAsset.Origin.Y, half);
            ScaleColumn((short)x, (short)top, MathUtils.FixedDiv(half, TEXTURESIZE / 2), spriteAsset, column,
                vis.bright ? noshade : LightAt(TileIndex(vis.tilex, vis.tiley), vis.worldx, vis.worldy),
                vis.bright ? 0 : ShadeOffsetForHeight(height), translation: vis.translation);
        }
    }
}
