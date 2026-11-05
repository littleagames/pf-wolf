using PFWolf.Constants;

namespace PFWolf.Rendering;

/// <summary>
/// Small things drawn in among the sprites, farthest first: rain and snow (Weather.cs) and
/// particles (Particles.cs). Each is a point in the world put on the screen as sprites are
/// (ProjectPoint), drawn as a square, a round dot, a streak or a splash. Walls hide them per
/// screen column: the posts the trace queued (NoteOccluders), so one shows above a short wall
/// in front of a taller one. A see-through door farther away in a pixel's column is drawn
/// before the pixel is (SeeThroughPosts), so the point shows in front of it.
/// </summary>
/// <param name="floorRow">The screen row the floor is on for something half high (WL_DRAW's FloorRow)</param>
internal sealed class PointDrawing(
    VideoManager video,
    RenderState render,
    Func<int, int> floorRow,
    SeeThroughPosts? seeThrough = null)
{
    internal enum Shape : byte
    {
        Square,         // size by size, around its middle
        Dot,            // a round blob size across
        Streak,         // a line from the head to the tail, size wide
        Splash,         // a raindrop's crown: droplets thrown out to each side and up
    }

    struct Point
    {
        public short X0, Y0, X1, Y1;    // the head, and a streak's tail (the same for the rest)
        public byte Size;               // pixels across (a splash's: how far it has spread)
        public byte Color;
        public Shape Shape;
    }

    /// <summary>This frame has points to draw: the posts are noted as they're drawn</summary>
    internal bool On;

    //
    // Each column's walls, as the trace queued them: how high each is (as WallHeight, so nearer
    // is higher) and the rows it covers
    //
    short[] occheight = [], occtop = [], occbottom = [];
    byte[] occcount = [];

    /// <summary>Gets the frame ready, before the walls are traced: on when there'll be points to draw</summary>
    internal void Setup(bool on)
    {
        On = on;
        if (!On || occcount.Length == render.ViewWidth)
            return;
        occcount = new byte[render.ViewWidth];
        occheight = new short[render.ViewWidth * RenderState.MaxPosts];
        occtop = new short[render.ViewWidth * RenderState.MaxPosts];
        occbottom = new short[render.ViewWidth * RenderState.MaxPosts];
    }

    /// <summary>Notes what the column's queued posts cover, before DrawPosts draws them</summary>
    internal void NoteOccluders()
    {
        if ((uint)render.PixX >= (uint)occcount.Length)
            return;
        int at = render.PixX * RenderState.MaxPosts;
        for (int i = 0; i < render.PostCount; i++, at++)
        {
            // the rows DrawPost fills
            int half = render.PostHeights[i] >> 3;
            if (half <= 0) half = 100;
            int floor = floorRow(half);
            int top = floor - render.PostStories[i] * 2 * half;
            int bottom = floor - 1 - render.PostFirstStory[i] * 2 * half;

            // an arch's underside is above all that's past it too, up to where the trace leaves it
            if (render.PostUnderside[i] != 0)
            {
                int exithalf = render.PostUnderside[i] >> 3;
                bottom = Math.Max(bottom, floorRow(exithalf) - 2 * exithalf - 1);
            }

            occheight[at] = render.PostHeights[i];
            occtop[at] = (short)Math.Clamp(top, short.MinValue, short.MaxValue);
            occbottom[at] = (short)Math.Clamp(bottom, short.MinValue, short.MaxValue);
        }
        occcount[render.PixX] = (byte)render.PostCount;
    }

    // Whether a wall nearer than something height high (as WallHeight) covers row y of column x
    bool Occluded(int x, int y, int height)
    {
        int at = x * RenderState.MaxPosts;
        for (int end = at + occcount[x]; at < end; at++)
            if (occheight[at] > height && y >= occtop[at] && y <= occbottom[at])
                return true;
        return false;
    }

    /// <summary>
    /// Where a point (global units, z texels up) is on screen, as sprites are placed: nx how far
    /// along the view it is, half how many pixels half a story is there. False when it's too close
    /// or behind the view.
    /// </summary>
    internal bool ProjectPoint(long gx, long gy, double z, out int sx, out int sy, out long nx, out double half)
    {
        long dx = gx - render.ViewX, dy = gy - render.ViewY;
        nx = (dx * render.ViewCos >> 16) - (dy * render.ViewSin >> 16);
        long ny = (dx * render.ViewSin >> 16) + (dy * render.ViewCos >> 16);
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

    Point[] points = [];
    int[] keys = [];      // each point's height (as WallHeight), to sort them farthest first
    int count, next;

    /// <summary>How many points this frame has</summary>
    internal int Count => count;

    /// <summary>Starts the frame's list afresh, before the weather and particles add theirs</summary>
    internal void Clear() => count = next = 0;

    /// <summary>
    /// Adds a point nx along the view: a head (x0, y0) and a streak's tail (x1, y1), size pixels
    /// across. One off the screen altogether isn't kept.
    /// </summary>
    internal void Add(int x0, int y0, int x1, int y1, int size, byte color, long nx, Shape shape)
    {
        if (Math.Max(x0, x1) + size < 0 || Math.Min(x0, x1) - size >= render.ViewWidth
            || Math.Max(y0, y1) + size < 0 || Math.Min(y0, y1) - size >= render.ViewHeight)
            return;

        if (count == points.Length)
        {
            Array.Resize(ref points, Math.Max(256, count * 2));
            Array.Resize(ref keys, points.Length);
        }

        points[count] = new Point
        {
            X0 = (short)Math.Clamp(x0, short.MinValue, short.MaxValue), Y0 = (short)Math.Clamp(y0, short.MinValue, short.MaxValue),
            X1 = (short)Math.Clamp(x1, short.MinValue, short.MaxValue), Y1 = (short)Math.Clamp(y1, short.MinValue, short.MaxValue),
            Size = (byte)Math.Clamp(size, 0, 255), Color = color, Shape = shape,
        };
        keys[count] = (int)Math.Min(render.HeightNumerator * 256L / Math.Max(nx, 1), int.MaxValue);
        count++;
    }

    /// <summary>Puts the frame's points farthest first, once they're all added</summary>
    internal void Sort() => Array.Sort(keys, points, 0, count);

    /// <summary>Draws the points farther away than something height high (as WallHeight), farthest first</summary>
    internal void DrawBeyond(int height)
    {
        if (next >= count || keys[next] >= height)
            return;
        if (!On || occcount.Length < render.ViewWidth)
        {
            next = count;           // the walls weren't noted for them this frame
            return;
        }

        int pitch = (int)video.BufferPitch;
        unsafe
        {
            byte* dest = (byte*)render.VbufPtr + render.ScreenOfs;
            for (; next < count && keys[next] < height; next++)
            {
                ref var point = ref points[next];
                int key = keys[next];
                switch (point.Shape)
                {
                    case Shape.Splash:
                    {
                        // droplets thrown out to each side and up from where it landed
                        int r = point.Size, dot = Math.Max(r / 4, 1);
                        DrawSquare(dest, pitch, point.X0 - r, point.Y0 - (r + 1) / 2, dot, point.Color, key);
                        DrawSquare(dest, pitch, point.X0 + r - dot + 1, point.Y0 - (r + 1) / 2, dot, point.Color, key);
                        if (r > 2)
                            DrawSquare(dest, pitch, point.X0, point.Y0 - r, dot, point.Color, key);
                        break;
                    }
                    case Shape.Square:
                        DrawSquare(dest, pitch, point.X0 - (point.Size >> 1), point.Y0 - (point.Size >> 1), point.Size, point.Color, key);
                        break;
                    case Shape.Dot:
                        DrawDot(dest, pitch, point.X0, point.Y0, point.Size, point.Color, key);
                        break;
                    default:
                        DrawStreak(dest, pitch, ref point, key);
                        break;
                }
            }
        }
    }

    // A streak, head to tail, size pixels wide
    unsafe void DrawStreak(byte* dest, int pitch, ref Point point, int height)
    {
        int dx = point.X1 - point.X0, dy = point.Y1 - point.Y0;
        int steps = Math.Min(Math.Max(Math.Abs(dx), Math.Abs(dy)), Math.Max(render.ViewHeight, render.ViewWidth));
        for (int s = 0; s <= steps; s++)
        {
            int px = point.X0 + (steps == 0 ? 0 : dx * s / steps);
            int py = point.Y0 + (steps == 0 ? 0 : dy * s / steps);
            if ((uint)py >= (uint)render.ViewHeight)
                continue;
            for (int x = Math.Max(px, 0); x < Math.Min(px + point.Size, render.ViewWidth); x++)
                Plot(dest, pitch, x, py, point.Color, height);
        }
    }

    // A size by size square from (left, top), where no wall nearer than height covers it
    unsafe void DrawSquare(byte* dest, int pitch, int left, int top, int size, byte color, int height)
    {
        for (int y = Math.Max(top, 0); y < Math.Min(top + size, render.ViewHeight); y++)
            for (int x = Math.Max(left, 0); x < Math.Min(left + size, render.ViewWidth); x++)
                Plot(dest, pitch, x, y, color, height);
    }

    // A round blob size across around (cx, cy); up to 2 across it's a square
    unsafe void DrawDot(byte* dest, int pitch, int cx, int cy, int size, byte color, int height)
    {
        if (size <= 2)
        {
            DrawSquare(dest, pitch, cx - (size >> 1), cy - (size >> 1), size, color, height);
            return;
        }

        // each pixel whose middle is inside the circle
        double r = size / 2.0, r2 = r * r;
        int left = cx - (size >> 1), top = cy - (size >> 1);
        for (int y = Math.Max(top, 0); y < Math.Min(top + size, render.ViewHeight); y++)
        {
            double fy = y - top + 0.5 - r;
            for (int x = Math.Max(left, 0); x < Math.Min(left + size, render.ViewWidth); x++)
            {
                double fx = x - left + 0.5 - r;
                if (fx * fx + fy * fy <= r2)
                    Plot(dest, pitch, x, y, color, height);
            }
        }
    }

    unsafe void Plot(byte* dest, int pitch, int x, int y, byte color, int height)
    {
        if (Occluded(x, y, height))
            return;
        seeThrough?.DrawBeyond(x, height);
        dest[y * pitch + x] = color;
    }
}
