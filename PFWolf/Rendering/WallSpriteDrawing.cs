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
        int viewwidth = render.ViewWidth;
        if (wallspriteraydx.Length == viewwidth && wallspriteraysangle == render.MidAngle)
            return;

        if (wallspriteraydx.Length != viewwidth)
        {
            wallspriteraydx = new double[viewwidth];
            wallspriteraydy = new double[viewwidth];
        }

        const double TORADIANS = 2 * Math.PI / GameConstants.FineAngles;
        for (int x = 0; x < viewwidth; x++)
        {
            double angle = (render.MidAngle + render.PixelAngle[x]) * TORADIANS;
            wallspriteraydx[x] = Math.Cos(angle);
            wallspriteraydy[x] = -Math.Sin(angle);
        }
        wallspriteraysangle = render.MidAngle;
    }

    /// <summary>How far (x, y) is from the view point along the view direction, as CalcHeight's nx.</summary>
    internal double ViewDepth(double x, double y) =>
        ((x - render.ViewX) * render.ViewCos - (y - render.ViewY) * render.ViewSin) / 65536.0;

    /// <summary>A post's height (as WallHeight) at a view depth, kept short of DrawScaleds' 32000 "drawn" mark.</summary>
    internal short HeightAtDepth(double nx) =>
        (short)Math.Min(render.HeightNumerator * 256.0 / Math.Max(nx, GameConstants.MinDist), 31999);

    /// <summary>
    /// Fills in a wall sprite's VisList entry: its height is the panel's middle's, for sorting
    /// it among the other sprites. False if the panel is wholly behind the view.
    /// </summary>
    internal bool TransformWallSprite(Entities.Actors.Actor actor, VisibleObject vis)
    {
        if (WallSprites.GetWallSpriteSpan(actor) is not { } span)
            return false;

        if (ViewDepth(span.X1, span.Y1) < GameConstants.MinDist && ViewDepth(span.X2, span.Y2) < GameConstants.MinDist)
            return false;

        vis.ViewHeight = HeightAtDepth(ViewDepth((span.X1 + span.X2) / 2, (span.Y1 + span.Y2) / 2));
        vis.WallSprite = actor;
        return true;
    }

    /// <summary>Whether a wall sprite's tile, or one beside it (a panel on a tile edge shows from there), was seen.</summary>
    internal bool WallSpriteSeen(Entities.Actors.Actor actor)
    {
        int x = actor.TileX, y = actor.TileY;
        return map.Spotvis[x, y]
            || map.Spotvis[x + 1, y] || map.Spotvis[x - 1, y]
            || map.Spotvis[x, y + 1] || map.Spotvis[x, y - 1];
    }

    internal void ScaleWallSprite(VisibleObject vis)
    {
        if (vis.WallSprite == null || WallSprites.GetWallSpriteSpan(vis.WallSprite) is not { } span)
            return;

        var spriteAsset = assets.Find<SpriteAsset>(vis.ShapeNum);
        if (spriteAsset == null)
            return;

        SetupWallSpriteRays();

        //
        // the panel from its left end, relative to the view point
        //
        double ax = span.X1 - render.ViewX, ay = span.Y1 - render.ViewY;
        double ex = span.X2 - span.X1, ey = span.Y2 - span.Y1;
        double cos = render.ViewCos / 65536.0, sin = render.ViewSin / 65536.0;

        for (int x = 0; x < render.ViewWidth; x++)
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
            if (render.WallHeight[x] >= height)
                continue;                                   // a wall is in front

            int half = height >> 3;
            if (half == 0)
                continue;

            // Stretched across the panel, so only its offset's height counts: that row on the floor
            int column = Math.Min((int)(t * spriteAsset.Width), spriteAsset.Width - 1);
            int top = floorRow(half) - SpriteScaler.TexelsToPixels(spriteAsset.Origin.Y, half);
            scaler.ScaleColumn((short)x, (short)top, MathUtils.FixedDiv(half, GameConstants.TextureSize / 2), spriteAsset, column,
                vis.Bright ? Lighting.NoShade : lighting.LightAt(map.TileIndexOrNone(vis.TileX, vis.TileY), vis.WorldX, vis.WorldY),
                vis.Bright ? 0 : lighting.ShadeOffsetForHeight(height), translation: vis.Translation);
        }
    }
}
