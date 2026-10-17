namespace PFWolf.Editor.Editing;

public enum EditTool
{
    Paint,
    Line,
    Rectangle,
    Fill,
    Pick,
    Select,
    MagicWand,
}

/// <summary>A rotate or flip, and what became of the values on the tiles that point somewhere</summary>
public sealed record TransformReport(TileTransform Transform, RemapCounts Counts);

/// <summary>
/// What the map canvas's mouse and keys do to the level: the tools, the selection and the
/// clipboard. Lines, boxes, pastes and moves show in the level as they're dragged, as one
/// edit that's put back and redone each time the pointer moves, then committed on release.
/// </summary>
public sealed class ToolController
{
    private MapDocument? _document;
    private MapEdit? _edit;
    private (int X, int Y) _anchor;
    private (int X, int Y) _last;
    private bool _pressed;

    // Moving the selection's contents: what was lifted, and the selection it was lifted from
    private TileClip? _moving;
    private TileSelection? _movingFrom;

    // Dragging out a selection that adds to or takes from another: the one it started with
    private TileSelection? _selectBase;
    private SelectMode _selectMode;

    private TileClip? _clipboard;
    private TileClip? _pasting;

    public MapDocument? Document
    {
        get => _document;
        set
        {
            CancelGesture();
            if (_document != null)
                _document.Changed -= OnDocumentChanged;
            _document = value;
            if (_document != null)
                _document.Changed += OnDocumentChanged;
            Selection = null;
            _pasting = null;
            RaiseChanged();
        }
    }

    public EditTool Tool { get; set; } = EditTool.Paint;

    /// <summary>The plane the tools change</summary>
    public int Plane { get; set; }

    /// <summary>What the tools put down</summary>
    public ushort Value { get; set; }

    /// <summary>
    /// What a tile's value becomes when the tools put down a value over it (its old value, the
    /// value): when a layer is only part of a plane's value, as the patches are of plane 0's.
    /// Null: the value as it is.
    /// </summary>
    public Func<ushort, ushort, ushort>? Combine { get; set; }

    /// <summary>The part of a tile's value the layer being edited holds, for the eyedropper and fill. Null: all of it.</summary>
    public Func<ushort, ushort>? Read { get; set; }

    // Puts a value down on the plane being edited, through Combine
    private void Put(MapEdit edit, int x, int y, ushort value)
        => edit.Set(Plane, x, y, Combine is { } combine && edit.Document.Contains(x, y) ? combine(edit.Document[Plane, x, y], value) : value);

    private ushort ReadAt(MapDocument document, int x, int y)
        => Read is { } read ? read(document[Plane, x, y]) : document[Plane, x, y];

    /// <summary>What clearing a tile puts on each plane: open floor on plane 0, nothing on the rest</summary>
    public Func<int, ushort> EraseValue { get; set; } = _ => 0;

    /// <summary>The rectangle tool draws only the edge</summary>
    public bool Outline { get; set; }

    /// <summary>Copying takes every plane, not just the one being edited</summary>
    public bool CopyAllPlanes { get; set; } = true;

    /// <summary>The magic wand takes only the tiles joined to the one clicked, not every one like it on the level</summary>
    public bool WandJoined { get; set; } = true;

    /// <summary>Pasting leaves what's under the empty tiles of the paste (<see cref="IsBlank"/>)</summary>
    public bool PasteSkipsBlank { get; set; }

    /// <summary>Whether a value on a plane is nothing much (open floor, no thing), which a paste over can leave out</summary>
    public Func<int, ushort, bool>? IsBlank { get; set; }

    /// <summary>
    /// How the next selection made goes with the one there (the map sets it from Shift and Ctrl
    /// as the button goes down)
    /// </summary>
    public SelectMode SelectMode { get; set; }

    public TileSelection? Selection { get; private set; }

    /// <summary>Pasting: the pasted tiles follow the pointer until a click puts them down</summary>
    public bool IsPasting => _pasting != null;

    public bool HasClipboard => _clipboard != null;

    /// <summary>The level or what's drawn over it changed</summary>
    public event EventHandler? Changed;

    /// <summary>The eyedropper picked a value from the level</summary>
    public event EventHandler<ushort>? ValuePicked;

