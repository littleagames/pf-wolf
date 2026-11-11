using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
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

    // The frame a press started on, for a drag along the timeline
    private TimelineFrame? _dragged;

    private void OnFramePressed(object? sender, PointerPressedEventArgs e)
    {
        if ((sender as Control)?.DataContext is TimelineFrame frame)
        {
            _dragged = frame;
            ViewModel.PickFrame(frame);
        }
    }

    // Let go over another frame of the state: the one pressed on moves there; over a state's name:
    // it goes to that state's end (with Ctrl, a copy)
    private void OnFrameReleased(object? sender, PointerReleasedEventArgs e)
    {
        var from = _dragged;
        _dragged = null;
        if (from == null)
            return;
        var over = this.InputHitTest(e.GetPosition(this)) as Visual;
        while (over != null && !(over is Control control && (control.Classes.Contains("frame") || control.Classes.Contains("statechip"))))
            over = over.GetVisualParent();
        switch ((over as Control)?.DataContext)
        {
            case TimelineFrame to when to != from:
                ViewModel.MoveFrame(from.Index, to.Index);
                break;
            case string state when state != ViewModel.SelectedState:
                ViewModel.MoveFrameToState(from.Index, state, copy: e.KeyModifiers.HasFlag(KeyModifiers.Control));
                break;
        }
    }

    // A state's name clicked: that state shown
    private void OnStateChipTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is string state)
            ViewModel.SelectedState = state;
    }

    // A frame's right edge dragged: its tics as the edge goes, set when it's let go
    private TimelineFrame? _stretched;
    private double _stretchFrom;
    private int _stretchTics;

    private void OnGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control grip || grip.DataContext is not TimelineFrame frame || !frame.CanStretch)
            return;
        e.Handled = true;       // not the frame's own press (a pick, a drag along the timeline)
        _stretched = frame;
        _stretchFrom = e.GetPosition(this).X;
        _stretchTics = frame.Frame.TicTime;
        frame.DragTics = _stretchTics;
        e.Pointer.Capture(grip);
    }

    private void OnGripMoved(object? sender, PointerEventArgs e)
    {
        if (_stretched is not { } frame)
            return;
        frame.DragTics = Math.Max(0, _stretchTics + (int)Math.Round((e.GetPosition(this).X - _stretchFrom) / 3));
    }

    private void OnGripReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_stretched is not { } frame)
            return;
        _stretched = null;
        e.Pointer.Capture(null);
        e.Handled = true;
        var tics = frame.DragTics ?? frame.Frame.TicTime;
        frame.DragTics = null;
        ViewModel.SetFrameTics(frame.Index, tics);
    }

    private void OnUseDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.GoToUseCommand.CanExecute(null))
            ViewModel.GoToUseCommand.Execute(null);
    }
}
