using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets;
using PFWolf.Editor.Editing;
using PFWolf.Editor.Rendering;
using PFWolf.Fonts;

namespace PFWolf.Editor.ViewModels;

/// <summary>
/// The glyph editor: a Wolf3D (vga) font's letters drawn a pixel at a time, each one's width, the
/// font's height, a line of text in it as it's drawn, saved into a mod as fonts/NAME.fnt
/// </summary>
public sealed partial class GlyphEditorViewModel : ObservableObject
{
    // Copied glyphs go between fonts too
    private static GlyphData? _clipboard;

    private readonly PaletteColor[] _palette;
    private readonly ColorThemeAsset? _theme;
    private readonly Func<string, FontAsset?> _findFont;
    private readonly Func<FontDocument, Task<bool>> _save;
    private CharacterGrid? _grid;
    private (int X, int Y)? _last;
    private bool _drawing, _erasing;

    /// <param name="otherFonts">The game's other Wolf3D fonts, for copying in the characters this one hasn't got</param>
    /// <param name="findFont">One of those by name</param>
    /// <param name="save">Saves the font into a mod; false when it isn't saved</param>
    public GlyphEditorViewModel(FontDocument document, PaletteColor[] palette, ColorThemeAsset? theme, IReadOnlyList<string> otherFonts,
        Func<string, FontAsset?> findFont, Func<FontDocument, Task<bool>> save)
    {
        Document = document;
        _palette = palette;
        _theme = theme;
        _findFont = findFont;
        _save = save;
        OtherFonts = [.. otherFonts.Where(name => !name.Equals(document.Name, StringComparison.OrdinalIgnoreCase))];
        _copyFrom = OtherFonts.FirstOrDefault();
        _textColor = theme?.Colors.Keys.FirstOrDefault(name => name.Equals("White", StringComparison.OrdinalIgnoreCase)) ?? "15";
        _selectedCode = document.Has('A') ? 'A' : Enumerable.Range(33, 223).FirstOrDefault(document.Has, ' ');
        document.Changed += (_, _) => Refresh();
        Refresh();
    }

    public FontDocument Document { get; }
    public string Name => Document.Name;
    public string Title => $"Glyphs - {Name}{(Document.IsDirty ? " *" : "")}";

    public IReadOnlyList<string> OtherFonts { get; }

    [ObservableProperty] private string? _copyFrom;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _info = "";

    /// <summary>The character being drawn</summary>
    [ObservableProperty] private int _selectedCode;

    partial void OnSelectedCodeChanged(int value) => Refresh();

    [ObservableProperty] private string _testText = "The quick brown fox jumps over the lazy dog 0123456789";

    partial void OnTestTextChanged(string value) => Refresh();

    [ObservableProperty] private string _textColor;

    partial void OnTextColorChanged(string value) => Refresh();

    [ObservableProperty] private int _zoom = 24;

    // What's shown: the glyph big, the grid of characters, the line of text
    [ObservableProperty] private Bitmap? _glyphImage;
    [ObservableProperty] private Bitmap? _gridImage;
    [ObservableProperty] private double _gridWidth;
    [ObservableProperty] private double _gridHeight;
    [ObservableProperty] private Bitmap? _testImage;
    [ObservableProperty] private double _testWidth;
    [ObservableProperty] private double _testHeight;

    /// <summary>The glyph picked as drawn (palette indices); null when it has no width</summary>
    public IndexedPicture? GlyphPicture { get; private set; }

    /// <summary>The line of text as drawn in the font as it is now</summary>
    public IndexedPicture? TestPicture { get; private set; }

    public bool HasGlyph => Document.Has(SelectedCode);

    /// <summary>The picked character's width (0 takes it out of the font)</summary>
    public decimal? GlyphWidth
    {
        get => Document.Width(SelectedCode);
        set
        {
            if (value is { } width && (int)width != Document.Width(SelectedCode))
                Document.SetWidth(SelectedCode, (int)width);
        }
    }

