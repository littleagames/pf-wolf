using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Editor.Rendering;
using PFWolf.Fonts;

namespace PFWolf.Editor.ViewModels;

/// <summary>A font in the font window's list</summary>
public sealed partial class FontItem(FontEntry entry, Font? font, IReadOnlyList<string> problems) : ObservableObject
{
    /// <summary>A new font made here, not saved in a mod yet</summary>
    public bool IsDraft { get; init; }

    /// <summary>Changed here, not saved</summary>
    [ObservableProperty] private bool _hasEdits;

    public FontEntry Entry { get; } = entry;

    /// <summary>The font as the game builds it; null when it can't be built</summary>
    public Font? Font { get; } = font;

    /// <summary>What's wrong with it (what the game would say building it, and what it wouldn't)</summary>
    public IReadOnlyList<string> Problems { get; } = problems;

    public string Name => Entry.Name;
    public string TypeText => Entry.IsDefined ? Entry.Type : "vga (no definition)";
    public string HeightText => Font is { } font ? $"{font.Height} high" : "can't be made";
    public bool HasProblems => Problems.Count > 0;
    public string ToolTip => HasProblems ? string.Join("\n", Problems) : $"{TypeText}, {HeightText}";

    public override string ToString() => Name;
}

/// <summary>A filter of the font list</summary>
public sealed record FontTypeOption(string? Type, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// The font window: every font the game prints in, drawn as the game draws it, with which
/// characters it has, where it's defined, what prints in it and what's wrong with it
/// </summary>
public sealed partial class FontBrowserViewModel : ObservableObject
{
    public const string DefaultSample = "The quick brown fox jumps over the lazy dog 0123456789";

    private GameContent? _content;
    private List<FontItem> _all = [];
    private ColorThemeAsset? _theme;

    public FontBrowserViewModel()
    {
        _selectedType = Types[0];
    }

    public IReadOnlyList<FontTypeOption> Types { get; } =
    [
        new(null, "All"),
        new("vga", "vga (Wolf3D fonts)"),
        new("graphic", "graphic (a picture each)"),
        new("sheet", "sheet (one picture)"),
        new("problems", "With problems"),
    ];

    public ObservableCollection<FontItem> Items { get; } = [];
    public ObservableCollection<FontUse> Uses { get; } = [];
    public ObservableCollection<string> Problems { get; } = [];
    public ObservableCollection<string> DefinedIn { get; } = [];

    /// <summary>The colors.yaml names, for the color pickers</summary>
    public ObservableCollection<string> ColorNames { get; } = [];

    [ObservableProperty] private FontTypeOption _selectedType;
    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private string _countText = "";
    [ObservableProperty] private string _title = "Fonts";
    [ObservableProperty] private string _status = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(CanExportFont))]
    [NotifyCanExecuteChangedFor(nameof(CopyNameCommand))]
    private FontItem? _selectedItem;

    public bool HasSelection => SelectedItem != null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoToUseCommand))]
    private FontUse? _selectedUse;

    [ObservableProperty] private string _details = "";
    [ObservableProperty] private string _definitionYaml = "";
    [ObservableProperty] private string _coverageText = "";

    // The preview: what it says, in what colors, how big
    [ObservableProperty] private string _sampleText = DefaultSample;
    [ObservableProperty] private string _textColor = "White";
    [ObservableProperty] private string _backgroundColor = "0";
    [ObservableProperty] private int _zoom = 3;

    [ObservableProperty] private Bitmap? _previewImage;
    [ObservableProperty] private double _previewWidth;
    [ObservableProperty] private double _previewHeight;

    [ObservableProperty] private Bitmap? _charactersImage;
    [ObservableProperty] private double _charactersWidth;
    [ObservableProperty] private double _charactersHeight;

    /// <summary>A use's Go to: the Definitions window on an entry, or the script browser on a line</summary>
    public event EventHandler<FontUseTarget>? GoTo;

    public void Load(GameContent? content)
    {
        _content = content;
        var picked = SelectedItem?.Name;
        _all = [];
        ColorNames.Clear();
        if (content == null)
        {
            Title = "Fonts";
            Refilter();
            return;
        }

        _theme = content.Assets.FindInGamePackIfAny<ColorThemeAsset>("colors");
        LoadStyles(content);
        foreach (var name in _theme?.Colors.Keys.Order(StringComparer.OrdinalIgnoreCase) ?? Enumerable.Empty<string>())
            ColorNames.Add(name);
        if (!ColorNames.Contains(TextColor))
            TextColor = ColorNames.FirstOrDefault(name => name.Equals("White", StringComparison.OrdinalIgnoreCase)) ?? "15";
        if (ColorNames.FirstOrDefault(name => name.Equals("BKGDCOLOR", StringComparison.OrdinalIgnoreCase)) is { } menuBack && BackgroundColor == "0")
            BackgroundColor = menuBack;

        foreach (var entry in FontCatalog.Build(content))
        {
            var problems = new List<string>();
            var canvas = Canvas(1, 1);
            var font = FontCatalog.BuildFont(content, entry, canvas, problems);
            problems.AddRange(FontCatalog.Notes(entry));
            if (font != null)
            {
                // Colors in the definition (shadow, outline, glow) the game won't know
                font.Draw(canvas, 0, 0, "A", "0");
                problems.AddRange(canvas.UnknownColors.Select(color => $"There's no color '{color}' in colors.yaml (it draws palette index 0)"));
            }
            if (entry.IsDefined && entry.Uses.Count == 0)
                problems.Add("Nothing names it: no game-info, menu, message style, status bar, intermission, presenter or script (Lua's game.drawtext can still name it in a way the editor can't see)");
            _all.Add(new FontItem(entry, font, problems) { HasEdits = _edits.ContainsKey(entry.Name) });
        }
        // New fonts not saved yet stay listed
        foreach (var name in _drafts.Keys)
            _all.Add(DraftItem(content, name));
        _all.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        Title = $"Fonts - {content.Title}";
        Refilter();
        SelectedItem = Items.FirstOrDefault(item => item.Name.Equals(picked, StringComparison.OrdinalIgnoreCase)) ?? Items.FirstOrDefault();
    }

    /// <summary>Picks a font by name (the list's filter cleared if it hides it)</summary>
    public void Show(string name)
    {
        if (_all.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is not { } item)
            return;
        if (!Items.Contains(item))
        {
            Filter = "";
            SelectedType = Types[0];
        }
        SelectedItem = item;
    }

    partial void OnFilterChanged(string value) => Refilter();
    partial void OnSelectedTypeChanged(FontTypeOption value) => Refilter();

    private void Refilter()
    {
        var picked = SelectedItem;
        Items.Clear();
        foreach (var item in _all.Where(Shows))
            Items.Add(item);
        CountText = Items.Count == _all.Count ? $"{_all.Count} fonts" : $"{Items.Count} of {_all.Count} fonts";
        if (picked != null && Items.Contains(picked))
            SelectedItem = picked;
    }

    private bool Shows(FontItem item)
    {
        var type = SelectedType?.Type;
        if (type == "problems" ? !item.HasProblems : type != null && !item.Entry.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.IsNullOrWhiteSpace(Filter))
            return true;
        var words = Filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.All(word => item.Name.Contains(word, StringComparison.OrdinalIgnoreCase)
                                 || item.Entry.SourceName?.Contains(word, StringComparison.OrdinalIgnoreCase) == true
                                 || item.Entry.Uses.Any(use => use.Label.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    partial void OnSelectedItemChanged(FontItem? value)
    {
        Uses.Clear();
        Problems.Clear();
        DefinedIn.Clear();
        Details = "";
        DefinitionYaml = "";
        CoverageText = "";
        if (value == null)
        {
            OpenStyle(null);
            Redraw();
            return;
        }

        var entry = value.Entry;
        foreach (var use in entry.Uses)
            Uses.Add(use);
        // None picked: picking one would scroll the panel down to it
        SelectedUse = null;
        foreach (var problem in value.Problems)
            Problems.Add(problem);
        foreach (var site in entry.DefinedIn)
            DefinedIn.Add(site.File);
        DefinitionYaml = entry.DefinedIn.Count > 0 ? entry.DefinedIn[^1].Yaml : "";
        Details = Describe(value);
        OpenStyle(value);
    }

    private string Describe(FontItem item)
    {
        var entry = item.Entry;
        var what = entry.Type switch
        {
            "vga" => entry.IsDefined ? "A Wolf3D font: one-bit glyphs drawn in the text's color" : "A Wolf3D font with no fonts.yaml definition: printed in by its own name",
            "graphic" => "A picture for each character",
            "sheet" => "Characters cut from one picture",
            _ => $"A '{entry.Type}' font, which the game doesn't know",
        };
        var lines = new List<string> { what };
        if (item.Font is { } font)
            lines.Add(font.LineHeight != font.Height ? $"{font.Height} pixels high, lines {font.LineHeight} apart" : $"{font.Height} pixels high");
        if (entry.SourceName != null)
        {
            var origin = entry.SourceOrigins.LastOrDefault(origin => origin.Action != PFWolf.Loaders.AssetOrigin.LeftOut);
            lines.Add(origin != null ? $"Glyphs: {entry.SourceName}, from {origin.Source}: {origin.Path}" : $"Glyphs: {entry.SourceName}");
        }
        else if (entry.Definition is { } definition)
        {
            var pictures = entry.Type == "sheet" ? definition.Image is { } image ? [image] : new List<string>()
                : FontBuilder.GlyphPictures(definition).Values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (pictures.Count > 0)
                lines.Add($"Pictures: {string.Join(", ", pictures.Take(6))}{(pictures.Count > 6 ? $" and {pictures.Count - 6} more" : "")}");
        }
        return string.Join("\n", lines);
    }

    partial void OnSampleTextChanged(string value) => Redraw();
    partial void OnTextColorChanged(string value) => Redraw();
    partial void OnBackgroundColorChanged(string value) => Redraw();
    partial void OnZoomChanged(int value) => Redraw();

    private IndexedTextCanvas Canvas(int width, int height)
        => new(width, height, _content?.Palette ?? [], _theme);

    // How far a font's shadow, outline and glow reach past its letters, so the preview leaves room
    private static int Reach(Font font)
    {
        int outline = font.Outline is { } o && o != FontOutline.None ? o.Thickness : 0;
        int glow = font.Glow is { } g && g != FontGlow.None ? g.Radius : 0;
        int shadow = font.Shadow is { } s && s != FontShadow.None ? Math.Max(Math.Abs(s.X), Math.Abs(s.Y)) : 0;
        return outline + Math.Max(glow, shadow) + 2;
    }

    /// <summary>The preview as drawn (palette indices), whether or not there's a window to show it in</summary>
    public IndexedPicture? PreviewPicture { get; private set; }

    // A picture for the window; none without one (tests)
    private Bitmap? Bitmap(IndexedPicture picture)
        => Avalonia.Application.Current == null || _content == null ? null : ArtCache.Render(picture, _content.Palette);

    /// <summary>Draws the preview and the character grid again</summary>
    public void Redraw()
    {
        PreviewImage = null;
        PreviewPicture = null;
        CharactersImage = null;
        if (_content == null || PreviewFont is not { } font)
            return;

        var zoom = Math.Clamp(Zoom, 1, 8);
        var color = string.IsNullOrWhiteSpace(TextColor) ? "15" : TextColor.Trim();
        var background = string.IsNullOrWhiteSpace(BackgroundColor) ? "0" : BackgroundColor.Trim();

        // The sample, a line each, with the font's own shadow, outline, gradient and glow
        var lines = (string.IsNullOrEmpty(SampleText) ? " " : SampleText).Replace("\r\n", "\n").Split('\n');
        int reach = Reach(font);
        int width = Math.Clamp(lines.Max(font.Measure) + reach * 2, 8, 2048);
        int height = Math.Clamp(lines.Length * Math.Max(font.LineHeight, 1) + reach * 2, 8, 2048);
        var canvas = Canvas(width, height);
        Array.Fill(canvas.Pixels, canvas.ResolveColor(background));
        for (int i = 0; i < lines.Length; i++)
            font.Draw(canvas, reach, reach + i * font.LineHeight, lines[i], color, font.Shadow, font.Gradient, font.Outline, font.Glow, background);
        PreviewPicture = new IndexedPicture(width, height, canvas.Pixels, null);
        PreviewImage = Bitmap(PreviewPicture);
        (PreviewWidth, PreviewHeight) = (width * zoom, height * zoom);

        DrawCharacters(font, color, background, zoom);
    }

    /// <summary>The character grid: 16 across, codes 0-255, cells this big</summary>
    public (int CellWidth, int CellHeight) CharacterCell { get; private set; }

    private void DrawCharacters(Font font, string color, string background, int zoom)
    {
        int glyphWidth = Enumerable.Range(0, 256).Where(code => font.Has((char)code)).Select(code => font.Advance((char)code)).DefaultIfEmpty(4).Max();
        int cellWidth = Math.Max(glyphWidth, 4) + 2, cellHeight = Math.Max(font.Height, 4) + 2;
        CharacterCell = (cellWidth, cellHeight);
        var canvas = Canvas(cellWidth * 16 + 1, cellHeight * 16 + 1);
        byte back = canvas.ResolveColor(background), missing = canvas.ResolveColor("#3C3C3C"), lines = canvas.ResolveColor("#606060");
        Array.Fill(canvas.Pixels, lines);

        int has = 0;
        for (int code = 0; code < 256; code++)
        {
            int x = code % 16 * cellWidth + 1, y = code / 16 * cellHeight + 1;
            bool present = font.Has((char)code);
            has += present ? 1 : 0;
            for (int row = 0; row < cellHeight - 1; row++)
                Array.Fill(canvas.Pixels, present ? back : missing, (y + row) * canvas.Width + x, cellWidth - 1);
            // The glyph plain, without the font's effects, so each one's own pixels show
            if (present)
                font.Draw(canvas, x, y, ((char)code).ToString(), color, FontShadow.None, FontGradient.None, FontOutline.None, FontGlow.None, background);
        }
        CoverageText = $"{has} of 256 characters";
        CharactersImage = Bitmap(new IndexedPicture(canvas.Width, canvas.Height, canvas.Pixels, null));
        (CharactersWidth, CharactersHeight) = (canvas.Width * zoom, canvas.Height * zoom);
    }

    /// <summary>What's under the mouse on the character grid (in its own pixels)</summary>
    public string CharacterAt(double x, double y)
    {
        if (PreviewFont is not { } font || CharacterCell.CellWidth == 0)
            return "";
        int column = (int)(x / CharacterCell.CellWidth), row = (int)(y / CharacterCell.CellHeight);
        if (column is < 0 or > 15 || row is < 0 or > 15)
            return "";
        var ch = (char)(row * 16 + column);
        var shown = ch is < ' ' or (>= '\x7f' and < '\xa0') ? "" : $" '{ch}'";
        return font.Has(ch) ? $"Code {(int)ch}{shown}: {font.Advance(ch)} wide" : $"Code {(int)ch}{shown}: none (leaves {font.Advance(ch)} pixels)";
    }

    //
    // Doing things with the font picked
    //

    /// <summary>The picked font's Wolf3D font file (a vga font's glyphs), for Export</summary>
    public (string Name, byte[] Data)? ExportableFont
        => _content != null && SelectedItem?.Entry.SourceName is { } source && _content.Find<FontAsset>(source) is { } asset
            ? (source, asset.RawData)
            : null;

    public bool CanExportFont => ExportableFont != null;

    private bool HasPicked => SelectedItem != null;

    /// <summary>Asks the window to put text on the clipboard</summary>
    public event EventHandler<string>? CopyText;

    [RelayCommand(CanExecute = nameof(HasPicked))]
    private void CopyName()
    {
        if (SelectedItem is { } item)
        {
            CopyText?.Invoke(this, item.Name);
            Status = $"Copied {item.Name}";
        }
    }

    private bool CanGoToUse => SelectedUse?.Target != null;

    [RelayCommand(CanExecute = nameof(CanGoToUse))]
    private void GoToUse()
    {
        if (SelectedUse?.Target is { } target)
            GoTo?.Invoke(this, target);
    }
}
