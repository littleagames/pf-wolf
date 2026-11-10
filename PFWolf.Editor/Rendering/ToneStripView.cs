using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PFWolf.Assets.Sounds;
using PFWolf.Editor.Audio.Opl;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Rendering;

using Point = Avalonia.Point;

/// <summary>
/// An AdLib or PC speaker sound's tones along time, a column a tick (140 a second), each at its
/// pitch (a row a semitone, C's marked); silent ticks empty. Drawn on with the left button (the tone
/// under the mouse, snapped to semitones when asked), erased with the right; Shift and a drag
/// picks a run of ticks for the tools. Ticks past the end are drawn into to make it longer.
/// </summary>
public sealed class ToneStripView : Control
{
    public static readonly StyledProperty<ToneSoundEditorViewModel?> EditorProperty =
        AvaloniaProperty.Register<ToneStripView, ToneSoundEditorViewModel?>(nameof(Editor));
    public static readonly StyledProperty<int> RevisionProperty = AvaloniaProperty.Register<ToneStripView, int>(nameof(Revision));
    public static readonly StyledProperty<int> PlayheadTickProperty = AvaloniaProperty.Register<ToneStripView, int>(nameof(PlayheadTick), -1);
    public static readonly StyledProperty<double> PixelsPerTickProperty = AvaloniaProperty.Register<ToneStripView, double>(nameof(PixelsPerTick), 8);

    static ToneStripView()
    {
        AffectsRender<ToneStripView>(EditorProperty, RevisionProperty, PlayheadTickProperty, PixelsPerTickProperty);
        AffectsMeasure<ToneStripView>(EditorProperty, RevisionProperty, PixelsPerTickProperty);
        FocusableProperty.OverrideDefaultValue<ToneStripView>(true);
    }

    public ToneSoundEditorViewModel? Editor { get => GetValue(EditorProperty); set => SetValue(EditorProperty, value); }
    public int Revision { get => GetValue(RevisionProperty); set => SetValue(RevisionProperty, value); }
    public int PlayheadTick { get => GetValue(PlayheadTickProperty); set => SetValue(PlayheadTickProperty, value); }
    public double PixelsPerTick { get => GetValue(PixelsPerTickProperty); set => SetValue(PixelsPerTickProperty, value); }

    /// <summary>What's under the mouse: tick, tone, frequency, note</summary>
    public event EventHandler<string>? Hover;

    private const double LabelWidth = 44, RulerHeight = 18, RowHeight = 7;

