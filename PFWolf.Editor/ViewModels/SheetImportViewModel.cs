using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;
using PFWolf.Editor.Rendering;
using PFWolf.Fonts;
using PFWolf.Loaders;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.ViewModels;

/// <summary>
/// Bringing in a sheet of characters (a PNG or BMP of cells): its cells set (guessed first) and
/// shown over it, and the font it makes drawn: a sheet font (the picture and a fonts.yaml entry
/// saved into a mod) or a vga font (its glyphs to the glyph editor, to save from there)
/// </summary>
public sealed partial class SheetImportViewModel : ObservableObject
{
    private readonly RgbaImage _image;
    private readonly PaletteColor[] _palette;
    private readonly ColorThemeAsset? _theme;
    private readonly GraphicAsset _picture;
    private readonly Func<string, bool, string?> _nameProblem;

    /// <param name="nameProblem">What's wrong with a name for the font (a vga font's, or a sheet font's), or null</param>
    public SheetImportViewModel(string name, byte[] file, RgbaImage image, PaletteColor[] palette, ColorThemeAsset? theme, Func<string, bool, string?> nameProblem)
    {
        File = file;
        _image = image;
        _palette = palette;
        _theme = theme;
        _nameProblem = nameProblem;
        _picture = FontSheets.ToPicture(image, palette);
        _name = name;
        var cells = FontSheets.Guess(image.Width, image.Height);
        // A sheet with no transparency usually has a background color: its corner's
        Corner = FontSheets.CornerColor(image);
        _keyOutCorner = Corner != null && !FontSheets.HasTransparency(image);
        (_cellWidth, _cellHeight, _firstChar) = (cells.CellWidth, cells.CellHeight, cells.FirstChar);
        _textColor = theme?.Colors.Keys.FirstOrDefault(key => key.Equals("White", StringComparison.OrdinalIgnoreCase)) ?? "15";
        Refresh();
    }

    /// <summary>The picture file as it came</summary>
    public byte[] File { get; }

    public string Size => $"{_image.Width} x {_image.Height}";

    /// <summary>Saves a sheet font into a mod (its name, the picture, its fonts.yaml entry); false when it isn't</summary>
    public Func<string, byte[], YamlMappingNode, Task<bool>>? SaveSheet { get; init; }

    /// <summary>Opens a vga font made from the sheet in the glyph editor</summary>
    public Action<FontDocument>? OpenVga { get; init; }

    /// <summary>The dialog's work is done (saved, or the glyph editor opened): it closes</summary>
    public event EventHandler? Done;

    [ObservableProperty] private string _name;
    [ObservableProperty] private decimal? _cellWidth;
    [ObservableProperty] private decimal? _cellHeight;
    [ObservableProperty] private decimal? _firstChar;

    /// <summary>The characters in the cells, in order, for a sheet that isn't in code order; empty: from the first character on</summary>
    [ObservableProperty] private string _chars = "";

    /// <summary>Make a vga font of it (one-bit glyphs, edited in the glyph editor) rather than a sheet font</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AsSheet), nameof(DoneLabel))]
    private bool _asVga;

    public bool AsSheet => !AsVga;

    /// <summary>The sheet's top-left color, null when it's see-through</summary>
    public (byte R, byte G, byte B)? Corner { get; }

    public bool HasCorner => Corner != null;

    public string CornerText => Corner is var (r, g, b) ? $"See-through: the corner's color (#{r:X2}{g:X2}{b:X2})" : "";

    /// <summary>Leave out the sheet's background (the corner's color), as see-through</summary>
    [ObservableProperty] private bool _keyOutCorner;

    private (byte R, byte G, byte B)? Key => KeyOutCorner ? Corner : null;

    // A sheet font's settings
    [ObservableProperty] private bool _proportional = true;
    [ObservableProperty] private bool _colorize;

    // A vga font's: how bright a pixel is to draw, and the gap after each glyph
    [ObservableProperty] private decimal? _threshold = 128;
    [ObservableProperty] private decimal? _spacing = 1;

    [ObservableProperty] private string _sampleText = "The quick brown fox jumps over the lazy dog 0123456789";
    [ObservableProperty] private string _textColor;

    [ObservableProperty] private Bitmap? _sheetImage;
    [ObservableProperty] private double _sheetWidth;
    [ObservableProperty] private double _sheetHeight;
    [ObservableProperty] private Bitmap? _previewImage;
    [ObservableProperty] private double _previewWidth;
    [ObservableProperty] private double _previewHeight;
    [ObservableProperty] private string _info = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    [NotifyCanExecuteChangedFor(nameof(DoneCommand))]
    private string _problem = "";

    public bool HasProblem => Problem.Length > 0;

    public string DoneLabel => AsVga ? "Open in the glyph editor…" : "Save to mod…";

    /// <summary>The preview as drawn (palette indices)</summary>
    public IndexedPicture? PreviewPicture { get; private set; }

    partial void OnNameChanged(string value) => Refresh();
    partial void OnCellWidthChanged(decimal? value) => Refresh();
    partial void OnCellHeightChanged(decimal? value) => Refresh();
    partial void OnFirstCharChanged(decimal? value) => Refresh();
    partial void OnCharsChanged(string value) => Refresh();
    partial void OnAsVgaChanged(bool value) => Refresh();
    partial void OnProportionalChanged(bool value) => Refresh();
    partial void OnColorizeChanged(bool value) => Refresh();
    partial void OnKeyOutCornerChanged(bool value) => Refresh();
    partial void OnThresholdChanged(decimal? value) => Refresh();
    partial void OnSpacingChanged(decimal? value) => Refresh();
    partial void OnSampleTextChanged(string value) => Refresh();
    partial void OnTextColorChanged(string value) => Refresh();

