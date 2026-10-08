using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PFWolf.Assets;

namespace PFWolf.Editor.Rendering;

using Point = Avalonia.Point;

/// <summary>
/// A palette's 256 colors as a 16 by 16 grid of swatches, index 0 top left. A click picks a
/// color; shift-click or a drag picks a run of them (Anchor to Current). Arrow keys move, with
/// Shift to extend. Colors in Marked get a dot (the ones the preview picture uses).
/// </summary>
public sealed class PaletteGrid : Control
{
    public const int Columns = 16;

    public static readonly StyledProperty<PaletteColor[]?> ColorsProperty =
        AvaloniaProperty.Register<PaletteGrid, PaletteColor[]?>(nameof(Colors));

    /// <summary>Where the run picked starts (where it was clicked)</summary>
    public static readonly StyledProperty<int> AnchorProperty =
        AvaloniaProperty.Register<PaletteGrid, int>(nameof(Anchor));

    /// <summary>The color being edited: the run's other end</summary>
    public static readonly StyledProperty<int> CurrentProperty =
        AvaloniaProperty.Register<PaletteGrid, int>(nameof(Current));

    public static readonly StyledProperty<bool[]?> MarkedProperty =
        AvaloniaProperty.Register<PaletteGrid, bool[]?>(nameof(Marked));

    static PaletteGrid()
    {
        AffectsRender<PaletteGrid>(ColorsProperty, AnchorProperty, CurrentProperty, MarkedProperty);
        FocusableProperty.OverrideDefaultValue<PaletteGrid>(true);
    }

    public PaletteColor[]? Colors
    {
        get => GetValue(ColorsProperty);
        set => SetValue(ColorsProperty, value);
    }

    public int Anchor
    {
        get => GetValue(AnchorProperty);
        set => SetValue(AnchorProperty, value);
    }

    public int Current
    {
        get => GetValue(CurrentProperty);
        set => SetValue(CurrentProperty, value);
    }

    public bool[]? Marked
    {
        get => GetValue(MarkedProperty);
        set => SetValue(MarkedProperty, value);
    }

    /// <summary>A color was picked: Extend keeps the anchor (shift-click, a drag), else it's picked on its own</summary>
    public event EventHandler<(int Index, bool Extend)>? Picked;

    /// <summary>The color under the pointer, or -1 when it's off the grid</summary>
    public event EventHandler<int>? Hovered;

    private bool _dragging;
    private int _hovered = -1;

    private static readonly IPen DarkPen = new Pen(Brushes.Black, 1);
    private static readonly IPen RunPen = new Pen(Brushes.White, 1);
    private static readonly IPen CurrentOuterPen = new Pen(Brushes.Black, 3);
    private static readonly IPen CurrentInnerPen = new Pen(Brushes.White, 1.5);
    private static readonly IPen HoverPen = new Pen(new SolidColorBrush(Color.FromArgb(0xc0, 0xff, 0xff, 0xff)), 1);

    // The grid's square of cells, at the top, centered across
    private (double Cell, Point Origin) Layout()
    {
        var cell = Math.Floor(Math.Min(Bounds.Width, Bounds.Height) / Columns);
        var size = cell * Columns;
        return (cell, new Point(Math.Floor((Bounds.Width - size) / 2), 0));
    }

    public override void Render(DrawingContext context)
    {
        var (cell, origin) = Layout();
        if (cell < 2 || Colors is not { } colors)
            return;

        int from = Math.Min(Anchor, Current), to = Math.Max(Anchor, Current);
        for (int i = 0; i < Columns * Columns; i++)
        {
            var rect = CellRect(i, cell, origin);
            var color = i < colors.Length ? colors[i] : new PaletteColor(0, 0, 0);
            context.FillRectangle(new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue)), rect);
            context.DrawRectangle(DarkPen, rect.Deflate(0.5));

            if (Marked is { } marked && i < marked.Length && marked[i] && cell >= 8)
            {
                // A dot that shows on light and dark colors alike
                var dot = new Rect(rect.X + 3, rect.Y + 3, Math.Max(3, cell / 6), Math.Max(3, cell / 6));
                var light = color.Red * 3 + color.Green * 6 + color.Blue > 1280;
                context.FillRectangle(light ? Brushes.Black : Brushes.White, dot);
            }
        }

        // The run picked, then the color being edited on top
        if (to > from)
        {
            for (int i = from; i <= to; i++)
                context.DrawRectangle(RunPen, CellRect(i, cell, origin).Deflate(1.5));
        }
        if (_hovered >= 0 && _hovered != Current)
            context.DrawRectangle(HoverPen, CellRect(_hovered, cell, origin).Deflate(1.5));
        if (Current is >= 0 and < Columns * Columns)
        {
            var rect = CellRect(Current, cell, origin);
            context.DrawRectangle(CurrentOuterPen, rect.Deflate(1));
            context.DrawRectangle(CurrentInnerPen, rect.Deflate(1));
        }
    }

    private static Rect CellRect(int index, double cell, Point origin)
        => new(origin.X + index % Columns * cell, origin.Y + index / Columns * cell, cell, cell);

    private int IndexAt(Point point)
    {
        var (cell, origin) = Layout();
        if (cell < 2)
            return -1;
        int column = (int)Math.Floor((point.X - origin.X) / cell), row = (int)Math.Floor((point.Y - origin.Y) / cell);
        return column is >= 0 and < Columns && row is >= 0 and < Columns ? row * Columns + column : -1;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        Focus();
        var index = IndexAt(e.GetPosition(this));
        if (index < 0)
            return;
        _dragging = true;
        e.Pointer.Capture(this);
        Picked?.Invoke(this, (index, e.KeyModifiers.HasFlag(KeyModifiers.Shift)));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var index = IndexAt(e.GetPosition(this));
        if (_dragging)
        {
            // A drag runs from where it started; off the grid it keeps the last color
            if (index >= 0 && index != Current)
                Picked?.Invoke(this, (index, true));
        }
        if (index != _hovered)
        {
            _hovered = index;
            Hovered?.Invoke(this, index);
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hovered != -1)
        {
            _hovered = -1;
            Hovered?.Invoke(this, -1);
            InvalidateVisual();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            return;

        int step = e.Key switch
        {
            Key.Left => -1,
            Key.Right => 1,
            Key.Up => -Columns,
            Key.Down => Columns,
            Key.Home => -Current,
            Key.End => Columns * Columns - 1 - Current,
            _ => 0,
        };
        if (step == 0 && e.Key is not (Key.Home or Key.End))
            return;

        Picked?.Invoke(this, (Math.Clamp(Current + step, 0, Columns * Columns - 1), e.KeyModifiers.HasFlag(KeyModifiers.Shift)));
        e.Handled = true;
    }
}
