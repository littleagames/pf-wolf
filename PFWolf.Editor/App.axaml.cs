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

            // A game folder given on the command line opens straight away, with the mods after it
            // (pk3s or folders) in place of the remembered ones: a standalone game's mod opens
            // as that game
            if (desktop.Args is [var folder, .. var mods] && Directory.Exists(folder))
            {
                viewModel.GameFolder = Path.GetFullPath(folder);
                var modPaths = mods.Where(mod => File.Exists(mod) || Directory.Exists(mod)).Select(Path.GetFullPath).ToList();
                if (modPaths.Count > 0)
                {
                    viewModel.Mods.Clear();
                    foreach (var mod in modPaths)
                        viewModel.Mods.Add(mod);
                    viewModel.SelectedGame = viewModel.GameChoices[0];
                }
            }
            if (viewModel.CanLoad)
                _ = viewModel.LoadAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
