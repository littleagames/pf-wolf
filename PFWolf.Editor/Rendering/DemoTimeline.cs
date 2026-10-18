using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PFWolf.Assets.Demos;

namespace PFWolf.Editor.Rendering;

using Point = Avalonia.Point;

/// <summary>
/// A demo's controls along time: turning and walking as bars above and below a middle line, a lane
/// for each button held, the traced events as ticks, and a line at the frame shown. Clicking or
/// dragging picks the frame.
/// </summary>
public sealed class DemoTimeline : Control
{
    public static readonly StyledProperty<DemoFile?> DemoProperty = AvaloniaProperty.Register<DemoTimeline, DemoFile?>(nameof(Demo));
    public static readonly StyledProperty<int> FrameProperty = AvaloniaProperty.Register<DemoTimeline, int>(nameof(Frame));
    public static readonly StyledProperty<IReadOnlyList<TraceEvent>?> EventsProperty =
        AvaloniaProperty.Register<DemoTimeline, IReadOnlyList<TraceEvent>?>(nameof(Events));

    static DemoTimeline()
    {
        AffectsRender<DemoTimeline>(DemoProperty, FrameProperty, EventsProperty);
        FocusableProperty.OverrideDefaultValue<DemoTimeline>(true);
    }

    public DemoFile? Demo { get => GetValue(DemoProperty); set => SetValue(DemoProperty, value); }
    public int Frame { get => GetValue(FrameProperty); set => SetValue(FrameProperty, value); }
    public IReadOnlyList<TraceEvent>? Events { get => GetValue(EventsProperty); set => SetValue(EventsProperty, value); }

    /// <summary>A frame was clicked or dragged to</summary>
    public event EventHandler<int>? Scrub;

    private const double LabelWidth = 56, StripHeight = 34, LaneHeight = 12, EventHeight = 14, RulerHeight = 16;

