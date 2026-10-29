using PFWolf.Assets;

namespace PFWolf.Rendering;

/// <summary>
/// See-through doors: a shut door whose texture is masked (a pk3 picture with clear pixels, now
/// 0xFF) is seen through. The ray trace goes on past it as past an open door, keeping it
/// (WallPosts.QueueSeeThrough) rather than queuing a post, so the walls, floor and sprites behind
/// it are drawn. It's drawn here, over them, in among the sprites and weather by distance: before
/// a sprite's column (or a raindrop) is drawn, the doors in that column farther away than it are
/// (DrawBeyond), and the rest once they all are (DrawRest).
/// </summary>
/// <param name="floorRow">The screen row the floor is on for something half high (WL_DRAW's FloorRow)</param>
internal sealed class SeeThroughPosts(
    VideoManager video,
    Lighting lighting,
    RenderState render,
    Func<int, int> floorRow)
{
    const byte Mask = 0xff;             // a masked texture's see-through pixels

    /// <summary>Draws column x's see-through doors farther away than something height high (as WallHeight), farthest first</summary>
    internal void DrawBeyond(int x, int height)
    {
        if (!render.AnySeeThrough || (uint)x >= (uint)render.SeeThroughCount.Length)
            return;
        int first = x * RenderState.MaxSeeThrough;
        int left = render.SeeThroughCount[x];
        while (left > 0 && render.SeeThroughHeight[first + left - 1] < height)
        {
            left--;
            render.SeeThroughCount[x] = left;
            DrawDoor(x, first + left);
        }
    }

    /// <summary>Draws every see-through door not drawn yet: those nearer than every sprite</summary>
    internal void DrawRest()
    {
        if (!render.AnySeeThrough)
            return;
        int width = Math.Min(render.ViewWidth, render.SeeThroughCount.Length);
        for (int x = 0; x < width; x++)
            DrawBeyond(x, int.MaxValue);
        render.AnySeeThrough = false;
    }

    /// <summary>
    /// Draws see-through door <paramref name="at"/> in column x, as DrawPost draws a wall from
    /// the floor up, leaving what's behind showing through its clear pixels
    /// </summary>
    void DrawDoor(int x, int at)
    {
        short height = render.SeeThroughHeight[at];
        int half = height >> 3;
        if (half <= 0) half = 100;

        int floor = floorRow(half);
        int bottom = floor - 1;
        int top = floor - render.SeeThroughStories[at] * 2 * half;
        int y = Math.Min(bottom, render.ViewHeight - 1);
        if (top < 0) top = 0;
        if (y < top) return;

        // stepped as DrawPost steps a wall's texels
        long num = (long)(bottom - y) * (GameConstants.TextureSize / 2);
        long t = num / half;
        long rem = num % half;
        int pitch = (int)video.BufferPitch;

        TextureAsset lower = render.SeeThroughLower[at], upper = render.SeeThroughUpper[at];
        byte[] lowerdata = lower.RawData, upperdata = upper.RawData;
        int lowerheight = lower.Height, upperheight = upper.Height;
        int lowercolumn = WallPosts.TextureColumn(lower, render.SeeThroughLowerOfs[at]);
        int uppercolumn = WallPosts.TextureColumn(upper, render.SeeThroughUpperOfs[at]);
        byte[] shade = render.SeeThroughLight[at];
        int shadeofs = lighting.ShadeOffsetForHeight(height);

        unsafe
        {
            byte* dest = (byte*)render.VbufPtr + render.ScreenOfs + x;
            byte texel = 0;
            long texelt = -1;           // the t texel is for
            for (; y >= top; y--)
            {
                if (t != texelt)
                {
                    texel = t < GameConstants.TextureSize
                        ? lowerdata[lowercolumn + lowerheight - 1 - (int)(t % lowerheight)]
                        : upperdata[uppercolumn + upperheight - 1 - (int)(t % upperheight)];
                    texelt = t;
                }
                if (texel != Mask)
                    dest[y * pitch] = shade[shadeofs + texel];

                rem += GameConstants.TextureSize / 2;
                while (rem >= half)
                {
                    rem -= half;
                    t++;
                }
            }
        }
    }
}
