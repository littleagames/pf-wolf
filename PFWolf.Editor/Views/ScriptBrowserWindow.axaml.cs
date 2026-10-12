using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.CodeCompletion;
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

        // Tooltips: what the game's names and Lua's are, with the pointer over them
        SourceBox.PointerHover += OnSourceHover;
        SourceBox.PointerHoverStopped += OnSourceHoverStopped;

        // Autocomplete: offered after '.', ':', a quote or two letters of a word, and on Ctrl+Space
        SourceBox.TextArea.TextEntered += (_, e) =>
        {
            if (_completion == null && e.Text is { Length: 1 } typed && (char.IsLetter(typed[0]) || typed[0] is '_' or '.' or ':' or '"' or '\''))
                ShowCompletion(asked: false);
        };
        SourceBox.TextArea.TextEntering += (_, e) =>
        {
            // '(' takes the name picked and goes on; anything else that can't be in a name closes the list
            if (_completion == null || e.Text is not { Length: > 0 } typed || char.IsLetterOrDigit(typed[0]) || typed[0] == '_')
                return;
            if (typed[0] == '(')
                _completion.CompletionList.RequestInsertion(e);
            else
                _completion.Close();
        };
        SourceBox.TextArea.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.Control)
            {
                ShowCompletion(asked: true);
                e.Handled = true;
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    // What's under the pointer, shown as a tooltip while it rests there
    private void OnSourceHover(object? sender, PointerEventArgs e)
    {
        var position = SourceBox.GetPositionFromPoint(e.GetPosition(SourceBox));
        if (position is not { IsAtEndOfLine: false } at || _completion != null)
            return;
        var line = SourceBox.Document.GetLineByNumber(at.Line);
        if (ViewModel.Describe(SourceBox.Document.GetText(line), at.Column - 1) is not { } info)
            return;

        ToolTip.SetTip(SourceBox, new StackPanel
        {
            MaxWidth = 460,
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = info.Signature, FontFamily = new FontFamily("Consolas,Menlo,monospace"), TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = info.Summary, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 },
                new TextBlock { Text = info.Kind, FontSize = 11, Opacity = 0.55 },
            },
        });
        ToolTip.SetIsOpen(SourceBox, true);
        e.Handled = true;
    }

    private void OnSourceHoverStopped(object? sender, PointerEventArgs e)
    {
        ToolTip.SetIsOpen(SourceBox, false);
        ToolTip.SetTip(SourceBox, null);
    }

    private CompletionWindow? _completion;

    // Opens the autocomplete list for the caret, when there's something to offer
    private void ShowCompletion(bool asked)
    {
        var caret = SourceBox.CaretOffset;
        var line = SourceBox.Document.GetLineByOffset(caret);
        var before = SourceBox.Document.GetText(line.Offset, caret - line.Offset);
        if (ViewModel.Complete(before, asked) is not { } context)
            return;

        _completion?.Close();
        var window = new CompletionWindow(SourceBox.TextArea) { StartOffset = caret - context.PrefixLength, CloseWhenCaretAtBeginning = true };
        foreach (var option in context.Options)
            window.CompletionList.CompletionData.Add(new ScriptCompletionData(option));
        window.Closed += (_, _) => _completion = null;
        _completion = window;
        window.Show();
        // with part of the word typed, the list starts on the first that fits
        if (context.PrefixLength > 0)
            window.CompletionList.SelectItem(before[^context.PrefixLength..]);
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