    public SheetCells Cells => new(Math.Clamp((int)(CellWidth ?? 8), 1, 255), Math.Clamp((int)(CellHeight ?? 8), 1, 255), Math.Clamp((int)(FirstChar ?? 32), 0, 255), string.IsNullOrEmpty(Chars) ? null : Chars);

    private IndexedTextCanvas Canvas(int width, int height) => new(width, height, _palette, _theme);

    private Bitmap? Bitmap(IndexedPicture picture) => Avalonia.Application.Current == null ? null : ArtCache.Render(picture, _palette);

    /// <summary>The sheet font's fonts.yaml entry</summary>
    public YamlMappingNode SheetDefinition => FontSheets.SheetDefinition(Name.Trim(), Cells, Proportional, Colorize, Key);

    /// <summary>The vga font the sheet makes</summary>
    public FontDocument VgaFont() => FontSheets.ToVgaFont(Name.Trim(), _image, Cells, (int)Math.Clamp(Threshold ?? 128, 0, 255), (int)Math.Clamp(Spacing ?? 1, 0, 16), Key);

    // The font as it would be made
    private Font? MakeFont(List<string> problems)
    {
        if (AsVga)
            return VgaFont().ToAsset() is { } asset ? new VgaFont(asset) : null;
        var definition = YamlDataEntryLoader.DeserializeQuietly<FontDefinition>(YamlTree.ToText(SheetDefinition));
        var image = Name.Trim();
        return new FontBuilder(_ => null, picture => picture.Equals(image, StringComparison.OrdinalIgnoreCase) ? _picture : null,
            color => Canvas(1, 1).ResolveColor(color), problems.Add).Build(image, definition);
    }

    private void Refresh()
    {
        var cells = Cells;
        var count = cells.Count(_image.Width, _image.Height);
        var problems = new List<string>();
        if (_nameProblem(Name.Trim(), AsVga) is { } nameProblem)
            problems.Add(nameProblem);
        if (count == 0)
            problems.Add("No whole cell fits the picture");

        // The sheet with its cells marked
        var paper = Canvas(1, 1);
        byte back = paper.ResolveColor("#28282E"), line = paper.ResolveColor("#FFD040");
        var pixels = new byte[_image.Width * _image.Height];
        var key = Key;
        for (int i = 0; i < pixels.Length; i++)
        {
            bool keyed = key is var (kr, kg, kb) && _image.Pixels[i * 4] == kr && _image.Pixels[i * 4 + 1] == kg && _image.Pixels[i * 4 + 2] == kb;
            pixels[i] = !keyed && (_picture.OpacityMask == null || _picture.OpacityMask[i] != 0) ? _picture.RawData[i] : back;
        }
        int columns = cells.Columns(_image.Width), rows = cells.Rows(_image.Height);
        for (int c = 0; c <= columns; c++)
            for (int y = 0; y < _image.Height && c * cells.CellWidth < _image.Width; y += 2)
                pixels[y * _image.Width + Math.Min(c * cells.CellWidth, _image.Width - 1)] = line;
        for (int r = 0; r <= rows; r++)
            for (int x = 0; x < _image.Width && r * cells.CellHeight < _image.Height; x += 2)
                pixels[Math.Min(r * cells.CellHeight, _image.Height - 1) * _image.Width + x] = line;
        var zoom = Math.Clamp(480 / Math.Max(_image.Width, 1), 1, 4);
        SheetImage = Bitmap(new IndexedPicture(_image.Width, _image.Height, pixels, null));
        (SheetWidth, SheetHeight) = (_image.Width * zoom, _image.Height * zoom);

        // The text in the font it makes
        PreviewPicture = null;
        PreviewImage = null;
        if (count > 0 && MakeFont(problems) is { } font)
        {
            int width = Math.Clamp(font.Measure(SampleText) + 4, 8, 2048), height = Math.Max(font.Height, 1) + 4;
            var canvas = Canvas(width, height);
            font.Draw(canvas, 2, 2, SampleText, TextColor);
            PreviewPicture = new IndexedPicture(width, height, canvas.Pixels, null);
            PreviewImage = Bitmap(PreviewPicture);
            (PreviewWidth, PreviewHeight) = (width * 3, height * 3);
        }

        Info = $"{Size}: {columns} x {rows} cells of {cells.CellWidth} x {cells.CellHeight}, {(string.IsNullOrEmpty(cells.Chars) ? $"characters {cells.FirstChar} to {cells.FirstChar + Math.Max(count - 1, 0)}" : $"{count} characters, as listed")}";
        Problem = string.Join("\n", problems);
    }

    private bool CanFinish => !HasProblem;

    /// <summary>A sheet font: saved into a mod. A vga font: opened in the glyph editor, to save from there.</summary>
    [RelayCommand(CanExecute = nameof(CanFinish))]
    private async Task DoneAsync()
    {
        if (AsVga)
        {
            OpenVga?.Invoke(VgaFont());
            Done?.Invoke(this, EventArgs.Empty);
            return;
        }
        if (SaveSheet != null && await SaveSheet(Name.Trim(), File, SheetDefinition))
            Done?.Invoke(this, EventArgs.Empty);
    }
}
