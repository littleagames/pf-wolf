using PFWolf.Constants;

namespace PFWolf.Rendering;

/// <summary>
/// Where a point in the world is from this frame's view (RenderState: the camera's spot, angle
/// and scale), as sprites are placed: how far along the view and across it, and the pixel on
/// screen. Points (PointDrawing) and objective markers (Ui/ObjectiveMarkerDrawing.cs) share it.
/// </summary>
internal static class ViewProjection
{
    /// <summary>A point (global units) seen from the view: nx how far ahead, ny how far to the right (behind: nx below 0)</summary>
    internal static (long nx, long ny) ToView(RenderState render, long gx, long gy)
    {
        long dx = gx - render.ViewX, dy = gy - render.ViewY;
        long nx = (dx * render.ViewCos >> 16) - (dy * render.ViewSin >> 16);
        long ny = (dx * render.ViewSin >> 16) + (dy * render.ViewCos >> 16);
        return (nx, ny);
    }

    /// <summary>
    /// Where a point (global units, z texels up from the player's floor) is on the view, in view
    /// pixels: nx how far along the view it is, half how many pixels half a story is there. False
    /// when it's too close or behind the view.
    /// </summary>
    internal static bool Project(RenderState render, long gx, long gy, double z, out int sx, out int sy, out long nx, out double half)
    {
        (nx, long ny) = ToView(render, gx, gy);
        if (nx < GameConstants.MinDist)
        {
            sx = sy = 0;
            half = 0;
            return false;
        }

        sx = (int)Math.Clamp(render.CenterX + ny * render.Scale / nx, -100000, 100000);
        half = render.HeightNumerator * 32.0 / nx;
        sy = (int)Math.Clamp(render.CenterY + half * (render.ViewEyeZ - z) / (GameConstants.TextureSize / 2), -100000, 100000);
        return true;
    }
}
