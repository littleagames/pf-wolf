using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace PFWolf.Editor.Rendering;

using Point = Avalonia.Point;

/// <summary>What happened at a pixel of the pixel editor's canvas</summary>
public readonly record struct PixelPointer(int X, int Y, bool Secondary, KeyModifiers Modifiers);

/// <summary>
/// The pixel editor's picture, drawn big with each pixel a square (on a checkerboard where it's
/// see-through), with lines between pixels when they're big enough, and a sprite's offset as a
/// cross. Pointer presses, drags and releases come out in pixels.
/// </summary>
public sealed class PixelCanvas : Control
{
    public static readonly StyledProperty<Bitmap?> PictureProperty =
        AvaloniaProperty.Register<PixelCanvas, Bitmap?>(nameof(Picture));

    public static readonly StyledProperty<int> ZoomProperty =
        AvaloniaProperty.Register<PixelCanvas, int>(nameof(Zoom), 8);

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<PixelCanvas, bool>(nameof(ShowGrid), true);

    /// <summary>A sprite's offset, drawn as a cross there; null for none</summary>
    public static readonly StyledProperty<PixelPoint?> MarkProperty =
        AvaloniaProperty.Register<PixelCanvas, PixelPoint?>(nameof(Mark));

    static PixelCanvas()
    {
        AffectsRender<PixelCanvas>(PictureProperty, ZoomProperty, ShowGridProperty, MarkProperty);
        AffectsMeasure<PixelCanvas>(PictureProperty, ZoomProperty);
        FocusableProperty.OverrideDefaultValue<PixelCanvas>(true);
    }

    public Bitmap? Picture
    {
        get => GetValue(PictureProperty);
        set => SetValue(PictureProperty, value);
    }

    public int Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public PixelPoint? Mark
    {
        get => GetValue(MarkProperty);
        set => SetValue(MarkProperty, value);
    }

    public event EventHandler<PixelPointer>? Pressed;
    public event EventHandler<PixelPointer>? Dragged;
    public event EventHandler? Released;

    /// <summary>The pixel under the pointer, or null off the picture</summary>
    public event EventHandler<PixelPoint?>? Hovered;

    private bool _dragging;
    private bool _secondary;
    private PixelPoint? _hovered;

    private static readonly IBrush CheckLight = new SolidColorBrush(Color.FromRgb(0x9a, 0x9a, 0x9a));
    private static readonly IBrush CheckDark = new SolidColorBrush(Color.FromRgb(0x6a, 0x6a, 0x6a));
    private static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.FromArgb(0x50, 0, 0, 0)), 1);
    private static readonly IPen HoverPen = new Pen(Brushes.White, 1);
    private static readonly IPen MarkPen = new Pen(Brushes.Cyan, 1);

    private PixelSize Size => Picture?.PixelSize ?? new PixelSize(0, 0);

    protected override Size MeasureOverride(Size availableSize) => new(Size.Width * Zoom, Size.Height * Zoom);

    public override void Render(DrawingContext context)
    {
        if (Picture is not { } picture)
            return;
        int zoom = Zoom, width = Size.Width, height = Size.Height;
        var whole = new Rect(0, 0, width * zoom, height * zoom);

        // A checkerboard of 8-screen-pixel squares behind see-through pixels
        context.FillRectangle(CheckLight, whole);
        const int check = 8;
        for (int y = 0; y < whole.Height; y += check)
            for (int x = (y / check % 2) * check; x < whole.Width; x += check * 2)
                context.FillRectangle(CheckDark, new Rect(x, y, Math.Min(check, whole.Width - x), Math.Min(check, whole.Height - y)));

        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
            context.DrawImage(picture, new Rect(0, 0, width, height), whole);

        if (ShowGrid && zoom >= 6)
        {
            for (int x = 1; x < width; x++)
                context.DrawLine(GridPen, new Point(x * zoom + 0.5, 0), new Point(x * zoom + 0.5, whole.Height));
            for (int y = 1; y < height; y++)
                context.DrawLine(GridPen, new Point(0, y * zoom + 0.5), new Point(whole.Width, y * zoom + 0.5));
        }

        if (Mark is { } mark)
        {
            double mx = mark.X * zoom + zoom / 2.0, my = mark.Y * zoom + zoom / 2.0, arm = Math.Max(6, zoom * 1.5);
            context.DrawLine(MarkPen, new Point(mx - arm, my), new Point(mx + arm, my));
            context.DrawLine(MarkPen, new Point(mx, my - arm), new Point(mx, my + arm));
        }

        if (_hovered is { } hovered)
            context.DrawRectangle(HoverPen, new Rect(hovered.X * zoom + 0.5, hovered.Y * zoom + 0.5, zoom - 1, zoom - 1));
    }

    private PixelPoint? PixelAt(Point point)
    {
        if (Zoom <= 0)
            return null;
        int x = (int)Math.Floor(point.X / Zoom), y = (int)Math.Floor(point.Y / Zoom);
        return x >= 0 && y >= 0 && x < Size.Width && y < Size.Height ? new PixelPoint(x, y) : null;
    }

    // Where the pointer is in pixels, off the picture too (a line or drag can run off it)
    private (int X, int Y) Unclamped(Point point) => ((int)Math.Floor(point.X / Zoom), (int)Math.Floor(point.Y / Zoom));

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var properties = e.GetCurrentPoint(this).Properties;
        if (!properties.IsLeftButtonPressed && !properties.IsRightButtonPressed)
            return;
        Focus();
        if (PixelAt(e.GetPosition(this)) is not { } pixel)
            return;
        _dragging = true;
        _secondary = properties.IsRightButtonPressed;
        e.Pointer.Capture(this);
        Pressed?.Invoke(this, new PixelPointer(pixel.X, pixel.Y, _secondary, e.KeyModifiers));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        if (_dragging)
        {
            var (x, y) = Unclamped(position);
            Dragged?.Invoke(this, new PixelPointer(x, y, _secondary, e.KeyModifiers));
        }

        var pixel = PixelAt(position);
        if (pixel != _hovered)
        {
            _hovered = pixel;
            Hovered?.Invoke(this, pixel);
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging)
            return;
        _dragging = false;
        e.Pointer.Capture(null);
        Released?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_dragging)
        {
            _dragging = false;
            Released?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hovered != null)
        {
            _hovered = null;
            Hovered?.Invoke(this, null);
            InvalidateVisual();
        }
    }
}
