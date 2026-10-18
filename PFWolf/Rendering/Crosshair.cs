namespace PFWolf.Rendering;

/// <summary>
/// A small cross in the middle of the view, over the weapon in hand (the Game Options crosshair
/// setting), in game-info crosshair-color. It marks the middle column shots go down; it stays
/// at the view's middle row when looking up or down, since aiming ignores the pitch.
/// </summary>
internal sealed class Crosshair(VideoManager video, RenderState render, string? color)
{
    private readonly string colorName = string.IsNullOrWhiteSpace(color) ? "White" : color;

    internal void Draw()
    {
        // Arms of about 2% of the view's height, a pixel thick for each 200 rows, with a gap
        int thick = Math.Max(1, (render.ViewHeight + 100) / 200);
        int arm = Math.Max(2, render.ViewHeight / 50);
        int gap = Math.Max(1, arm / 2);
        int cx = render.CenterX, cy = render.BaseCenterY;
        byte col = video.ResolveColor(colorName);

        for (int d = gap; d <= gap + arm; d++)
        {
            for (int t = -(thick / 2); t < thick - thick / 2; t++)
            {
                Plot(cx - d, cy + t, col);
                Plot(cx + d, cy + t, col);
                Plot(cx + t, cy - d, col);
                Plot(cx + t, cy + d, col);
            }
        }
    }

    private unsafe void Plot(int x, int y, byte col)
    {
        if (x < 0 || y < 0 || x >= render.ViewWidth || y >= render.ViewHeight)
            return;
        byte* dest = (byte*)render.VbufPtr + render.ScreenOfs;
        dest[video.YLookup[y] + x] = col;
    }
}
