namespace PFWolf.Managers;

/// <summary>How the screen changes during a fade, or a transition from one frame to the next.</summary>
internal enum FadeStyle
{
    /// <summary>Every color steps toward its target together (Wolf3D's VL_FadeIn/VL_FadeOut)</summary>
    Palette,

    /// <summary>Pixels switch over one at a time in a pseudo-random order (Wolf3D's death FizzleFade)</summary>
    Fizzle,

    /// <summary>Columns of the old frame slide down at staggered speeds, uncovering the new one (DOOM's screen melt)</summary>
    Melt,

    /// <summary>The old frame breaks up into growing blocks, which shrink back down on the new one</summary>
    Mosaic,

    /// <summary>The old frame turns smoothly into the new one, every pixel blended at once</summary>
    Crossfade,

    /// <summary>Pixels switch over in an ordered 8x8 dither pattern (a Bayer matrix), chunky as 320x200</summary>
    Dither,

    /// <summary>Blotches of the old frame burn away, with a glowing ember edge, onto the new one</summary>
    Burn,

    /// <summary>A circle closes on the old frame and opens on the new one (a cartoon's iris out and in)</summary>
    Iris,

    /// <summary>Two doors slide shut from the sides, then part on the new frame (an elevator's)</summary>
    Doors,

    /// <summary>The picture squashes to a bright line, then a dot, and back out (a CRT turned off and on)</summary>
    Crt,

    /// <summary>The new frame sweeps in around the middle like a clock's hand, from twelve o'clock</summary>
    Clock,

    /// <summary>Blocks of the new frame spiral in from the edges to the middle</summary>
    Spiral,

    /// <summary>A soft edge sweeps across from the right, the new frame behind it</summary>
    WipeLeft,

    /// <summary>A soft edge sweeps across from the left, the new frame behind it</summary>
    WipeRight,

    /// <summary>A soft edge sweeps up from the bottom, the new frame behind it</summary>
    WipeUp,

    /// <summary>A soft edge sweeps down from the top, the new frame behind it</summary>
    WipeDown,

    /// <summary>The screen turns over in slats like venetian blinds, top to bottom</summary>
    Blinds,
}

/// <summary>Fade styles by the names game-info, the console and scripts give them</summary>
internal static class FadeStyles
{
    /// <summary>
    /// A style by name, whatever the case and with or without dashes: wipe-left, WipeLeft and
    /// wipe_left are all <see cref="FadeStyle.WipeLeft"/>. Not by number.
    /// </summary>
    internal static bool TryParse(string? name, out FadeStyle style)
    {
        style = FadeStyle.Palette;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var squashed = name.Trim().Replace("-", "").Replace("_", "");
        foreach (var value in Enum.GetValues<FadeStyle>())
        {
            if (value.ToString().Equals(squashed, StringComparison.OrdinalIgnoreCase))
            {
                style = value;
                return true;
            }
        }
        return false;
    }

    /// <summary>A style's name as it's written: lower case, a dash between words (wipe-left)</summary>
    internal static string Name(FadeStyle style)
    {
        var text = style.ToString();
        var name = new System.Text.StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            if (i > 0 && char.IsUpper(text[i]))
                name.Append('-');
            name.Append(char.ToLowerInvariant(text[i]));
        }
        return name.ToString();
    }

    /// <summary>Every style's name, in order</summary>
    internal static string[] Names => [.. Enum.GetValues<FadeStyle>().Select(Name)];
}

/// <summary>
/// The frames a transition works between, as 32-bit pixels in the screen surface's format,
/// full screen in size (<see cref="Stride"/> pixels a row). The transition only touches the
/// rectangle <see cref="X"/>, <see cref="Y"/>, <see cref="Width"/>, <see cref="Height"/>.
/// </summary>
internal sealed class TransitionCanvas
{
    public required uint[] From { get; init; }
    public required uint[] To { get; init; }

    /// <summary>What goes on screen this frame; starts as a copy of <see cref="From"/>.</summary>
    public required uint[] Output { get; init; }

    public required int Stride { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }

    /// <summary>Screen pixels per 320x200 pixel</summary>
    public required int ScaleFactor { get; init; }

    /// <summary>Whether <see cref="From"/> / <see cref="To"/> are one solid color (a fade out / fade in).</summary>
    public bool FromIsSolid { get; init; }
    public bool ToIsSolid { get; init; }

