using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>The demo browser, beside the main window (Ctrl+Shift+D)</summary>
public partial class DemoBrowserWindow : Window
{
    private DemoBrowserViewModel? _subscribed;

    public DemoBrowserWindow()
    {
        InitializeComponent();
        Timeline.Scrub += (_, frame) =>
        {
            ViewModel.Pause();
            ViewModel.Frame = frame;
        };

        // Keep the player in sight as it plays
        DataContextChanged += (_, _) =>
        {
            if (_subscribed != null)
                _subscribed.FrameShown -= OnFrameShown;
            _subscribed = DataContext as DemoBrowserViewModel;
            if (_subscribed != null)
                _subscribed.FrameShown += OnFrameShown;
        };

        // Nothing plays on once the window's gone
        Closed += (_, _) => ViewModel.Pause();

        // On the way down, so the map (which takes arrows and Esc for its tools) doesn't get them first
        AddHandler(KeyDownEvent, OnPlayKey, RoutingStrategies.Tunnel);
    }

    private DemoBrowserViewModel ViewModel => (DemoBrowserViewModel)DataContext!;

    private void OnFrameShown(object? sender, EventArgs e)
    {
        if (ViewModel.FollowPlayer && ViewModel.PlayerSpot is { } spot)
            Map.KeepInView(spot.X, spot.Y);
    }

    private void OnPlayKey(object? sender, KeyEventArgs e)
    {
        if (DataContext is not DemoBrowserViewModel)
            return;

        // A second is 17.5 frames
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? "18" : "1";
        switch (e.Key)
        {
            case Key.Space:
                Run(ViewModel.PlayCommand);
                break;
            case Key.Left:
                Run(ViewModel.StepCommand, "-" + step);
                break;
            case Key.Right:
                Run(ViewModel.StepCommand, step);
                break;
            case Key.Home when !Map.IsFocused:      // over the map it fits the level
                Run(ViewModel.StepCommand, "-1000000");
                break;
            case Key.End:
                Run(ViewModel.GoToEndCommand);
                break;
            case Key.O when e.KeyModifiers == KeyModifiers.Control:
                OpenDemo(this, new RoutedEventArgs());
                break;
            case Key.Escape:
                Close();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private static void Run(System.Windows.Input.ICommand command, object? parameter = null)
    {
        if (command.CanExecute(parameter))
            command.Execute(parameter);
    }

    private static readonly FilePickerFileType DemoFiles = new("Demo") { Patterns = ["*.dmo", "*.wl6", "*.wl1", "*.sod", "*.sdm"] };

    private async void OpenDemo(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open a demo",
            AllowMultiple = true,
            FileTypeFilter = [DemoFiles, FilePickerFileTypes.All],
        });
        foreach (var file in files)
        {
            if (file.TryGetLocalPath() is { } path)
                ViewModel.Open(path);
        }
    }

    private async void SaveDemo(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem?.Entry is not { } entry)
            return;
        var name = entry.Number is { } number ? $"DEMO{number}.dmo" : Path.ChangeExtension(entry.Name, ".dmo");
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Save {entry.Name} as a demo file",
            SuggestedFileName = name,
            DefaultExtension = "dmo",
            FileTypeChoices = [new FilePickerFileType("Demo") { Patterns = ["*.dmo"] }],
        });
        if (file == null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(entry.Demo.Data);
        ViewModel.Status = $"Saved {entry.Name} as {file.Name}";
    }
}
