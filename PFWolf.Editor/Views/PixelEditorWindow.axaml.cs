using Avalonia.Controls;
using Avalonia.Input;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>The pixel editor, for one picture (opened from the texture and sprite browser)</summary>
public partial class PixelEditorWindow : Window
{
    private bool _closeConfirmed;

    public PixelEditorWindow()
    {
        InitializeComponent();
        Canvas.Pressed += (_, pointer) => ViewModel.Press(pointer);
        Canvas.Dragged += (_, pointer) => ViewModel.Drag(pointer);
        Canvas.Released += (_, _) => ViewModel.Release();
        Canvas.Hovered += (_, pixel) => ViewModel.Hover(pixel);
        Colors.Picked += (_, picked) => ViewModel.PickColor(picked.Index, false);
        Colors.RightPicked += (_, index) => ViewModel.PickColor(index, true);

        // Ctrl+wheel zooms
        Scroller.AddHandler(PointerWheelChangedEvent, (_, e) =>
        {
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
                return;
            ViewModel.ZoomByCommand.Execute(e.Delta.Y > 0 ? "1" : "-1");
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private PixelEditorViewModel ViewModel => (PixelEditorViewModel)DataContext!;

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_closeConfirmed || DataContext is not PixelEditorViewModel viewModel || !viewModel.IsDirty)
            return;

        e.Cancel = true;
        if (await viewModel.CanCloseAsync())
        {
            _closeConfirmed = true;
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        (DataContext as PixelEditorViewModel)?.Dispose();
    }
}