    /// <summary>The font's height (rows added or cut at the bottom)</summary>
    public decimal? FontHeight
    {
        get => Document.Height;
        set
        {
            if (value is { } height && (int)height != Document.Height)
                Document.SetHeight((int)height);
        }
    }

    private IndexedTextCanvas Canvas(int width, int height) => new(width, height, _palette, _theme);

    private Bitmap? Bitmap(IndexedPicture picture) => Avalonia.Application.Current == null ? null : ArtCache.Render(picture, _palette);

    private void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(GlyphWidth));
        OnPropertyChanged(nameof(FontHeight));
        OnPropertyChanged(nameof(HasGlyph));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        PasteGlyphCommand.NotifyCanExecuteChanged();

        // The glyph, a pixel a cell: the text color on dark grey
        int code = SelectedCode, width = Document.Width(code), height = Document.Height;
        var paper = Canvas(1, 1);
        byte ink = paper.ResolveColor(TextColor), back = paper.ResolveColor("#28282E");
        if (width > 0)
        {
            var pixels = new byte[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = Document.Pixel(code, x, y) ? ink : back;
            GlyphPicture = new IndexedPicture(width, height, pixels, null);
        }
        else
            GlyphPicture = null;
        GlyphImage = GlyphPicture == null ? null : Bitmap(GlyphPicture);

        // The font as it is now, as the game draws it
        var asset = Document.ToAsset();
        TestPicture = null;
        if (asset != null)
        {
            var font = new VgaFont(asset);
            _grid = CharacterGrid.Draw(font, Canvas, TextColor, "0", SelectedCode);
            GridImage = Bitmap(_grid.Picture);
            (GridWidth, GridHeight) = (_grid.Picture.Width * 2, _grid.Picture.Height * 2);

            int lineWidth = Math.Clamp(font.Measure(TestText) + 4, 8, 2048), lineHeight = height + 4;
            var canvas = Canvas(lineWidth, lineHeight);
            font.Draw(canvas, 2, 2, TestText, TextColor);
            TestPicture = new IndexedPicture(lineWidth, lineHeight, canvas.Pixels, null);
            TestImage = Bitmap(TestPicture);
            (TestWidth, TestHeight) = (lineWidth * 3, lineHeight * 3);
        }

        var count = Enumerable.Range(0, 256).Count(Document.Has);
        int bytes;
        string size;
        try
        {
            bytes = Document.ToFile().Length;
            size = $"{bytes:N0} bytes";
        }
        catch (InvalidDataException e)
        {
            size = e.Message;
        }
        Info = $"{CharacterGrid.Describe(code)}: {(width > 0 ? $"{width} wide" : "not in the font")}   ·   {height} high, {count} characters, {size}";
    }

    //
    // The grid and the canvas
    //

    /// <summary>Picks the character clicked on the grid (in its own pixels)</summary>
    public void PickAt(double x, double y)
    {
        if (_grid?.CodeAt(x, y) is int code)
            SelectedCode = code;
    }

    /// <summary>What's under the mouse on the grid</summary>
    public string CharacterAt(double x, double y)
        => _grid?.CodeAt(x, y) is int code ? $"{CharacterGrid.Describe(code)}{(Document.Has(code) ? $", {Document.Width(code)} wide" : ", not in the font")}" : "";

    /// <summary>A press on the glyph: the left button draws, the right erases; a drag is one step</summary>
    public void Press(int x, int y, bool erase)
    {
        _erasing = erase;
        _drawing = true;
        Document.BeginEdit(erase ? $"Erased in {SelectedCode}" : $"Drew in {SelectedCode}");
        Document.SetPixel(SelectedCode, x, y, !erase);
        _last = (x, y);
    }

    public void Drag(int x, int y)
    {
        if (!_drawing || _last is not { } last)
            return;
        Document.Line(SelectedCode, last.X, last.Y, x, y, !_erasing);
        _last = (x, y);
    }

    public void Release()
    {
        _drawing = false;
        _last = null;
        Document.EndEdit();
        Refresh();
    }

    //
    // Tools
    //

    [RelayCommand] private void WiderRight() => Document.SetWidth(SelectedCode, Document.Width(SelectedCode) + 1);
    [RelayCommand] private void NarrowerRight() => Document.SetWidth(SelectedCode, Document.Width(SelectedCode) - 1);
    [RelayCommand] private void WiderLeft() => Document.SetWidth(SelectedCode, Document.Width(SelectedCode) + 1, atLeft: true);
    [RelayCommand] private void NarrowerLeft() => Document.SetWidth(SelectedCode, Document.Width(SelectedCode) - 1, atLeft: true);
    [RelayCommand] private void ShiftLeft() => Document.Shift(SelectedCode, -1, 0);
    [RelayCommand] private void ShiftRight() => Document.Shift(SelectedCode, 1, 0);
    [RelayCommand] private void ShiftUp() => Document.Shift(SelectedCode, 0, -1);
    [RelayCommand] private void ShiftDown() => Document.Shift(SelectedCode, 0, 1);
    [RelayCommand] private void FlipAcross() => Document.Flip(SelectedCode, vertical: false);
    [RelayCommand] private void FlipOver() => Document.Flip(SelectedCode, vertical: true);
    [RelayCommand] private void ClearGlyph() => Document.Clear(SelectedCode);
    [RelayCommand] private void RemoveGlyph() => Document.Remove(SelectedCode);

    [RelayCommand] private void RowsAtTopMore() => Document.SetHeight(Document.Height + 1, atTop: true);
    [RelayCommand] private void RowsAtTopFewer() => Document.SetHeight(Document.Height - 1, atTop: true);
    [RelayCommand] private void RowsAtBottomMore() => Document.SetHeight(Document.Height + 1);
    [RelayCommand] private void RowsAtBottomFewer() => Document.SetHeight(Document.Height - 1);

    [RelayCommand]
    private void CopyGlyph()
    {
        _clipboard = Document.Copy(SelectedCode);
        PasteGlyphCommand.NotifyCanExecuteChanged();
        Status = $"Copied {CharacterGrid.Describe(SelectedCode)}";
    }

    private static bool CanPaste => _clipboard != null;

    [RelayCommand(CanExecute = nameof(CanPaste))]
    private void PasteGlyph()
    {
        if (_clipboard is { } glyph)
            Document.Paste(SelectedCode, glyph);
    }

    /// <summary>The characters this font hasn't got, copied from the font picked</summary>
    [RelayCommand]
    private void CopyMissing()
    {
        if (CopyFrom is not { } name || _findFont(name) is not { } other)
        {
            Status = "Pick a font to copy the missing characters from";
            return;
        }
        var copied = Document.CopyMissingFrom(other);
        Status = copied == 0 ? $"{name} has none of the characters {Name} hasn't got" : $"Copied {copied} characters from {name}";
    }

    private bool CanUndo => Document.CanUndo;
    private bool CanRedo => Document.CanRedo;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        var label = Document.UndoLabel;
        Document.Undo();
        Status = $"Undid: {label}";
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        var label = Document.RedoLabel;
        Document.Redo();
        Status = $"Redid: {label}";
    }

    /// <summary>Saves the font into a mod as fonts/NAME.fnt</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            Document.ToFile();
        }
        catch (InvalidDataException e)
        {
            Status = $"Not saved: {e.Message}";
            return;
        }
        if (await _save(Document))
        {
            Document.MarkSaved();
            Status = $"Saved {Name}";
            Refresh();
        }
    }

    /// <summary>The font file as it is now, for Export</summary>
    public byte[] ToFile() => Document.ToFile();
}
