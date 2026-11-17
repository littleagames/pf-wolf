using Avalonia.Controls;
using Avalonia.Input;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>
/// A menu drawn as the game draws it (MenuPreviewModel): the menu window's and the Definitions
/// window's YAML tab's. Clicking picks a part, double-clicking opens it, and the keys move the
/// cursor as the game's menus do.
/// </summary>
public partial class MenuPreviewView : UserControl
{
    public MenuPreviewView()
    {
        InitializeComponent();

        Picture.PointerPressed += (_, e) =>
        {
            Frame.Focus();
            var at = e.GetPosition(Picture);
            if (e.ClickCount >= 2)
                Model?.Open(at.X, at.Y);
            else
                Model?.Click(at.X, at.Y);
            e.Handled = true;
        };

        Frame.KeyDown += (_, e) =>
        {
            if (Model is not { } model || e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift))
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
