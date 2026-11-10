using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using PFWolf.Assets.Sounds;
using PFWolf.Editor.Audio.Opl;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Rendering;

using Point = Avalonia.Point;

/// <summary>
/// An IMF song's notes on a piano roll, in a ScrollViewer: time across (ticks, with the grid
/// and beats), pitch up (a row a semitone, the keys down the left), each channel's notes in its
/// color, a note's pitch changes as a line through it. The ruler on top sets where play starts.
/// With the Draw tool a click on nothing puts a note there (dragged out for its length); a note
/// is picked by clicking it (Ctrl adds), moved by dragging it, made longer or shorter by dragging
/// its right edge; a drag on nothing with the Select tool (or Shift) picks the notes in a box.
/// </summary>
public sealed class PianoRollView : Control
{
    public static readonly StyledProperty<SongEditorViewModel?> EditorProperty =
        AvaloniaProperty.Register<PianoRollView, SongEditorViewModel?>(nameof(Editor));
    public static readonly StyledProperty<int> RevisionProperty = AvaloniaProperty.Register<PianoRollView, int>(nameof(Revision));
    public static readonly StyledProperty<double> PixelsPerTickProperty = AvaloniaProperty.Register<PianoRollView, double>(nameof(PixelsPerTick), 0.25);
    public static readonly StyledProperty<int> PlayheadTickProperty = AvaloniaProperty.Register<PianoRollView, int>(nameof(PlayheadTick), -1);

    static PianoRollView()
    {
        AffectsRender<PianoRollView>(EditorProperty, RevisionProperty, PixelsPerTickProperty, PlayheadTickProperty);
        AffectsMeasure<PianoRollView>(EditorProperty, RevisionProperty, PixelsPerTickProperty);
        FocusableProperty.OverrideDefaultValue<PianoRollView>(true);
    }

    public SongEditorViewModel? Editor { get => GetValue(EditorProperty); set => SetValue(EditorProperty, value); }
    public int Revision { get => GetValue(RevisionProperty); set => SetValue(RevisionProperty, value); }
    public double PixelsPerTick { get => GetValue(PixelsPerTickProperty); set => SetValue(PixelsPerTickProperty, value); }
    public int PlayheadTick { get => GetValue(PlayheadTickProperty); set => SetValue(PlayheadTickProperty, value); }

    public const double KeyWidth = 52, RulerHeight = 22, RowHeight = 10;
    private const int TopNote = 119, BottomNote = 12;   // B8 down to C0
    private const double EdgeGrip = 5;