    private static readonly IBrush Background = new SolidColorBrush(Color.FromRgb(0x1c, 0x1c, 0x1c));
    private static readonly IBrush LaneBack = new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x28));
    private static readonly IBrush TurnBrush = new SolidColorBrush(Color.FromRgb(0x5c, 0x9c, 0xe6));
    private static readonly IBrush MoveBrush = new SolidColorBrush(Color.FromRgb(0x4c, 0xb0, 0x6c));
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.FromArgb(0xb0, 0xff, 0xff, 0xff));
    private static readonly IPen MiddlePen = new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0xff, 0xff, 0xff)), 1);
    private static readonly IPen TickPen = new Pen(new SolidColorBrush(Color.FromArgb(0x50, 0xff, 0xff, 0xff)), 1);
    private static readonly IPen PlayheadPen = new Pen(Brushes.White, 1.5);
    private static readonly Typeface Font = new(FontFamily.Default);

    /// <summary>A lane's color for each button: fire red, use yellow, run and strafe blue, slots grey</summary>
    private static readonly IBrush[] ButtonBrushes =
    [
        new SolidColorBrush(Color.FromRgb(0xe0, 0x50, 0x40)),
        new SolidColorBrush(Color.FromRgb(0x60, 0xa0, 0xe0)),
        new SolidColorBrush(Color.FromRgb(0x40, 0xc0, 0xd0)),
        new SolidColorBrush(Color.FromRgb(0xe8, 0xc8, 0x40)),
        new SolidColorBrush(Color.FromRgb(0xa0, 0xa0, 0xa8)),
        new SolidColorBrush(Color.FromRgb(0xa0, 0xa0, 0xa8)),
        new SolidColorBrush(Color.FromRgb(0xa0, 0xa0, 0xa8)),
        new SolidColorBrush(Color.FromRgb(0xa0, 0xa0, 0xa8)),
    ];

    /// <summary>A traced event's color, as the map's markers draw them</summary>
    public static IBrush EventBrush(TraceEventKind kind) => kind switch
    {
        TraceEventKind.Kill => DemoOverlay.KillBrush,
        TraceEventKind.Pickup => DemoOverlay.PickupBrush,
        TraceEventKind.Hurt => DemoOverlay.HurtBrush,
        TraceEventKind.Door => DemoOverlay.DoorBrush,
        TraceEventKind.PushWall => DemoOverlay.PushWallBrush,
        _ => DemoOverlay.DeathBrush,
    };

    protected override Size MeasureOverride(Size availableSize)
        => new(0, RulerHeight + StripHeight * 2 + LaneHeight * DemoFile.ButtonNames.Length + EventHeight + 6);

    private double TrackWidth => Math.Max(1, Bounds.Width - LabelWidth);

    private double XOf(int frame, int frames) => LabelWidth + frame / (double)Math.Max(1, frames) * TrackWidth;

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Background, new Rect(Bounds.Size));
        if (Demo is not { } demo || demo.Frames.Count == 0)
            return;

        var frames = demo.Frames;
        int count = frames.Count;
        int columns = (int)TrackWidth;
        double y = 0;

        DrawRuler(context, demo, y);
        y += RulerHeight;

        // Turning and walking: the biggest each column holds, either way from the middle
        DrawStrip(context, "Turn", y, columns, count, frame => frame.Turn, TurnBrush);
        y += StripHeight;
        DrawStrip(context, "Move", y, columns, count, frame => (sbyte)Math.Clamp(-frame.Move, sbyte.MinValue, sbyte.MaxValue), MoveBrush);
        y += StripHeight;

        // A lane a button, filled where it's held
        for (int button = 0; button < DemoFile.ButtonNames.Length; button++)
        {
            context.FillRectangle(LaneBack, new Rect(LabelWidth, y + 1, TrackWidth, LaneHeight - 2));
            Label(context, DemoFile.ButtonNames[button], y, LaneHeight);
            int bit = 1 << button;
            int? runStart = null;
            for (int column = 0; column <= columns; column++)
            {
                bool held = false;
                if (column < columns)
                {
                    int from = (int)((long)count * column / columns), to = Math.Max(from + 1, (int)((long)count * (column + 1) / columns));
                    for (int i = from; i < to && i < count && !held; i++)
                        held = (frames[i].Buttons & bit) != 0;
                }
                if (held && runStart == null)
                    runStart = column;
                else if (!held && runStart is { } begin)
                {
                    context.FillRectangle(ButtonBrushes[button], new Rect(LabelWidth + begin, y + 2, column - begin, LaneHeight - 4));
                    runStart = null;
                }
            }
            y += LaneHeight;
        }

        // The trace's events as ticks
        Label(context, "Events", y, EventHeight);
        if (Events is { } events)
        {
            foreach (var e in events)
            {
                var x = XOf(e.Frame, count);
                context.FillRectangle(EventBrush(e.Kind), new Rect(x - 1, y + 2, 3, EventHeight - 4));
            }
        }

        var playhead = XOf(Math.Clamp(Frame, 0, count - 1), count);
        context.DrawLine(PlayheadPen, new Point(playhead, 0), new Point(playhead, Bounds.Height));
    }

    // Seconds along the top
    private void DrawRuler(DrawingContext context, DemoFile demo, double y)
    {
        double seconds = demo.Duration.TotalSeconds;
        if (seconds <= 0)
            return;
        double step = new[] { 1.0, 2, 5, 10, 15, 30, 60 }.FirstOrDefault(s => TrackWidth / (seconds / s) >= 50, 120);
        for (double s = 0; s <= seconds; s += step)
        {
            double x = LabelWidth + s / seconds * TrackWidth;
            context.DrawLine(TickPen, new Point(x, y + RulerHeight - 4), new Point(x, y + RulerHeight));
            var text = new FormattedText($"{(int)s / 60}:{(int)s % 60:00}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, 10, LabelBrush);
            context.DrawText(text, new Point(x + 2, y));
        }
    }

    private void DrawStrip(DrawingContext context, string label, double y, int columns, int count, Func<DemoFrame, sbyte> value, IBrush brush)
    {
        var frames = Demo!.Frames;
        context.FillRectangle(LaneBack, new Rect(LabelWidth, y + 1, TrackWidth, StripHeight - 2));
        Label(context, label, y, StripHeight);
        double middle = Math.Floor(y + StripHeight / 2) + 0.5, half = StripHeight / 2 - 2;
        context.DrawLine(MiddlePen, new Point(LabelWidth, middle), new Point(Bounds.Width, middle));
        for (int column = 0; column < columns; column++)
        {
            int from = (int)((long)count * column / columns), to = Math.Max(from + 1, (int)((long)count * (column + 1) / columns));
            int low = 0, high = 0;
            for (int i = from; i < to && i < count; i++)
            {
                int v = value(frames[i]);
                low = Math.Min(low, v);
                high = Math.Max(high, v);
            }
            if (low == 0 && high == 0)
                continue;
            double top = middle - high / 128.0 * half, bottom = middle - low / 128.0 * half;
            context.FillRectangle(brush, new Rect(LabelWidth + column, top, 1, Math.Max(1, bottom - top)));
        }
    }

    private static void Label(DrawingContext context, string text, double y, double height)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, 10, LabelBrush);
        context.DrawText(formatted, new Point(4, y + (height - formatted.Height) / 2));
    }

    private bool _dragging;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (Demo is { Frames.Count: > 0 })
        {
            _dragging = true;
            e.Pointer.Capture(this);
            ScrubTo(e.GetPosition(this).X);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
            ScrubTo(e.GetPosition(this).X);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    private void ScrubTo(double x)
    {
        if (Demo is not { Frames.Count: > 0 } demo)
            return;
        var frame = (int)Math.Round((x - LabelWidth) / TrackWidth * demo.Frames.Count);
        Scrub?.Invoke(this, Math.Clamp(frame, 0, demo.Frames.Count - 1));
    }
}