    /// <summary>A color as a pixel in the screen surface's format (ARGB8888 unless set)</summary>
    public Func<byte, byte, byte, uint> MapRgb { get; init; } = (r, g, b) => 0xff000000u | (uint)r << 16 | (uint)g << 8 | b;

    public uint Black => MapRgb(0, 0, 0);
    public uint White => MapRgb(255, 255, 255);

    /// <summary>Array index of a pixel given relative to the rectangle.</summary>
    public int Index(int x, int y) => (Y + y) * Stride + X + x;

    /// <summary>Whether every pixel of a frame (stride pixels a row) inside a rectangle is the same color.</summary>
    public static bool IsSolid(uint[] pixels, int stride, int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
            return true;
        uint first = pixels[y * stride + x];
        for (int row = y; row < y + height; row++)
        {
            if (pixels.AsSpan(row * stride + x, width).ContainsAnyExcept(first))
                return false;
        }
        return true;
    }
}

internal abstract class ScreenTransition
{
    /// <summary>
    /// Draws the transition <paramref name="progress"/> (0..1) of the way through into the
    /// canvas's Output. Returns true once it has finished, which it must at progress 1.
    /// </summary>
    public abstract bool Draw(TransitionCanvas canvas, double progress);

    /// <summary>
    /// The transition for a style, over a canvas. The fizzle walks an LFSR (its feedback mask, and
    /// how many of its bits are y) big enough for the whole screen. Not for the palette style,
    /// which steps the palette instead.
    /// </summary>
    public static ScreenTransition Create(FadeStyle style, TransitionCanvas canvas, uint rndmask, int rndbitsY) => style switch
    {
        FadeStyle.Fizzle => new FizzleTransition(rndmask, rndbitsY),
        FadeStyle.Melt => new MeltTransition(canvas),
        FadeStyle.Mosaic => new MosaicTransition(),
        FadeStyle.Crossfade => new CrossfadeTransition(),
        FadeStyle.Dither => new DitherTransition(),
        FadeStyle.Burn => new BurnTransition(),
        FadeStyle.Iris => new IrisTransition(),
        FadeStyle.Doors => new DoorsTransition(),
        FadeStyle.Crt => new CrtTransition(),
        FadeStyle.Clock => new ClockTransition(),
        FadeStyle.Spiral => new SpiralTransition(),
        FadeStyle.WipeLeft or FadeStyle.WipeRight or FadeStyle.WipeUp or FadeStyle.WipeDown => new WipeTransition(style),
        FadeStyle.Blinds => new BlindsTransition(),
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Not a transition style"),
    };

    /// <summary>Mixes two pixels channel by channel; <paramref name="amount"/> 0 is all a, 256 all b.</summary>
    protected static uint Blend(uint a, uint b, int amount)
    {
        if (amount <= 0) return a;
        if (amount >= 256) return b;

        // Two channels at a time, each in its own 16 bits: 255 * 256 never carries into the next
        uint ia = (uint)(256 - amount), ib = (uint)amount;
        uint redBlue = (((a & 0x00ff00ffu) * ia + (b & 0x00ff00ffu) * ib) >> 8) & 0x00ff00ffu;
        uint alphaGreen = (((a >> 8) & 0x00ff00ffu) * ia + ((b >> 8) & 0x00ff00ffu) * ib) & 0xff00ff00u;
        return redBlue | alphaGreen;
    }

    /// <summary>Smoothstep: slow at the start and the end</summary>
    protected static double Ease(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }

    /// <summary>
    /// For transitions that close over the picture and open again (iris, doors, CRT): how far
    /// shut they are (0 open, 1 shut), the picture showing through, and the color they shut to.
    /// Fading out to a color they only close, onto it; fading in from one they only open. Between
    /// two pictures they close on the old one to black, stay shut a moment, and open on the new.
    /// </summary>
    protected static (double Shut, uint[] Picture, uint Color) CloseThenOpen(TransitionCanvas canvas, double progress, uint between)
    {
        if (canvas.ToIsSolid)
            return (progress, canvas.From, canvas.To[canvas.Index(0, 0)]);
        if (canvas.FromIsSolid)
            return (1 - progress, canvas.To, canvas.From[canvas.Index(0, 0)]);
        return progress < 0.5
            ? (Math.Min(progress * 2.2, 1), canvas.From, between)
            : (Math.Min((1 - progress) * 2.2, 1), canvas.To, between);
    }