    private static readonly IBrush Background = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x22));
    private static readonly IBrush BlackKeyRow = new SolidColorBrush(Color.FromRgb(0x19, 0x19, 0x1c));
    private static readonly IBrush KeyWhite = new SolidColorBrush(Color.FromRgb(0xd8, 0xd8, 0xd8));
    private static readonly IBrush KeyBlack = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x34));
    private static readonly IBrush RulerBack = new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x30));
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.FromArgb(0xc0, 0xff, 0xff, 0xff));
    private static readonly IBrush KeyLabelBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
    private static readonly IBrush LoopBrush = new SolidColorBrush(Color.FromArgb(0x28, 0x60, 0xa0, 0xff));
    private static readonly IBrush PastEndBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x00, 0x00, 0x00));
    private static readonly IBrush BoxBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xff, 0xff, 0xff));
    private static readonly IPen RowPen = new Pen(new SolidColorBrush(Color.FromArgb(0x18, 0xff, 0xff, 0xff)), 1);
    private static readonly IPen BeatPen = new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0xff, 0xff, 0xff)), 1);
    private static readonly IPen OctavePen = new Pen(new SolidColorBrush(Color.FromArgb(0x30, 0xff, 0xff, 0xff)), 1);
    private static readonly IPen SelectedPen = new Pen(Brushes.White, 1.5);
    private static readonly IPen BendPen = new Pen(new SolidColorBrush(Color.FromArgb(0xe0, 0xff, 0xff, 0xff)), 1);
    private static readonly IPen CursorPen = new Pen(new SolidColorBrush(Color.FromRgb(0x60, 0xa0, 0xff)), 1.5);
    private static readonly IPen PlayheadPen = new Pen(Brushes.White, 1.5);
    private static readonly IPen EndPen = new Pen(new SolidColorBrush(Color.FromRgb(0xe0, 0x60, 0x50)), 1.5);
    private static readonly IPen BoxPen = new Pen(Brushes.White, 1);
    private static readonly Typeface Font = new(FontFamily.Default);

    // Each channel's note color: as it is, being dragged, and dimmed (not shown)
    private static readonly IBrush[,] NoteBrushes = MakeNoteBrushes();

    private static IBrush[,] MakeNoteBrushes()
    {
        var brushes = new IBrush[SongChannelItem.Colors.Length, 3];
        for (var channel = 0; channel < SongChannelItem.Colors.Length; channel++)
        {
            var color = SongChannelItem.Colors[channel];
            byte[] alphas = [0xff, 0x90, 0x50];
            for (var kind = 0; kind < 3; kind++)
                brushes[channel, kind] = new SolidColorBrush(Color.FromArgb(alphas[kind], color.R, color.G, color.B));
        }
        return brushes;
    }

    private static bool IsBlack(int note) => (((note % 12) + 12) % 12) is 1 or 3 or 6 or 8 or 10;

    // Where things are, in the control's own pixels
    private double XOf(double tick) => KeyWidth + tick * PixelsPerTick;
    private double TickOf(double x) => (x - KeyWidth) / PixelsPerTick;
    private static double YOf(double note) => RulerHeight + (TopNote - note) * RowHeight;
    private static int NoteOf(double y) => TopNote - (int)Math.Floor((y - RulerHeight) / RowHeight);

    private Rect NoteRect(ImfNote note, int length)
        => new(XOf(note.Start), YOf(note.NearestNote.Note) + 1, Math.Max(2, length * PixelsPerTick), RowHeight - 2);

    protected override Size MeasureOverride(Size availableSize)
    {
        var length = Editor?.Song.Length ?? 0;
        return new Size(KeyWidth + (length + 2 * 700) * PixelsPerTick, RulerHeight + (TopNote - BottomNote + 1) * RowHeight);
    }

    private ScrollViewer? _scroller;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _scroller = this.FindAncestorOfType<ScrollViewer>();
        if (_scroller != null)
            _scroller.ScrollChanged += (_, _) => InvalidateVisual();
    }

    private Rect Viewport => _scroller is { } scroller
        ? new Rect(new Point(scroller.Offset.X, scroller.Offset.Y), scroller.Viewport)
        : new Rect(Bounds.Size);

    /// <summary>Scrolls so a note is in view (vertically centred)</summary>
    public void ScrollToNote(int midiNote)
    {
        if (_scroller != null)
            _scroller.Offset = new Vector(_scroller.Offset.X, Math.Max(0, YOf(midiNote) - _scroller.Viewport.Height / 2));
    }

    /// <summary>Keeps the playhead in view while playing</summary>
    public void FollowPlayhead()
    {
        if (_scroller == null || PlayheadTick < 0)
            return;
        var x = XOf(PlayheadTick);
        var view = Viewport;
        if (x < view.Left + KeyWidth || x > view.Right - 40)
            _scroller.Offset = new Vector(Math.Max(0, x - KeyWidth - 40), _scroller.Offset.Y);
    }

    public override void Render(DrawingContext context)
    {
        var view = Viewport;
        context.FillRectangle(Background, view);
        if (Editor is not { } editor)
            return;
        var song = editor.Song;

        double firstTick = Math.Max(0, TickOf(view.Left + KeyWidth)), lastTick = TickOf(view.Right);
        int topNote = Math.Min(TopNote, NoteOf(view.Top + RulerHeight)), bottomNote = Math.Max(BottomNote, NoteOf(view.Bottom));

        // Rows: black keys darker, a line under each C
        for (var note = bottomNote; note <= topNote; note++)
        {
            var y = YOf(note);
            if (IsBlack(note))
                context.FillRectangle(BlackKeyRow, new Rect(view.Left, y, view.Width, RowHeight));
            context.DrawLine(note % 12 == 0 ? OctavePen : RowPen, new Point(view.Left, y + RowHeight), new Point(view.Right, y + RowHeight));
        }

        // The grid: a line a row, brighter a beat; zoomed out, only every so many so they're 6
        // pixels apart at least (beats, then bars of four)
        var row = Math.Max(1, editor.TicksPerRow);
        var rowsPerBeat = Math.Max(1, editor.RowsPerBeat);
        var every = 1;
        foreach (var step in new[] { 1, rowsPerBeat, rowsPerBeat * 4, rowsPerBeat * 16, rowsPerBeat * 64 })
        {
            every = step;
            if (row * step * PixelsPerTick >= 6)
                break;
        }
        for (var k = (long)(firstTick / row / every) * every; k * row <= lastTick; k += every)
        {
            var x = Math.Floor(XOf(k * row)) + 0.5;
            context.DrawLine(k % rowsPerBeat == 0 ? BeatPen : RowPen, new Point(x, view.Top), new Point(x, view.Bottom));
        }
        var beat = row * rowsPerBeat;

        // The loop region and past the song's end
        if (editor.LoopRegion is { } loop)
            context.FillRectangle(LoopBrush, new Rect(XOf(loop.From), view.Top, (loop.To - loop.From) * PixelsPerTick, view.Height));
        var endX = XOf(song.Length);
        if (endX < view.Right)
            context.FillRectangle(PastEndBrush, new Rect(endX, view.Top, view.Right - endX, view.Height));

        // The notes, the channels not shown first and dim
        var selected = editor.Selection.ToHashSet();
        foreach (var dim in new[] { true, false })
        {
            foreach (var note in song.Notes)
            {
                if (editor.IsShown(note.Channel) == dim)
                    continue;
                var end = note.End ?? song.Length;
                if (end < firstTick || note.Start > lastTick)
                    continue;
                var midi = note.NearestNote.Note;
                if (midi < bottomNote - 1 || midi > topNote + 1)
                    continue;
                DrawNote(context, editor, note, end - note.Start, dim, selected.Contains(note), 0, 0);
            }
        }

        // What a drag will do
        switch (_drag)
        {
            case DragKind.Draw when _drawNote is { } drawn:
                DrawNote(context, editor, drawn, drawn.Length ?? 1, false, true, 0, 0);
                break;
            case DragKind.Move:
                foreach (var note in editor.Selection)
                    DrawNote(context, editor, note, (note.End ?? song.Length) - note.Start, false, true, _moveTicks, _moveSemitones, ghost: true);
                break;
            case DragKind.Resize when _resizeNote is { } resizing:
                DrawNote(context, editor, resizing, _resizeLength, false, true, 0, 0, ghost: true);
                break;
            case DragKind.Box:
                var box = new Rect(_pressPoint, _dragPoint).Normalize();
                context.FillRectangle(BoxBrush, box);
                context.DrawRectangle(BoxPen, box);
                break;
        }

        // The song's end, the cursor, the playhead
        context.DrawLine(EndPen, new Point(endX, view.Top), new Point(endX, view.Bottom));
        var cursorX = XOf(editor.CursorTick);
        context.DrawLine(CursorPen, new Point(cursorX, view.Top), new Point(cursorX, view.Bottom));
        if (PlayheadTick >= 0)
        {
            var playX = XOf(PlayheadTick);
            context.DrawLine(PlayheadPen, new Point(playX, view.Top), new Point(playX, view.Bottom));
        }

        DrawRuler(context, view, firstTick, lastTick, beat);
        DrawKeys(context, view, topNote, bottomNote);
    }

    private void DrawNote(DrawingContext context, SongEditorViewModel editor, ImfNote note, int length, bool dim, bool selected,
        int ticks, int semitones, bool ghost = false)
    {
        var midi = note.NearestNote.Note + semitones;
        var rect = new Rect(XOf(note.Start + ticks), YOf(midi) + 1, Math.Max(2, length * PixelsPerTick), RowHeight - 2);
        context.FillRectangle(NoteBrushes[note.Channel, ghost ? 1 : dim ? 2 : 0], rect, 2);
        if (selected)
            context.DrawRectangle(SelectedPen, rect, 2);

        // Its pitch changes: a line from its own pitch to each one's, a semitone a row
        var bends = editor.Song.PitchChangesOf(note).ToList();
        if (bends.Count == 0 || dim)
            return;
        var exact = OplPitch.MidiNote(note.Frequency);
        var y = YOf(midi + exact - note.NearestNote.Note) + RowHeight / 2;
        var x = rect.Left;
        foreach (var bend in bends.Where(bend => bend.Tick < note.Start + length))
        {
            var bendX = XOf(bend.Tick + ticks);
            var bendY = YOf(midi + OplPitch.MidiNote(bend.Frequency) - note.NearestNote.Note) + RowHeight / 2;
            context.DrawLine(BendPen, new Point(x, y), new Point(bendX, y));
            context.DrawLine(BendPen, new Point(bendX, y), new Point(bendX, bendY));
            (x, y) = (bendX, bendY);
        }
        context.DrawLine(BendPen, new Point(x, y), new Point(rect.Right, y));
    }

    // Seconds along the top, kept in view as it scrolls down
    private void DrawRuler(DrawingContext context, Rect view, double firstTick, double lastTick, double beat)
    {
        var top = view.Top;
        context.FillRectangle(RulerBack, new Rect(view.Left, top, view.Width, RulerHeight));
        var step = new[] { 35, 70, 140, 350, 700, 1400, 3500, 7000, 14000, 42000 }.FirstOrDefault(s => s * PixelsPerTick >= 60, 84000);
        for (var tick = (int)(firstTick / step) * step; tick <= lastTick; tick += step)
        {
            var x = XOf(tick);
            context.DrawLine(BeatPen, new Point(x, top + RulerHeight - 6), new Point(x, top + RulerHeight));
            var seconds = tick / (double)ImfSong.TicksPerSecond;
            var label = step < ImfSong.TicksPerSecond ? $"{(int)seconds / 60}:{seconds % 60:00.0}" : $"{(int)seconds / 60}:{(int)seconds % 60:00}";
            context.DrawText(new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, 10, LabelBrush), new Point(x + 3, top + 2));
        }
        if (Editor is { } editor)
        {
            var cursorX = XOf(editor.CursorTick);
            context.DrawGeometry(CursorPen.Brush, null, new PolylineGeometry(
                [new Point(cursorX - 5, top + RulerHeight - 8), new Point(cursorX + 5, top + RulerHeight - 8), new Point(cursorX, top + RulerHeight)], true));
        }
    }

    // The keys down the left, kept in view as it scrolls across
    private void DrawKeys(DrawingContext context, Rect view, int topNote, int bottomNote)
    {
        var left = view.Left;
        context.FillRectangle(RulerBack, new Rect(left, view.Top, KeyWidth, RulerHeight));
        for (var note = bottomNote; note <= topNote; note++)
        {
            var y = YOf(note);
            if (y + RowHeight < view.Top + RulerHeight)
                continue;
            var black = IsBlack(note);
            context.FillRectangle(black ? KeyBlack : KeyWhite, new Rect(left, y, black ? KeyWidth * 0.62 : KeyWidth, RowHeight - (black ? 0 : 1)));
            if (note % 12 == 0)
                context.DrawText(new FormattedText(ImfSongEdits.NoteName(note), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, 9, KeyLabelBrush),
                    new Point(left + KeyWidth - 22, y - 1));
        }
        context.FillRectangle(RulerBack, new Rect(left, view.Top, KeyWidth, RulerHeight));
    }

    //
    // The mouse
    //

    private enum DragKind { None, Draw, Move, Resize, Box, Cursor }

    private DragKind _drag;
    private Point _pressPoint, _dragPoint;
    private ImfNote? _drawNote;
    private ImfNote? _resizeNote;
    private int _resizeLength;
    private int _moveTicks, _moveSemitones;

    private int Snap(double tick, bool floor = false) => ImfSongEdits.Snap(tick, Editor?.TicksPerRow ?? 1, floor);

    private int RowTicks => Math.Max(1, (int)Math.Round(Editor?.TicksPerRow ?? 1));

    /// <summary>The note under a point (the one drawn on top), if any</summary>
    private ImfNote? NoteAt(Point point)
    {
        if (Editor is not { } editor)
            return null;
        return editor.Song.Notes.LastOrDefault(note => editor.IsShown(note.Channel)
            && NoteRect(note, (note.End ?? editor.Song.Length) - note.Start).Contains(point));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (Editor is not { } editor)
            return;
        var point = e.GetPosition(this);
        var properties = e.GetCurrentPoint(this).Properties;
        _pressPoint = _dragPoint = point;

        // The ruler: where play starts
        if (point.Y < Viewport.Top + RulerHeight && point.X > Viewport.Left + KeyWidth)
        {
            if (!properties.IsLeftButtonPressed)
                return;
            _drag = DragKind.Cursor;
            editor.CursorTick = Snap(TickOf(point.X));
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }
        if (point.X < Viewport.Left + KeyWidth)
        {
            // A key: hear it
            if (properties.IsLeftButtonPressed)
                editor.AuditionNote(ImfSongEdits.NoteOf(editor.CurrentChannel.Channel, 0, 1, NoteOf(point.Y)));
            return;
        }

        var note = NoteAt(point);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (properties.IsRightButtonPressed)
        {
            // Picks what the menu's for
            if (note != null && !editor.Selection.Contains(note))
                editor.Selection = [note];
            return;
        }
        if (!properties.IsLeftButtonPressed)
            return;

        if (note != null)
        {
            if (ctrl)
            {
                editor.Selection = editor.Selection.Contains(note) ? editor.Selection.Where(other => other != note).ToList() : [.. editor.Selection, note];
                e.Handled = true;
                return;
            }
            if (!editor.Selection.Contains(note))
                editor.Selection = [note];
            var rect = NoteRect(note, (note.End ?? editor.Song.Length) - note.Start);
            if (point.X >= rect.Right - EdgeGrip && note.Length != null)
            {
                _drag = DragKind.Resize;
                _resizeNote = note;
                _resizeLength = note.Length.Value;
            }
            else
            {
                _drag = DragKind.Move;
                _moveTicks = _moveSemitones = 0;
                editor.AuditionNote(note);
            }
        }
        else if (editor.Tool == PianoTool.Select || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            _drag = DragKind.Box;
        else
        {
            _drag = DragKind.Draw;
            var start = Snap(TickOf(point.X), floor: true);
            _drawNote = ImfSongEdits.NoteOf(editor.CurrentChannel.Channel, start, Math.Max(1, Snap(start + editor.TicksPerRow) - start), NoteOf(point.Y));
            editor.AuditionNote(_drawNote);
        }
        e.Pointer.Capture(this);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (Editor is not { } editor)
            return;
        _dragPoint = point;
        switch (_drag)
        {
            case DragKind.None:
                Cursor = NoteAt(point) is { Length: not null } hover && point.X >= NoteRect(hover, hover.Length!.Value).Right - EdgeGrip
                    ? new Cursor(StandardCursorType.SizeWestEast) : Cursor.Default;
                return;
            case DragKind.Cursor:
                editor.CursorTick = Snap(TickOf(point.X));
                return;
            case DragKind.Draw when _drawNote is { } drawn:
                var end = Math.Max(drawn.Start + 1, Snap(TickOf(point.X)));
                _drawNote = drawn with { Length = Math.Max(1, end - drawn.Start) };
                break;
            case DragKind.Move:
                var ticks = Snap(Math.Abs(TickOf(point.X) - TickOf(_pressPoint.X))) * Math.Sign(point.X - _pressPoint.X);
                var semitones = (int)Math.Round((_pressPoint.Y - point.Y) / RowHeight);
                if (semitones != _moveSemitones && editor.Selection.FirstOrDefault() is { } first)
                    editor.AuditionNote(first with { FNumber = ImfSongEdits.Transpose(first.FNumber, first.Block, semitones).FNumber,
                        Block = ImfSongEdits.Transpose(first.FNumber, first.Block, semitones).Block });
                (_moveTicks, _moveSemitones) = (ticks, semitones);
                break;
            case DragKind.Resize when _resizeNote is { } resizing:
                _resizeLength = Math.Max(1, Snap(TickOf(point.X)) - resizing.Start);
                break;
        }
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);
        var drag = _drag;
        _drag = DragKind.None;
        if (Editor is not { } editor)
            return;

        switch (drag)
        {
            case DragKind.Draw when _drawNote is { } drawn:
                editor.AddNote(drawn.Start, drawn.NearestNote.Note, drawn.Length ?? 1);
                break;
            case DragKind.Move:
                editor.MoveSelection(_moveTicks, _moveSemitones);
                break;
            case DragKind.Resize when _resizeNote is { } resizing:
                editor.ResizeNote(resizing, _resizeLength);
                break;
            case DragKind.Box:
                var box = new Rect(_pressPoint, _dragPoint).Normalize();
                var inBox = editor.Song.Notes.Where(note => editor.IsShown(note.Channel)
                    && NoteRect(note, (note.End ?? editor.Song.Length) - note.Start).Intersects(box)).ToList();
                editor.Selection = e.KeyModifiers.HasFlag(KeyModifiers.Control) ? [.. editor.Selection.Union(inBox)] : inBox;
                break;
        }
        _drawNote = null;
        _resizeNote = null;
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        // Ctrl: zoom in and out round the mouse
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && Editor is { } editor && _scroller != null)
        {
            var point = e.GetPosition(this);
            var tick = TickOf(point.X);
            var onScreen = point.X - _scroller.Offset.X;
            editor.PixelsPerTick = Math.Clamp(editor.PixelsPerTick * (e.Delta.Y > 0 ? 1.25 : 0.8), 0.01, 8);
            InvalidateMeasure();
            UpdateLayout();
            _scroller.Offset = new Vector(Math.Max(0, KeyWidth + tick * editor.PixelsPerTick - onScreen), _scroller.Offset.Y);
            e.Handled = true;
            return;
        }
        base.OnPointerWheelChanged(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || Editor is not { } editor)
            return;
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        switch (e.Key)
        {
            case Key.Delete or Key.Back:
                editor.DeleteSelectionCommand.Execute(null);
                break;
            case Key.Up:
                editor.MoveSelection(0, ctrl ? 12 : 1);
                break;
            case Key.Down:
                editor.MoveSelection(0, ctrl ? -12 : -1);
                break;
            case Key.Left:
                editor.MoveSelection(-RowTicks, 0);
                break;
            case Key.Right:
                editor.MoveSelection(RowTicks, 0);
                break;
            case Key.A when ctrl:
                editor.SelectAllCommand.Execute(null);
                break;
            default:
                return;
        }
        e.Handled = true;
    }
}
