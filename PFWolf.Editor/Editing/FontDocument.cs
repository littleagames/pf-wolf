using PFWolf.Assets;

namespace PFWolf.Editor.Editing;

/// <summary>A glyph on its own (the glyph editor's clipboard): its width, height and pixels, a byte each, non-zero where it draws</summary>
public sealed record GlyphData(int Width, int Height, byte[] Pixels);

/// <summary>
/// A Wolf3D font being drawn in the glyph editor: its height, and each of its 256 characters'
/// width and pixels (a byte each, non-zero where it draws). Each edit (a stroke, a shift, a new
/// height) is one undo step. Saved as a Wolf3D font file (<see cref="FontAsset.Write"/>).
/// </summary>
public sealed class FontDocument
{
    private sealed record State(int Height, int[] Widths, byte[][] Pixels)
    {
        public State Copy() => new(Height, (int[])Widths.Clone(), [.. Pixels.Select(pixels => (byte[])pixels.Clone())]);

        public bool Same(State other)
            => Height == other.Height && Widths.AsSpan().SequenceEqual(other.Widths)
               && Pixels.Zip(other.Pixels).All(pair => pair.First.AsSpan().SequenceEqual(pair.Second));
    }

    private State _state;
    private State _saved;
    private readonly List<(State State, string Label)> _undo = [];
    private readonly List<(State State, string Label)> _redo = [];
    private State? _editStart;
    private string _editLabel = "";

    private FontDocument(string name, State state, bool isNew)
    {
        Name = name;
        _state = state;
        _saved = isNew ? new State(0, new int[256], new byte[256][]) : state.Copy();
        IsNew = isNew;
        // The value drawn pixels get: what the font's own drawn pixels are (Wolf3D's are 1)
        Ink = state.Pixels.SelectMany(pixels => pixels).FirstOrDefault(pixel => pixel != 0) is var ink and > 0 ? ink : (byte)1;
    }

    /// <summary>A font file's glyphs, to draw on</summary>
    public static FontDocument From(string name, FontAsset font, bool isNew = false)
    {
        var widths = new int[256];
        var pixels = new byte[256][];
        for (int ch = 0; ch < 256; ch++)
        {
            widths[ch] = font.Width[ch];
            pixels[ch] = font.Glyph(ch);
        }
        return new FontDocument(name, new State(Math.Max((int)font.Height, 1), widths, pixels), isNew);
    }

    /// <summary>A new font with no glyphs but a space <paramref name="spaceWidth"/> wide</summary>
    public static FontDocument Blank(string name, int height, int spaceWidth)
    {
        var widths = new int[256];
        var pixels = Enumerable.Range(0, 256).Select(_ => Array.Empty<byte>()).ToArray();
        widths[' '] = spaceWidth;
        pixels[' '] = new byte[spaceWidth * height];
        return new FontDocument(name, new State(height, widths, pixels), isNew: true);
    }

    public string Name { get; }

    /// <summary>A font the game hasn't got yet: saving adds it</summary>
    public bool IsNew { get; private set; }

    public int Height => _state.Height;

    /// <summary>What a drawn pixel is set to</summary>
    public byte Ink { get; }

    public int Width(int ch) => _state.Widths[ch];

    /// <summary>Whether the font has a glyph for the character (a width; a space's glyph is blank)</summary>
    public bool Has(int ch) => _state.Widths[ch] > 0;

    public bool Pixel(int ch, int x, int y)
        => x >= 0 && y >= 0 && x < _state.Widths[ch] && y < Height && _state.Pixels[ch][y * _state.Widths[ch] + x] != 0;

    public bool IsDirty => !_state.Same(_saved);
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string? UndoLabel => _undo.Count > 0 ? _undo[^1].Label : null;
    public string? RedoLabel => _redo.Count > 0 ? _redo[^1].Label : null;

