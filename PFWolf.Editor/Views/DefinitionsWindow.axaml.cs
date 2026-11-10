using Avalonia.Controls;
using Avalonia.Input;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>The definitions window: game info, game packs, actors, map definitions, animations and cropped pictures in fields, and every YAML file as text</summary>
public partial class DefinitionsWindow : Window
{
    private DefinitionsViewModel? _viewModel;

    public DefinitionsWindow()
    {
        InitializeComponent();
        // YAML can't be indented with tabs: Tab puts in spaces
        SourceBox.Options.ConvertTabsToSpaces = true;
        SourceBox.Options.IndentationSize = 2;
        SourceBox.TextArea.Caret.PositionChanged += (_, _) => UpdateCaretText();
        KeyDown += OnWindowKeyDown;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel != null)
        {
            _viewModel.Files.GoToLineRequested -= OnGoToLine;
            _viewModel.Files.SelectTextRequested -= OnSelectText;
            _viewModel.ShowFilesRequested -= OnShowFiles;
            _viewModel.ShowTabRequested -= OnShowTab;
        }
        _viewModel = DataContext as DefinitionsViewModel;
        if (_viewModel != null)
        {
            _viewModel.Files.GoToLineRequested += OnGoToLine;
            _viewModel.Files.SelectTextRequested += OnSelectText;
            _viewModel.ShowFilesRequested += OnShowFiles;
            _viewModel.ShowTabRequested += OnShowTab;
        }
    }

    private void OnShowTab(object? sender, Definitions.DefinitionKind kind) => Tabs.SelectedItem = kind switch
    {
        Definitions.DefinitionKind.GameInfo => GameInfoTab,
        Definitions.DefinitionKind.GamePackInfo => GamePacksTab,
        Definitions.DefinitionKind.Actors => ActorsTab,
        Definitions.DefinitionKind.Animations => AnimationsTab,
        Definitions.DefinitionKind.Graphics => GraphicsTab,
        Definitions.DefinitionKind.Factions => FactionsTab,
        Definitions.DefinitionKind.Elevator => ElevatorTab,
        Definitions.DefinitionKind.Presenter => PresenterTab,
        Definitions.DefinitionKind.Particles => ParticlesTab,
        Definitions.DefinitionKind.Fonts => FontsTab,
        _ => MapDefsTab,
    };

    private void OnShowFiles(object? sender, EventArgs e) => Tabs.SelectedItem = FilesTab;

    // The tab shown, when it's one of the definitions tabs
    private EntryEditorViewModel? ShownEditor => _viewModel == null ? null
        : Tabs.SelectedItem == GameInfoTab ? _viewModel.GameInfo
        : Tabs.SelectedItem == ActorsTab ? _viewModel.Actors
        : Tabs.SelectedItem == MapDefsTab ? _viewModel.MapDefs
        : Tabs.SelectedItem == AnimationsTab ? _viewModel.Animations
        : Tabs.SelectedItem == GraphicsTab ? _viewModel.Graphics
        : Tabs.SelectedItem == FactionsTab ? _viewModel.Factions
        : Tabs.SelectedItem == ElevatorTab ? _viewModel.Elevator
        : Tabs.SelectedItem == PresenterTab ? _viewModel.Presenter
        : Tabs.SelectedItem == ParticlesTab ? _viewModel.Particles
        : Tabs.SelectedItem == FontsTab ? _viewModel.Fonts
        : Tabs.SelectedItem == GamePacksTab ? _viewModel.GamePacks
        : null;

    // Each tab reads its definitions once it's shown
    private void OnTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel == null || e.Source != Tabs)
            return;
        foreach (var editor in new[] { _viewModel.GameInfo, _viewModel.Actors, _viewModel.MapDefs, _viewModel.Animations, _viewModel.Graphics, _viewModel.Factions,
                     _viewModel.Elevator, _viewModel.Presenter, _viewModel.Particles, _viewModel.Fonts, _viewModel.GamePacks })
        {
            if (editor != ShownEditor)
                editor.Deactivate();
        }
        ShownEditor?.Activate();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ShownEditor?.Activate();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel == null)
            return;
        if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
        {
            if (ShownEditor is { } editor)
            {
                if (editor.SaveCommand.CanExecute(null))
                    editor.SaveCommand.Execute(null);
            }
            else if (Tabs.SelectedItem == FilesTab && _viewModel.Files.SaveCommand.CanExecute(null))
                _viewModel.Files.SaveCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
        {
            // (in a file's text the editor has it first, and finds in the text)
            if (Tabs.SelectedItem == LevelOwnTab)
                OpenFind(LevelOwnBox);
            else if (Tabs.SelectedItem == FilesTab && FilesFilter.IsKeyboardFocusWithin && _viewModel.Files.HasSelection)
                OpenFind(SourceBox);
            else if (Tabs.SelectedItem == FilesTab)
            {
                FilesFilter.Focus();
                FilesFilter.SelectAll();
            }
            else if ((Tabs.SelectedItem as TabItem)?.Content is EntryEditorView entries)
                entries.FocusFilter();
            e.Handled = true;
        }
    }

    // The editor's find panel, with what's searched for in the files' list in it
    private void OpenFind(AvaloniaEdit.TextEditor editor)
    {
        // (there's no find panel till the editor's first shown)
        if (editor.SearchPanel is not { } find)
            return;
        editor.Focus();
        if (editor == SourceBox && _viewModel is { Files: { SearchInside: true } files } && files.Filter.Trim().Length > 0)
            find.SearchPattern = files.Filter.Trim();
        find.Open();
        // (its search box is there once it's laid out, as AvaloniaEdit's own Ctrl+F does it)
        Avalonia.Threading.Dispatcher.UIThread.Post(find.Reactivate, Avalonia.Threading.DispatcherPriority.Input);
    }

    // After the editor has the file just picked (its text is bound, so it changes a moment later)
    private void OnGoToLine(object? sender, int line)
    {
        Tabs.SelectedItem = FilesTab;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => SelectLine(line), Avalonia.Threading.DispatcherPriority.Background);
    }

    private void SelectLine(int line)
    {
        var document = SourceBox.Document;
        if (document.LineCount == 0)
            return;
        var docLine = document.GetLineByNumber(Math.Clamp(line, 1, document.LineCount));

        Tabs.SelectedItem = FilesTab;
        SourceBox.Focus();
        SourceBox.Select(docLine.Offset, docLine.Length);
        SourceBox.TextArea.Caret.Line = docLine.LineNumber;
        SourceBox.ScrollToLine(docLine.LineNumber);
    }

    // The search's first in a file picked, selected without taking the keyboard from the list or the search box
    private void OnSelectText(object? sender, (int Index, int Length) found)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var document = SourceBox.Document;
            if (found.Index + found.Length > document.TextLength)
                return;
            SourceBox.Select(found.Index, found.Length);
            var at = document.GetLocation(found.Index);
            SourceBox.ScrollTo(at.Line, at.Column);
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void UpdateCaretText()
    {
        if (_viewModel == null)
            return;
        var caret = SourceBox.TextArea.Caret;
        _viewModel.Files.CaretText = $"Line {caret.Line}, column {caret.Column}";
    }
}
