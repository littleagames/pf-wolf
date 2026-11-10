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
/// An AdLib sound's tones along time, a column a tick (140 a second), each at its pitch (a
/// row a semitone, C's marked); silent ticks empty. Drawn on with the left button (the tone
/// under the mouse, snapped to semitones when asked), erased with the right; Shift and a drag
/// picks a run of ticks for the tools. Ticks past the end are drawn into to make it longer.
/// </summary>
public sealed class ToneStripView : Control
{
    public static readonly StyledProperty<AdLibEditorViewModel?> EditorProperty =
        AvaloniaProperty.Register<ToneStripView, AdLibEditorViewModel?>(nameof(Editor));
    public static readonly StyledProperty<int> RevisionProperty = AvaloniaProperty.Register<ToneStripView, int>(nameof(Revision));
    public static readonly StyledProperty<int> PlayheadTickProperty = AvaloniaProperty.Register<ToneStripView, int>(nameof(PlayheadTick), -1);
    public static readonly StyledProperty<double> PixelsPerTickProperty = AvaloniaProperty.Register<ToneStripView, double>(nameof(PixelsPerTick), 8);

    static ToneStripView()
    {
        AffectsRender<ToneStripView>(EditorProperty, RevisionProperty, PlayheadTickProperty, PixelsPerTickProperty);
        AffectsMeasure<ToneStripView>(EditorProperty, RevisionProperty, PixelsPerTickProperty);
        FocusableProperty.OverrideDefaultValue<ToneStripView>(true);
    }

    public AdLibEditorViewModel? Editor { get => GetValue(EditorProperty); set => SetValue(EditorProperty, value); }
    public int Revision { get => GetValue(RevisionProperty); set => SetValue(RevisionProperty, value); }
    public int PlayheadTick { get => GetValue(PlayheadTickProperty); set => SetValue(PlayheadTickProperty, value); }
    public double PixelsPerTick { get => GetValue(PixelsPerTickProperty); set => SetValue(PixelsPerTickProperty, value); }

    /// <summary>What's under the mouse: tick, tone, frequency, note</summary>
    public event EventHandler<string>? Hover;

    private const double LabelWidth = 44, RulerHeight = 18, RowHeight = 7;
    // The tones shown: from 8 (lower ones are coarse steps) to 255
    private const int LowestTone = 8;

    private static readonly IBrush Background = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x22));
    private static readonly IBrush PastEnd = new SolidColorBrush(Color.FromArgb(0x60, 0, 0, 0));
    private static readonly IBrush ToneBrush = new SolidColorBrush(Color.FromRgb(0xe8, 0xb4, 0x4a));
    private static readonly IBrush StemBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xe8, 0xb4, 0x4a));
    private static readonly IBrush RangeBrush = new SolidColorBrush(Color.FromArgb(0x30, 0x60, 0xa0, 0xff));
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.FromArgb(0xc0, 0xff, 0xff, 0xff));
    private static readonly IBrush RulerBack = new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x30));
    private static readonly IPen RowPen = new Pen(new SolidColorBrush(Color.FromArgb(0x14, 0xff, 0xff, 0xff)), 1);
    private static readonly IPen OctavePen = new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0xff, 0xff, 0xff)), 1);
    private static readonly IPen SecondPen = new Pen(new SolidColorBrush(Color.FromArgb(0x30, 0xff, 0xff, 0xff)), 1);
    private static readonly IPen EndPen = new Pen(new SolidColorBrush(Color.FromRgb(0xe0, 0x60, 0x50)), 1.5);
    private static readonly IPen PlayheadPen = new Pen(Brushes.White, 1.5);
    private static readonly Typeface Font = new(FontFamily.Default);

    private AdLibDraft? Draft => Editor?.Draft;

    // The pitch range shown (MIDI notes, fractional) for the block
    private (double Low, double High) Notes(AdLibDraft draft)
        => (OplPitch.MidiNote(draft.Frequency(LowestTone)), OplPitch.MidiNote(draft.Frequency(255)));

    private double YOfNote(AdLibDraft draft, double note)
    {
        var (_, high) = Notes(draft);
        return RulerHeight + (high - note) * RowHeight;
    }

    private double YOfTone(AdLibDraft draft, int tone) => YOfNote(draft, OplPitch.MidiNote(draft.Frequency(Math.Max(tone, LowestTone))));

    private int ToneOfY(AdLibDraft draft, double y)
    {
        var (_, high) = Notes(draft);
        var note = high - (y - RulerHeight) / RowHeight;
        var frequency = 440 * Math.Pow(2, (note - 69) / 12);
        return (int)Math.Clamp(Math.Round(frequency * (1 << (20 - draft.Block)) / OplPitch.ChipRate), 1, 255);
    }

    private double XOf(double tick) => LabelWidth + tick * PixelsPerTick;
    private int TickOf(double x) => (int)Math.Floor((x - LabelWidth) / PixelsPerTick);

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Draft is not { } draft)
            return new Size(0, 0);
        var (low, high) = Notes(draft);
        return new Size(XOf(draft.Tones.Length + AdLibDraft.TicksPerSecond), RulerHeight + (high - low + 1) * RowHeight);
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
            context.FillRectangle(StemBrush, new Rect(x, y, Math.Max(1, PixelsPerTick - 1), bottom - y));
            context.FillRectangle(ToneBrush, new Rect(x, y - RowHeight / 2, Math.Max(1, PixelsPerTick - 1), RowHeight));
        }

        var endX = XOf(draft.Tones.Length);
        context.FillRectangle(PastEnd, new Rect(endX, RulerHeight, Math.Max(0, Bounds.Width - endX), Bounds.Height));
        context.DrawLine(EndPen, new Point(endX, 0), new Point(endX, Bounds.Height));
        if (PlayheadTick >= 0)
            context.DrawLine(PlayheadPen, new Point(XOf(PlayheadTick), 0), new Point(XOf(PlayheadTick), Bounds.Height));
    }

    private enum DragKind { None, Draw, Erase, Range }

    private DragKind _drag;
    private int _lastTick, _lastTone, _rangeStart;

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
            _lastTone = _drag == DragKind.Draw ? ToneOfY(draft, point.Y) : 0;
            var tone = _lastTone;
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
        Hover?.Invoke(this, $"Tick {tick} ({tick / 140.0:0.00} s)   "
            + (existing > 0 ? $"tone {existing}: {draft.Frequency(existing):0.#} Hz, {ImfSongEdits.NoteName((int)Math.Round(OplPitch.MidiNote(draft.Frequency(existing))))}" : "silent")
            + $"   ·   the mouse is at tone {toneHere} ({ImfSongEdits.NoteName((int)Math.Round(OplPitch.MidiNote(draft.Frequency(toneHere))))})");

        switch (_drag)
        {
            case DragKind.Draw or DragKind.Erase:
                var tone = _drag == DragKind.Draw ? toneHere : 0;
                // Every tick between the last point and this one, the tone going from one to the other
                var (fromTick, fromTone) = (_lastTick, _lastTone);
                editor.SetTones(fromTick, tick, at => tick == fromTick ? tone
                    : (int)Math.Round(fromTone + (tone - fromTone) * (at - fromTick) / (double)(tick - fromTick)));
                (_lastTick, _lastTone) = (tick, tone);
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