    /// <summary>Fills part of a row of the rectangle (x from x0 up to x1) with a color.</summary>
    protected static void FillRow(TransitionCanvas canvas, int y, int x0, int x1, uint color)
    {
        x0 = Math.Max(x0, 0);
        x1 = Math.Min(x1, canvas.Width);
        if (x1 > x0)
            Array.Fill(canvas.Output, color, canvas.Index(x0, y), x1 - x0);
    }

    /// <summary>Copies part of a row of the rectangle (x from x0 up to x1) from a frame.</summary>
    protected static void CopyRow(TransitionCanvas canvas, uint[] from, int y, int x0, int x1)
    {
        x0 = Math.Max(x0, 0);
        x1 = Math.Min(x1, canvas.Width);
        if (x1 > x0)
            Array.Copy(from, canvas.Index(x0, y), canvas.Output, canvas.Index(x0, y), x1 - x0);
    }
}

/// <summary>
/// Wolf3D's FizzleFade: a maximal-length LFSR visits every x/y pair once, in an order that looks
/// random, and each pixel it lands on inside the rectangle switches to the new frame.
/// </summary>
internal sealed class FizzleTransition(uint rndmask, int rndbitsY) : ScreenTransition
{
    private int _rndval = 1;
    private long _revealed;
    private bool _done;

