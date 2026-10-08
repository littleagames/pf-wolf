using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Editor.Rendering;

namespace PFWolf.Editor.ViewModels;

/// <summary>A picture as the art browser lists it, its thumbnail made when it's first shown</summary>
public sealed class ArtItem(ArtEntry entry, ArtCache art)
{
    public ArtEntry Entry { get; } = entry;
    public string Name => Entry.Name;
    public Bitmap? Thumbnail => art.Whole(Entry.Kind, Entry.AssetName);

    /// <summary>"8 sides" under a rotating sprite frame's thumbnail</summary>
    public string SidesText => Entry.Rotations.Count > 0 ? $"{Entry.Rotations.Count} sides" : "";
    public bool IsRotating => Entry.Rotations.Count > 0;

    /// <summary>A rotating frame's sides for the picker</summary>
    public List<RotationItem> MakeRotations()
        => Entry.Rotations.Select(name => new RotationItem(name, name[^1] - '0', art.Whole(Entry.Kind, name))).ToList();

    public string ToolTip => $"{Entry.Name}{(IsRotating ? $" ({SidesText})" : "")}  {Entry.Width} × {Entry.Height}  ({ArtBrowserViewModel.KindName(Entry.Kind)}, {Entry.Source})"
        + (Entry.Uses.Count > 0 ? "" : "\nNot used by mapdefs, actordefs or game-info");
}

/// <summary>One side of a rotating sprite frame: 1 faces the viewer, 5 faces away</summary>
public sealed record RotationItem(string AssetName, int Side, Bitmap? Thumbnail)
{
    public string Label => Side switch
    {
        1 => "1 front",
        5 => "5 back",
        _ => Side.ToString(),
    };
}

/// <summary>Which kinds of picture the browser lists</summary>
public sealed record ArtKindOption(ArtKind? Kind, string Label)
{
    public override string ToString() => Label;
}

/// <summary>Which pictures the browser lists by how they're used and where they come from</summary>
public enum ArtShow
{
    Everything,
    InUse,
    NotInUse,
    FromMods,
}

public sealed record ArtShowOption(ArtShow Show, string Label)
{
    public override string ToString() => Label;
}

/// <summary>A colour behind the preview, which shows through a sprite's see-through pixels</summary>
public sealed record ArtBackgroundOption(string Label, IBrush Brush)
{
    public override string ToString() => Label;
}

/// <summary>
/// The texture and sprite browser: every wall texture, flat, sprite and picture of the loaded
/// game and mods, with where each came from and what uses it
/// </summary>
public sealed partial class ArtBrowserViewModel : ObservableObject
{
    private List<ArtItem> _all = [];
    private HashSet<string> _modSources = new(StringComparer.OrdinalIgnoreCase);
    private GameContent? _content;
    private ArtCache? _art;

    public ArtBrowserViewModel()
    {
        _selectedKind = Kinds[0];
        _selectedShow = Shows[0];
        _selectedBackground = Backgrounds[0];
    }

    /// <summary>"Use in palette" picked: the main window puts it in the palette</summary>
    public event EventHandler<ArtUse>? UseRequested;

    private static readonly (ArtKind? Kind, string Label)[] KindLabels =
    [
        (null, "All"),
        (ArtKind.Texture, "Wall textures"),
        (ArtKind.Flat, "Flats"),
        (ArtKind.Sprite, "Sprites"),
        (ArtKind.Picture, "Pictures"),
    ];

    public ObservableCollection<ArtKindOption> Kinds { get; } = [.. KindLabels.Select(kind => new ArtKindOption(kind.Kind, kind.Label))];

    public IReadOnlyList<ArtShowOption> Shows { get; } =
    [
        new(ArtShow.Everything, "Everything"),
        new(ArtShow.InUse, "In use"),
        new(ArtShow.NotInUse, "Not in use"),
        new(ArtShow.FromMods, "From the mods"),
    ];

