using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>The definitions window: game info, game packs, actors and map definitions in fields, and every YAML file as text</summary>
public partial class DefinitionsWindow : Window
{
    private DefinitionsViewModel? _viewModel;

    public DefinitionsWindow()
    {
        InitializeComponent();
        SourceBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.CaretIndexProperty)
                UpdateCaretText();
        };
        SourceBox.AddHandler(KeyDownEvent, OnSourceKeyDown, RoutingStrategies.Tunnel);
        KeyDown += OnWindowKeyDown;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel != null)
        {
            _viewModel.Files.GoToLineRequested -= OnGoToLine;
            _viewModel.ShowFilesRequested -= OnShowFiles;
        }
        _viewModel = DataContext as DefinitionsViewModel;
        if (_viewModel != null)
        {
            _viewModel.Files.GoToLineRequested += OnGoToLine;
            _viewModel.ShowFilesRequested += OnShowFiles;
        }
    }

    private void OnShowFiles(object? sender, EventArgs e) => Tabs.SelectedItem = FilesTab;

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.S || e.KeyModifiers != KeyModifiers.Control || _viewModel == null)
            return;
        if (Tabs.SelectedItem == FilesTab && _viewModel.Files.SaveCommand.CanExecute(null))
            _viewModel.Files.SaveCommand.Execute(null);
        e.Handled = true;
    }

    // YAML can't be indented with tabs: Tab puts in spaces
    private void OnSourceKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab || e.KeyModifiers != KeyModifiers.None || SourceBox.IsReadOnly)
            return;
        var text = SourceBox.Text ?? "";
        int start = Math.Min(SourceBox.SelectionStart, SourceBox.SelectionEnd), end = Math.Max(SourceBox.SelectionStart, SourceBox.SelectionEnd);
        int lineStart = text.LastIndexOf('\n', Math.Max(0, start - 1)) + 1;
        if (start == 0)
            lineStart = 0;
        var indent = new string(' ', 2 - (start - lineStart) % 2);
        SourceBox.Text = text[..start] + indent + text[end..];
        SourceBox.CaretIndex = start + indent.Length;
        e.Handled = true;
    }

    private void OnGoToLine(object? sender, int line)
    {
        var text = SourceBox.Text ?? "";
        int index = 0;
        for (int i = 1; i < line && index >= 0; i++)
        {
            index = text.IndexOf('\n', index);
            if (index >= 0)
                index++;
        }
        if (index < 0)
            index = text.Length;
        int lineEnd = text.IndexOfAny(['\r', '\n'], index);
        if (lineEnd < 0)
            lineEnd = text.Length;

        Tabs.SelectedItem = FilesTab;
        SourceBox.Focus();
        SourceBox.CaretIndex = index;
        SourceBox.SelectionStart = index;
        SourceBox.SelectionEnd = lineEnd;
    }

    private void UpdateCaretText()
    {
        if (_viewModel == null)
            return;
        var text = SourceBox.Text ?? "";
        int caret = Math.Clamp(SourceBox.CaretIndex, 0, text.Length);
        int line = 1, lineStart = 0;
        for (int i = 0; i < caret; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }
        _viewModel.Files.CaretText = $"Line {line}, column {caret - lineStart + 1}";
    }
}