    public void Press(int x, int y)
    {
        if (_document is not { } document || !document.Contains(x, y))
            return;

        _pressed = true;
        _anchor = _last = (x, y);

        if (_pasting != null)
        {
            // The paste is already showing where it'll go
            _edit?.Commit();
            _edit = null;
            Selection = _pasting.SelectionAt(x, y).Clip(document.Width, document.Height);
            _pasting = null;
            _pressed = false;
            RaiseChanged();
            return;
        }

        switch (Tool)
        {
            case EditTool.Paint:
                _edit = document.BeginEdit("Paint");
                Put(_edit, x, y, Value);
                _edit.Refresh();
                break;

            case EditTool.Line:
            case EditTool.Rectangle:
                _edit = document.BeginEdit(Tool == EditTool.Line ? "Line" : "Rectangle");
                DrawShape(x, y);
                break;

            case EditTool.Fill:
                using (var fill = document.BeginEdit("Fill"))
                {
                    foreach (var (fx, fy) in TileShapes.FloodFill(document, Plane, x, y, Read))
                        Put(fill, fx, fy, Value);
                }
                _pressed = false;
                break;

            case EditTool.Pick:
                ValuePicked?.Invoke(this, ReadAt(document, x, y));
                _pressed = false;
                break;

            case EditTool.Select:
                if (SelectMode == SelectMode.Replace && Selection is { } selection && selection.Contains(x, y))
                {
                    // Drag the selection's contents somewhere else
                    _moving = TileClip.Copy(document, selection, CopyAllPlanes ? null : Plane);
                    _movingFrom = selection;
                    _edit = document.BeginEdit("Move");
                }
                else
                {
                    _selectBase = SelectMode == SelectMode.Replace ? null : Selection;
                    _selectMode = SelectMode;
                    SelectBand(x, y);
                }
                break;

            case EditTool.MagicWand:
                Wand(document, x, y);
                _pressed = false;
                break;
        }
    }

    public void Move(int x, int y)
    {
        if (_document is not { } document)
            return;

        if (_pasting != null)
        {
            ShowPaste(x, y);
            return;
        }

        if (!_pressed || (x, y) == _last)
            return;

        switch (Tool)
        {
            case EditTool.Paint when _edit != null:
                // Fill the gap a quick stroke leaves between pointer events
                foreach (var (lx, ly) in TileShapes.Line(_last.X, _last.Y, x, y))
                    Put(_edit, lx, ly, Value);
                _edit.Refresh();
                break;

            case EditTool.Line:
            case EditTool.Rectangle:
                DrawShape(x, y);
                break;

            case EditTool.Select when _moving != null && _edit != null && _movingFrom != null:
                int dx = x - _anchor.X, dy = y - _anchor.Y;
                _edit.Revert();
                Clear(_edit, _movingFrom, CopyAllPlanes ? null : Plane);
                _moving.Paste(_edit, _movingFrom.Left + dx, _movingFrom.Top + dy);
                Selection = _movingFrom.Offset(dx, dy);
                _edit.Refresh();
                break;

            case EditTool.Select:
                SelectBand(Math.Clamp(x, 0, document.Width - 1), Math.Clamp(y, 0, document.Height - 1));
                break;
        }

        _last = (x, y);
    }

    public void Release()
    {
        _pressed = false;
        _moving = null;
        _movingFrom = null;
        _selectBase = null;
        _edit?.Commit();
        _edit = null;
        RaiseChanged();     // what can be done with the selection now the button's up
    }

    // The box dragged from the anchor to (x, y), with the selection it adds to or takes from
    private void SelectBand(int x, int y)
    {
        var band = TileSelection.Rect(TileRect.FromCorners(_anchor.X, _anchor.Y, x, y));
        Selection = _selectBase is { } start ? start.Combine(band, _selectMode) : _selectMode == SelectMode.Subtract ? null : band;
        RaiseChanged();
    }