    /// <summary>The glyphs changed (an edit, undo or redo)</summary>
    public event EventHandler? Changed;

    //
    // Drawing: a stroke is one step
    //

    /// <summary>Starts an edit: what's drawn until <see cref="EndEdit"/> is one undo step</summary>
    public void BeginEdit(string label)
    {
        EndEdit();
        _editStart = _state.Copy();
        _editLabel = label;
    }

    /// <summary>Ends the edit; one that changed nothing leaves no undo step</summary>
    public void EndEdit()
    {
        if (_editStart is not { } start)
            return;
        _editStart = null;
        if (start.Same(_state))
            return;
        _undo.Add((start, _editLabel));
        _redo.Clear();
    }

    /// <summary>Sets a pixel of a glyph on or off (inside an edit)</summary>
    public void SetPixel(int ch, int x, int y, bool on)
    {
        if (Put(ch, x, y, on))
            Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A line of pixels from one to the other, both ends included (inside an edit)</summary>
    public void Line(int ch, int x0, int y0, int x1, int y1, bool on)
    {
        bool changed = false;
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            changed |= Put(ch, x0, y0, on);
            if (x0 == x1 && y0 == y1)
                break;
            int twice = 2 * error;
            if (twice >= dy)
            {
                error += dy;
                x0 += sx;
            }
            if (twice <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    private bool Put(int ch, int x, int y, bool on)
    {
        if (x < 0 || y < 0 || x >= _state.Widths[ch] || y >= Height)
            return false;
        ref byte pixel = ref _state.Pixels[ch][y * _state.Widths[ch] + x];
        byte value = on ? Ink : (byte)0;
        if ((pixel != 0) == on)
            return false;
        pixel = value;
        return true;
    }

    //
    // Whole-glyph and whole-font changes, each a step
    //

    // Runs a change as one step; one that changes nothing leaves none
    private void Step(string label, Action change)
    {
        EndEdit();
        var before = _state.Copy();
        change();
        if (before.Same(_state))
            return;
        _undo.Add((before, label));
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // A glyph's pixels as rows, made a new size: columns kept from the left (or right), rows from the top
    private byte[] Resized(int ch, int width, int left, int top)
    {
        int oldWidth = _state.Widths[ch];
        var pixels = new byte[width * Height];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < width; x++)
            {
                int ox = x + left, oy = y + top;
                if (ox >= 0 && oy >= 0 && ox < oldWidth && oy < _state.Pixels[ch].Length / Math.Max(oldWidth, 1) && oy < Height)
                    pixels[y * width + x] = _state.Pixels[ch][oy * oldWidth + ox];
            }
        return pixels;
    }

    /// <summary>
    /// A glyph made <paramref name="width"/> wide (0 to 255), columns added or taken off at its
    /// right, or its left with <paramref name="atLeft"/>
    /// </summary>
    public void SetWidth(int ch, int width, bool atLeft = false)
    {
        width = Math.Clamp(width, 0, 255);
        Step($"Character {ch} {width} wide", () =>
        {
            _state.Pixels[ch] = Resized(ch, width, atLeft ? _state.Widths[ch] - width : 0, 0);
            _state.Widths[ch] = width;
        });
    }

    /// <summary>A glyph's pixels moved, those moved off it lost</summary>
    public void Shift(int ch, int dx, int dy)
        => Step($"Character {ch} moved", () => _state.Pixels[ch] = Resized(ch, _state.Widths[ch], -dx, -dy));

    /// <summary>A glyph mirrored left to right, or (vertical) upside down</summary>
    public void Flip(int ch, bool vertical)
        => Step($"Character {ch} flipped", () =>
        {
            int width = _state.Widths[ch];
            var flipped = new byte[width * Height];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < width; x++)
                    flipped[y * width + x] = _state.Pixels[ch][(vertical ? Height - 1 - y : y) * width + (vertical ? x : width - 1 - x)];
            _state.Pixels[ch] = flipped;
        });

    /// <summary>A glyph's pixels all off (it keeps its width, as a space does)</summary>
    public void Clear(int ch) => Step($"Character {ch} cleared", () => Array.Clear(_state.Pixels[ch]));

    /// <summary>A character taken out of the font: 0 wide, no glyph</summary>
    public void Remove(int ch)
        => Step($"Character {ch} taken out", () =>
        {
            _state.Widths[ch] = 0;
            _state.Pixels[ch] = [];
        });

    /// <summary>A glyph as it is, for the clipboard</summary>
    public GlyphData Copy(int ch) => new(_state.Widths[ch], Height, (byte[])_state.Pixels[ch].Clone());

    /// <summary>A glyph put in a character's place: its width, its rows from the top (cut or padded to this font's height)</summary>
    public void Paste(int ch, GlyphData glyph) => Step($"Character {ch} pasted", () => Put(ch, glyph));

    private void Put(int ch, GlyphData glyph)
    {
        var pixels = new byte[glyph.Width * Height];
        for (int y = 0; y < Math.Min(Height, glyph.Height); y++)
            for (int x = 0; x < glyph.Width; x++)
            {
                int from = y * glyph.Width + x;
                if (from < glyph.Pixels.Length && glyph.Pixels[from] != 0)
                    pixels[y * glyph.Width + x] = Ink;
            }
        _state.Widths[ch] = glyph.Width;
        _state.Pixels[ch] = pixels;
    }

    /// <summary>The characters this font hasn't got and <paramref name="other"/> has, copied from it; how many</summary>
    public int CopyMissingFrom(FontAsset other)
    {
        int copied = 0;
        Step("Characters copied in", () =>
        {
            for (int ch = 0; ch < 256; ch++)
            {
                if (_state.Widths[ch] > 0 || other.Width[ch] == 0)
                    continue;
                Put(ch, new GlyphData(other.Width[ch], other.Height, other.Glyph(ch)));
                copied++;
            }
        });
        return copied;
    }

    /// <summary>The font made <paramref name="height"/> high (1 to 255): rows added or cut at the bottom, or the top with <paramref name="atTop"/></summary>
    public void SetHeight(int height, bool atTop = false)
    {
        height = Math.Clamp(height, 1, 255);
        if (height == Height)
            return;
        Step($"{height} high", () =>
        {
            int oldHeight = Height, top = atTop ? oldHeight - height : 0;
            for (int ch = 0; ch < 256; ch++)
            {
                int width = _state.Widths[ch];
                var pixels = new byte[width * height];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int oy = y + top;
                        if (oy >= 0 && oy < oldHeight)
                            pixels[y * width + x] = _state.Pixels[ch][oy * width + x];
                    }
                _state.Pixels[ch] = pixels;
            }
            _state = _state with { Height = height };
        });
    }

    //
    // Undo, saving
    //

    public void Undo() => Move(_undo, _redo);

    public void Redo() => Move(_redo, _undo);

    private void Move(List<(State State, string Label)> from, List<(State State, string Label)> to)
    {
        EndEdit();
        if (from.Count == 0)
            return;
        var (state, label) = from[^1];
        from.RemoveAt(from.Count - 1);
        to.Add((_state.Copy(), label));
        _state = state.Copy();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The font as a Wolf3D font file; throws InvalidDataException when it's too big for one</summary>
    public byte[] ToFile()
        => FontAsset.Write(Height, [.. Enumerable.Range(0, 256).Select(ch => (_state.Widths[ch], _state.Pixels[ch]))]);

    /// <summary>The font as the game reads it, to draw with; null when it's too big to be a font file</summary>
    public FontAsset? ToAsset()
    {
        try
        {
            return FontAsset.FromFile(ToFile());
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>What's drawn now is what's saved</summary>
    public void MarkSaved()
    {
        _saved = _state.Copy();
        IsNew = false;
    }
}
