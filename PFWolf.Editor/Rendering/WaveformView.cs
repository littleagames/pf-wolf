using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace PFWolf.Editor.Rendering;

using Point = Avalonia.Point;

/// <summary>
/// A sound's samples drawn as a waveform (the loudest and quietest sample under each column),
/// with a line where playback is. Clicking asks to play from there.
/// </summary>
public sealed class WaveformView : Control
{
    public static readonly StyledProperty<short[]?> SamplesProperty =
        AvaloniaProperty.Register<WaveformView, short[]?>(nameof(Samples));

    /// <summary>Where playback is, 0 (the start) to 1 (the end); below 0 hides the line</summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<WaveformView, double>(nameof(Progress), -1);

    public static readonly StyledProperty<IBrush?> WaveBrushProperty =
        AvaloniaProperty.Register<WaveformView, IBrush?>(nameof(WaveBrush), new SolidColorBrush(Color.FromRgb(0x4c, 0xb0, 0x6c)));

    static WaveformView()
    {
        AffectsRender<WaveformView>(SamplesProperty, ProgressProperty, WaveBrushProperty);
    }

    public short[]? Samples
    {
        get => GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public IBrush? WaveBrush
    {
        get => GetValue(WaveBrushProperty);
        set => SetValue(WaveBrushProperty, value);
    }

    /// <summary>The waveform was clicked: play from this far in (0 to 1)</summary>
    public event EventHandler<double>? Seek;

    private static readonly IBrush Background = new SolidColorBrush(Color.FromRgb(0x1c, 0x1c, 0x1c));
    private static readonly IPen MiddlePen = new Pen(new SolidColorBrush(Color.FromArgb(0x50, 0xff, 0xff, 0xff)), 1);
    private static readonly IPen PlayheadPen = new Pen(Brushes.White, 1.5);

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Background, bounds);
        var middle = Math.Floor(bounds.Height / 2) + 0.5;
        context.DrawLine(MiddlePen, new Point(0, middle), new Point(bounds.Width, middle));

        if (Samples is not { Length: > 0 } samples || bounds.Width < 1)
            return;

        var columns = (int)bounds.Width;
        var half = bounds.Height / 2 - 1;
        var brush = WaveBrush ?? Brushes.Green;
        for (var column = 0; column < columns; column++)
        {
            var from = (int)((long)samples.Length * column / columns);
            var to = Math.Max(from + 1, (int)((long)samples.Length * (column + 1) / columns));
            short low = short.MaxValue, high = short.MinValue;
            for (var index = from; index < to && index < samples.Length; index++)
            {
                low = Math.Min(low, samples[index]);
                high = Math.Max(high, samples[index]);
            }
            var top = middle - high / 32768.0 * half;
            var bottom = middle - low / 32768.0 * half;
            context.FillRectangle(brush, new Rect(column, top, 1, Math.Max(1, bottom - top)));
        }

        if (Progress >= 0)
        {
            var x = Math.Clamp(Progress, 0, 1) * bounds.Width;
            context.DrawLine(PlayheadPen, new Point(x, 0), new Point(x, bounds.Height));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Bounds.Width > 0 && Samples is { Length: > 0 })
        {
            Seek?.Invoke(this, Math.Clamp(e.GetPosition(this).X / Bounds.Width, 0, 1));
            e.Handled = true;
        }
    }
}