    public override bool Draw(TransitionCanvas canvas, double progress)
    {
        if (_done)
            return true;

        long target = progress >= 1 ? long.MaxValue : (long)((long)canvas.Width * canvas.Height * progress);

        while (_revealed < target)
        {
            //
            // separate random value into x/y pair
            //
            int x = _rndval >> rndbitsY;
            int y = _rndval & ((1 << rndbitsY) - 1);

            //
            // advance to next random element
            //
            _rndval = (_rndval >> 1) ^ ((_rndval & 1) != 0 ? 0 : (int)rndmask);

            if (x < canvas.Width && y < canvas.Height)
            {
                int i = canvas.Index(x, y);
                canvas.Output[i] = canvas.To[i];
                _revealed++;
            }

            if (_rndval == 1)
            {
                // Entire sequence done. It never produces 0, so the corner pixel is left to here.
                int corner = canvas.Index(0, 0);
                canvas.Output[corner] = canvas.To[corner];
                _done = true;
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// DOOM's wipe_doMelt: the screen is cut into 2-pixel wide columns (in 320x200 terms), each given
/// a small random head start. Every step a column that has started falls further, speeding up
/// until it moves 8 pixels a step, and the new frame shows through above it.
/// </summary>
internal sealed class MeltTransition : ScreenTransition
{
    private const int ColumnWidth = 2;  // in 320x200 pixels, as DOOM

    private readonly int[] _columnY;    // how far each column has fallen, in 320x200 pixels; negative is waiting
    private readonly int _height;       // the rectangle's height in 320x200 pixels
    private readonly int _totalSteps;   // steps until the slowest column is off the bottom
    private int _steps;

    public MeltTransition(TransitionCanvas canvas)
    {
        int columnPixels = ColumnWidth * canvas.ScaleFactor;
        _columnY = new int[(canvas.Width + columnPixels - 1) / columnPixels];
        _height = Math.Max(1, canvas.Height / canvas.ScaleFactor);

        // wipe_initMelt: a random start, then each column within a pixel of its neighbour
        _columnY[0] = -Random.Shared.Next(16);
        for (int i = 1; i < _columnY.Length; i++)
        {
            int y = _columnY[i - 1] + Random.Shared.Next(3) - 1;
            _columnY[i] = y > 0 ? 0 : y == -16 ? -15 : y;
        }

        int slowest = _columnY.Min();
        for (int y = slowest; y < _height; _totalSteps++)
            y = Fall(y);
    }

    private int Fall(int y)
    {
        if (y < 0)
            return y + 1;

        int dy = y < 16 ? y + 1 : 8;
        return Math.Min(y + dy, _height);
    }

    public override bool Draw(TransitionCanvas canvas, double progress)
    {
        int targetSteps = progress >= 1 ? _totalSteps : (int)(_totalSteps * progress);
        for (; _steps < targetSteps; _steps++)
        {
            for (int i = 0; i < _columnY.Length; i++)
                _columnY[i] = Fall(_columnY[i]);
        }

        int columnPixels = ColumnWidth * canvas.ScaleFactor;

        for (int x = 0; x < canvas.Width; x++)
        {
            int fallen = Math.Min(Math.Max(_columnY[x / columnPixels], 0) * canvas.ScaleFactor, canvas.Height);

            for (int y = 0; y < fallen; y++)
            {
                int i = canvas.Index(x, y);
                canvas.Output[i] = canvas.To[i];
            }

            for (int y = fallen; y < canvas.Height; y++)
                canvas.Output[canvas.Index(x, y)] = canvas.From[canvas.Index(x, y - fallen)];
        }

        return _steps >= _totalSteps;
    }
}

/// <summary>
/// A SNES-style mosaic: each block of the screen is filled with the color at its middle. Between
/// two pictures the blocks grow on the old one and shrink back down on the new one, crossfading
/// while they're at their largest. When fading to a solid color they only grow as the color comes
/// in, and when fading in from one they only shrink.
/// </summary>
internal sealed class MosaicTransition : ScreenTransition
{
    private const int MaxBlock = 16;    // in 320x200 pixels, the SNES's largest mosaic

    public override bool Draw(TransitionCanvas canvas, double progress)
    {
        progress = Math.Clamp(progress, 0, 1);

        double size, mix;
        if (canvas.ToIsSolid)
        {
            size = progress;
            mix = progress;
        }
        else if (canvas.FromIsSolid)
        {
            size = 1 - progress;
            mix = progress;
        }
        else
        {
            size = 1 - Math.Abs(progress * 2 - 1);
            mix = Math.Clamp((progress - 0.4) / 0.2, 0, 1);
        }

        int block = Math.Max(1, (int)Math.Round((1 + size * (MaxBlock - 1)) * canvas.ScaleFactor));
        int amount = (int)(mix * 256);

        for (int by = 0; by < canvas.Height; by += block)
        {
            int bh = Math.Min(block, canvas.Height - by);
            for (int bx = 0; bx < canvas.Width; bx += block)
            {
                int bw = Math.Min(block, canvas.Width - bx);

                int sample = canvas.Index(bx + bw / 2, by + bh / 2);
                uint color = Blend(canvas.From[sample], canvas.To[sample], amount);

                for (int y = by; y < by + bh; y++)
                {
                    int row = canvas.Index(bx, y);
                    Array.Fill(canvas.Output, color, row, bw);
                }
            }
        }

        return progress >= 1;
    }
}

/// <summary>A crossfade: every pixel blended from the old frame to the new at once.</summary>
internal sealed class CrossfadeTransition : ScreenTransition
{
    public override bool Draw(TransitionCanvas canvas, double progress)
    {
        int amount = (int)(Math.Clamp(progress, 0, 1) * 256);
        for (int y = 0; y < canvas.Height; y++)
        {
            int row = canvas.Index(0, y);
            for (int i = row; i < row + canvas.Width; i++)
                canvas.Output[i] = Blend(canvas.From[i], canvas.To[i], amount);
        }
        return progress >= 1;
    }
}

/// <summary>
/// A transition that switches each pixel over once the progress passes its rank (0..65535):
/// the order is all that differs between the dither, clock, spiral and burn. The ranks are
/// worked out once, the first time it draws.
/// </summary>
internal abstract class RankedTransition : ScreenTransition
{
    private ushort[]? _ranks;

    /// <summary>Every pixel's rank in the rectangle, row by row.</summary>
    protected abstract ushort[] Rank(TransitionCanvas canvas);

    protected ushort[] Ranks(TransitionCanvas canvas) => _ranks ??= Rank(canvas);

    public override bool Draw(TransitionCanvas canvas, double progress)
    {
        var ranks = Ranks(canvas);
        int threshold = progress >= 1 ? int.MaxValue : (int)(Math.Max(progress, 0) * 65536);

        for (int y = 0, r = 0; y < canvas.Height; y++)
        {
            int row = canvas.Index(0, y);
            for (int x = 0; x < canvas.Width; x++, r++)
                canvas.Output[row + x] = ranks[r] < threshold ? canvas.To[row + x] : canvas.From[row + x];
        }
        return progress >= 1;
    }
}

/// <summary>
/// An ordered dither: the 8x8 Bayer matrix says which pixel of each 8x8 goes next, so the new
/// frame comes in as an even, regular pattern of 64 steps (a PC demo's or an Amiga's fade). The
/// cells are 320x200 pixels, so it's as chunky at any resolution.
/// </summary>
internal sealed class DitherTransition : RankedTransition
{
    private static readonly int[] Bayer =
    [
         0, 32,  8, 40,  2, 34, 10, 42,
        48, 16, 56, 24, 50, 18, 58, 26,
        12, 44,  4, 36, 14, 46,  6, 38,
        60, 28, 52, 20, 62, 30, 54, 22,
         3, 35, 11, 43,  1, 33,  9, 41,
        51, 19, 59, 27, 49, 17, 57, 25,
        15, 47,  7, 39, 13, 45,  5, 37,
        63, 31, 55, 23, 61, 29, 53, 21,
    ];

    protected override ushort[] Rank(TransitionCanvas canvas)
    {
        var ranks = new ushort[canvas.Width * canvas.Height];
        int scale = canvas.ScaleFactor;
        for (int y = 0, r = 0; y < canvas.Height; y++)
        {
            int row = (y / scale & 7) * 8;
            for (int x = 0; x < canvas.Width; x++, r++)
                ranks[r] = (ushort)(Bayer[row + (x / scale & 7)] * 1024);
        }
        return ranks;
    }
}

/// <summary>The new frame sweeps in clockwise around the middle, from twelve o'clock.</summary>
internal sealed class ClockTransition : RankedTransition
{
    protected override ushort[] Rank(TransitionCanvas canvas)
    {
        var ranks = new ushort[canvas.Width * canvas.Height];
        double cx = canvas.Width / 2.0, cy = canvas.Height / 2.0;
        for (int y = 0, r = 0; y < canvas.Height; y++)
        {
            double dy = y + 0.5 - cy;
            for (int x = 0; x < canvas.Width; x++, r++)
            {
                // From straight up, clockwise (y runs down the screen)
                double angle = Math.Atan2(x + 0.5 - cx, -dy);
                if (angle < 0)
                    angle += 2 * Math.PI;
                ranks[r] = (ushort)Math.Min(angle / (2 * Math.PI) * 65536, 65535);
            }
        }
        return ranks;
    }
}

/// <summary>
/// Blocks of the new frame spiral in: along the top from the top left, down the right side,
/// back along the bottom and up the left, ring by ring into the middle (a JRPG's battle start).
/// </summary>
internal sealed class SpiralTransition : RankedTransition
{
    private const int Block = 20;       // in 320x200 pixels: 16 across and 10 down

    protected override ushort[] Rank(TransitionCanvas canvas)
    {
        int size = Block * canvas.ScaleFactor;
        int columns = Math.Max(1, (canvas.Width + size - 1) / size);
        int rows = Math.Max(1, (canvas.Height + size - 1) / size);

        // Each block's turn in the spiral
        var order = new int[columns * rows];
        int left = 0, top = 0, right = columns - 1, bottom = rows - 1, turn = 0;
        while (left <= right && top <= bottom)
        {
            for (int x = left; x <= right; x++) order[top * columns + x] = turn++;
            for (int y = top + 1; y <= bottom; y++) order[y * columns + right] = turn++;
            if (top < bottom)
                for (int x = right - 1; x >= left; x--) order[bottom * columns + x] = turn++;
            if (left < right)
                for (int y = bottom - 1; y > top; y--) order[y * columns + left] = turn++;
            left++; top++; right--; bottom--;
        }

        var ranks = new ushort[canvas.Width * canvas.Height];
        for (int y = 0, r = 0; y < canvas.Height; y++)
        {
            int blockRow = y / size * columns;
            for (int x = 0; x < canvas.Width; x++, r++)
                ranks[r] = (ushort)((long)order[blockRow + x / size] * 65536 / order.Length);
        }
        return ranks;
    }
}

/// <summary>
/// The old frame burns away in blotches (smooth value noise, its ranks evened out so it burns at a
/// steady pace), and just ahead of the burnt edge it scorches: yellow, orange, red, then charred.
/// </summary>
internal sealed class BurnTransition : RankedTransition
{
    private const int Band = 7800;      // how far ahead of the burnt edge it scorches, in ranks (about 12%)

    private uint _yellow, _orange, _red, _black;
    private bool _colorsMapped;

    protected override ushort[] Rank(TransitionCanvas canvas)
    {
        int scale = canvas.ScaleFactor;
        int gw = Math.Max(1, (canvas.Width + scale - 1) / scale);
        int gh = Math.Max(1, (canvas.Height + scale - 1) / scale);

        // Value noise in 320x200 pixels: random heights on lattices 32, 16, 8 and 4 apart,
        // smoothly interpolated and added up, each half as much as the last
        var noise = new float[gw * gh];
        var random = new Random();
        float weight = 1;
        for (int cell = 32; cell >= 4; cell /= 2, weight /= 2)
        {
            int lw = gw / cell + 2, lh = gh / cell + 2;
            var lattice = new float[lw * lh];
            for (int i = 0; i < lattice.Length; i++)
                lattice[i] = (float)random.NextDouble();

            for (int y = 0; y < gh; y++)
            {
                int ly = y / cell;
                float fy = Smooth((y % cell) / (float)cell);
                for (int x = 0; x < gw; x++)
                {
                    int lx = x / cell;
                    float fx = Smooth((x % cell) / (float)cell);
                    float topRow = Lerp(lattice[ly * lw + lx], lattice[ly * lw + lx + 1], fx);
                    float bottomRow = Lerp(lattice[(ly + 1) * lw + lx], lattice[(ly + 1) * lw + lx + 1], fx);
                    noise[y * gw + x] += Lerp(topRow, bottomRow, fy) * weight;
                }
            }
        }

        // Even the ranks out: the lowest noise burns first, and as many burn each step
        var order = new int[noise.Length];
        for (int i = 0; i < order.Length; i++)
            order[i] = i;
        Array.Sort(noise, order);
        var cellRanks = new ushort[noise.Length];
        for (int i = 0; i < order.Length; i++)
            cellRanks[order[i]] = (ushort)((long)i * 65536 / order.Length);

        var ranks = new ushort[canvas.Width * canvas.Height];
        for (int y = 0, r = 0; y < canvas.Height; y++)
        {
            int row = y / scale * gw;
            for (int x = 0; x < canvas.Width; x++, r++)
                ranks[r] = cellRanks[row + x / scale];
        }
        return ranks;

        static float Smooth(float t) => t * t * (3 - 2 * t);
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

    public override bool Draw(TransitionCanvas canvas, double progress)
    {
        var ranks = Ranks(canvas);
        if (!_colorsMapped)
        {
            _colorsMapped = true;
            _yellow = canvas.MapRgb(255, 244, 170);
            _orange = canvas.MapRgb(255, 136, 16);
            _red = canvas.MapRgb(150, 24, 0);
            _black = canvas.Black;
        }

        // The burnt edge: everything ranked under it is gone. It starts a band before the first
        // rank, so the scorching creeps in, and ends past the last.
        int edge = progress >= 1 ? int.MaxValue - Band : (int)(Math.Max(progress, 0) * (65536 + Band)) - Band;

        for (int y = 0, r = 0; y < canvas.Height; y++)
        {
            int row = canvas.Index(0, y);
            for (int x = 0; x < canvas.Width; x++, r++)
            {
                int i = row + x;
                int ahead = ranks[r] - edge;        // how far the pixel is from burning
                if (ahead < 0)
                    canvas.Output[i] = canvas.To[i];
                else if (ahead >= Band)
                    canvas.Output[i] = canvas.From[i];
                else
                    canvas.Output[i] = Scorch(canvas.From[i], ahead * 256 / Band);
            }
        }
        return progress >= 1;
    }

    // A pixel e/256 of the band ahead of the burnt edge
    private uint Scorch(uint pixel, int e)
    {
        if (e < 38)
            return Blend(_yellow, _orange, e * 256 / 38);
        if (e < 90)
            return Blend(_orange, _red, (e - 38) * 256 / 52);
        uint charred = Blend(pixel, _black, 220);
        if (e < 140)
            return Blend(_red, charred, (e - 90) * 256 / 50);
        return Blend(charred, pixel, (e - 140) * 256 / 116);
    }
}

/// <summary>
/// A cartoon's iris: a circle around the middle closes on the old frame, leaving black, and opens
/// on the new one.
/// </summary>
internal sealed class IrisTransition : ScreenTransition
{
    public override bool Draw(TransitionCanvas canvas, double progress)
    {
        progress = Math.Clamp(progress, 0, 1);
        var (shut, picture, color) = CloseThenOpen(canvas, progress, canvas.Black);

        double cx = canvas.Width / 2.0, cy = canvas.Height / 2.0;
        double radius = (1 - Ease(shut)) * (Math.Sqrt(cx * cx + cy * cy) + 1);

        for (int y = 0; y < canvas.Height; y++)
        {
            double dy = y + 0.5 - cy;
            double span = radius * radius - dy * dy;
            if (span <= 0)
            {
                FillRow(canvas, y, 0, canvas.Width, color);
                continue;
            }

            double half = Math.Sqrt(span);
            int x0 = (int)Math.Ceiling(cx - half - 0.5), x1 = (int)Math.Floor(cx + half - 0.5) + 1;
            FillRow(canvas, y, 0, x0, color);
            CopyRow(canvas, picture, y, x0, x1);
            FillRow(canvas, y, x1, canvas.Width, color);
        }
        return progress >= 1;
    }
}

/// <summary>
/// An elevator's doors: two steel panels slide in from the sides until they meet, then part on
/// the new frame. Fading to or from a color, they take on the color as they finish shutting.
/// </summary>
internal sealed class DoorsTransition : ScreenTransition
{
    public override bool Draw(TransitionCanvas canvas, double progress)
    {
        progress = Math.Clamp(progress, 0, 1);
        uint steel = canvas.MapRgb(84, 88, 96);
        var (shut, picture, color) = CloseThenOpen(canvas, progress, steel);

        // Steel doors; fading to or from a color, they turn that color in the last quarter of
        // shutting (the first of opening), so the fade ends on it (or starts from it)
        bool toColor = canvas.ToIsSolid || canvas.FromIsSolid;
        uint door = toColor ? Blend(steel, color, (int)(Math.Clamp((shut - 0.75) / 0.25, 0, 1) * 256)) : steel;

        int width = (int)Math.Round(Ease(shut) * canvas.Width / 2.0);
        int lip = Math.Max(1, canvas.ScaleFactor);             // the doors' leading edges, lit
        uint edge = Blend(door, canvas.White, 72);
        uint shadow = canvas.Black;

        // Shut on a fade's color, they're that color and nothing else
        bool lit = width > 0 && (width < canvas.Width / 2 || !toColor);

        for (int y = 0; y < canvas.Height; y++)
        {
            // Left door, its lit edge; the picture between; the right door's edge, then the door
            FillRow(canvas, y, 0, width - lip, door);
            FillRow(canvas, y, width - lip, width, lit ? edge : door);
            CopyRow(canvas, picture, y, width, canvas.Width - width);
            FillRow(canvas, y, canvas.Width - width, canvas.Width - width + lip, lit ? edge : door);
            FillRow(canvas, y, canvas.Width - width + lip, canvas.Width, door);

            // A shadow the doors cast on the picture as they near
            if (width > 0 && width < canvas.Width / 2)
            {
                for (int x = width; x < Math.Min(width + lip, canvas.Width - width); x++)
                {
                    int i = canvas.Index(x, y);
                    canvas.Output[i] = Blend(canvas.Output[i], shadow, 128);
                }
                for (int x = Math.Max(canvas.Width - width - lip, width); x < canvas.Width - width; x++)
                {
                    int i = canvas.Index(x, y);
                    canvas.Output[i] = Blend(canvas.Output[i], shadow, 128);
                }
            }
        }
        return progress >= 1;
    }
}

/// <summary>
/// An old TV switched off and on: the picture squashes down to a bright line across the middle,
/// the line shrinks to a dot, and the dot fades. Turning on runs it backwards.
/// </summary>
internal sealed class CrtTransition : ScreenTransition
{
    private const double Squash = 0.55;     // how much of closing the picture's squash takes; the line and dot the rest

    public override bool Draw(TransitionCanvas canvas, double progress)
    {
        progress = Math.Clamp(progress, 0, 1);
        var (shut, picture, color) = CloseThenOpen(canvas, progress, canvas.Black);
        uint white = canvas.White;

        int cy = canvas.Height / 2;
        int line = Math.Max(1, canvas.ScaleFactor);         // the line's thickness

        if (shut < Squash)
        {
            // The picture squashed to `height` rows around the middle, washing out as it goes
            double t = shut / Squash;
            double scale = 1 - t * t;
            int height = Math.Max(line, (int)Math.Round(canvas.Height * scale));
            int top = cy - height / 2;
            int bright = (int)(t * 200);

            for (int y = 0; y < canvas.Height; y++)
            {
                if (y < top || y >= top + height)
                {
                    FillRow(canvas, y, 0, canvas.Width, color);
                    continue;
                }

                int source = Math.Clamp((int)(cy + (y + 0.5 - cy) / Math.Max(scale, 1e-3)), 0, canvas.Height - 1);
                int from = canvas.Index(0, source), to = canvas.Index(0, y);
                for (int x = 0; x < canvas.Width; x++)
                    canvas.Output[to + x] = Blend(picture[from + x], white, bright);
            }
        }
        else
        {
            // The line shrinks in from the ends to a dot, which then fades
            double t = (shut - Squash) / (1 - Squash);
            double length = Math.Max(1 - t / 0.7, 0);
            int half = Math.Max(line, (int)(canvas.Width / 2.0 * length * length));
            int fade = t <= 0.7 ? 0 : (int)((t - 0.7) / 0.3 * 256);
            uint glow = Blend(white, color, fade);
            int left = canvas.Width / 2 - half, right = canvas.Width / 2 + half;
            int top = cy - line / 2;

            for (int y = 0; y < canvas.Height; y++)
            {
                FillRow(canvas, y, 0, canvas.Width, color);
                if (y >= top && y < top + line)
                    FillRow(canvas, y, left, right, glow);
            }
        }
        return progress >= 1;
    }
}

/// <summary>
/// A wipe: an edge 16 pixels soft (in 320x200 terms) sweeps across, the new frame behind it.
/// </summary>
internal sealed class WipeTransition(FadeStyle direction) : ScreenTransition
{
    private const int Soft = 16;

    public override bool Draw(TransitionCanvas canvas, double progress)
    {
        progress = Math.Clamp(progress, 0, 1);
        bool across = direction is FadeStyle.WipeLeft or FadeStyle.WipeRight;
        bool backwards = direction is FadeStyle.WipeLeft or FadeStyle.WipeUp;
        int length = across ? canvas.Width : canvas.Height;
        int soft = Soft * canvas.ScaleFactor;

        // How much of the new frame each column (or row) shows: where the edge's got to, less
        // how far along the sweep it is, over the soft part
        double reached = progress * (length + soft);
        var amounts = new int[length];
        for (int u = 0; u < length; u++)
        {
            int along = backwards ? length - 1 - u : u;
            amounts[u] = progress >= 1 ? 256 : (int)Math.Clamp((reached - along) * 256 / soft, 0, 256);
        }

        for (int y = 0; y < canvas.Height; y++)
        {
            int row = canvas.Index(0, y);
            if (across)
            {
                for (int x = 0; x < canvas.Width; x++)
                    canvas.Output[row + x] = Blend(canvas.From[row + x], canvas.To[row + x], amounts[x]);
            }
            else
            {
                int amount = amounts[y];
                if (amount <= 0)
                    CopyRow(canvas, canvas.From, y, 0, canvas.Width);
                else if (amount >= 256)
                    CopyRow(canvas, canvas.To, y, 0, canvas.Width);
                else
                    for (int x = 0; x < canvas.Width; x++)
                        canvas.Output[row + x] = Blend(canvas.From[row + x], canvas.To[row + x], amount);
            }
        }
        return progress >= 1;
    }
}

/// <summary>
/// Venetian blinds: the screen's cut into slats 20 pixels tall (in 320x200 terms), and each turns
/// over from its middle out onto the new frame, the top slat first and each a little behind the
/// one above.
/// </summary>
internal sealed class BlindsTransition : ScreenTransition
{
    private const int Slat = 20;
    private const double Stagger = 0.3;     // how much of the time the bottom slat starts behind the top

    public override bool Draw(TransitionCanvas canvas, double progress)
    {
        progress = Math.Clamp(progress, 0, 1);
        int slat = Slat * canvas.ScaleFactor;
        int slats = Math.Max(1, (canvas.Height + slat - 1) / slat);

        for (int y = 0; y < canvas.Height; y++)
        {
            int k = y / slat;
            double start = slats == 1 ? 0 : Stagger * k / (slats - 1);
            double turned = progress >= 1 ? 1 : Ease((progress - start) / (1 - Stagger));

            // Rows within turned/2 of the slat's middle show the new frame
            double middle = k * slat + Math.Min(slat, canvas.Height - k * slat) / 2.0;
            bool shown = Math.Abs(y + 0.5 - middle) < turned * slat / 2.0 + (turned >= 1 ? 1 : 0);
            CopyRow(canvas, shown ? canvas.To : canvas.From, y, 0, canvas.Width);
        }
        return progress >= 1;
    }
}
