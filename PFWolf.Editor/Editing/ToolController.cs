using PFWolf.Assets;

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
    Erase,
    Scatter,
    Ellipse,
    Room,
    Stamp,
    /// <summary>Places, picks up and moves a level's extra things (MapAsset.ExtraThings) anywhere, off the tile grid</summary>
    FreeThing,
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
            SelectedExtra = -1;
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

    /// <summary>How many tiles across the paint, erase and scatter brushes are, 1 to 9</summary>
    public int BrushSize { get; set; } = 1;

    /// <summary>The brushes are round, not square</summary>
    public bool BrushRound { get; set; }

    /// <summary>How many of the tiles the scatter brush passes over it puts something on, in percent</summary>
    public int ScatterDensity { get; set; } = 20;

    /// <summary>What the scatter brush picks from at random; empty: <see cref="Value"/></summary>
    public IReadOnlyList<ushort> ScatterValues { get; set; } = [];

    public Random Random { get; set; } = new();

    /// <summary>Lines keep to rows, columns and diagonals, and boxes, ellipses and rooms to squares (Shift, as the map sets it)</summary>
    public bool Constrain { get; set; }

    // The scatter brush: the tiles it's passed over this stroke, each tried once
    private readonly HashSet<(int X, int Y)> _scattered = [];

    // The stamp: where it's been put down this drag
    private readonly HashSet<(int X, int Y)> _stamped = [];

    /// <summary>
    /// The tiles the tool would work on with the pointer on (x, y), to show under it: the
    /// brush's square, the stamp (the clipboard) around it, else the tile
    /// </summary>
    public TileRect HoverArea(int x, int y) => Tool switch
    {
        EditTool.Paint or EditTool.Erase or EditTool.Scatter => TileShapes.BrushBounds(x, y, BrushSize),
        EditTool.Stamp when _clipboard != null && _pasting == null
            => new TileRect(x - _clipboard.Width / 2, y - _clipboard.Height / 2, x - _clipboard.Width / 2 + _clipboard.Width - 1, y - _clipboard.Height / 2 + _clipboard.Height - 1),
        _ => new TileRect(x, y, x, y),
    };

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
            case EditTool.Erase:
                _edit = document.BeginEdit(Tool == EditTool.Paint ? "Paint" : "Erase");
                Brush(_edit, x, y);
                _edit.Refresh();
                break;

            case EditTool.Scatter:
                _scattered.Clear();
                _edit = document.BeginEdit("Scatter");
                Scatter(_edit, x, y);
                _edit.Refresh();
                break;

            case EditTool.Line:
            case EditTool.Rectangle:
            case EditTool.Ellipse:
            case EditTool.Room:
                _edit = document.BeginEdit(Tool.ToString());
                DrawShape(x, y);
                break;

            case EditTool.Stamp:
                if (_clipboard == null)
                {
                    _pressed = false;
                    break;
                }
                _stamped.Clear();
                _edit = document.BeginEdit("Stamp");
                Stamp(_edit, x, y);
                _edit.Refresh();
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
            case EditTool.Paint or EditTool.Erase when _edit != null:
                // Fill the gap a quick stroke leaves between pointer events
                foreach (var (lx, ly) in TileShapes.Line(_last.X, _last.Y, x, y).Skip(1))
                    Brush(_edit, lx, ly);
                _edit.Refresh();
                break;

            case EditTool.Scatter when _edit != null:
                foreach (var (lx, ly) in TileShapes.Line(_last.X, _last.Y, x, y).Skip(1))
                    Scatter(_edit, lx, ly);
                _edit.Refresh();
                break;

            case EditTool.Line:
            case EditTool.Rectangle:
            case EditTool.Ellipse:
            case EditTool.Room:
                DrawShape(x, y);
                break;

            case EditTool.Stamp when _edit != null && _clipboard != null:
                // Side by side with the first, so a drag tiles it
                int sx = _anchor.X + (int)Math.Floor((x - _anchor.X) / (double)_clipboard.Width) * _clipboard.Width;
                int sy = _anchor.Y + (int)Math.Floor((y - _anchor.Y) / (double)_clipboard.Height) * _clipboard.Height;
                Stamp(_edit, sx, sy);
                _edit.Refresh();
                break;

            case EditTool.Select when _moving != null && _edit != null && _movingFrom != null:
                int dx = x - _anchor.X, dy = y - _anchor.Y;
                _edit.Revert();
                Clear(_edit, _movingFrom, CopyAllPlanes ? null : Plane);
                _moving.Paste(_edit, _movingFrom.Left + dx, _movingFrom.Top + dy);
                Selection = _movingFrom.Offset(dx, dy).Clip(document.Width, document.Height);
                _edit.Refresh();
                break;

            case EditTool.Select:
                SelectBand(Math.Clamp(x, 0, document.Width - 1), Math.Clamp(y, 0, document.Height - 1));
                break;
        }

        _last = (x, y);
    }

    /*
    =============================================================================

                                    FREE THINGS

    A UWMF level's extra things (MapAsset.ExtraThings) stand anywhere, not on the object plane:
    off a tile's middle, or a second on a tile. The free-thing tool puts the palette's thing down
    where it's clicked (snapped to an eighth of a tile, or the tile's middle with Shift), picks
    one up and drags it, and turns (the rotate and flip keys) or deletes the one picked.

    =============================================================================
    */

    /// <summary>The extra thing the free-thing tool has picked (its place in the level's extra things), or -1</summary>
    public int SelectedExtra { get; private set; } = -1;

    /// <summary>
    /// What the free-thing tool puts down: the palette's thing as an extra thing (where it goes is
    /// set on placing), or null when the palette's value isn't a thing
    /// </summary>
    public Func<MapThing?>? NewExtraThing { get; set; }

    /// <summary>How near (in tiles) a click has to be to an extra thing to pick it up</summary>
    public const double ExtraReach = 0.35;

    // A drag of an extra thing: the extra things as it found them, and where the pointer was on the thing
    private List<MapThing>? _extrasBefore;
    private (double X, double Y) _grab;
    private bool _extraMoved;

    /// <summary>A spot snapped to an eighth of a tile, or with <paramref name="middle"/> to its tile's middle</summary>
    public static double Snap(double value, bool middle) => middle ? Math.Floor(value) + 0.5 : Math.Round(value * 8) / 8;

    /// <summary>The extra thing nearest (x, y) within reach (its place in the list), or -1</summary>
    public static int ExtraAt(IReadOnlyList<MapThing> things, double x, double y)
    {
        int nearest = -1;
        double best = ExtraReach * ExtraReach;
        for (int i = things.Count - 1; i >= 0; i--)
        {
            double dx = things[i].X - x, dy = things[i].Y - y, d = dx * dx + dy * dy;
            if (d <= best)
                (best, nearest) = (d, i);
        }
        return nearest;
    }

    /// <summary>The button goes down at (x, y), in tiles with their fractions: the free-thing tool's own, else the tile's</summary>
    public void PressAt(double x, double y)
    {
        if (Tool != EditTool.FreeThing)
        {
            Press((int)Math.Floor(x), (int)Math.Floor(y));
            return;
        }
        if (_document is not { } document || !document.Contains((int)Math.Floor(x), (int)Math.Floor(y)))
            return;

        var things = document.Map.ExtraThings;
        int hit = ExtraAt(things, x, y);
        if (hit >= 0)
        {
            // Pick it up to drag
            SelectedExtra = hit;
            _extrasBefore = [.. things];
            _grab = (x - things[hit].X, y - things[hit].Y);
            _extraMoved = false;
            _pressed = true;
        }
        else if (NewExtraThing?.Invoke() is { } template)
        {
            var placed = template with { X = Snap(x, Constrain), Y = Snap(y, Constrain) };
            document.ChangeExtraThings($"Place {template.Class}", things, [.. things, placed]);
            SelectedExtra = document.Map.ExtraThings.Count - 1;
        }
        else
            SelectedExtra = -1;
        RaiseChanged();
    }

    /// <summary>The pointer moves to (x, y), in tiles with their fractions: the free-thing tool's drag, else the tile's</summary>
    public void MoveAt(double x, double y)
    {
        if (Tool != EditTool.FreeThing)
        {
            Move((int)Math.Floor(x), (int)Math.Floor(y));
            return;
        }
        if (_document is not { } document || _extrasBefore == null || SelectedExtra < 0 || SelectedExtra >= _extrasBefore.Count)
            return;

        // Kept on the level
        double nx = Math.Clamp(Snap(x - _grab.X, Constrain), 0, document.Width - 0.01);
        double ny = Math.Clamp(Snap(y - _grab.Y, Constrain), 0, document.Height - 0.01);
        var things = new List<MapThing>(_extrasBefore);
        if ((things[SelectedExtra].X, things[SelectedExtra].Y) == (nx, ny) && !_extraMoved)
            return;
        things[SelectedExtra] = things[SelectedExtra] with { X = nx, Y = ny };
        _extraMoved = true;
        document.PreviewExtraThings(things);
    }

    // Ends a drag of an extra thing: one undo step if it moved
    private void ReleaseExtra()
    {
        if (_extrasBefore == null)
            return;
        if (_extraMoved && _document != null)
            _document.ChangeExtraThings("Move thing", _extrasBefore, _document.Map.ExtraThings);
        _extrasBefore = null;
        _extraMoved = false;
    }

    /// <summary>Takes the extra thing picked off the level; false when none is</summary>
    public bool DeleteExtra()
    {
        if (_document is not { } document || SelectedExtra < 0 || SelectedExtra >= document.Map.ExtraThings.Count)
            return false;
        var things = document.Map.ExtraThings;
        var thing = things[SelectedExtra];
        document.ChangeExtraThings($"Delete {thing.Class}", things, things.Where((_, i) => i != SelectedExtra).ToList());
        SelectedExtra = -1;
        RaiseChanged();
        return true;
    }

    // Turns the extra thing picked the way a rotate or flip turns things; false when none is
    private bool TransformExtra(TileTransform transform)
    {
        if (_document is not { } document || Tool != EditTool.FreeThing || SelectedExtra < 0 || SelectedExtra >= document.Map.ExtraThings.Count)
            return false;
        var things = document.Map.ExtraThings;
        var turned = new List<MapThing>(things);
        turned[SelectedExtra] = things[SelectedExtra] with { Angle = TileTransforms.Angle(transform, things[SelectedExtra].Angle) };
        document.ChangeExtraThings(TileTransforms.Describe(transform), things, turned);
        RaiseChanged();
        return true;
    }

    public void Release()
    {
        ReleaseExtra();
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

    // The line, box, ellipse or room from the anchor to (x, y), drawn again over what was there
    private void DrawShape(int x, int y)
    {
        _edit!.Revert();
        if (Constrain)
            (x, y) = Tool == EditTool.Line ? TileShapes.Straighten(_anchor.X, _anchor.Y, x, y) : TileShapes.Square(_anchor.X, _anchor.Y, x, y);
        var rect = TileRect.FromCorners(_anchor.X, _anchor.Y, x, y);
        switch (Tool)
        {
            case EditTool.Line:
                foreach (var (tx, ty) in TileShapes.Line(_anchor.X, _anchor.Y, x, y))
                    Put(_edit, tx, ty, Value);
                break;
            case EditTool.Ellipse:
                foreach (var (tx, ty) in TileShapes.Ellipse(rect, Outline))
                    Put(_edit, tx, ty, Value);
                break;
            case EditTool.Room:
                // Walls round the edge, the inside cleared (open floor on plane 0)
                foreach (var (tx, ty) in TileShapes.Rectangle(rect, outline: false))
                {
                    bool edge = tx == rect.Left || tx == rect.Right || ty == rect.Top || ty == rect.Bottom;
                    Put(_edit, tx, ty, edge ? Value : EraseValue(Plane));
                }
                break;
            default:
                foreach (var (tx, ty) in TileShapes.Rectangle(rect, Outline))
                    Put(_edit, tx, ty, Value);
                break;
        }
        _edit.Refresh();
    }

    // The paint or erase brush on (x, y)
    private void Brush(MapEdit edit, int x, int y)
    {
        var value = Tool == EditTool.Erase ? EraseValue(Plane) : Value;
        foreach (var (bx, by) in TileShapes.Brush(x, y, BrushSize, BrushRound))
            Put(edit, bx, by, value);
    }

    // The scatter brush on (x, y): each tile it hasn't passed over yet this stroke gets something, at the density's odds
    private void Scatter(MapEdit edit, int x, int y)
    {
        foreach (var tile in TileShapes.Brush(x, y, BrushSize, BrushRound))
        {
            if (!edit.Document.Contains(tile.X, tile.Y) || !_scattered.Add(tile) || Random.Next(100) >= ScatterDensity)
                continue;
            var value = ScatterValues.Count > 0 ? ScatterValues[Random.Next(ScatterValues.Count)] : Value;
            Put(edit, tile.X, tile.Y, value);
        }
    }

    // The clipboard put down around (x, y), unless it's been put there this drag
    private void Stamp(MapEdit edit, int x, int y)
    {
        if (_clipboard == null || !_stamped.Add((x, y)))
            return;
        _clipboard.Paste(edit, x - _clipboard.Width / 2, y - _clipboard.Height / 2, PasteSkipsBlank ? IsBlank : null);
    }

    /// <summary>The clipboard's size, for the stamp's options; null when nothing's been copied</summary>
    public (int Width, int Height)? ClipboardSize => _clipboard is { } clip ? (clip.Width, clip.Height) : null;

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
        if (Tool == EditTool.FreeThing && DeleteExtra())
            return;
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

    /// <summary>Whether there's something to rotate or flip: a paste being placed, the stamp, or the selection</summary>
    public bool CanTransform => _document != null && !_pressed
        && (_pasting != null || (Tool == EditTool.Stamp && _clipboard != null) || Selection != null
            || (Tool == EditTool.FreeThing && SelectedExtra >= 0));

    /// <summary>
    /// Rotates or flips the paste being placed, or else the selection's tiles where they are
    /// (turning around its middle), as one undo step. Things on them turn to face the new way.
    /// </summary>
    public void Transform(TileTransform transform)
    {
        if (_document is not { } document || !CanTransform)
            return;
        if (_pasting == null && TransformExtra(transform))
            return;

        var remap = MakeRemap?.Invoke(transform);
        RemapCounts counts;
        if (_pasting != null)
        {
            _pasting = _pasting.Transform(transform, remap, out counts);
            ShowPaste(_pasteAt.X, _pasteAt.Y);
        }
        else if (Tool == EditTool.Stamp && _clipboard != null)
            _clipboard = _clipboard.Transform(transform, remap, out counts);
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
        // Only the part on the level: what's past its edge is dropped
        Selection = _pasting.SelectionAt(x, y).Clip(_edit.Document.Width, _edit.Document.Height);
        _edit.Refresh();
    }

    /// <summary>Esc: drops a paste or a drag in progress, else the selection</summary>
    public void Escape()
    {
        if (_edit != null || _extrasBefore != null)
            CancelGesture();
        else if (SelectedExtra >= 0)
            SelectedExtra = -1;
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
        if (_extrasBefore != null)
        {
            // A drag of an extra thing: back where it was
            _document?.PreviewExtraThings(_extrasBefore);
            _extrasBefore = null;
            _extraMoved = false;
        }
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

    /// <summary>Something the tools show changed (the brush's size, the tool): the views draw again</summary>
    public void Refresh() => RaiseChanged();

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        // An undo may have taken the extra thing picked away
        if (_document != null && SelectedExtra >= _document.Map.ExtraThings.Count)
            SelectedExtra = -1;
        RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
