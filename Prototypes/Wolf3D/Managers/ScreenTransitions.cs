namespace Wolf3D.Managers;

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

    /// <summary>Array index of a pixel given relative to the rectangle.</summary>
    public int Index(int x, int y) => (Y + y) * Stride + X + x;
}

internal abstract class ScreenTransition
{
    /// <summary>
    /// Draws the transition <paramref name="progress"/> (0..1) of the way through into the
    /// canvas's Output. Returns true once it has finished, which it must at progress 1.
    /// </summary>
    public abstract bool Draw(TransitionCanvas canvas, double progress);

    /// <summary>Mixes two pixels channel by channel; <paramref name="amount"/> 0 is all a, 256 all b.</summary>
    protected static uint Blend(uint a, uint b, int amount)
    {
        if (amount <= 0) return a;
        if (amount >= 256) return b;

        uint result = 0;
        for (int shift = 0; shift < 32; shift += 8)
        {
            int ca = (int)(a >> shift) & 0xff;
            int cb = (int)(b >> shift) & 0xff;
            result |= (uint)(ca + ((cb - ca) * amount >> 8)) << shift;
        }
        return result;
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
