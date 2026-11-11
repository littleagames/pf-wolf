using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>The actor window, beside the main window (Ctrl+Shift+A)</summary>
public partial class ActorBrowserWindow : Window
{
    // The game runs at 70 tics a second; the preview steps two at a time
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(1000.0 / 35) };

    public ActorBrowserWindow()
    {
        InitializeComponent();

        // Enter in the search goes to the classes found
        Search.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                (List.ContainerFromItem(List.SelectedItem ?? new object()) ?? (Control)List).Focus();
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);

        _timer.Tick += (_, _) => (DataContext as ActorBrowserViewModel)?.Tick(2);
        Opened += (_, _) => _timer.Start();
        Closed += (_, _) => _timer.Stop();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is not ActorBrowserViewModel browser)
                return;
            browser.CopyText += async (_, text) =>
            {
                if (Clipboard is { } clipboard)
                    await clipboard.SetValueAsync(DataFormat.Text, text);
            };
        };
    }

    private ActorBrowserViewModel ViewModel => (ActorBrowserViewModel)DataContext!;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;
        var typing = FocusManager?.GetFocusedElement() is TextBox;
        var ctrl = e.KeyModifiers == KeyModifiers.Control;
        if (ctrl && e.Key == Key.F)
        {
            Search.Focus();
            Search.SelectAll();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.S)
        {
            ViewModel.SaveClassCommand.Execute(null);
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.Z && !typing)
        {
            ViewModel.UndoClassCommand.Execute(null);
            e.Handled = true;
        }
        else if ((ctrl && e.Key == Key.Y || e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.Z) && !typing)
        {
            ViewModel.RedoClassCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (typing || e.KeyModifiers != KeyModifiers.None)
            return;
        else if (e.Key == Key.Space)
        {
            ViewModel.PlayPauseCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Right && FocusManager?.GetFocusedElement() is not ListBoxItem)
        {
            ViewModel.StepFrameCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Home && FocusManager?.GetFocusedElement() is not ListBoxItem)
        {
            ViewModel.RestartCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnFramePressed(object? sender, PointerPressedEventArgs e)
    {
        if ((sender as Control)?.DataContext is TimelineFrame frame)
            ViewModel.PickFrame(frame);
    }

    private void OnUseDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.GoToUseCommand.CanExecute(null))
            ViewModel.GoToUseCommand.Execute(null);
    }
}
