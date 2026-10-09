using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Editor.Rendering;

namespace PFWolf.Editor.ViewModels;

/// <summary>A text as the text browser lists it</summary>
public sealed class TextItem(TextEntry entry)
{
    public TextEntry Entry { get; } = entry;
    public string Name => Entry.Name;
    public string FormatText => TextCatalog.FormatName(Entry.Format);

    public string ToolTip => $"{Entry.Name}  ({FormatText}, {Entry.Source})"
        + (Entry.Uses.Count > 0 ? "\n" + string.Join("\n", Entry.Uses) : "\nNothing in game-info shows it (the engine itself may)");
}

/// <summary>
/// The text browser: every text in the loaded game (its data files' and the pk3s' texts/), an
/// article (the help screens, an episode's end text) shown page by page as the game shows it,
/// with what's wrong with it. A text can be changed, or a new one made, and saved to a mod's
/// texts/ folder, where the game reads it in place of its own.
/// </summary>
public sealed partial class TextBrowserViewModel : ObservableObject, IDisposable
{
    private List<TextItem> _all = [];
    private GameContent? _content;
    private ArticleArt? _art;
    private FontAsset? _font;
    private string? _fontProblem;
    private byte _backColor;
    private string _original = "";
    // Set while the text shown is swapped for another's, so it isn't taken for an edit
    private bool _loadingSource;
    // Texts saved, by name, and the mod each went to: the game loaded still has the old
    // ones until it's loaded again
    private readonly Dictionary<string, (string Text, string Folder)> _saved = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Asks for a mod (a folder or a pk3) to save in; null when none is picked</summary>
    public Func<string, Task<string?>>? PickMod { get; set; }

    /// <summary>Asks for a line of text (title, prompt, initial, check); null when cancelled</summary>
    public Func<string, string, string, Func<string, string?>, Task<string?>>? AskText { get; set; }

    /// <summary>A text was saved into this mod (the editor adds it to the mods)</summary>
    public event EventHandler<string>? Saved;

    public ObservableCollection<TextItem> Items { get; } = [];
    public ObservableCollection<string> Origins { get; } = [];
    public ObservableCollection<string> Uses { get; } = [];
    public ObservableCollection<ArticleProblem> Problems { get; } = [];

    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private string _countText = "";
    [ObservableProperty] private string _title = "Texts";

