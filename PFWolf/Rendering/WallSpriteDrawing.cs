using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Rendering;

/// <summary>
/// WALL SPRITE DRAWING: each screen column's view ray (the one the wall trace casts) is met with
/// the panel (WallSprites), which gives the sprite column that shows there and how far away it
/// is -- measured along the view, as CalcHeight measures walls, so a panel and a wall at the
/// same spot are the same height. The texture is pinned to the panel, so from behind it comes
/// out mirrored.
/// </summary>
/// <param name="floorRow">The screen row the floor is on for something half high (WL_DRAW's FloorRow)</param>
internal sealed class WallSpriteDrawing(
    AssetManager assets,
    MapManager map,
    Lighting lighting,
    RenderState render,
    SpriteScaler scaler,
    Func<int, int> floorRow)
{
    // The direction of each column's ray, as fractions of a unit (y grows south)
    double[] wallspriteraydx = [], wallspriteraydy = [];
    int wallspriteraysangle = -1;

    void SetupWallSpriteRays()
    {
        int viewwidth = render.viewwidth;
        if (wallspriteraydx.Length == viewwidth && wallspriteraysangle == render.midangle)
            return;

        if (wallspriteraydx.Length != viewwidth)
        {
            wallspriteraydx = new double[viewwidth];
            wallspriteraydy = new double[viewwidth];
        }

        const double TORADIANS = 2 * Math.PI / Program.FINEANGLES;
        for (int x = 0; x < viewwidth; x++)
        {
            double angle = (render.midangle + render.pixelangle[x]) * TORADIANS;
            wallspriteraydx[x] = Math.Cos(angle);
            wallspriteraydy[x] = -Math.Sin(angle);
        }
        wallspriteraysangle = render.midangle;
    }

    /// <summary>How far (x, y) is from the view point along the view direction, as CalcHeight's nx.</summary>
    internal double ViewDepth(double x, double y) =>
        ((x - render.viewx) * render.viewcos - (y - render.viewy) * render.viewsin) / 65536.0;

    /// <summary>A post's height (as wallheight) at a view depth, kept short of DrawScaleds' 32000 "drawn" mark.</summary>
    internal short HeightAtDepth(double nx) =>
        (short)Math.Min(render.heightnumerator * 256.0 / Math.Max(nx, Program.MINDIST), 31999);

    /// <summary>
    /// Fills in a wall sprite's vislist entry: its height is the panel's middle's, for sorting
    /// it among the other sprites. False if the panel is wholly behind the view.
    /// </summary>
    internal bool TransformWallSprite(Entities.Actors.Actor actor, visobj_t vis)
    {
        if (WallSprites.GetWallSpriteSpan(actor) is not { } span)
            return false;

        if (ViewDepth(span.X1, span.Y1) < Program.MINDIST && ViewDepth(span.X2, span.Y2) < Program.MINDIST)
            return false;

        vis.viewheight = HeightAtDepth(ViewDepth((span.X1 + span.X2) / 2, (span.Y1 + span.Y2) / 2));
        vis.wallsprite = actor;
        return true;
    }

    /// <summary>Whether a wall sprite's tile, or one beside it (a panel on a tile edge shows from there), was seen.</summary>
    internal bool WallSpriteSeen(Entities.Actors.Actor actor)
    {
        int x = actor.TileX, y = actor.TileY;
        return map.spotvis[x, y]
            || map.spotvis[x + 1, y] || map.spotvis[x - 1, y]
            || map.spotvis[x, y + 1] || map.spotvis[x, y - 1];
    }

    internal void ScaleWallSprite(visobj_t vis)
    {
        if (vis.wallsprite == null || WallSprites.GetWallSpriteSpan(vis.wallsprite) is not { } span)
            return;

        var spriteAsset = assets.Find<SpriteAsset>(vis.shapenum);
        if (spriteAsset == null)
            return;

        SetupWallSpriteRays();

        //
        // the panel from its left end, relative to the view point
        //
        double ax = span.X1 - render.viewx, ay = span.Y1 - render.viewy;
        double ex = span.X2 - span.X1, ey = span.Y2 - span.Y1;
        double cos = render.viewcos / 65536.0, sin = render.viewsin / 65536.0;

        for (int x = 0; x < render.viewwidth; x++)
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
            if (render.wallheight[x] >= height)
                continue;                                   // a wall is in front

            int half = height >> 3;
            if (half == 0)
                continue;

            // Stretched across the panel, so only its offset's height counts: that row on the floor
            int column = Math.Min((int)(t * spriteAsset.Width), spriteAsset.Width - 1);
            int top = floorRow(half) - SpriteScaler.TexelsToPixels(spriteAsset.Origin.Y, half);
            scaler.ScaleColumn((short)x, (short)top, MathUtils.FixedDiv(half, Program.TEXTURESIZE / 2), spriteAsset, column,
                vis.bright ? Lighting.noshade : lighting.LightAt(Lighting.TileIndex(vis.tilex, vis.tiley), vis.worldx, vis.worldy),
                vis.bright ? 0 : lighting.ShadeOffsetForHeight(height), translation: vis.translation);
        }
    }
}
