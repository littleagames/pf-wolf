using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PFWolf.Editor.ViewModels;
using PFWolf.Editor.Views;

namespace PFWolf.Editor;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainWindowViewModel(EditorSettings.Load());
            desktop.MainWindow = new MainWindow { DataContext = viewModel };
            desktop.ShutdownRequested += (_, _) => viewModel.SaveSettings();

            // A game folder given on the command line opens straight away
            if (desktop.Args is [var folder, ..] && Directory.Exists(folder))
                viewModel.GameFolder = Path.GetFullPath(folder);
            if (viewModel.CanLoad)
                _ = viewModel.LoadAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
