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
        Sprite,         // a sprite's picture, scaled to Width by Height from (X0, Y0)
    }

    /// <summary>How a point's pixels are laid over what's behind them (BlendTable)</summary>
    internal enum Blend : byte
    {
        Opaque,
        Translucent75,  // three parts it to one behind
        Translucent50,
        Translucent25,
        Additive,       // its red, green and blue added to what's behind
    }

    struct Point
    {
        public short X0, Y0, X1, Y1;    // the head, and a streak's tail (the same for the rest); a sprite's top left
        public byte Size;               // pixels across (a splash's: how far it has spread)
        public byte Color;
        public Shape Shape;
        public Blend Blend;
        public Assets.SpriteAsset? Sprite;
        public byte[]? Shade;           // a sprite's light row, from ShadeOfs
        public int ShadeOfs;
        public short Width, Height;     // a sprite's size on screen
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
    internal void Add(int x0, int y0, int x1, int y1, int size, byte color, long nx, Shape shape, Blend blend = Blend.Opaque)
    {
        if (Math.Max(x0, x1) + size < 0 || Math.Min(x0, x1) - size >= render.ViewWidth
            || Math.Max(y0, y1) + size < 0 || Math.Min(y0, y1) - size >= render.ViewHeight)
            return;

        int at = Next(nx);      // first: it may make the list bigger
        points[at] = new Point
        {
            X0 = Short(x0), Y0 = Short(y0), X1 = Short(x1), Y1 = Short(y1),
            Size = (byte)Math.Clamp(size, 0, 255), Color = color, Shape = shape, Blend = blend,
        };
    }

    /// <summary>
    /// Adds a sprite's picture nx along the view, width by height pixels from (left, top), its
    /// indices lit through shade from shadeofs
    /// </summary>
    internal void AddSprite(int left, int top, int width, int height, Assets.SpriteAsset sprite, byte[] shade, int shadeofs, long nx, Blend blend)
    {
        if (width <= 0 || height <= 0 || left + width <= 0 || left >= render.ViewWidth || top + height <= 0 || top >= render.ViewHeight)
            return;

        int at = Next(nx);      // first: it may make the list bigger
        points[at] = new Point
        {
            X0 = Short(left), Y0 = Short(top), Width = Short(width), Height = Short(height),
            Shape = Shape.Sprite, Blend = blend, Sprite = sprite, Shade = shade, ShadeOfs = shadeofs,
        };
    }

    static short Short(int value) => (short)Math.Clamp(value, short.MinValue, short.MaxValue);

    // A slot for one more point, nx along the view, with its key
    int Next(long nx)
    {
        if (count == points.Length)
        {
            Array.Resize(ref points, Math.Max(256, count * 2));
            Array.Resize(ref keys, points.Length);
        }
        keys[count] = (int)Math.Min(render.HeightNumerator * 256L / Math.Max(nx, 1), int.MaxValue);
        return count++;
    }

    /// <summary>Puts the frame's points farthest first, once they're all added</summary>
    internal void Sort() => Array.Sort(keys, points, 0, count);


    // The blend table the point being drawn goes through (BlendTable), null for opaque
    byte[]? blending;

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
                blending = BlendTable(point.Blend);
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
                    case Shape.Sprite:
                        DrawSprite(dest, pitch, ref point, key);
                        break;
                    default:
                        DrawStreak(dest, pitch, ref point, key);
                        break;
                }
            }
            blending = null;
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

    // A sprite's picture scaled onto its rectangle, its see-through pixels left out, lit through its shade row
    unsafe void DrawSprite(byte* dest, int pitch, ref Point point, int height)
    {
        var sprite = point.Sprite!;
        byte[] shade = point.Shade!;
        int ofs = point.ShadeOfs;
        int width = point.Width, rows = point.Height;
        for (int x = Math.Max((int)point.X0, 0); x < Math.Min(point.X0 + width, render.ViewWidth); x++)
        {
            int column = (int)((long)(x - point.X0) * sprite.Width / width);
            for (int y = Math.Max((int)point.Y0, 0); y < Math.Min(point.Y0 + rows, render.ViewHeight); y++)
            {
                int at = (int)((long)(y - point.Y0) * sprite.Height / rows) * sprite.Width + column;
                if (sprite.OpacityMask[at] != 0)
                    Plot(dest, pitch, x, y, shade[ofs + sprite.RawData[at]], height);
            }
        }
    }

    unsafe void Plot(byte* dest, int pitch, int x, int y, byte color, int height)
    {
        if (Occluded(x, y, height))
            return;
        seeThrough?.DrawBeyond(x, height);
        byte* pixel = dest + y * pitch + x;
        *pixel = blending == null ? color : blending[color << 8 | *pixel];
    }

    /*
    =============================================================================

                                  BLEND TABLES

    The view is drawn in the palette's 256 colors, so a pixel laid over another is looked up:
    a table for each blend, 256 by 256, the nearest palette color to the two mixed (or added).
    Made the first time each is wanted, through a 32x32x32 cube of the nearest palette color to
    every color (5 bits a channel), so a table is 65536 lookups rather than searches.

    =============================================================================
    */

    readonly byte[]?[] blendTables = new byte[]?[5];
    byte[]? nearestCube;

    /// <summary>The table a blend draws through: its color's index times 256 plus what's behind's; null for opaque (or no palette: tests)</summary>
    internal byte[]? BlendTable(Blend blend)
    {
        if (blend == Blend.Opaque || video == null)
            return null;
        return blendTables[(int)blend] ??= MakeBlendTable(blend);
    }

    byte[] MakeBlendTable(Blend blend)
    {
        var palette = new (int R, int G, int B)[256];
        for (int i = 0; i < 256; i++)
        {
            var (r, g, b) = video.PaletteRgb(i);
            palette[i] = (r, g, b);
        }
        nearestCube ??= MakeNearestCube(palette);

        double share = blend switch { Blend.Translucent75 => 0.75, Blend.Translucent50 => 0.5, Blend.Translucent25 => 0.25, _ => 1 };
        var table = new byte[256 * 256];
        for (int src = 0; src < 256; src++)
        {
            var s = palette[src];
            for (int dst = 0; dst < 256; dst++)
            {
                var d = palette[dst];
                int r, g, b;
                if (blend == Blend.Additive)
                    (r, g, b) = (Math.Min(s.R + d.R, 255), Math.Min(s.G + d.G, 255), Math.Min(s.B + d.B, 255));
                else
                    (r, g, b) = ((int)(s.R * share + d.R * (1 - share) + 0.5), (int)(s.G * share + d.G * (1 - share) + 0.5), (int)(s.B * share + d.B * (1 - share) + 0.5));
                table[src << 8 | dst] = nearestCube[(r >> 3) << 10 | (g >> 3) << 5 | (b >> 3)];
            }
        }
        return table;
    }

    // The nearest palette color to the middle of each 8x8x8 block of colors
    static byte[] MakeNearestCube((int R, int G, int B)[] palette)
    {
        var cube = new byte[32 * 32 * 32];
        for (int r = 0; r < 32; r++)
            for (int g = 0; g < 32; g++)
                for (int b = 0; b < 32; b++)
                {
                    int cr = r << 3 | 4, cg = g << 3 | 4, cb = b << 3 | 4;
                    int best = 0, bestDistance = int.MaxValue;
                    for (int i = 0; i < palette.Length; i++)
                    {
                        int dr = cr - palette[i].R, dg = cg - palette[i].G, db = cb - palette[i].B;
                        int distance = dr * dr + dg * dg + db * db;
                        if (distance < bestDistance)
                            (best, bestDistance) = (i, distance);
                    }
                    cube[r << 10 | g << 5 | b] = (byte)best;
                }
        return cube;
    }
}