    private static readonly IBrush Background = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x22));
    private static readonly IBrush PastEnd = new SolidColorBrush(Color.FromArgb(0x60, 0, 0, 0));
    private static readonly IBrush ToneBrush = new SolidColorBrush(Color.FromRgb(0xe8, 0xb4, 0x4a));
    private static readonly IBrush StemBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xe8, 0xb4, 0x4a));
    private static readonly IBrush PcToneBrush = new SolidColorBrush(Color.FromRgb(0x6c, 0xd0, 0x8a));
    private static readonly IBrush PcStemBrush = new SolidColorBrush(Color.FromArgb(0x30, 0x6c, 0xd0, 0x8a));
    private static readonly IBrush RangeBrush = new SolidColorBrush(Color.FromArgb(0x30, 0x60, 0xa0, 0xff));
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.FromArgb(0xc0, 0xff, 0xff, 0xff));
    private static readonly IBrush RulerBack = new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x30));
    private static readonly IPen RowPen = new Pen(new SolidColorBrush(Color.FromArgb(0x14, 0xff, 0xff, 0xff)), 1);
    private static readonly IPen OctavePen = new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0xff, 0xff, 0xff)), 1);
    private static readonly IPen SecondPen = new Pen(new SolidColorBrush(Color.FromArgb(0x30, 0xff, 0xff, 0xff)), 1);
    private static readonly IPen EndPen = new Pen(new SolidColorBrush(Color.FromRgb(0xe0, 0x60, 0x50)), 1.5);
    private static readonly IPen PlayheadPen = new Pen(Brushes.White, 1.5);
    private static readonly Typeface Font = new(FontFamily.Default);

    private ToneDraft? Draft => Editor?.Current;

    // The pitch range shown (MIDI notes, fractional)
    private static (double Low, double High) Notes(ToneDraft draft)
        => (OplPitch.MidiNote(draft.Shown.Low), OplPitch.MidiNote(draft.Shown.High));

    private static double YOfNote(ToneDraft draft, double note)
    {
        var (low, high) = Notes(draft);
        return RulerHeight + (high - Math.Clamp(note, low, high)) * RowHeight;
    }

    private static double NoteOf(ToneDraft draft, int tone) => OplPitch.MidiNote(draft.Frequency(tone));

    private static double YOfTone(ToneDraft draft, int tone) => YOfNote(draft, NoteOf(draft, tone));

    private static double NoteOfY(ToneDraft draft, double y)
    {
        var (low, high) = Notes(draft);
        return Math.Clamp(high - (y - RulerHeight) / RowHeight, low, high);
    }

    private static int ToneOfNote(ToneDraft draft, double note) => draft.ToneOfFrequency(440 * Math.Pow(2, (note - 69) / 12));

    private static int ToneOfY(ToneDraft draft, double y) => ToneOfNote(draft, NoteOfY(draft, y));

    private double XOf(double tick) => LabelWidth + tick * PixelsPerTick;
    private int TickOf(double x) => (int)Math.Floor((x - LabelWidth) / PixelsPerTick);

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Draft is not { } draft)
            return new Size(0, 0);
        var (low, high) = Notes(draft);
        return new Size(XOf(draft.Tones.Length + ToneDraft.TicksPerSecond), RulerHeight + (high - low + 1) * RowHeight);
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Background, new Rect(Bounds.Size));
        if (Draft is not { } draft || Editor is not { } editor)
            return;
        var (low, high) = Notes(draft);

        // A row a semitone, brighter at each C, labelled
        for (var note = (int)Math.Ceiling(low); note <= high; note++)
        {
            var y = YOfNote(draft, note);
            context.DrawLine(note % 12 == 0 ? OctavePen : RowPen, new Point(LabelWidth, y), new Point(Bounds.Width, y));
            if (note % 12 == 0)
                context.DrawText(new FormattedText(ImfSongEdits.NoteName(note), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, 10, LabelBrush),
                    new Point(4, y - 7));
        }

        // Tenths of a second, and the ruler
        context.FillRectangle(RulerBack, new Rect(LabelWidth, 0, Bounds.Width, RulerHeight));
        for (var tick = 0; XOf(tick) < Bounds.Width; tick += 14)
        {
            var x = Math.Floor(XOf(tick)) + 0.5;
            context.DrawLine(SecondPen, new Point(x, tick % 140 == 0 ? 0 : RulerHeight - 5), new Point(x, Bounds.Height));
            if (tick % 140 == 0 || PixelsPerTick * 14 >= 40)
                context.DrawText(new FormattedText($"{tick / 140.0:0.0}s", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, 10, LabelBrush), new Point(x + 2, 2));
        }

        if (editor.Range is { } range)
            context.FillRectangle(RangeBrush, new Rect(XOf(range.From), RulerHeight, (range.To - range.From) * PixelsPerTick, Bounds.Height));

        // The tones: a block at each one's pitch, a faint stem under it
        var bottom = Bounds.Height;
        for (var tick = 0; tick < draft.Tones.Length; tick++)
        {
            var tone = draft.Tones[tick];
            if (tone == 0)
                continue;
            var y = YOfTone(draft, tone);
            var x = XOf(tick);
            context.FillRectangle(draft is PcDraft ? PcStemBrush : StemBrush, new Rect(x, y, Math.Max(1, PixelsPerTick - 1), bottom - y));
            context.FillRectangle(draft is PcDraft ? PcToneBrush : ToneBrush, new Rect(x, y - RowHeight / 2, Math.Max(1, PixelsPerTick - 1), RowHeight));
        }

        var endX = XOf(draft.Tones.Length);
        context.FillRectangle(PastEnd, new Rect(endX, RulerHeight, Math.Max(0, Bounds.Width - endX), Bounds.Height));
        context.DrawLine(EndPen, new Point(endX, 0), new Point(endX, Bounds.Height));
        if (PlayheadTick >= 0)
            context.DrawLine(PlayheadPen, new Point(XOf(PlayheadTick), 0), new Point(XOf(PlayheadTick), Bounds.Height));
    }

    private enum DragKind { None, Draw, Erase, Range }

    private DragKind _drag;
    private int _lastTick, _rangeStart;
    private double _lastNote;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (Editor is not { } editor || Draft is not { } draft)
            return;
        var point = e.GetPosition(this);
        if (point.X < LabelWidth)
            return;
        var properties = e.GetCurrentPoint(this).Properties;
        var tick = Math.Max(0, TickOf(point.X));
        if (properties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Shift) || point.Y < RulerHeight)
        {
            _drag = DragKind.Range;
            _rangeStart = tick;
            editor.Range = (tick, tick + 1);
        }
        else if (properties.IsLeftButtonPressed || properties.IsRightButtonPressed)
        {
            _drag = properties.IsLeftButtonPressed ? DragKind.Draw : DragKind.Erase;
            editor.BeginStroke();
            _lastTick = tick;
            _lastNote = NoteOfY(draft, point.Y);
            var tone = _drag == DragKind.Draw ? ToneOfY(draft, point.Y) : 0;
            editor.SetTones(tick, tick, _ => tone);
        }
        else
            return;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Editor is not { } editor || Draft is not { } draft)
            return;
        var point = e.GetPosition(this);
        var tick = Math.Max(0, TickOf(point.X));
        var toneHere = ToneOfY(draft, point.Y);
        var existing = tick < draft.Tones.Length ? draft.Tones[tick] : 0;
        var word = draft.ToneWord;
        Hover?.Invoke(this, $"Tick {tick} ({tick / 140.0:0.00} s)   "
            + (existing > 0 ? $"{word} {existing}: {draft.Frequency(existing):0.#} Hz, {ImfSongEdits.NoteName((int)Math.Round(NoteOf(draft, existing)))}" : "silent")
            + $"   ·   the mouse is at {word} {toneHere} ({ImfSongEdits.NoteName((int)Math.Round(NoteOf(draft, toneHere)))})");

        switch (_drag)
        {
            case DragKind.Erase:
                editor.SetTones(_lastTick, tick, _ => 0);
                _lastTick = tick;
                break;
            case DragKind.Draw:
                // Every tick between the last point and this one, the pitch going from one to the other
                var (fromTick, fromNote) = (_lastTick, _lastNote);
                var noteHere = NoteOfY(draft, point.Y);
                editor.SetTones(fromTick, tick, at => tick == fromTick ? toneHere
                    : ToneOfNote(draft, fromNote + (noteHere - fromNote) * (at - fromTick) / (tick - fromTick)));
                (_lastTick, _lastNote) = (tick, noteHere);
                break;
            case DragKind.Range:
                editor.Range = (Math.Min(_rangeStart, tick), Math.Max(_rangeStart, tick) + 1);
                break;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);
        if (Editor is { } editor)
        {
            if (_drag == DragKind.Draw)
                editor.EndStroke("Drew tones");
            else if (_drag == DragKind.Erase)
                editor.EndStroke("Erased tones");
        }
        _drag = DragKind.None;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            PixelsPerTick = Math.Clamp(PixelsPerTick * (e.Delta.Y > 0 ? 1.25 : 0.8), 1, 40);
            e.Handled = true;
            return;
        }
        base.OnPointerWheelChanged(e);
    }
}