    /// <summary>The magic wand: the tiles holding what the one clicked does on the plane being edited, joined to it or anywhere</summary>
    private void Wand(MapDocument document, int x, int y)
    {
        IEnumerable<(int X, int Y)> tiles;
        if (WandJoined)
            tiles = TileShapes.FloodFill(document, Plane, x, y, Read);
        else
        {
            var value = ReadAt(document, x, y);
            tiles = TileShapes.Rectangle(new TileRect(0, 0, document.Width - 1, document.Height - 1), outline: false)
                .Where(tile => ReadAt(document, tile.X, tile.Y) == value);
        }

        var picked = TileSelection.FromTiles(tiles);
        Selection = picked == null ? Selection
            : SelectMode != SelectMode.Replace && Selection is { } start ? start.Combine(picked, SelectMode)
            : SelectMode == SelectMode.Subtract ? null
            : picked;
        RaiseChanged();
    }

    private void DrawShape(int x, int y)
    {
        _edit!.Revert();
        var tiles = Tool == EditTool.Line
            ? TileShapes.Line(_anchor.X, _anchor.Y, x, y)
            : TileShapes.Rectangle(TileRect.FromCorners(_anchor.X, _anchor.Y, x, y), Outline);
        foreach (var (tx, ty) in tiles)
            Put(_edit, tx, ty, Value);
        _edit.Refresh();
    }

    public void Copy()
    {
        if (_document != null && Selection is { } selection)
        {
            _clipboard = TileClip.Copy(_document, selection, CopyAllPlanes ? null : Plane);
            RaiseChanged();
        }
    }

    public void Cut()
    {
        Copy();
        Delete("Cut");
    }

    /// <summary>Copies the selection and picks the copy up to put down somewhere else, starting one tile down and right</summary>
    public void Duplicate()
    {
        if (Selection is not { } selection)
            return;
        Copy();
        StartPaste(selection.Left + 1, selection.Top + 1);
    }

    /// <summary>Clears the selection: open floor on plane 0, nothing on the others</summary>
    public void Delete(string description = "Delete")
    {
        if (_document == null || Selection is not { } selection)
            return;
        using var edit = _document.BeginEdit(description);
        Clear(edit, selection, CopyAllPlanes ? null : Plane);
    }

    /// <summary>Clears one tile on the plane being edited (Delete over the 3D view)</summary>
    public void EraseTile(int x, int y)
    {
        if (_document is not { } document || !document.Contains(x, y))
            return;
        CancelGesture();
        using var edit = document.BeginEdit("Erase");
        Put(edit, x, y, EraseValue(Plane));
    }

    /// <summary>
    /// Puts <paramref name="to"/> wherever the layer being edited holds <paramref name="from"/>,
    /// in the selection (or the whole level), as one undo step; how many tiles changed
    /// </summary>
    public int Replace(ushort from, ushort to, bool selectionOnly)
    {
        if (_document is not { } document || from == to)
            return 0;
        CancelGesture();
        var tiles = selectionOnly && Selection is { } selection
            ? selection.Tiles
            : TileShapes.Rectangle(new TileRect(0, 0, document.Width - 1, document.Height - 1), outline: false);

        int count = 0;
        using var edit = document.BeginEdit("Replace");
        foreach (var (x, y) in tiles)
        {
            if (ReadAt(document, x, y) != from)
                continue;
            Put(edit, x, y, to);
            count++;
        }
        return count;
    }

    /// <summary>Picks up the clipboard: it follows the pointer from (x, y) until a click puts it down</summary>
    public void StartPaste(int x, int y)
    {
        if (_document == null || _clipboard == null)
            return;
        CancelGesture();
        _pasting = _clipboard;
        _edit = _document.BeginEdit("Paste");
        ShowPaste(Math.Max(x, 0), Math.Max(y, 0));
    }

    /// <summary>
    /// The arrow keys: moves the paste being placed, or else the selection's tiles, a tile at
    /// a time (each move its own undo step)
    /// </summary>
    public void Nudge(int dx, int dy)
    {
        if (_document is not { } document)
            return;
        if (_pasting != null)
        {
            ShowPaste(_pasteAt.X + dx, _pasteAt.Y + dy);
            return;
        }
        if (_pressed || Selection is not { } selection)
            return;

        int? plane = CopyAllPlanes ? null : Plane;
        var clip = TileClip.Copy(document, selection, plane);
        using (var edit = document.BeginEdit("Move"))
        {
            Clear(edit, selection, plane);
            clip.Paste(edit, selection.Left + dx, selection.Top + dy);
        }
        Selection = selection.Offset(dx, dy).Clip(document.Width, document.Height);
        RaiseChanged();
    }

