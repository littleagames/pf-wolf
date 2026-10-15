using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PFWolf.Editor.Views;

/// <summary>The editor icon's blueprint tile, as a background: a blue gradient with a faint grid</summary>
public sealed class BlueprintBackground : Control
{
    public static readonly StyledProperty<double> CellSizeProperty =
        AvaloniaProperty.Register<BlueprintBackground, double>(nameof(CellSize), 32);

    public double CellSize
    {
        get => GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    // icons/pfwolf-editor.svg's colors
    private static readonly IBrush Gradient = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Color.Parse("#2463b0"), 0), new GradientStop(Color.Parse("#0c2a55"), 1) },
    };

    private static readonly IPen GridPen = new Pen(new SolidColorBrush(Colors.White, 0.13), 1);

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Gradient, bounds);

        var cell = Math.Max(4, CellSize);
        for (var x = cell; x < bounds.Width; x += cell)
            context.DrawLine(GridPen, new Avalonia.Point(Math.Floor(x) + 0.5, 0), new Avalonia.Point(Math.Floor(x) + 0.5, bounds.Height));
        for (var y = cell; y < bounds.Height; y += cell)
            context.DrawLine(GridPen, new Avalonia.Point(0, Math.Floor(y) + 0.5), new Avalonia.Point(bounds.Width, Math.Floor(y) + 0.5));
    }
}
