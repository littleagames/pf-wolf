using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;
using PFWolf.Editor.Rendering;

namespace PFWolf.Editor.ViewModels;

/// <summary>What a press on the pixel editor's canvas does</summary>
public enum PixelTool
{
    Pencil,
    Eraser,
    Fill,
    Picker,
}

/// <summary>
/// The pixel editor: a picture drawn in the game palette with a pencil, eraser (see-through), fill
/// and color picker, the left button in one color and the right in another; saved as a PNG in
/// its mod (or a mod picked, for the game's own or a new picture)
/// </summary>
public sealed partial class PixelEditorViewModel : ObservableObject, IDisposable
{
    private readonly Func<PixelDocument, Task<bool>> _save;
    private (int X, int Y)? _last;
    private int _strokeValue;
    private bool _stroking;

    /// <param name="palette">The game palette as loaded: what the PNG's colors are matched to</param>
    /// <param name="save">Saves the document into its mod; false when it isn't saved</param>
    public PixelEditorViewModel(PixelDocument document, PaletteColor[] palette, Func<PixelDocument, Task<bool>> save)
    {
        Document = document;
        Palette = palette;
        _save = save;
        document.Changed += (_, _) => Refresh();

        // The most used color to start with, and one to its right for the right button
        var counts = new int[256];
        for (int i = 0; i < document.Indices.Length; i++)
            if (document.Opaque == null || document.Opaque[i])
                counts[document.Indices[i]]++;
        _primary = Array.IndexOf(counts, counts.Max());
        _secondary = document.CanBeClear ? PixelDocument.Clear : 0;
        int largest = Math.Max(document.Width, document.Height);
        _zoom = largest switch { > 200 => 3, > 128 => 4, > 64 => 6, _ => 10 };
        Refresh();
    }

    public PixelDocument Document { get; }
    public PaletteColor[] Palette { get; }

    /// <summary>Asks whether to save changes before closing; the editor answers with its dialog</summary>
    public Func<string, Task<UnsavedChoice>>? AskUnsaved { get; set; }

    /// <summary>
    /// Whether the window can close: true when nothing's unsaved, or it's saved or let go now
    /// </summary>
    public async Task<bool> CanCloseAsync()
    {
        Document.EndEdit();
        if (!IsDirty || AskUnsaved == null)
            return true;
        return await AskUnsaved($"{Document.Name} has changes that aren't saved. Save them first?") switch
        {
            UnsavedChoice.Save => await SaveAsync(),
            UnsavedChoice.Discard => true,
            _ => false,
        };
    }

    public IReadOnlyList<int> Zooms { get; } = [1, 2, 3, 4, 6, 8, 10, 12, 16, 24];

