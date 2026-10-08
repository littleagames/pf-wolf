using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;
using PFWolf.Editor.Rendering;

namespace PFWolf.Editor.ViewModels;

/// <summary>A palette as the palette browser lists it, marked when it has changes nobody saved</summary>
public sealed partial class PaletteListItem(PaletteInfo entry, PaletteDocument document) : ObservableObject
{
    public PaletteInfo Entry { get; } = entry;
    public PaletteDocument Document { get; } = document;
    public string Name => Entry.Name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    private bool _isDirty;

    public string Label => Name + (IsDirty ? " *" : "");
    public string Note => Entry.IsGamePalette ? "game palette" : Entry.Uses.Count > 0 ? $"{Entry.Uses.Count} use{(Entry.Uses.Count == 1 ? "" : "s")}" : "";
    public string ToolTip => $"{Name}  (from {Entry.Source})" + (Entry.Uses.Count > 0 ? "" : "\nNothing in game-info or the movies draws in it");
}

/// <summary>A kind of picture the preview can show</summary>
public sealed record PreviewKindOption(ArtKind Kind, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// The palette browser: every palette of the loaded game and mods, where each came from and what's
/// drawn in it, with its colors to edit and save to a mod's palettes/ folder, and a picture drawn
/// in the colors as they're edited
/// </summary>
public sealed partial class PaletteBrowserViewModel : ObservableObject
{
    private GameContent? _content;
    private PaletteColor[]? _copied;
    private IndexedPicture? _preview;
    // Set while the color fields are filled from the palette, so they don't edit it back
    private bool _syncing;

    public PaletteBrowserViewModel()
    {
        _selectedPreviewKind = PreviewKinds[0];
    }

    /// <summary>Asks for a mod folder to save in; null when none is picked</summary>
    public Func<string, Task<string?>>? PickModFolder { get; set; }

    /// <summary>A palette was saved into this mod folder (the editor adds it to the mods)</summary>
    public event EventHandler<string>? Saved;

    public ObservableCollection<PaletteListItem> Items { get; } = [];
    public ObservableCollection<string> Origins { get; } = [];
    public ObservableCollection<PaletteUse> Uses { get; } = [];

    public IReadOnlyList<PreviewKindOption> PreviewKinds { get; } =
    [
        new(ArtKind.Texture, "Wall textures and flats"),
        new(ArtKind.Sprite, "Sprites"),
        new(ArtKind.Picture, "Pictures"),
    ];

    public ObservableCollection<string> PreviewNames { get; } = [];

    [ObservableProperty] private string _title = "Palettes";
    [ObservableProperty] private string _status = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(SaveToCommand), nameof(RevertAllCommand))]
    private PaletteListItem? _selectedItem;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UsePictureCommand))]
    private PaletteUse? _selectedUse;

    // The grid
    [ObservableProperty] private PaletteColor[]? _gridColors;
    [ObservableProperty] private bool[]? _marked;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GradientCommand))]
    private int _anchor;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GradientCommand))]
    private int _current;

    [ObservableProperty] private string _hoverText = HoverHint;

    private const string HoverHint = "Click a color to edit it; Shift-click or drag picks a run; arrows move (Shift extends); dots: in the preview";

    // The color being edited
    [ObservableProperty] private int _red;
    [ObservableProperty] private int _green;
    [ObservableProperty] private int _blue;
    [ObservableProperty] private string _hexText = "";
    [ObservableProperty] private string _colorTitle = "";
    [ObservableProperty] private string _colorDetails = "";
    [ObservableProperty] private IBrush _currentBrush = Brushes.Black;
    [ObservableProperty] private IBrush _loadedBrush = Brushes.Black;
    [ObservableProperty] private string _findText = "";

    // The preview
    [ObservableProperty] private PreviewKindOption _selectedPreviewKind;
    [ObservableProperty] private string? _selectedPreviewName;
    [ObservableProperty] private Bitmap? _previewBitmap;
    [ObservableProperty] private bool _highlightPicked;
    [ObservableProperty] private string _previewDetails = "";

    public bool HasSelection => SelectedItem != null;
    public bool HasUses => Uses.Count > 0;
    private PaletteDocument? Document => SelectedItem?.Document;

    /// <summary>The run of colors picked, first to last</summary>
    public (int From, int To) Run => (Math.Min(Anchor, Current), Math.Max(Anchor, Current));

    /// <summary>Palettes with changes nobody saved</summary>
    public IEnumerable<PaletteDocument> DirtyDocuments => Items.Select(item => item.Document).Where(document => document.IsDirty);

    /// <summary>Lists the palettes of a newly loaded game (edits to the old one's are dropped), keeping the one picked if it's still there</summary>
    public void Load(GameContent? content)
    {
        _content = content;
        var picked = SelectedItem?.Name;
        foreach (var item in Items)
            item.Document.Changed -= OnDocumentChanged;
        Items.Clear();
        SelectedItem = null;

        if (content != null)
        {
            foreach (var entry in PaletteCatalog.Build(content))
            {
                var document = new PaletteDocument(entry.Name, entry.Colors, content.ModFolderOf(entry.Name, nameof(Palette)));
                document.Changed += OnDocumentChanged;
                Items.Add(new PaletteListItem(entry, document));
            }
            Title = $"Palettes - {content.Title}";
        }
        else
            Title = "Palettes";

        FillPreviewNames();
        SelectedItem = Items.FirstOrDefault(item => item.Name.Equals(picked, StringComparison.OrdinalIgnoreCase)) ?? Items.FirstOrDefault();
        Status = content == null ? "" : $"{Items.Count} palettes";
    }

    /// <summary>Shows a palette by name</summary>
    public void Show(string name)
    {
        if (Items.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } item)
            SelectedItem = item;
    }

    partial void OnSelectedItemChanged(PaletteListItem? value)
    {
        Origins.Clear();
        Uses.Clear();
        if (value != null)
        {
            foreach (var origin in value.Entry.Origins)
                Origins.Add($"{origin.Action} by {origin.Source}: {origin.Path}");
            foreach (var use in value.Entry.Uses)
                Uses.Add(use);
            SelectedUse = Uses.FirstOrDefault();

            // A palette drawn for one picture (a title or end screen) shows that picture
            if (value.Entry.Uses.FirstOrDefault(use => use.Picture != null) is { Picture: { } picture })
                ShowPicture(picture);
        }
        OnPropertyChanged(nameof(HasUses));
        Refresh();
    }

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        if (Items.FirstOrDefault(item => item.Document == sender) is { } item)
            item.IsDirty = item.Document.IsDirty;
        if (sender == Document)
            Refresh();
    }

    // Shows the palette's colors as they are now: the grid, the color picked, the preview
    private void Refresh()
    {
        GridColors = Document != null ? (PaletteColor[])Document.Colors.Clone() : null;
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        RevertCommand.NotifyCanExecuteChanged();
        RevertAllCommand.NotifyCanExecuteChanged();
        PasteCommand.NotifyCanExecuteChanged();
        ShowCurrent();
        RenderPreview();
    }

    //
    // Picking colors
    //

    /// <summary>Picks a color, or extends the run picked to it</summary>
    public void Pick(int index, bool extend)
    {
        Document?.EndRunningEdit();
        index = Math.Clamp(index, 0, PaletteCatalog.ColorCount - 1);
        if (!extend)
            Anchor = index;
        Current = index;
    }

    partial void OnAnchorChanged(int value) => PickedChanged();
    partial void OnCurrentChanged(int value) => PickedChanged();

    private void PickedChanged()
    {
        CopyCommand.NotifyCanExecuteChanged();
        RevertCommand.NotifyCanExecuteChanged();
        ShowCurrent();
        if (HighlightPicked)
            RenderPreview();
    }

    /// <summary>Says what's under the pointer on the grid</summary>
    public void Hover(int index)
    {
        if (index < 0 || GridColors is not { } colors)
        {
            HoverText = HoverHint;
            return;
        }
        var color = colors[index];
        var used = _preview != null && Marked is { } marked && marked[index] ? $"  · in {SelectedPreviewName}" : "";
        HoverText = $"{index} (0x{index:X2})  {PaletteCatalog.Hex(color)}  {color.Red}, {color.Green}, {color.Blue}{used}";
    }

    private void ShowCurrent()
    {
        if (Document is not { } document)
        {
            ColorTitle = ColorDetails = HexText = "";
            return;
        }

        var color = document.Colors[Current];
        var loaded = document.Loaded[Current];
        _syncing = true;
        try
        {
            Red = color.Red;
            Green = color.Green;
            Blue = color.Blue;
            HexText = PaletteCatalog.Hex(color);
        }
        finally
        {
            _syncing = false;
        }

        var (from, to) = Run;
        ColorTitle = to > from ? $"Color {Current}  ·  {to - from + 1} picked ({from}-{to})" : $"Color {Current} (0x{Current:X2})";
        var changed = !Same(color, loaded) ? $"\nAs loaded: {PaletteCatalog.Hex(loaded)}" : "";
        var pixels = _preview != null ? $"\n{Count(_preview, index => index == Current)} pixels of {SelectedPreviewName}" : "";
        ColorDetails = $"VGA (6-bit): {PaletteCatalog.ToVga(color.Red)}, {PaletteCatalog.ToVga(color.Green)}, {PaletteCatalog.ToVga(color.Blue)}{changed}{pixels}";
        CurrentBrush = new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue));
        LoadedBrush = new SolidColorBrush(Color.FromRgb(loaded.Red, loaded.Green, loaded.Blue));
    }

    private static bool Same(PaletteColor a, PaletteColor b) => a.Red == b.Red && a.Green == b.Green && a.Blue == b.Blue;

    /// <summary>Picks the color closest to an index or #RRGGBB typed in Find</summary>
    [RelayCommand]
    private void Find()
    {
        if (GridColors is not { } colors || string.IsNullOrWhiteSpace(FindText))
            return;
        var text = FindText.Trim();
        if (int.TryParse(text, out var index) && index is >= 0 and < PaletteCatalog.ColorCount && !text.StartsWith('#'))
        {
            Pick(index, false);
            Status = $"Color {index}";
        }
        else if (PaletteCatalog.TryParseHex(text, out var wanted))
        {
            var closest = PaletteCatalog.Closest(colors, wanted.Red, wanted.Green, wanted.Blue);
            Pick(closest, false);
            Status = Same(colors[closest], wanted)
                ? $"{PaletteCatalog.Hex(wanted)} is color {closest}"
                : $"The closest to {PaletteCatalog.Hex(wanted)} is color {closest}, {PaletteCatalog.Hex(colors[closest])} (as the game picks for #RRGGBB colors)";
        }
        else
            Status = "Find takes a color number (0-255) or #RRGGBB";
    }

    //
    // Editing
    //

    partial void OnRedChanged(int value) => SetFromFields();
    partial void OnGreenChanged(int value) => SetFromFields();
    partial void OnBlueChanged(int value) => SetFromFields();

    private void SetFromFields()
    {
        if (_syncing)
            return;
        SetCurrent(new PaletteColor((byte)Math.Clamp(Red, 0, 255), (byte)Math.Clamp(Green, 0, 255), (byte)Math.Clamp(Blue, 0, 255)));
    }

    /// <summary>Sets the color being edited from the hex box (on Enter or leaving it)</summary>
    public void ApplyHex()
    {
        if (Document == null)
            return;
        if (PaletteCatalog.TryParseHex(HexText, out var color))
        {
            Document.EndRunningEdit();
            SetCurrent(color);
            Document.EndRunningEdit();
        }
        else
            ShowCurrent();
    }

    private void SetCurrent(PaletteColor color)
    {
        if (Document is not { } document)
            return;
        var index = Current;
        // Dragging a slider is one undo step for the color
        document.Edit($"Change color {index}", colors => colors[index] = color, running: $"color {index}");
    }

    private bool CanGradient => Document != null && Run.To - Run.From >= 2;

    /// <summary>Blends the colors picked evenly from the first to the last</summary>
    [RelayCommand(CanExecute = nameof(CanGradient))]
    private void Gradient()
    {
        var (from, to) = Run;
        Document?.Edit($"Gradient {from}-{to}", colors => PaletteCatalog.Gradient(colors, from, to));
        Status = $"Blended colors {from} to {to}";
    }

    private bool CanCopy => Document != null;

    /// <summary>Copies the colors picked; returns them as #RRGGBB lines for the clipboard</summary>
    [RelayCommand(CanExecute = nameof(CanCopy))]
    private void Copy()
    {
        if (Document is not { } document)
            return;
        var (from, to) = Run;
        _copied = document.Colors[from..(to + 1)];
        CopiedText = string.Join(Environment.NewLine, _copied.Select(PaletteCatalog.Hex));
        PasteCommand.NotifyCanExecuteChanged();
        Status = _copied.Length == 1 ? $"Copied color {from}" : $"Copied colors {from}-{to}";
    }

    /// <summary>The colors last copied, as text for the clipboard</summary>
    public string CopiedText { get; private set; } = "";

    private bool CanPaste => Document != null && _copied != null;

    /// <summary>Pastes the colors copied over the ones from the first picked on (past 255 they're left out)</summary>
    [RelayCommand(CanExecute = nameof(CanPaste))]
    private void Paste()
    {
        if (Document is not { } document || _copied is not { } copied)
            return;
        var from = Run.From;
        var count = Math.Min(copied.Length, PaletteCatalog.ColorCount - from);
        document.Edit($"Paste at {from}", colors => Array.Copy(copied, 0, colors, from, count));
        Anchor = from;
        Current = from + count - 1;
        Status = count == 1 ? $"Pasted into color {from}" : $"Pasted into colors {from}-{from + count - 1}";
    }

    private bool CanRevert => Document is { } document && Enumerable.Range(Run.From, Run.To - Run.From + 1).Any(i => !Same(document.Colors[i], document.Loaded[i]));

    /// <summary>Puts the colors picked back as the game loaded them</summary>
    [RelayCommand(CanExecute = nameof(CanRevert))]
    private void Revert()
    {
        var (from, to) = Run;
        Document?.Edit($"Revert {from}-{to}", colors => Array.Copy(Document.Loaded, from, colors, from, to - from + 1));
    }

    private bool CanRevertAll => Document is { } document && !PaletteDocument.Same(document.Colors, document.Loaded);

    /// <summary>Puts every color back as the game loaded it</summary>
    [RelayCommand(CanExecute = nameof(CanRevertAll))]
    private void RevertAll()
    {
        if (Document is { } document)
            document.Edit("Revert all", colors => Array.Copy(document.Loaded, colors, colors.Length));
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        var label = Document?.UndoLabel;
        Document?.Undo();
        Status = label != null ? $"Undid: {label}" : "";
    }

    private bool CanUndo => Document?.CanUndo == true;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        var label = Document?.RedoLabel;
        Document?.Redo();
        Status = label != null ? $"Redid: {label}" : "";
    }

    private bool CanRedo => Document?.CanRedo == true;

    /// <summary>Replaces the palette's colors with a file's (from Import); says how it went</summary>
    public void Import(string fileName, byte[] data)
    {
        if (Document is not { } document)
            return;
        try
        {
            var (colors, how) = PaletteCatalog.Read(data);
            document.Edit($"Import {fileName}", target => Array.Copy(colors, target, target.Length));
            Status = $"Imported {fileName} ({how})";
        }
        catch (InvalidDataException e)
        {
            Status = $"Couldn't import {fileName}: {e.Message}";
        }
    }

    /// <summary>The palette's colors as a file of this format (for Export)</summary>
    public byte[]? Export(PaletteCatalog.FileFormat format)
        => Document is { } document ? PaletteCatalog.Write(document.Colors, format, document.Name) : null;

    //
    // Saving
    //

    /// <summary>Saves the palette to its mod folder (asking for one the first time)</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task SaveAsync()
    {
        if (Document is { } document)
            await SaveDocument(document, document.SaveFolder);
    }

    /// <summary>Saves the palette to a mod folder picked now</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task SaveToAsync()
    {
        if (Document is { } document)
            await SaveDocument(document, null);
    }

    /// <summary>Saves a palette to {folder}/palettes/NAME.pal, asking for a folder when there's none; false when it isn't saved</summary>
    public async Task<bool> SaveDocument(PaletteDocument document, string? folder)
    {
        folder ??= PickModFolder == null ? null : await PickModFolder($"A mod folder to save {document.Name} in (it goes in its palettes folder)");
        if (folder == null)
            return false;

        try
        {
            var path = document.Save(folder);
            var isGamePalette = Items.Any(item => item.Document == document && item.Entry.IsGamePalette);
            Status = $"Saved {path}" + (isGamePalette ? ". Load the game again to see the map and pictures in it." : "");
            Saved?.Invoke(this, folder);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Status = $"Couldn't save {document.Name}: {e.Message}";
            return false;
        }
    }

    //
    // The preview
    //

    partial void OnSelectedPreviewKindChanged(PreviewKindOption value) => FillPreviewNames();
    partial void OnSelectedPreviewNameChanged(string? value) => LoadPreview();
    partial void OnHighlightPickedChanged(bool value) => RenderPreview();

    private void FillPreviewNames()
    {
        var picked = SelectedPreviewName;
        PreviewNames.Clear();
        if (_content != null)
        {
            var assets = _content.Assets;
            Func<string, bool> exists = SelectedPreviewKind.Kind switch
            {
                ArtKind.Sprite => assets.Exists<SpriteAsset>,
                ArtKind.Picture => assets.Exists<GraphicAsset>,
                _ => assets.Exists<TextureAsset>,
            };
            foreach (var name in assets.AssetNames.Where(exists).Select(name => name.ToUpperInvariant()).Distinct().Order(StringComparer.Ordinal))
                PreviewNames.Add(name);
        }
        SelectedPreviewName = PreviewNames.FirstOrDefault(name => name.Equals(picked, StringComparison.OrdinalIgnoreCase)) ?? PreviewNames.FirstOrDefault();
    }

    /// <summary>Shows a picture of any kind in the preview</summary>
    public void ShowPicture(string name)
    {
        if (_content == null)
            return;
        var kind = _content.Assets.Exists<GraphicAsset>(name) ? ArtKind.Picture
            : _content.Assets.Exists<TextureAsset>(name) ? ArtKind.Texture
            : _content.Assets.Exists<SpriteAsset>(name) ? ArtKind.Sprite
            : (ArtKind?)null;
        if (kind == null)
            return;
        if (SelectedPreviewKind.Kind != kind)
            SelectedPreviewKind = PreviewKinds.First(option => option.Kind == kind);
        SelectedPreviewName = PreviewNames.FirstOrDefault(entry => entry.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private bool CanUsePicture => SelectedUse?.Picture != null;

    /// <summary>Shows the picture the use picked draws in this palette</summary>
    [RelayCommand(CanExecute = nameof(CanUsePicture))]
    private void UsePicture()
    {
        if (SelectedUse?.Picture is { } picture)
            ShowPicture(picture);
    }

    private void LoadPreview()
    {
        _preview = _content != null && SelectedPreviewName != null ? ArtCache.Indexed(_content, SelectedPreviewKind.Kind, SelectedPreviewName) : null;
        if (_preview != null)
        {
            var marked = new bool[PaletteCatalog.ColorCount];
            ForEachPixel(_preview, index => marked[index] = true);
            Marked = marked;
            PreviewDetails = $"{_preview.Width}x{_preview.Height}, {marked.Count(used => used)} colors (dotted on the grid)";
        }
        else
        {
            Marked = null;
            PreviewDetails = "";
        }
        RenderPreview();
        ShowCurrent();
    }

    private void RenderPreview()
    {
        var old = PreviewBitmap;
        if (_preview != null && Document is { } document)
        {
            var (from, to) = Run;
            PreviewBitmap = ArtCache.Render(_preview, document.Colors, HighlightPicked ? index => index >= from && index <= to : null);
        }
        else
            PreviewBitmap = null;
        old?.Dispose();
    }

    /// <summary>Picks the color of a pixel of the preview (x, y in the picture's pixels)</summary>
    public void PickPixel(int x, int y, bool extend)
    {
        if (_preview is not { } picture || x < 0 || y < 0 || x >= picture.Width || y >= picture.Height)
            return;
        int i = y * picture.Width + x;
        if (i >= picture.Indices.Length || picture.Opaque != null && (i >= picture.Opaque.Length || !picture.Opaque[i]))
            return;
        Pick(picture.Indices[i], extend);
    }

    /// <summary>The preview picture's size in pixels, for placing clicks on it</summary>
    public (int Width, int Height)? PreviewSize => _preview != null ? (_preview.Width, _preview.Height) : null;

    private static void ForEachPixel(IndexedPicture picture, Action<byte> action)
    {
        for (int i = 0; i < picture.Indices.Length; i++)
        {
            if (picture.Opaque == null || i < picture.Opaque.Length && picture.Opaque[i])
                action(picture.Indices[i]);
        }
    }

    private static int Count(IndexedPicture picture, Func<byte, bool> which)
    {
        int count = 0;
        ForEachPixel(picture, index => { if (which(index)) count++; });
        return count;
    }
}
