using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>
/// The startup splash: the editor's version, authors, website and credits, with the game's
/// load shown at the bottom. It stays up for a moment and until the load is done; a click or
/// Esc closes it sooner
/// </summary>
public partial class SplashWindow : Window
{
    private static readonly TimeSpan MinimumTime = TimeSpan.FromSeconds(1.5);

    private bool _shownLongEnough;

    public SplashWindow() => InitializeComponent();

    public SplashWindow(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelChanged;
        Closed += (_, _) => viewModel.PropertyChanged -= OnViewModelChanged;

        DispatcherTimer.RunOnce(() =>
        {
            _shownLongEnough = true;
            CloseIfReady();
        }, MinimumTime);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsLoading))
            CloseIfReady();
    }

    // A failed load closes it too: the main window shows the problems
    private void CloseIfReady()
    {
        if (_shownLongEnough && DataContext is MainWindowViewModel { IsLoading: false })
            Close();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // The website link handles its own click
        if (!e.Handled)
            Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
            Close();
    }
}
