using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using PFWolf.Editor.Data;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>The script browser (the Lua the game runs), beside the main window (F12)</summary>
public partial class ScriptBrowserWindow : Window
{
    public ScriptBrowserWindow()
    {
        InitializeComponent();

        // Enter in the search goes to the scripts found (the text box would take it on the way up)
        Search.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                (List.ContainerFromItem(List.SelectedItem ?? new object()) ?? (Control)List).Focus();
                e.Handled = true;
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private ScriptBrowserViewModel ViewModel => (ScriptBrowserViewModel)DataContext!;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ScriptBrowserViewModel viewModel)
            viewModel.SelectLine += (_, line) => SelectLine(line);
    }

    // Selects a line of the script and scrolls to it
    private void SelectLine(int line)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var text = SourceBox.Text ?? "";
            int start = 0;
            for (int i = 1; i < line && start < text.Length; i++)
            {
                int next = text.IndexOf('\n', start);
                if (next < 0)
                    break;
                start = next + 1;
            }
            int end = start;
            while (end < text.Length && text[end] is not ('\r' or '\n'))
                end++;

            SourceBox.Focus();
            SourceBox.SelectionStart = start;
            // The caret goes to the selection's end with it (setting CaretIndex would undo the selection)
            SourceBox.SelectionEnd = end;
            SourceBox.ScrollToLine(Math.Max(line - 1, 0));
        });
    }

    private void OnProblemPicked(object? sender, SelectionChangedEventArgs e)
    {
        if (ProblemList.SelectedItem is ScriptProblem problem)
            ViewModel.GoTo(problem.Line);
    }

    private void OnActionPicked(object? sender, SelectionChangedEventArgs e)
    {
        if (ActionList.SelectedItem is ScriptActionRow action)
            ViewModel.GoTo(action.Line);
    }

    private void OnHookPicked(object? sender, SelectionChangedEventArgs e)
    {
        if (HookList.SelectedItem is ScriptItem hook)
            ViewModel.GoTo(hook.Line);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;

        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
        {
            Search.Focus();
            Search.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