    public IReadOnlyList<ArtBackgroundOption> Backgrounds { get; } =
    [
        new("Dark", new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20))),
        new("Grey", new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80))),
        new("Light", new SolidColorBrush(Color.FromRgb(0xf0, 0xf0, 0xf0))),
        new("Wolf3D floor", new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x70))),
        new("Wolf3D ceiling", new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38))),
        new("Magenta", new SolidColorBrush(Color.FromRgb(0xff, 0x00, 0xff))),
    ];

    public IReadOnlyList<int> Zooms { get; } = [1, 2, 3, 4, 6, 8];

    public ObservableCollection<ArtItem> Items { get; } = [];

    [ObservableProperty] private ArtKindOption _selectedKind;
    [ObservableProperty] private ArtShowOption _selectedShow;
    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private double _thumbSize = 64;
    [ObservableProperty] private ArtBackgroundOption _selectedBackground;
    [ObservableProperty] private string _countText = "";
    [ObservableProperty] private string _title = "Textures and sprites";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(Preview), nameof(PreviewWidth), nameof(PreviewHeight), nameof(Details))]
    [NotifyCanExecuteChangedFor(nameof(UseInPaletteCommand))]
    private ArtItem? _selectedItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewWidth), nameof(PreviewHeight))]
    private int _zoom = 4;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UseInPaletteCommand))]
    private ArtUse? _selectedUse;

    public ObservableCollection<string> Origins { get; } = [];
    public ObservableCollection<ArtUse> Uses { get; } = [];

    /// <summary>The sides of the rotating sprite frame picked, for the rotation picker</summary>
    public ObservableCollection<RotationItem> Rotations { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview), nameof(PreviewWidth), nameof(PreviewHeight), nameof(Details), nameof(CurrentAssetName))]
    private RotationItem? _selectedRotation;

    public bool HasSelection => SelectedItem != null;
    public bool HasUses => Uses.Count > 0;
    public bool HasRotations => Rotations.Count > 0;

    /// <summary>The picture shown: the entry's, or the side of a rotating frame picked</summary>
    public Bitmap? Preview => SelectedRotation?.Thumbnail ?? SelectedItem?.Thumbnail;

    /// <summary>The asset the preview shows ("GARDA3" for side 3 of GARDA): what Copy name and Export PNG take</summary>
    public string CurrentAssetName => SelectedRotation?.AssetName ?? SelectedItem?.Entry.AssetName ?? "";

    public double PreviewWidth => (Preview?.PixelSize.Width ?? 0) * Zoom;
    public double PreviewHeight => (Preview?.PixelSize.Height ?? 0) * Zoom;

    /// <summary>What the picture is, its size and anything particular to its kind</summary>
    public string Details
    {
        get
        {
            if (SelectedItem?.Entry is not { } entry)
                return "";

            if (entry.Kind == ArtKind.Sprite && _content?.Find<SpriteAsset>(CurrentAssetName) is { } sprite)
            {
                var name = CurrentAssetName;
                var sprites = $"Sprite {name}, {sprite.Width} × {sprite.Height}, offset {sprite.Offset.x}, {sprite.Offset.y}";
                if (SelectedRotation is { } side)
                    return sprites + $"\nFrame {entry.Name[^1]} from side {side.Side} of {Rotations.Count} (1 faces you, 5 faces away)";
                return name.Length >= 2 && name[^1] == '0' ? sprites + "\nThe same from every side" : sprites;
            }

            var details = $"{KindName(entry.Kind)}, {entry.Width} × {entry.Height}";
            if (entry.Kind is ArtKind.Texture && entry.Height > TextureAsset.StorySize)
            {
                var stories = (double)entry.Height / Math.Max(1, entry.Width);
                details += $", {stories:0.##} stories tall";
            }
            return details;
        }
    }

    public static string KindName(ArtKind kind) => kind switch
    {
        ArtKind.Texture => "Wall texture",
        ArtKind.Flat => "Flat",
        ArtKind.Sprite => "Sprite",
        _ => "Picture",
    };

    /// <summary>Lists the pictures of a newly loaded game, keeping the one picked if it's still there</summary>
    public void Load(GameContent? content, ArtCache? art)
    {
        _content = content;
        _art = art;
        var picked = SelectedItem?.Entry;

        if (content == null || art == null)
        {
            _all = [];
            _modSources = [];
            Title = "Textures and sprites";
        }
        else
        {
            _all = ArtCatalog.Build(content).Select(entry => new ArtItem(entry, art)).ToList();
            _modSources = content.Assets.LoadedMods.Select(mod => mod.Source.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Title = $"Textures and sprites - {content.Title}";
        }

        // The kinds with how many there are of each
        var kindPicked = SelectedKind?.Kind;
        Kinds.Clear();
        foreach (var (kind, label) in KindLabels)
            Kinds.Add(new ArtKindOption(kind, $"{label}  ({_all.Count(item => kind == null || item.Entry.Kind == kind)})"));
        SelectedKind = Kinds.First(option => option.Kind == kindPicked);

        Refilter();
        SelectedItem = picked != null ? Find(picked.Kind, picked.Name) : null;
        SelectedItem ??= Items.FirstOrDefault();
    }

    /// <summary>
    /// Shows the same pictures drawn by another art cache (the game palette changed), keeping
    /// what's picked, its side and the zoom
    /// </summary>
    public void Recolor(ArtCache art)
    {
        if (_content == null)
            return;
        _art = art;
        var (picked, side, zoom) = (SelectedItem?.Entry, SelectedRotation?.AssetName, Zoom);
        _all = _all.Select(item => new ArtItem(item.Entry, art)).ToList();
        Refilter();
        SelectedItem = picked != null ? Items.FirstOrDefault(item => item.Entry == picked) : SelectedItem;
        if (side != null && Rotations.FirstOrDefault(rotation => rotation.AssetName == side) is { } turned)
            SelectedRotation = turned;
        Zoom = zoom;
    }

    /// <summary>
    /// Shows a picture by name, of the kind asked for when there's one of each; a side of a
    /// rotating sprite ("GARDA3") shows its frame turned to that side
    /// </summary>
    public void Show(ArtKind kind, string name)
    {
        var found = _all.FirstOrDefault(item => item.Entry.Kind == kind && item.Entry.Holds(name))
            ?? _all.FirstOrDefault(item => item.Entry.Holds(name));
        if (found == null)
            return;

        if (!Items.Contains(found))
        {
            Filter = "";
            SelectedShow = Shows[0];
            if (SelectedKind?.Kind != null && SelectedKind.Kind != found.Entry.Kind)
                SelectedKind = Kinds.First(option => option.Kind == found.Entry.Kind);
        }
        SelectedItem = found;
        if (Rotations.FirstOrDefault(side => side.AssetName.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } turned)
            SelectedRotation = turned;
    }

    /// <summary>Turns a rotating sprite to the side before or after, round from 8 back to 1</summary>
    [RelayCommand]
    private void Turn(string step)
    {
        if (Rotations.Count == 0)
            return;
        int at = SelectedRotation != null ? Rotations.IndexOf(SelectedRotation) : 0;
        int by = int.TryParse(step, out var parsed) ? parsed : 1;
        SelectedRotation = Rotations[((at + by) % Rotations.Count + Rotations.Count) % Rotations.Count];
    }

    partial void OnSelectedRotationChanged(RotationItem? value)
    {
        // The side shown may come from somewhere else (a mod replacing just some sides)
        if (value != null && _content != null)
            ShowOrigins(ArtCatalog.Origins(_content, value.AssetName, nameof(SpriteAsset)));
    }

    private void ShowOrigins(IEnumerable<PFWolf.Loaders.AssetOrigin> origins)
    {
        Origins.Clear();
        foreach (var origin in origins)
            Origins.Add($"{origin.Action} by {origin.Source}: {origin.Path}");
    }

    private ArtItem? Find(ArtKind kind, string name)
        => Items.FirstOrDefault(item => item.Entry.Kind == kind && item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    partial void OnSelectedKindChanged(ArtKindOption value) => Refilter();
    partial void OnSelectedShowChanged(ArtShowOption value) => Refilter();
    partial void OnFilterChanged(string value) => Refilter();

    private void Refilter()
    {
        var selected = SelectedItem;
        var words = Filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Items.Clear();
        foreach (var item in _all.Where(Matches))
            Items.Add(item);
        CountText = Items.Count == _all.Count ? $"{Items.Count} pictures" : $"{Items.Count} of {_all.Count} pictures";
        // The picture picked stays picked; when it's filtered out, the first one left is
        SelectedItem = selected != null && Items.Contains(selected) ? selected : Items.FirstOrDefault();

        bool Matches(ArtItem item)
        {
            var entry = item.Entry;
            if (SelectedKind?.Kind is { } kind && entry.Kind != kind)
                return false;

            bool shown = SelectedShow.Show switch
            {
                ArtShow.InUse => entry.Uses.Count > 0,
                ArtShow.NotInUse => entry.Uses.Count == 0,
                ArtShow.FromMods => _modSources.Contains(entry.Source),
                _ => true,
            };

            // Every word is in the name, or in something that uses it ("guard", "door 90")
            return shown && words.All(word => entry.Name.Contains(word, StringComparison.OrdinalIgnoreCase)
                || entry.Rotations.Any(side => side.Contains(word, StringComparison.OrdinalIgnoreCase))
                || entry.Uses.Any(use => use.Label.Contains(word, StringComparison.OrdinalIgnoreCase)));
        }
    }

    partial void OnSelectedItemChanged(ArtItem? value)
    {
        Origins.Clear();
        Uses.Clear();
        Rotations.Clear();
        SelectedRotation = null;
        if (value == null)
        {
            OnPropertyChanged(nameof(HasUses));
            OnPropertyChanged(nameof(HasRotations));
            return;
        }

        foreach (var side in value.MakeRotations())
            Rotations.Add(side);
        OnPropertyChanged(nameof(HasRotations));
        SelectedRotation = Rotations.FirstOrDefault();
        OnPropertyChanged(nameof(CurrentAssetName));

        if (SelectedRotation == null)
            ShowOrigins(value.Entry.Origins);
        foreach (var use in value.Entry.Uses)
            Uses.Add(use);
        OnPropertyChanged(nameof(HasUses));
        SelectedUse = Uses.FirstOrDefault(use => use.Target != null);

        // Big pictures (title screens) start smaller, so they fit
        int largest = Math.Max(value.Entry.Width, value.Entry.Height);
        Zoom = largest switch { > 256 => 1, > 128 => 2, _ => Zoom < 3 ? 4 : Zoom };
    }

    private bool CanUseInPalette => SelectedUse?.Target != null;

    [RelayCommand(CanExecute = nameof(CanUseInPalette))]
    private void UseInPalette()
    {
        if (SelectedUse is { Target: not null } use)
            UseRequested?.Invoke(this, use);
    }
}
