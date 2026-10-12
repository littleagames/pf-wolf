using PFWolf.Editor.Data;
using PFWolf.Editor.Rendering;

namespace PFWolf.Editor.Editing;

/// <summary>
/// A picture being drawn in the pixel editor: palette indices row by row, which pixels show (for
/// kinds that can have see-through pixels: sprites, pictures and flats; a wall texture shows
/// every one), and a sprite's offset. Each edit (a stroke, a fill) is one undo step.
/// </summary>
public sealed class PixelDocument
{
    /// <summary>The value of a see-through pixel, where a palette index goes</summary>
    public const int Clear = -1;

    private readonly List<(byte[] Indices, bool[]? Opaque, string Label)> _undo = [];
    private readonly List<(byte[] Indices, bool[]? Opaque, string Label)> _redo = [];
    private byte[] _savedIndices;
    private bool[]? _savedOpaque;
    private bool _editing, _editChanged;

    public PixelDocument(ArtKind kind, string name, IndexedPicture picture, (int X, int Y)? offset = null, bool isNew = false)
    {
        Kind = kind;
        Name = name.ToUpperInvariant();
        Width = picture.Width;
        Height = picture.Height;
        Indices = picture.Indices.ToArray();
        CanBeClear = kind != ArtKind.Texture;
        Opaque = CanBeClear ? picture.Opaque?.ToArray() ?? Enumerable.Repeat(true, Width * Height).ToArray() : null;
        Offset = offset;
        IsNew = isNew;
        _savedIndices = Indices.ToArray();
        _savedOpaque = Opaque?.ToArray();
    }

    /// <summary>A new picture of one color (or see-through, with <see cref="Clear"/>)</summary>
    public static PixelDocument Blank(ArtKind kind, string name, int width, int height, int fill)
    {
        var clear = fill == Clear && kind != ArtKind.Texture;
        var picture = new IndexedPicture(width, height, Enumerable.Repeat((byte)Math.Max(fill, 0), width * height).ToArray(),
            clear ? new bool[width * height] : null);
        // A new sprite stands on the middle of its bottom row
        return new PixelDocument(kind, name, picture, kind == ArtKind.Sprite ? (width / 2, height) : null, isNew: true);
    }

    public ArtKind Kind { get; }
    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public byte[] Indices { get; }

    /// <summary>Which pixels show; null for a wall texture, which shows them all</summary>
    public bool[]? Opaque { get; }

    /// <summary>Whether pixels can be see-through (not a wall texture's)</summary>
    public bool CanBeClear { get; }

    /// <summary>A sprite's offset (its PNG's grAb chunk); null for anything else</summary>
    public (int X, int Y)? Offset { get; }

    /// <summary>A picture the game doesn't have yet: saving adds it</summary>
    public bool IsNew { get; private set; }

    public IndexedPicture Picture => new(Width, Height, Indices, Opaque);

    public bool IsDirty => !Indices.AsSpan().SequenceEqual(_savedIndices) || Opaque != null && !Opaque.AsSpan().SequenceEqual(_savedOpaque);

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string? UndoLabel => _undo.Count > 0 ? _undo[^1].Label : null;
    public string? RedoLabel => _redo.Count > 0 ? _redo[^1].Label : null;

    /// <summary>The pixels changed (an edit, undo or redo)</summary>
    public event EventHandler? Changed;

    public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>A pixel's palette index, or <see cref="Clear"/> when it's see-through</summary>
    public int ValueAt(int x, int y)
    {
        int i = y * Width + x;
        return Opaque != null && !Opaque[i] ? Clear : Indices[i];
    }

    /// <summary>Starts an edit: what's drawn until <see cref="EndEdit"/> is one undo step</summary>
    public void BeginEdit(string label)
    {
        EndEdit();
        _undo.Add((Indices.ToArray(), Opaque?.ToArray(), label));
        _editing = true;
        _editChanged = false;
    }

    /// <summary>Ends the edit; one that changed nothing leaves no undo step</summary>
    public void EndEdit()
    {
        if (!_editing)
            return;
        _editing = false;
        if (!_editChanged)
            _undo.RemoveAt(_undo.Count - 1);
        else
            _redo.Clear();
    }

    /// <summary>Sets a pixel to a palette index or see-through (inside an edit)</summary>
    public void SetPixel(int x, int y, int value)
    {
        if (Put(x, y, value))
            Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Draws a line of pixels from one to the other, both ends included (inside an edit)</summary>
    public void Line(int x0, int y0, int x1, int y1, int value)
    {
        bool changed = false;
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            changed |= Put(x0, y0, value);
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

    /// <summary>
    /// Fills the pixels joined to this one (up, down and across) that are the same as it; with
    /// <paramref name="everywhere"/>, every pixel the same as it in the picture (inside an edit)
    /// </summary>
    public void Fill(int x, int y, int value, bool everywhere = false)
    {
        if (!Contains(x, y))
            return;
        int target = ValueAt(x, y);
        if (target == Normalize(value))
            return;

        bool changed = false;
        if (everywhere)
        {
            for (int py = 0; py < Height; py++)
                for (int px = 0; px < Width; px++)
                    if (ValueAt(px, py) == target)
                        changed |= Put(px, py, value);
        }
        else
        {
            var stack = new Stack<(int X, int Y)>();
            stack.Push((x, y));
            while (stack.Count > 0)
            {
                var (px, py) = stack.Pop();
                if (!Contains(px, py) || ValueAt(px, py) != target)
                    continue;
                changed |= Put(px, py, value);
                stack.Push((px + 1, py));
                stack.Push((px - 1, py));
                stack.Push((px, py + 1));
                stack.Push((px, py - 1));
            }
        }
        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Undo() => Step(_undo, _redo);

    public void Redo() => Step(_redo, _undo);

    /// <summary>What's drawn now is what's saved</summary>
    public void MarkSaved()
    {
        _savedIndices = Indices.ToArray();
        _savedOpaque = Opaque?.ToArray();
        IsNew = false;
    }

    // A value a pixel can hold: see-through only where pixels can be
    private int Normalize(int value) => value == Clear && !CanBeClear ? 0 : value;

    private bool Put(int x, int y, int value)
    {
        if (!Contains(x, y))
            return false;
        value = Normalize(value);
        if (ValueAt(x, y) == value)
            return false;

        int i = y * Width + x;
        if (value == Clear)
            Opaque![i] = false;
        else
        {
            Indices[i] = (byte)value;
            if (Opaque != null)
                Opaque[i] = true;
        }
        _editChanged = true;
        return true;
    }

    private void Step(List<(byte[] Indices, bool[]? Opaque, string Label)> from, List<(byte[] Indices, bool[]? Opaque, string Label)> to)
    {
        EndEdit();
        if (from.Count == 0)
            return;
        var (indices, opaque, label) = from[^1];
        from.RemoveAt(from.Count - 1);
        to.Add((Indices.ToArray(), Opaque?.ToArray(), label));
        indices.CopyTo(Indices, 0);
        opaque?.CopyTo(Opaque!, 0);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