    [ObservableProperty] private Bitmap? _bitmap;
    [ObservableProperty] private int _zoom;
    [ObservableProperty] private bool _showGrid = true;
    [ObservableProperty] private string _hoverText = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool[]? _usedColors;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPencil), nameof(IsEraser), nameof(IsFill), nameof(IsPicker))]
    private PixelTool _tool = PixelTool.Pencil;

    public bool IsPencil { get => Tool == PixelTool.Pencil; set { if (value) Tool = PixelTool.Pencil; } }
    public bool IsEraser { get => Tool == PixelTool.Eraser; set { if (value) Tool = PixelTool.Eraser; } }
    public bool IsFill { get => Tool == PixelTool.Fill; set { if (value) Tool = PixelTool.Fill; } }
    public bool IsPicker { get => Tool == PixelTool.Picker; set { if (value) Tool = PixelTool.Picker; } }

    /// <summary>The left button's color: a palette index, or <see cref="PixelDocument.Clear"/></summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrimaryBrush), nameof(PrimaryText), nameof(GridCurrent))]
    private int _primary;

    /// <summary>The right button's color</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SecondaryBrush), nameof(SecondaryText))]
    private int _secondary;

    public IBrush PrimaryBrush => BrushOf(Primary);
    public IBrush SecondaryBrush => BrushOf(Secondary);
    public string PrimaryText => Describe(Primary);
    public string SecondaryText => Describe(Secondary);

    /// <summary>The color the palette grid shows picked (a see-through color picks none)</summary>
    public int GridCurrent => Primary >= 0 ? Primary : -1;

    public bool CanBeClear => Document.CanBeClear;

    /// <summary>A sprite's offset as a pixel, for the canvas's cross</summary>
    public PixelPoint? Mark => Document.Offset is { } offset ? new PixelPoint(offset.X, offset.Y) : null;

    public string Title => $"{Document.Name}{(Document.IsDirty || Document.IsNew ? " *" : "")} - {ArtBrowserViewModel.KindName(Document.Kind)}, {Document.Width} × {Document.Height}";

    public string Help => Document.CanBeClear
        ? "Left button draws in the left color, right in the right. B pencil, E eraser (see-through), F fill (Ctrl: every pixel of that color), I picker; Shift-click draws a line from the last pixel; X swaps the colors."
        : "Left button draws in the left color, right in the right. B pencil, F fill (Ctrl: every pixel of that color), I picker; Shift-click draws a line from the last pixel; X swaps the colors. A wall texture has no see-through pixels.";

    /// <summary>Whether there are changes nobody saved</summary>
    public bool IsDirty => Document.IsDirty || Document.IsNew;

    private IBrush BrushOf(int value)
    {
        if (value < 0 || value >= Palette.Length)
            return Brushes.Transparent;
        var color = Palette[value];
        return new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue));
    }

    private string Describe(int value)
        => value < 0 ? "see-through" : value < Palette.Length ? $"{value}  {PaletteCatalog.Hex(Palette[value])}" : value.ToString();

    private void Refresh()
    {
        var old = Bitmap;
        Bitmap = ArtCache.Render(Document.Picture, Palette);
        old?.Dispose();

        var used = new bool[256];
        for (int i = 0; i < Document.Indices.Length; i++)
            if (Document.Opaque == null || Document.Opaque[i])
                used[Document.Indices[i]] = true;
        UsedColors = used;

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(IsDirty));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    //
    // Drawing
    //

    /// <summary>A press on the canvas: draws, fills or picks with the tool</summary>
    public void Press(PixelPointer pointer)
    {
        int value = pointer.Secondary ? Secondary : Primary;
        switch (Tool)
        {
            case PixelTool.Picker:
                Pick(pointer.X, pointer.Y, pointer.Secondary);
                return;
            case PixelTool.Fill:
            {
                bool everywhere = pointer.Modifiers.HasFlag(KeyModifiers.Control);
                Document.BeginEdit(everywhere ? "Fill every pixel of a color" : "Fill");
                Document.Fill(pointer.X, pointer.Y, value, everywhere);
                Document.EndEdit();
                return;
            }
        }

        _strokeValue = Tool == PixelTool.Eraser ? PixelDocument.Clear : value;
        Document.BeginEdit(Tool == PixelTool.Eraser ? "Erase" : "Draw");
        _stroking = true;
        // Shift-click: a straight line on from the last pixel drawn
        if (pointer.Modifiers.HasFlag(KeyModifiers.Shift) && _last is { } last)
            Document.Line(last.X, last.Y, pointer.X, pointer.Y, _strokeValue);
        else
            Document.SetPixel(pointer.X, pointer.Y, _strokeValue);
        _last = (pointer.X, pointer.Y);
    }

    /// <summary>A drag: the stroke goes on, every pixel between included</summary>
    public void Drag(PixelPointer pointer)
    {
        if (Tool == PixelTool.Picker)
        {
            if (Document.Contains(pointer.X, pointer.Y))
                Pick(pointer.X, pointer.Y, pointer.Secondary);
            return;
        }
        if (!_stroking || _last is not { } last || last == (pointer.X, pointer.Y))
            return;
        Document.Line(last.X, last.Y, pointer.X, pointer.Y, _strokeValue);
        _last = (pointer.X, pointer.Y);
    }

    public void Release()
    {
        if (_stroking)
            Document.EndEdit();
        _stroking = false;
    }

    public void Hover(PixelPoint? pixel)
    {
        if (pixel is not { } at)
        {
            HoverText = "";
            return;
        }
        var offset = Document.Offset is { } o ? $"   (from the offset {at.X - o.X}, {at.Y - o.Y})" : "";
        HoverText = $"{at.X}, {at.Y}:  {Describe(Document.ValueAt(at.X, at.Y))}{offset}";
    }

    private void Pick(int x, int y, bool secondary)
    {
        var value = Document.ValueAt(x, y);
        if (secondary)
            Secondary = value;
        else
            Primary = value;
    }

    /// <summary>A color picked on the palette grid (the right button's with <paramref name="secondary"/>)</summary>
    public void PickColor(int index, bool secondary)
    {
        if (secondary)
            Secondary = index;
        else
            Primary = index;
        if (Tool is PixelTool.Eraser or PixelTool.Picker)
            Tool = PixelTool.Pencil;
    }

    [RelayCommand]
    private void SetTool(string tool)
    {
        if (Enum.TryParse<PixelTool>(tool, out var picked) && (picked != PixelTool.Eraser || CanBeClear))
            Tool = picked;
    }

    [RelayCommand]
    private void SwapColors() => (Primary, Secondary) = (Secondary, Primary);

    /// <summary>Makes the left color see-through (where pixels can be)</summary>
    [RelayCommand]
    private void ClearColor()
    {
        if (CanBeClear)
            Primary = PixelDocument.Clear;
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        var label = Document.UndoLabel;
        Document.Undo();
        Status = label != null ? $"Undid: {label}" : "";
    }

    private bool CanUndo => Document.CanUndo;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        var label = Document.RedoLabel;
        Document.Redo();
        Status = label != null ? $"Redid: {label}" : "";
    }

    private bool CanRedo => Document.CanRedo;

    [RelayCommand]
    private void ZoomBy(string step)
    {
        int at = Zooms.ToList().FindIndex(zoom => zoom >= Zoom);
        at = Math.Clamp((at < 0 ? Zooms.Count - 1 : at) + (int.TryParse(step, out var by) ? by : 1), 0, Zooms.Count - 1);
        Zoom = Zooms[at];
    }

    /// <summary>Saves the picture into its mod (or a mod picked); false when it isn't saved</summary>
    [RelayCommand]
    public async Task<bool> SaveAsync()
    {
        Document.EndEdit();
        if (!await _save(Document))
        {
            Status = "Not saved";
            return false;
        }
        Document.MarkSaved();
        Refresh();
        Status = $"Saved {Document.Name}";
        return true;
    }

    public void Dispose()
    {
        Bitmap?.Dispose();
        Bitmap = null;
    }
}
