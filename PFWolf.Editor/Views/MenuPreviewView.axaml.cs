using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using PFWolf.Editor.Editing;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>The guide lines' ends: down the image's height, across its width</summary>
public static class MenuGuides
{
    public static readonly IValueConverter Down = new FuncValueConverter<double, Avalonia.Point>(height => new Avalonia.Point(0, height));
    public static readonly IValueConverter Across = new FuncValueConverter<double, Avalonia.Point>(width => new Avalonia.Point(width, 0));
}

/// <summary>
/// A menu drawn as the game draws it (MenuPreviewModel): the menu window's and the Definitions
/// window's YAML tab's. Clicking picks a part, double-clicking opens it, and the keys move the
/// cursor as the game's menus do. In the menu window a part is dragged to move it (an edge to
/// size it), and Ctrl+arrows nudge the part picked.
/// </summary>
public partial class MenuPreviewView : UserControl
{
    private bool _pressed;

    public MenuPreviewView()
    {
        InitializeComponent();

        Picture.PointerPressed += (_, e) =>
        {
            Frame.Focus();
            if (Model is not { } model || !e.GetCurrentPoint(Picture).Properties.IsLeftButtonPressed)
                return;
            var at = e.GetPosition(Picture);
            if (e.ClickCount >= 2)
            {
                model.Open(at.X, at.Y);
                e.Handled = true;
                return;
            }
            model.Click(at.X, at.Y);
            _pressed = model.BeginDrag(at.X, at.Y, e.KeyModifiers.HasFlag(KeyModifiers.Shift), e.KeyModifiers.HasFlag(KeyModifiers.Alt));
            if (_pressed)
                e.Pointer.Capture(Picture);
            e.Handled = true;
        };

        Picture.PointerMoved += (_, e) =>
        {
            if (Model is not { } model)
                return;
            var at = e.GetPosition(Picture);
            if (_pressed)
                model.DragTo(at.X, at.Y);
            else
                Picture.Cursor = new Cursor(model.ModeAt(at.X, at.Y, e.KeyModifiers.HasFlag(KeyModifiers.Shift)) switch
                {
                    MenuDragMode.Move => StandardCursorType.SizeAll,
                    MenuDragMode.ResizeWidth => StandardCursorType.SizeWestEast,
                    MenuDragMode.ResizeHeight => StandardCursorType.SizeNorthSouth,
                    MenuDragMode.Resize => StandardCursorType.BottomRightCorner,
                    MenuDragMode.Spacing => StandardCursorType.SizeNorthSouth,
                    _ => StandardCursorType.Hand,
                });
        };

        Picture.PointerReleased += (_, e) =>
        {
            if (!_pressed)
                return;
            _pressed = false;
            e.Pointer.Capture(null);
            Model?.EndDrag();
            e.Handled = true;
        };

        Picture.PointerCaptureLost += (_, _) =>
        {
            if (_pressed)
            {
                _pressed = false;
                Model?.CancelDrag();
            }
        };

        Frame.KeyDown += (_, e) =>
        {
            if (Model is not { } model)
                return;
            if (e.Key == Key.Escape && _pressed)
            {
                _pressed = false;
                model.CancelDrag();
                e.Handled = true;
                return;
            }

            // Ctrl+arrows nudge the part picked (Shift: 8 pixels)
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && model.CanLayOut
                && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
            {
                int step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 8 : 1;
                model.Nudge(e.Key switch { Key.Left => -step, Key.Right => step, _ => 0 }, e.Key switch { Key.Up => -step, Key.Down => step, _ => 0 });
                e.Handled = true;
                return;
            }

            if (e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift))
                return;
            switch (e.Key)
            {
                case Key.Down:
                    model.Step(1);
                    break;
                case Key.Up:
                    model.Step(-1);
                    break;
                case Key.Home or Key.PageUp:
                    model.End(last: false);
                    break;
                case Key.End or Key.PageDown:
                    model.End(last: true);
                    break;
                case >= Key.A and <= Key.Z:
                    if (!model.Letter((char)('A' + (e.Key - Key.A))))
                        return;
                    break;
                default:
                    return;
            }
            e.Handled = true;
        };
    }

    private MenuPreviewModel? Model => DataContext as MenuPreviewModel;

    private void OnZoomPicked(object? sender, SelectionChangedEventArgs e)
    {
        if (Model is { } model)
            model.Zoom = ZoomBox.SelectedIndex + 2;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (Model is { } model)
            ZoomBox.SelectedIndex = Math.Clamp(model.Zoom - 2, 0, 2);
    }
}