    /// <summary>Makes what a transform searches the mapdefs with, so things turn with it; null turns the tiles only</summary>
    public Func<TileTransform, TileRemap>? MakeRemap { get; set; }

    /// <summary>A rotate or flip was done, with what became of the values pointing somewhere</summary>
    public event EventHandler<TransformReport>? Transformed;

    /// <summary>Whether there's something to rotate or flip: a paste being placed, or the selection</summary>
    public bool CanTransform => _document != null && (_pasting != null || (Selection != null && !_pressed));

    /// <summary>
    /// Rotates or flips the paste being placed, or else the selection's tiles where they are
    /// (turning around its middle), as one undo step. Things on them turn to face the new way.
    /// </summary>
    public void Transform(TileTransform transform)
    {
        if (_document is not { } document || !CanTransform)
            return;

        var remap = MakeRemap?.Invoke(transform);
        RemapCounts counts;
        if (_pasting != null)
        {
            _pasting = _pasting.Transform(transform, remap, out counts);
            ShowPaste(_pasteAt.X, _pasteAt.Y);
        }
        else
        {
            var selection = Selection!;
            int? plane = CopyAllPlanes ? null : Plane;
            var clip = TileClip.Copy(document, selection, plane).Transform(transform, remap, out counts);
            var placed = TileTransforms.Place(transform, selection.Bounds, document.Width, document.Height);
            using (var edit = document.BeginEdit(TileTransforms.Describe(transform)))
            {
                Clear(edit, selection, plane);
                clip.Paste(edit, placed.Left, placed.Top);
            }
            Selection = clip.SelectionAt(placed.Left, placed.Top).Clip(document.Width, document.Height);
        }
        RaiseChanged();
        Transformed?.Invoke(this, new TransformReport(transform, counts));
    }

    private (int X, int Y) _pasteAt;

    private void ShowPaste(int x, int y)
    {
        if (_edit == null || _pasting == null)
            return;
        _pasteAt = (x, y);
        _edit.Revert();
        _pasting.Paste(_edit, x, y, PasteSkipsBlank ? IsBlank : null);
        Selection = _pasting.SelectionAt(x, y);
        _edit.Refresh();
    }

    /// <summary>Esc: drops a paste or a drag in progress, else the selection</summary>
    public void Escape()
    {
        if (_edit != null)
            CancelGesture();
        else
            Selection = null;
        RaiseChanged();
    }

    /// <summary>Selects one tile (a level check's)</summary>
    public void SelectTile(int x, int y)
    {
        if (_document == null || !_document.Contains(x, y))
            return;
        CancelGesture();
        Selection = TileSelection.Rect(new TileRect(x, y, x, y));
        RaiseChanged();
    }

    public void SelectAll()
    {
        if (_document == null)
            return;
        Selection = TileSelection.Rect(new TileRect(0, 0, _document.Width - 1, _document.Height - 1));
        RaiseChanged();
    }

    /// <summary>Selects every tile that isn't selected, and none that is</summary>
    public void InvertSelection()
    {
        if (_document is not { } document)
            return;
        CancelGesture();
        var all = new TileRect(0, 0, document.Width - 1, document.Height - 1);
        Selection = Selection is { } selection
            ? TileSelection.FromTiles(TileShapes.Rectangle(all, outline: false).Where(tile => !selection.Contains(tile.X, tile.Y)))
            : TileSelection.Rect(all);
        RaiseChanged();
    }

    /// <summary>Drops whatever's being dragged or pasted, before an undo, say</summary>
    public void CancelGesture()
    {
        _edit?.Cancel();
        _edit = null;
        _pasting = null;
        _moving = null;
        _movingFrom = null;
        _selectBase = null;
        _pressed = false;
    }

    private void Clear(MapEdit edit, TileSelection selection, int? plane)
    {
        for (int p = 0; p < edit.Document.Planes; p++)
        {
            if (plane != null && plane != p)
                continue;
            foreach (var (x, y) in selection.Tiles)
            {
                if (plane == null)
                    edit.Set(p, x, y, EraseValue(p));
                else
                    Put(edit, x, y, EraseValue(p));     // only the layer being edited
            }
        }
    }

    private void OnDocumentChanged(object? sender, EventArgs e) => RaiseChanged();

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
