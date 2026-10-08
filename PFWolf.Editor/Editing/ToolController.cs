namespace PFWolf.Editor.Editing;

public enum EditTool
{
    Paint,
    Line,
    Rectangle,
    Fill,
    Pick,
    Select,
}

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

    // Moving the selection's contents: what was lifted, and where the selection started
    private TileClip? _moving;
    private TileRect _movingFrom;

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

    /// <summary>What clearing a tile puts on each plane: open floor on plane 0, nothing on the rest</summary>
    public Func<int, ushort> EraseValue { get; set; } = _ => 0;

    /// <summary>The rectangle tool draws only the edge</summary>
    public bool Outline { get; set; }

    /// <summary>Copying takes every plane, not just the one being edited</summary>
    public bool CopyAllPlanes { get; set; } = true;

    public TileRect? Selection { get; private set; }

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
            Selection = new TileRect(x, y, x + _pasting.Width - 1, y + _pasting.Height - 1);
            _pasting = null;
            _pressed = false;
            RaiseChanged();
            return;
        }

        switch (Tool)
        {
            case EditTool.Paint:
                _edit = document.BeginEdit("Paint");
                _edit.Set(Plane, x, y, Value);
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
                    foreach (var (fx, fy) in TileShapes.FloodFill(document, Plane, x, y))
                        fill.Set(Plane, fx, fy, Value);
                }
                _pressed = false;
                break;

            case EditTool.Pick:
                ValuePicked?.Invoke(this, document[Plane, x, y]);
                _pressed = false;
                break;

            case EditTool.Select:
                if (Selection is { } selection && selection.Contains(x, y))
                {
                    // Drag the selection's contents somewhere else
                    _moving = TileClip.Copy(document, selection, CopyAllPlanes ? null : Plane);
                    _movingFrom = selection;
                    _edit = document.BeginEdit("Move");
                }
                else
                {
                    Selection = new TileRect(x, y, x, y);
                    RaiseChanged();
                }
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
                    _edit.Set(Plane, lx, ly, Value);
                _edit.Refresh();
                break;

            case EditTool.Line:
            case EditTool.Rectangle:
                DrawShape(x, y);
                break;

            case EditTool.Select when _moving != null && _edit != null:
                int dx = x - _anchor.X, dy = y - _anchor.Y;
                _edit.Revert();
                Clear(_edit, _movingFrom, CopyAllPlanes ? null : Plane);
                _moving.Paste(_edit, _movingFrom.Left + dx, _movingFrom.Top + dy);
                Selection = _movingFrom.Offset(dx, dy);
                _edit.Refresh();
                break;

            case EditTool.Select:
                Selection = TileRect.FromCorners(_anchor.X, _anchor.Y, Math.Clamp(x, 0, document.Width - 1), Math.Clamp(y, 0, document.Height - 1));
                RaiseChanged();
                break;
        }

        _last = (x, y);
    }

    public void Release()
    {
        _pressed = false;
        _moving = null;
        _edit?.Commit();
        _edit = null;
    }

    private void DrawShape(int x, int y)
    {
        _edit!.Revert();
        var tiles = Tool == EditTool.Line
            ? TileShapes.Line(_anchor.X, _anchor.Y, x, y)
            : TileShapes.Rectangle(TileRect.FromCorners(_anchor.X, _anchor.Y, x, y), Outline);
        foreach (var (tx, ty) in tiles)
            _edit.Set(Plane, tx, ty, Value);
        _edit.Refresh();
    }

    public void Copy()
    {
        if (_document != null && Selection is { } selection)
            _clipboard = TileClip.Copy(_document, selection, CopyAllPlanes ? null : Plane);
    }

    public void Cut()
    {
        Copy();
        Delete("Cut");
    }

    /// <summary>Clears the selection: open floor on plane 0, nothing on the others</summary>
    public void Delete(string description = "Delete")
    {
        if (_document == null || Selection is not { } selection)
            return;
        using var edit = _document.BeginEdit(description);
        Clear(edit, selection, CopyAllPlanes ? null : Plane);
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

    private void ShowPaste(int x, int y)
    {
        if (_edit == null || _pasting == null)
            return;
        _edit.Revert();
        _pasting.Paste(_edit, x, y);
        Selection = new TileRect(x, y, x + _pasting.Width - 1, y + _pasting.Height - 1);
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
        Selection = new TileRect(x, y, x, y);
        RaiseChanged();
    }

    public void SelectAll()
    {
        if (_document == null)
            return;
        Selection = new TileRect(0, 0, _document.Width - 1, _document.Height - 1);
        RaiseChanged();
    }

    /// <summary>Drops whatever's being dragged or pasted, before an undo, say</summary>
    public void CancelGesture()
    {
        _edit?.Cancel();
        _edit = null;
        _pasting = null;
        _moving = null;
        _pressed = false;
    }

    private void Clear(MapEdit edit, TileRect rect, int? plane)
    {
        for (int p = 0; p < edit.Document.Planes; p++)
        {
            if (plane != null && plane != p)
                continue;
            foreach (var (x, y) in TileShapes.Rectangle(rect, outline: false))
                edit.Set(p, x, y, EraseValue(p));
        }
    }

    private void OnDocumentChanged(object? sender, EventArgs e) => RaiseChanged();

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
