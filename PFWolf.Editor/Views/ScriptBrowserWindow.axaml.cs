using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using PFWolf.Editor.Data;
using PFWolf.Editor.Rendering;
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

        SourceBox.SyntaxHighlighting = LuaHighlighting.Definition;
        SourceBox.Options.ConvertTabsToSpaces = true;
        SourceBox.Options.IndentationSize = 2;

        // The editor's text and the view model's Source, kept the same both ways (TextEditor.Text
        // doesn't bind)
        SourceBox.TextChanged += (_, _) =>
        {
            if (!_showingSource && DataContext is ScriptBrowserViewModel viewModel && viewModel.Source != SourceBox.Text)
                viewModel.Source = SourceBox.Text;
        };
    }

    // Set while the view model's Source is put in the editor, so it isn't sent back as an edit
    private bool _showingSource;

    private ScriptBrowserViewModel ViewModel => (ScriptBrowserViewModel)DataContext!;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not ScriptBrowserViewModel viewModel)
            return;

        viewModel.SelectLine += (_, line) => SelectLine(line);
        viewModel.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(ScriptBrowserViewModel.Source))
                ShowSource(viewModel.Source);
        };
        ShowSource(viewModel.Source);
    }

    // Puts the view model's text in the editor when it's another (a script picked, Revert, New)
    private void ShowSource(string source)
    {
        if (SourceBox.Text == source)
            return;
        _showingSource = true;
        try
        {
            SourceBox.Text = source;
            SourceBox.ScrollToHome();
        }
        finally
        {
            _showingSource = false;
        }
    }

    // Selects a line of the script and scrolls to it
    private void SelectLine(int line)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var document = SourceBox.Document;
            if (document.LineCount == 0)
                return;
            var docLine = document.GetLineByNumber(Math.Clamp(line, 1, document.LineCount));

            SourceBox.Focus();
            SourceBox.Select(docLine.Offset, docLine.Length);
            SourceBox.TextArea.Caret.Line = docLine.LineNumber;
            SourceBox.ScrollToLine(docLine.LineNumber);
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
