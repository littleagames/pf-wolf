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
    public Bitmap? Thumbnail => art.Whole(Entry.Kind, Entry.Name);

    public string ToolTip => $"{Entry.Name}  {Entry.Width} × {Entry.Height}  ({ArtBrowserViewModel.KindName(Entry.Kind)}, {Entry.Source})"
        + (Entry.Uses.Count > 0 ? "" : "\nNot used by mapdefs, actordefs or game-info");
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

    public bool HasSelection => SelectedItem != null;
    public bool HasUses => Uses.Count > 0;
    public Bitmap? Preview => SelectedItem?.Thumbnail;
    public double PreviewWidth => (SelectedItem?.Entry.Width ?? 0) * Zoom;
    public double PreviewHeight => (SelectedItem?.Entry.Height ?? 0) * Zoom;

    /// <summary>What the picture is, its size and anything particular to its kind</summary>
    public string Details
    {
        get
        {
            if (SelectedItem?.Entry is not { } entry)
                return "";

            var details = $"{KindName(entry.Kind)}, {entry.Width} × {entry.Height}";
            if (entry.Kind is ArtKind.Texture && entry.Height > TextureAsset.StorySize)
            {
                var stories = (double)entry.Height / Math.Max(1, entry.Width);
                details += $", {stories:0.##} stories tall";
            }
            if (entry.Kind == ArtKind.Sprite && _content?.Find<SpriteAsset>(entry.Name) is { } sprite)
            {
                details += $", offset {sprite.Offset.x}, {sprite.Offset.y}";
                if (ArtCatalog.SpriteFrame(entry.Name) is { } frame)
                    details += entry.Name[^1] == '0' ? ", seen the same from every side" : $", frame {frame[^1]} seen from side {entry.Name[^1]}";
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

    /// <summary>Shows a picture by name, of the kind asked for when there's one of each</summary>
    public void Show(ArtKind kind, string name)
    {
        var found = _all.FirstOrDefault(item => item.Entry.Kind == kind && item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? _all.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
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
                || entry.Uses.Any(use => use.Label.Contains(word, StringComparison.OrdinalIgnoreCase)));
        }
    }

    partial void OnSelectedItemChanged(ArtItem? value)
    {
        Origins.Clear();
        Uses.Clear();
        if (value == null)
        {
            OnPropertyChanged(nameof(HasUses));
            return;
        }

        foreach (var origin in value.Entry.Origins)
            Origins.Add($"{origin.Action} by {origin.Source}: {origin.Path}");
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