    [ObservableProperty] private string _status = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(SaveToCommand))]
    private TextItem? _selectedItem;

    /// <summary>The text shown, as changed in the box</summary>
    [ObservableProperty] private string _source = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RevertCommand))]
    private bool _isChanged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand), nameof(NextPageCommand))]
    private ArticleLayout? _layout;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand), nameof(NextPageCommand))]
    private int _pageIndex = -1;

    [ObservableProperty] private Bitmap? _pageImage;
    [ObservableProperty] private string _pageText = "";
    [ObservableProperty] private string _details = "";
    [ObservableProperty] private string _summary = "";

    public bool HasSelection => SelectedItem != null;
    public bool HasPreview => Layout != null;
    public bool HasUses => Uses.Count > 0;
    public bool HasProblems => Problems.Count > 0;

    /// <summary>Asks the window to put the text box's caret here and select this much: a problem picked</summary>
    public event EventHandler<(int Index, int Length)>? SelectInSource;

    /// <summary>Lists the texts of a newly loaded game, keeping the one picked if it's still there</summary>
    public void Load(GameContent? content)
    {
        _content = content;
        var picked = SelectedItem?.Name;
        // What was saved is in the game now, as long as its mod is loaded; what was new and unsaved is gone
        _saved.Clear();
        Status = "";

        if (content == null)
        {
            _all = [];
            _art = null;
            _font = null;
            Title = "Texts";
        }
        else
        {
            _all = TextCatalog.Build(content).Select(entry => new TextItem(entry)).ToList();
            _art = TextCatalog.Art(content, out _font, out _fontProblem);
            _backColor = TextCatalog.BackColor(content);
            Title = $"Texts - {content.Title}";
        }

        Refilter();
        SelectedItem = picked != null ? Items.FirstOrDefault(item => item.Name.Equals(picked, StringComparison.OrdinalIgnoreCase)) : null;
        SelectedItem ??= Items.FirstOrDefault(item => item.Entry.Format == TextFormat.Article) ?? Items.FirstOrDefault();
    }

    /// <summary>Shows a text by name</summary>
    public void Show(string name)
    {
        var found = _all.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (found == null)
            return;
        if (!Items.Contains(found))
            Filter = "";
        SelectedItem = found;
    }

    /// <summary>The same game in new colors: the page is drawn again</summary>
    public void Recolor()
    {
        if (_content != null)
        {
            _art = TextCatalog.Art(_content, out _font, out _fontProblem);
            _backColor = TextCatalog.BackColor(_content);
        }
        Relayout();
    }

    partial void OnFilterChanged(string value) => Refilter();

    private void Refilter()
    {
        var selected = SelectedItem;
        var words = Filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Items.Clear();
        foreach (var item in _all.Where(item => words.All(word => item.Name.Contains(word, StringComparison.OrdinalIgnoreCase)
                     || item.Entry.Uses.Any(use => use.Contains(word, StringComparison.OrdinalIgnoreCase)))))
            Items.Add(item);
        CountText = Items.Count == _all.Count ? $"{Items.Count} texts" : $"{Items.Count} of {_all.Count} texts";
        SelectedItem = selected != null && Items.Contains(selected) ? selected : Items.FirstOrDefault();
    }

    partial void OnSelectedItemChanged(TextItem? value)
    {
        Origins.Clear();
        Uses.Clear();
        if (value != null)
        {
            foreach (var origin in value.Entry.Origins)
                Origins.Add(origin);
            foreach (var use in value.Entry.Uses)
                Uses.Add(use);
        }
        OnPropertyChanged(nameof(HasUses));

        _original = value == null || _content == null ? ""
            : _saved.TryGetValue(value.Name, out var saved) ? saved.Text
            : TextCatalog.Read(_content, value.Name) ?? "";
        _loadingSource = true;
        try
        {
            Source = _original;
        }
        finally
        {
            _loadingSource = false;
        }
        IsChanged = false;
        PageIndex = -1;
        Relayout();
    }

    partial void OnSourceChanged(string value)
    {
        if (_loadingSource)
            return;
        IsChanged = value != _original;
        Relayout();
    }

    // Lays the text out again, keeping the page shown when it's still there
    private void Relayout()
    {
        Problems.Clear();
        var entry = SelectedItem?.Entry;
        if (entry == null || _art == null || !ArticleLayout.IsArticle(Source) && entry.Format != TextFormat.Article)
        {
            Layout = null;
            ShowPage(-1);
            Details = entry == null ? ""
                : entry.Format == TextFormat.Presenter ? "A Blake Stone presenter script (^FC colors, ^SH shapes, ^XX ends it): shown as text only"
                : "Not an article (it doesn't start with ^P): shown as text only";
            Summary = "";
            OnPropertyChanged(nameof(HasProblems));
            return;
        }

        Layout = ArticleLayout.Build(Source, _art);
        if (_fontProblem != null)
            Problems.Add(new ArticleProblem(0, 1, _fontProblem, IsError: false));
        foreach (var problem in Layout.Problems)
            Problems.Add(problem);
        OnPropertyChanged(nameof(HasProblems));

        int errors = Layout.Problems.Count(problem => problem.IsError);
        Details = "A Wolf3D article: ^P starts each page, ^E ends the text";
        Summary = $"{Layout.Pages.Count} page{(Layout.Pages.Count == 1 ? "" : "s")}"
            + (errors > 0 ? $", {errors} error{(errors == 1 ? "" : "s")}: the game quits or crashes showing it" : "")
            + (Layout.Problems.Count > errors ? $", {Layout.Problems.Count - errors} warning{(Layout.Problems.Count - errors == 1 ? "" : "s")}" : "");

        ShowPage(Layout.Pages.Count == 0 ? -1 : Math.Clamp(PageIndex, 0, Layout.Pages.Count - 1));
    }

    // Draws this page, which may be the one shown already (the text has changed)
    private void ShowPage(int index)
    {
        if (index == PageIndex)
            ShowPage();
        else
            PageIndex = index;
    }

    partial void OnPageIndexChanged(int value) => ShowPage();

    private void ShowPage()
    {
        var old = PageImage;
        if (_content == null || Layout == null || PageIndex < 0 || PageIndex >= Layout.Pages.Count)
        {
            PageImage = null;
            PageText = Layout != null ? "No pages" : "";
        }
        else
        {
            var page = Layout.Pages[PageIndex];
            PageImage = ArtCache.Render(ArticleScreen.Draw(page, _content, _font, _backColor), _content.Palette);
            PageText = $"Page {page.Number} of {Layout.Pages.Count}" + (page.CutFrom != null ? "  (full: some text is left out)" : "");
        }
        old?.Dispose();
    }

    private bool CanGoBack => Layout != null && PageIndex > 0;
    private bool CanGoOn => Layout != null && PageIndex < Layout.Pages.Count - 1;

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void PreviousPage() => PageIndex--;

    [RelayCommand(CanExecute = nameof(CanGoOn))]
    private void NextPage() => PageIndex++;

    /// <summary>Shows the page the text at this index is laid out on: the caret moved in the text</summary>
    public void ShowPageOf(int index)
    {
        if (Layout?.PageAt(index) is { } page and >= 0 && page != PageIndex)
            PageIndex = page;
    }

    /// <summary>Goes to a problem: its page, and its spot in the text</summary>
    public void GoTo(ArticleProblem problem)
    {
        ShowPageOf(problem.Index);
        int index = Math.Min(problem.Index, Source.Length);
        SelectInSource?.Invoke(this, (index, LineLength(index)));
    }

    /// <summary>Goes to the spot in the text a point on the page comes from (in 320x200 pixels), when something's drawn there</summary>
    public void GoToPoint(double x, double y)
    {
        if (Layout == null || PageIndex < 0 || PageIndex >= Layout.Pages.Count || _art == null)
            return;

        // The last drawn on top wins, as it's what shows
        foreach (var draw in Layout.Pages[PageIndex].Draws.Reverse())
        {
            if (draw.Index < 0)
                continue;
            var (width, height) = draw.Kind switch
            {
                ArticleDrawKind.Text => (draw.Text.Sum(_art.Advance), ArticleLayout.FontHeight),
                ArticleDrawKind.Bar => (draw.Width, draw.Height),
                _ => _art.PictureSize(draw.Text) ?? (0, 0),
            };
            if (x >= draw.X && x < draw.X + width && y >= draw.Y && y < draw.Y + height)
            {
                int length = draw.Kind == ArticleDrawKind.Text ? draw.Text.Length : LineLength(draw.Index);
                SelectInSource?.Invoke(this, (draw.Index, length));
                return;
            }
        }
    }

    private int LineLength(int index)
    {
        int end = index;
        while (end < Source.Length && Source[end] is not ('\r' or '\n'))
            end++;
        return end - index;
    }

    /// <summary>Back to the text as the game has it (or as it was last saved)</summary>
    [RelayCommand(CanExecute = nameof(IsChanged))]
    private void Revert() => Source = _original;

    //
    // Saving
    //

    /// <summary>Saves the text to its mod (a folder or a pk3): the one it comes from or was saved to (asking for one the first time)</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task SaveAsync()
    {
        if (SelectedItem is { } item)
            await SaveText(item, SaveFolderOf(item.Name));
    }

    /// <summary>Saves the text to a mod picked now</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task SaveToAsync()
    {
        if (SelectedItem is { } item)
            await SaveText(item, null);
    }

    // The mod a text was saved to, else the one it comes from (null for the game's own)
    private string? SaveFolderOf(string name)
        => _saved.TryGetValue(name, out var saved) ? saved.Folder : _content?.ModPathOf(name, nameof(TextAsset));

    /// <summary>Saves a text to {folder}/texts/NAME.txt, asking for a folder when there's none; false when it isn't saved</summary>
    public async Task<bool> SaveText(TextItem item, string? folder)
    {
        folder ??= PickMod == null ? null : await PickMod($"A mod to save {item.Name} in (it goes in its texts folder)");
        if (folder == null)
            return false;

        var text = Source;
        try
        {
            var path = TextCatalog.Save(folder, item.Name, text);
            _saved[item.Name] = (text, folder);
            _original = text;
            IsChanged = false;
            Status = $"Saved {path}" + (item.Entry.Uses.Count == 0
                ? $". Nothing shows it yet: name {item.Name} in game-info (a cluster's end-text, say)"
                : "");
            Saved?.Invoke(this, folder);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Status = $"Couldn't save {item.Name}: {e.Message}";
            return false;
        }
    }

    /// <summary>Starts a new text, by a name asked for, from a page to fill in: saving it adds it to a mod</summary>
    [RelayCommand]
    private async Task NewTextAsync()
    {
        if (_content is not { } content || AskText == null)
            return;

        var name = await AskText("New text", "Its name, which game-info shows it by (a cluster's end-text, say), and its file in texts/:",
            "", candidate => TextCatalog.CheckNewName(content, candidate.Trim())
                ?? (_all.Any(item => item.Name.Equals(candidate.Trim(), StringComparison.OrdinalIgnoreCase)) ? $"There's a {candidate.Trim().ToUpperInvariant()} already" : null));
        if (string.IsNullOrWhiteSpace(name))
            return;

        name = name.Trim().ToUpperInvariant();
        var isBlake = TextCatalog.IsBlake(content);
        var entry = new TextEntry(name, isBlake ? TextFormat.Presenter : TextFormat.Article, ["new: not saved yet"],
            TextCatalog.Uses(content).GetValueOrDefault(name, []))
        {
            Source = "new",
        };
        var item = new TextItem(entry);
        _all.Add(item);
        _all.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
        Filter = "";
        Refilter();
        SelectedItem = item;
        Source = isBlake ? TextCatalog.NewPresenterScript : TextCatalog.NewArticle;
        Status = $"{name} is new: Save puts it in a mod's texts folder";
    }

    public void Dispose()
    {
        PageImage?.Dispose();
        PageImage = null;
    }
}
