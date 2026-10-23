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

/// <summary>A picture as the art browser lists it, its thumbnail made when it's first shown</summary>
public sealed class ArtItem(ArtEntry entry, ArtCache art)
{
    public ArtEntry Entry { get; } = entry;
    public string Name => Entry.Name;
    public Bitmap? Thumbnail => art.Whole(Entry.Kind, Entry.AssetName);

    /// <summary>"8 sides" under a rotating sprite frame's thumbnail, "3 frames" under an animation's</summary>
    public string SidesText => Entry.Rotations.Count > 0 ? $"{Entry.Rotations.Count} sides"
        : Entry.Animation is { } animation ? $"{animation.Frames.Count} frames" : "";
    public bool IsRotating => Entry.Rotations.Count > 0;
    public bool HasSidesText => SidesText.Length > 0;

    /// <summary>An animation's frames for the frame strip</summary>
    public List<AnimationFrameItem> MakeFrames()
    {
        if (Entry.Animation is not { } animation)
            return [];
        var bitmaps = art.AnimationFrames(Entry.Kind, Entry.Name);
        return animation.Frames.Select((frame, i) => new AnimationFrameItem(frame.Name, frame.Tics, i, i < bitmaps.Count ? bitmaps[i] : null)).ToList();
    }

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

/// <summary>One frame of an animation, for the frame strip</summary>
public sealed record AnimationFrameItem(string Name, int Tics, int Index, Bitmap? Thumbnail)
{
    public string Label => $"{Index + 1}";
    public string ToolTip => $"{Name}, {Tics} tics (double-click to go to it)";
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
    /// <summary>A raw data mod's new chunks still under their made-up names (WALL0098)</summary>
    NeedsName,
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
/// A palette to show pictures in: one of the game's palettes, or (Colors null) each picture's
/// own, the one game-info draws it in (a title or end screen), else the game palette
/// </summary>
public sealed record ArtPaletteOption(string Label, string? Name, PaletteColor[]? Colors, bool IsGamePalette)
{
    public bool IsAutomatic => Colors == null;
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

    /// <summary>A mod's pictures changed: the editor adds the mod, loads the game again and shows the picture</summary>
    public event EventHandler<ArtChange>? Changed;

    /// <summary>Asks for a mod (a folder or a pk3) to save in; null when none is picked</summary>
    public Func<string, Task<string?>>? PickMod { get; set; }

    /// <summary>Asks for a line of text (title, prompt, initial, check); null when cancelled</summary>
    public Func<string, string, string, Func<string, string?>, Task<string?>>? AskText { get; set; }

    /// <summary>Asks whether to go ahead (title, message, yes button)</summary>
    public Func<string, string, string, Task<bool>>? Confirm { get; set; }

    /// <summary>Asks whether to save changes before they're lost (a pixel editor closing)</summary>
    public Func<string, Task<UnsavedChoice>>? AskUnsaved { get; set; }

    /// <summary>A picture is to be drawn in the pixel editor: the window opens one for it</summary>
    public event EventHandler<PixelEditorViewModel>? PixelEditRequested;

    private static readonly (ArtKind? Kind, string Label)[] KindLabels =
    [
        (null, "All"),
        (ArtKind.Texture, "Wall textures"),
        (ArtKind.Flat, "Flats"),
        (ArtKind.Sprite, "Sprites"),
        (ArtKind.Picture, "Pictures"),
        (ArtKind.Movie, "Movies"),
    ];

    public ObservableCollection<ArtKindOption> Kinds { get; } = [.. KindLabels.Select(kind => new ArtKindOption(kind.Kind, kind.Label))];

    public IReadOnlyList<ArtShowOption> Shows { get; } =
    [
        new(ArtShow.Everything, "Everything"),
        new(ArtShow.InUse, "In use"),
        new(ArtShow.NotInUse, "Not in use"),
        new(ArtShow.FromMods, "From the mods"),
        new(ArtShow.NeedsName, "Needs a name"),
    ];

    // A raw data mod's chunks its raw-data-map.yaml still marks to name, with that mod
    private Dictionary<string, ModEntry> _toName = new(StringComparer.OrdinalIgnoreCase);

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

    public static readonly ArtPaletteOption AutomaticPalette = new("Its own palette", null, null, false);

    /// <summary>The palettes the preview can show a picture in: each picture's own, then every palette the game has</summary>
    public ObservableCollection<ArtPaletteOption> Palettes { get; } = [AutomaticPalette];

    /// <summary>The palette the preview shows pictures in (the thumbnails stay in the game palette)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview), nameof(PaletteText))]
    private ArtPaletteOption _selectedPalette = AutomaticPalette;

    // Pictures drawn in a palette of their own (game-info's title and end screens), by picture name
    private Dictionary<string, ArtPaletteOption> _ownPalettes = new(StringComparer.OrdinalIgnoreCase);

    // The preview in a palette other than the game's, by palette and picture, each made once
    private readonly Dictionary<(string Palette, ArtKind Kind, string Name), Bitmap?> _recolored = [];

    /// <summary>The palette the picture shown is drawn in: the one picked, or its own (null: the game palette)</summary>
    public ArtPaletteOption? ShownPalette => PaletteFor(CurrentAssetName);

    private ArtPaletteOption? PaletteFor(string assetName)
    {
        if (!SelectedPalette.IsAutomatic)
            return SelectedPalette.IsGamePalette ? null : SelectedPalette;
        return _ownPalettes.GetValueOrDefault(assetName) ?? (SelectedItem != null ? _ownPalettes.GetValueOrDefault(SelectedItem.Entry.Name) : null);
    }

    /// <summary>Which palette the preview is in, under the palette picker</summary>
    public string PaletteText
    {
        get
        {
            if (SelectedItem == null)
                return "";
            if (SelectedItem.Entry.Kind == ArtKind.Movie)
                return "In its own palette (movies.yaml palette), else the game palette, as the game plays it";
            var shown = ShownPalette;
            if (SelectedPalette.IsAutomatic)
                return shown != null ? $"In {shown.Name}, the palette the game shows it in" : "In the game palette";
            return shown != null ? $"Shown in {shown.Name}; saving keeps its colors' numbers (the game palette's)" : "In the game palette";
        }
    }

    /// <summary>The palettes for the pixel editor: the game palette as loaded first, then the rest</summary>
    private List<ArtPaletteOption> PixelPalettes()
    {
        var game = new ArtPaletteOption(Palettes.FirstOrDefault(option => option.IsGamePalette)?.Label ?? "Game palette",
            Palettes.FirstOrDefault(option => option.IsGamePalette)?.Name, _content!.LoadedPalette, true);
        return [game, .. Palettes.Where(option => !option.IsAutomatic && !option.IsGamePalette)];
    }

    private void ForgetRecolored()
    {
        foreach (var bitmap in _recolored.Values)
            bitmap?.Dispose();
        _recolored.Clear();
    }

    // The game's palettes, and which pictures game-info draws in one of their own
    private void LoadPalettes(GameContent? content)
    {
        var picked = SelectedPalette;
        ForgetRecolored();
        Palettes.Clear();
        Palettes.Add(AutomaticPalette);
        _ownPalettes = new(StringComparer.OrdinalIgnoreCase);
        if (content != null)
        {
            foreach (var palette in PaletteCatalog.Build(content))
            {
                var option = new ArtPaletteOption(palette.IsGamePalette ? $"{palette.Name} (game palette)" : palette.Name, palette.Name, palette.Colors, palette.IsGamePalette);
                Palettes.Add(option);
                if (palette.IsGamePalette)
                    continue;
                foreach (var use in palette.Uses)
                    if (use.Picture != null)
                        _ownPalettes.TryAdd(use.Picture, option);
            }
        }
        SelectedPalette = Palettes.FirstOrDefault(option => option.Name != null && option.Name.Equals(picked.Name, StringComparison.OrdinalIgnoreCase)) ?? AutomaticPalette;
    }

    public ObservableCollection<ArtItem> Items { get; } = [];

    [ObservableProperty] private ArtKindOption _selectedKind;
    [ObservableProperty] private ArtShowOption _selectedShow;
    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private double _thumbSize = 64;
    [ObservableProperty] private ArtBackgroundOption _selectedBackground;
    [ObservableProperty] private string _countText = "";
    [ObservableProperty] private string _title = "Textures and sprites";
    [ObservableProperty] private string _status = "";

    /// <summary>The mod file the picture shown comes from; null for the game's own</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasModFile), nameof(FileText))]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand), nameof(DeleteCommand))]
    private ArtFile? _modFile;

    public bool HasModFile => ModFile != null;

    /// <summary>Where a change to the picture shown goes</summary>
    public string FileText => SelectedItem?.Entry.Kind == ArtKind.Movie
        ? "One of the game's movie files (gamepack-info's JamMovieFileLoader): the editor only plays it"
        : IsAnimation
        ? "Its frames are pictures of their own (change those); animdefs says how it plays (Definitions, F11: Animations)"
        : IsCrop
        ? "Cut from another picture (change that); graphicdefs says where (Definitions, F11: Cropped pictures)"
        : ModFile != null
            ? $"In {ModFile}: changes are saved there"
            : SelectedItem != null && _toName.TryGetValue(SelectedItem.Entry.Name, out var rawFile)
            ? $"A new chunk of {rawFile.ModName}'s data files, under the name the generator made up: Name… gives it one in its {rawFile.EntryPath}"
            : SelectedItem != null ? "The game's own: a change puts a picture of the same name in a mod" : "";

    // The sprite's offset, as its PNG's grAb chunk gives it
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyOffsetCommand))]
    private decimal? _offsetX;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyOffsetCommand))]
    private decimal? _offsetY;

    public bool IsSprite => SelectedItem?.Entry.Kind == ArtKind.Sprite;

    /// <summary>The kind of the picture shown (null when none is)</summary>
    public ArtKind? CurrentKind => SelectedItem?.Entry.Kind;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(Preview), nameof(PreviewWidth), nameof(PreviewHeight), nameof(Details), nameof(CanReplace))]
    [NotifyCanExecuteChangedFor(nameof(UseInPaletteCommand), nameof(NameChunkCommand))]
    private ArtItem? _selectedItem;

    /// <summary>Replace… applies: a picture is picked that isn't an animation or a crop</summary>
    public bool CanReplace => HasSelection && !IsAnimation && !IsCrop;

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

    /// <summary>The frames of the animation picked, for the frame strip</summary>
    public ObservableCollection<AnimationFrameItem> Frames { get; } = [];

    public bool HasFrames => Frames.Count > 0;

    /// <summary>The animation picked is an animation (animdefs): its pictures are its frames', which are edited as themselves</summary>
    public bool IsAnimation => SelectedItem?.Entry.Animation != null;

    /// <summary>
    /// The picture picked is cut from another (graphicdefs): its pixels are its source's, and a
    /// mod's picture of its name would be cut over, so it's changed through those
    /// </summary>
    public bool IsCrop => SelectedItem?.Entry.Crop != null;

    /// <summary>The frame shown while it isn't playing; picking one stops it</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview), nameof(PreviewWidth), nameof(PreviewHeight), nameof(CurrentAssetName))]
    private AnimationFrameItem? _selectedFrame;

    /// <summary>The animation picked plays in the preview</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayText))]
    private bool _isPlaying;

    public string PlayText => IsPlaying ? "❚❚ Pause" : "▶ Play";

    // The frame playing shows, and the clock it plays by
    private AnimationFrameItem? _playingFrame;
    private readonly System.Diagnostics.Stopwatch _playClock = new();
    private Avalonia.Threading.DispatcherTimer? _playTimer;

    /// <summary>The picture shown: the entry's, the side of a rotating frame picked, or an animation's frame</summary>
    public Bitmap? Preview
    {
        get
        {
            var frame = IsPlaying ? _playingFrame : SelectedFrame;
            var bitmap = frame?.Thumbnail ?? SelectedRotation?.Thumbnail ?? SelectedItem?.Thumbnail;
            if (bitmap == null || _content == null || SelectedItem?.Entry is not { } entry)
                return bitmap;

            // In another palette: drawn again from its indices (a frame by its own name, unless it's the animation's)
            var name = frame != null && !frame.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase) ? frame.Name
                : SelectedRotation?.AssetName ?? entry.AssetName;
            if (PaletteFor(name) is not { Colors: { } colors, Name: { } palette })
                return bitmap;
            var key = (palette, entry.Kind, name.ToLowerInvariant());
            if (!_recolored.TryGetValue(key, out var recolored))
                _recolored[key] = recolored = ArtCache.Indexed(_content, entry.Kind, name) is { } picture ? ArtCache.Render(picture, colors) : null;
            return recolored ?? bitmap;
        }
    }

    /// <summary>The asset the preview shows ("GARDA3" for side 3 of GARDA, an animation's frame): what Copy name and Export PNG take</summary>
    public string CurrentAssetName => (IsPlaying ? null : SelectedFrame)?.Name ?? SelectedRotation?.AssetName ?? SelectedItem?.Entry.AssetName ?? "";

    /// <summary>Plays the animation picked, or pauses it on the frame showing</summary>
    [RelayCommand]
    private void TogglePlay()
    {
        if (IsPlaying)
        {
            var showing = _playingFrame;
            IsPlaying = false;
            SelectedFrame = showing;
        }
        else if (HasFrames)
            Play();
    }

    private void Play()
    {
        SelectedFrame = null;
        _playClock.Restart();
        _playingFrame = Frames.FirstOrDefault();
        IsPlaying = true;
        _playTimer ??= new Avalonia.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(1000.0 / 70), Avalonia.Threading.DispatcherPriority.Render, (_, _) => PlayTick());
        _playTimer.Start();
        OnPreviewChanged();
    }

    /// <summary>Stops the animation playing (the window closing, another picture picked)</summary>
    public void StopPlaying()
    {
        _playTimer?.Stop();
        IsPlaying = false;
        _playingFrame = null;
    }

    partial void OnIsPlayingChanged(bool value)
    {
        if (!value)
            _playTimer?.Stop();
        OnPreviewChanged();
        OnPropertyChanged(nameof(CurrentAssetName));
    }

    // Shows the frame the animation's at, 70 tics a second as the game plays it
    private void PlayTick()
    {
        if (!IsPlaying || SelectedItem?.Entry.Animation is not { } animation || Frames.Count == 0)
        {
            StopPlaying();
            return;
        }
        int tics = (int)(_playClock.ElapsedMilliseconds * 70 / 1000);
        var frame = Frames[Math.Min(animation.FrameAt(tics), Frames.Count - 1)];
        if (frame == _playingFrame)
            return;
        _playingFrame = frame;
        OnPreviewChanged();
    }

    // Frames can differ in size, so the preview's size goes with it
    private void OnPreviewChanged()
    {
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(PreviewWidth));
        OnPropertyChanged(nameof(PreviewHeight));
    }

    partial void OnSelectedFrameChanged(AnimationFrameItem? value)
    {
        if (value != null && IsPlaying)
        {
            _playTimer?.Stop();
            IsPlaying = false;
        }
    }

    /// <summary>Goes to a frame's own texture or picture</summary>
    public void ShowFrame(AnimationFrameItem frame)
    {
        if (SelectedItem?.Entry is not { } entry)
            return;
        if (entry.Kind == ArtKind.Movie)
        {
            Status = $"{frame.Name} is only in the movie, not a picture of its own";
            return;
        }
        // A frame with the animation's own name is the animation's entry; its picture is the first frame
        if (frame.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase))
        {
            Status = $"{frame.Name} is the animation's own name: its first frame is the {KindName(entry.Kind).ToLowerInvariant()} it animates";
            return;
        }
        Show(entry.Kind, frame.Name);
    }

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
                var (originX, originY) = sprite.Origin;
                var sprites = $"Sprite {name}, {sprite.Width} × {sprite.Height}, offset {originX}, {originY}"
                              + (sprite.HasOffset ? "" : " (it has none: it stands as Wolf3D's do)");
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
            if (entry.Kind == ArtKind.Movie && entry.Animation is { } movie)
            {
                details += $"\nA JAM movie: {movie.Frames.Count} frames, {movie.TotalTics} tics ({movie.TotalTics / 70.0:0.##} s) as the game plays it "
                           + "(movies.yaml frame-tics, with its pauses); the preview leaves out its fades and sounds.";
            }
            else if (entry.Animation is { } animation)
            {
                details += $"\nAn animation (animdefs): {animation.Frames.Count} frames, {animation.TotalTics} tics ({animation.TotalTics / 70.0:0.##} s), "
                           + (animation.Loops ? "over and over" : "once, holding the last frame (the preview starts it over)")
                           + ". The map and 3D view show its first frame. Its frames are edited as themselves: double-click one to go to it.";
            }
            if (entry.Crop is { } crop)
            {
                details += $"\nCut from {crop.Source?.ToUpperInvariant()} at {crop.X}, {crop.Y} (graphicdefs)"
                           + (string.IsNullOrWhiteSpace(crop.Background) ? "" : $", see-through pixels filled with {crop.Background}");
                if (_content?.Assets.CropProblems.GetValueOrDefault(entry.Name) is { } problem)
                    details += $"\n⚠ {problem}";
            }
            return details;
        }
    }

    public static string KindName(ArtKind kind) => kind switch
    {
        ArtKind.Texture => "Wall texture",
        ArtKind.Flat => "Flat",
        ArtKind.Sprite => "Sprite",
        ArtKind.Movie => "Movie",
        _ => "Picture",
    };

    /// <summary>Lists the pictures of a newly loaded game, keeping the one picked if it's still there</summary>
    public void Load(GameContent? content, ArtCache? art)
    {
        _content = content;
        _art = art;
        var picked = SelectedItem?.Entry;
        LoadPalettes(content);

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
        _toName = content?.ChunksToName.ToDictionary(StringComparer.OrdinalIgnoreCase) ?? new(StringComparer.OrdinalIgnoreCase);

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
        {
            ShowOrigins(ArtCatalog.Origins(_content, value.AssetName, nameof(SpriteAsset)));
            RefreshFile();
        }
        OnPropertyChanged(nameof(PaletteText));
    }

    partial void OnSelectedPaletteChanged(ArtPaletteOption value) => OnPreviewChanged();

    // Where the picture shown comes from, and a sprite's offset
    private void RefreshFile()
    {
        ModFile = _content != null && SelectedItem != null ? ArtFiles.InMod(_content, SelectedItem.Entry.Kind, CurrentAssetName) : null;
        OnPropertyChanged(nameof(FileText));
        OnPropertyChanged(nameof(IsSprite));
        OnPropertyChanged(nameof(CurrentKind));
        var sprite = IsSprite ? _content?.Find<SpriteAsset>(CurrentAssetName) : null;
        OffsetX = sprite?.Origin.X;
        OffsetY = sprite?.Origin.Y;
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
                ArtShow.NeedsName => _toName.ContainsKey(entry.Name),
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
        StopPlaying();
        SelectedFrame = null;
        Frames.Clear();
        OnPropertyChanged(nameof(IsAnimation));
        OnPropertyChanged(nameof(IsCrop));
        EditPixelsCommand.NotifyCanExecuteChanged();
        EditOutsideCommand.NotifyCanExecuteChanged();
        if (value == null)
        {
            OnPropertyChanged(nameof(HasUses));
            OnPropertyChanged(nameof(HasRotations));
            OnPropertyChanged(nameof(HasFrames));
            RefreshFile();
            return;
        }

        foreach (var side in value.MakeRotations())
            Rotations.Add(side);
        OnPropertyChanged(nameof(HasRotations));
        foreach (var frame in value.MakeFrames())
            Frames.Add(frame);
        OnPropertyChanged(nameof(HasFrames));
        if (HasFrames)
            Play();
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
        RefreshFile();
        OnPropertyChanged(nameof(PaletteText));
    }

    private bool CanUseInPalette => SelectedUse?.Target != null;

    [RelayCommand(CanExecute = nameof(CanUseInPalette))]
    private void UseInPalette()
    {
        if (SelectedUse is { Target: not null } use)
            UseRequested?.Invoke(this, use);
    }

    //
    // Changing the mods' pictures
    //

    /// <summary>
    /// What adding a picture of this kind and name would do to one the game has already, for the
    /// add dialog; null when there's none
    /// </summary>
    public string? WhatAddingReplaces(ArtKind kind, string name)
    {
        if (_content == null || !ArtFiles.Exists(_content, kind, name))
            return null;
        if (kind == ArtKind.Picture && _content.Assets.Crops.GetValueOrDefault(name) is { } crop)
            return $"graphicdefs cuts {name.ToUpperInvariant()} from {crop.Source?.ToUpperInvariant()}, and that's used over a picture of the name: this one won't show.";
        var from = ArtCatalog.Origins(_content, name, ArtFiles.AssetType(kind)).LastOrDefault(origin => origin.Action != PFWolf.Loaders.AssetOrigin.LeftOut)?.Source;
        return $"It replaces the {name.ToUpperInvariant()} the game has{(from != null ? $" (from {from})" : "")}.";
    }

    /// <summary>Puts new pictures (or ones in place of the game's of those names) in a mod picked now</summary>
    public async Task AddAsync(ArtKind kind, IReadOnlyList<(string Name, byte[] Data)> pictures)
    {
        if (_content == null || pictures.Count == 0)
            return;
        var what = pictures.Count == 1 ? pictures[0].Name.ToUpperInvariant() : $"{pictures.Count} pictures";
        await Make(mod => ArtFiles.Add(kind, pictures, mod!), $"A mod to add {what} to (it goes in its {ArtFiles.Folder(kind)} folder)");
    }

    /// <summary>Puts a picture file in place of the picture shown: over its mod file, or in a mod picked now for the game's own</summary>
    public async Task ReplaceAsync(string fileName, byte[] data)
    {
        if (_content is not { } content || SelectedItem?.Entry is not { } entry)
            return;
        if (entry.Animation != null)
        {
            Status = $"{entry.Name} is an animation: replace its frames (double-click one to go to it)";
            return;
        }
        if (entry.Crop is { } crop)
        {
            Status = $"{entry.Name} is cut from {crop.Source?.ToUpperInvariant()}: replace that, or change where it's cut in Definitions (F11)";
            return;
        }
        var name = CurrentAssetName;
        await Make(mod => ArtFiles.Replace(content, entry.Kind, name, data, mod),
            ModFile == null ? $"A mod to put {fileName} in, in place of the game's {name} (it goes in its {ArtFiles.Folder(entry.Kind)} folder)" : null);
    }

    /// <summary>Saves a picture drawn in the game palette (from the pixel editor) over its mod file, or in a mod picked now</summary>
    public async Task<bool> SavePictureAsync(ArtKind kind, string name, IndexedPicture picture, (int X, int Y)? offset)
    {
        if (_content is not { } content)
            return false;
        var inMod = ArtFiles.InMod(content, kind, name) != null;
        return await Make(mod => ArtFiles.Save(content, kind, name, picture, offset, mod),
            inMod ? null : $"A mod to save {name.ToUpperInvariant()} in (it goes in its {ArtFiles.Folder(kind)} folder)");
    }

    private bool CanApplyOffset => IsSprite && OffsetX != null && OffsetY != null;

    /// <summary>Gives the sprite shown the offset typed in (its PNG's grAb chunk)</summary>
    [RelayCommand(CanExecute = nameof(CanApplyOffset))]
    private async Task ApplyOffsetAsync()
    {
        if (_content is not { } content || OffsetX is not { } x || OffsetY is not { } y)
            return;
        var name = CurrentAssetName;
        await Make(mod => ArtFiles.SetOffset(content, name, (int)x, (int)y, mod),
            ModFile == null ? $"A mod to save {name} with its new offset in (it goes in its sprites folder)" : null);
    }

    /// <summary>Renames the mod's picture shown (what uses it by name doesn't follow)</summary>
    [RelayCommand(CanExecute = nameof(HasModFile))]
    private async Task RenameAsync()
    {
        if (_content is not { } content || ModFile is not { } file || SelectedItem?.Entry is not { } entry || AskText == null)
            return;
        var name = CurrentAssetName;
        var uses = entry.Uses.Count > 0 ? $" {UsesText(entry)} use it by its name, and won't follow." : "";
        var newName = await AskText($"Rename {name}", $"A new name for {name} in {file}.{uses}", name, typed =>
            ArtFiles.CheckName(typed)
            ?? (typed.Equals(name, StringComparison.OrdinalIgnoreCase) ? "That's its name now." : null)
            ?? (ArtFiles.Exists(content, entry.Kind, typed) ? $"The game has a {typed.ToUpperInvariant()} already." : null));
        if (newName != null)
            await Make(_ => ArtFiles.Rename(file, entry.Kind, newName), null);
    }

    private bool CanNameChunk => SelectedItem != null && _toName.ContainsKey(SelectedItem.Entry.Name);

    /// <summary>
    /// Names a raw data mod's new chunk (WALL0098) in its raw-data-map.yaml, and in the mapdefs
    /// walls the generator gave it; the picture is still read from the data file
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanNameChunk))]
    private async Task NameChunkAsync()
    {
        if (_content is not { } content || SelectedItem?.Entry is not { } entry || AskText == null
            || !_toName.TryGetValue(entry.Name, out var file))
            return;
        var name = entry.Name;
        var newName = await AskText($"Name {name}", $"A name for {name}, a chunk of the raw data mod's data files, in {file.ModName}'s "
            + $"{file.EntryPath} (and the mapdefs walls made for it). Other definitions that use it by name don't follow.", name, typed =>
            ArtFiles.CheckName(typed)
            ?? (typed.Equals(name, StringComparison.OrdinalIgnoreCase) ? "That's its name now." : null)
            ?? (ArtFiles.Exists(content, entry.Kind, typed) ? $"The game has a {typed.ToUpperInvariant()} already." : null));
        if (newName != null)
            await Make(_ => RawDataMapFile.Rename(file, entry.Kind, name, newName.ToUpperInvariant()), null);
    }

    /// <summary>Takes the mod's picture shown out of its mod (the game's own of that name shows again)</summary>
    [RelayCommand(CanExecute = nameof(HasModFile))]
    private async Task DeleteAsync()
    {
        if (_content is not { } content || ModFile is not { } file || SelectedItem?.Entry is not { } entry || Confirm == null)
            return;
        var name = CurrentAssetName;
        var under = ArtCatalog.Origins(content, name, ArtFiles.AssetType(entry.Kind)).Count(origin => origin.Action != PFWolf.Loaders.AssetOrigin.LeftOut) > 1;
        var after = under
            ? $"The {name} it replaced shows again."
            : entry.Uses.Count > 0 ? $"Nothing else is named {name}: {UsesText(entry)} will show nothing." : "Nothing uses it.";
        if (!await Confirm($"Delete {name}", ModFiles.DeleteQuestion(file.ModPath, file.EntryPath, after), "Delete"))
            return;
        await Make(_ => ArtFiles.Delete(file, entry.Kind, name), null);
    }

    //
    // The pixel editor
    //

    /// <summary>A picture that can be drawn on or replaced: picked, and not an animation (its frames are)</summary>
    private bool CanEditPicture => HasSelection && !IsAnimation && !IsCrop;

    /// <summary>Opens the picture shown in the pixel editor</summary>
    [RelayCommand(CanExecute = nameof(CanEditPicture))]
    private void EditPixels()
    {
        if (_content is not { } content || SelectedItem?.Entry is not { } entry)
            return;
        var name = CurrentAssetName;
        if (ArtCache.Indexed(content, entry.Kind, name) is not { } picture)
        {
            Status = $"There's no {name} to draw";
            return;
        }
        // The point it stands on (where it has no offset, Wolf3D's), which saving writes as its offset
        (int, int)? offset = entry.Kind == ArtKind.Sprite && content.Find<SpriteAsset>(name) is { } sprite ? sprite.Origin : null;
        OpenPixelEditor(new PixelDocument(entry.Kind, name, picture, offset));
    }

    /// <summary>What's wrong with a new picture's name, or null when it will do</summary>
    public string? CheckNewName(ArtKind kind, string name)
        => ArtFiles.CheckName(name)
           ?? (_content != null && ArtFiles.Exists(_content, kind, name) ? $"The game has a {name.ToUpperInvariant()} already: draw on that one instead." : null);

    /// <summary>Opens a new blank picture in the pixel editor (it's added to a mod when it's saved)</summary>
    public void NewPicture(ArtKind kind, string name, int width, int height, bool clear)
    {
        if (_content == null)
            return;
        OpenPixelEditor(PixelDocument.Blank(kind, name, width, height, clear ? PixelDocument.Clear : 0));
    }

    private void OpenPixelEditor(PixelDocument document)
    {
        // In the palette the preview shows it in
        var palettes = PixelPalettes();
        var shown = PaletteFor(document.Name);
        var start = palettes.FirstOrDefault(option => shown != null && option.Name == shown.Name) ?? palettes[0];
        var editor = new PixelEditorViewModel(document, palettes, start,
            saving => SavePictureAsync(saving.Kind, saving.Name, saving.Picture, saving.Offset))
        {
            AskUnsaved = AskUnsaved,
        };
        PixelEditRequested?.Invoke(this, editor);
    }

    //
    // Editing in another program
    //

    // Made the first time a picture's opened outside; its watchers end with the editor
    private ExternalArtEdits? _external;

    /// <summary>The paint program pictures open in; empty for the one Windows edits PNGs with</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageEditorText))]
    private string _imageEditor = "";

    public string ImageEditorText => string.IsNullOrWhiteSpace(ImageEditor)
        ? "Opens in the program Windows edits PNGs with"
        : $"Opens in {Path.GetFileNameWithoutExtension(ImageEditor)}";

    /// <summary>Pictures saved in the other program and not brought in yet, the oldest first</summary>
    public ObservableCollection<ExternalArt> SavedOutside { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SavedOutsideText), nameof(HasSavedOutside))]
    private int _savedOutsideCount;

    public bool HasSavedOutside => SavedOutsideCount > 0;

    public string SavedOutsideText => SavedOutside.Count switch
    {
        0 => "",
        1 => $"{SavedOutside[0].AssetName} was saved in the other program.",
        _ => $"{SavedOutside[0].AssetName} and {SavedOutside.Count - 1} more were saved in the other program.",
    };

    /// <summary>Whether a save in the other program is brought in at once (the game loads again each time)</summary>
    [ObservableProperty] private bool _bringInAtOnce;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WatchingText), nameof(IsWatching))]
    private int _watchingCount;

    public bool IsWatching => WatchingCount > 0;

    public string WatchingText => WatchingCount switch
    {
        0 => "",
        1 => "Watching 1 picture open in another program",
        _ => $"Watching {WatchingCount} pictures open in other programs",
    };

    private ExternalArtEdits External
    {
        get
        {
            if (_external == null)
            {
                _external = new ExternalArtEdits(Path.Combine(Path.GetTempPath(), "PFWolf.Editor", "art"),
                    action => Avalonia.Threading.Dispatcher.UIThread.Post(action));
                _external.Saved += async (_, art) => await OnSavedOutside(art);
            }
            return _external;
        }
    }

    /// <summary>Opens the picture shown in the paint program, and watches for it being saved there</summary>
    [RelayCommand(CanExecute = nameof(CanEditPicture))]
    private void EditOutside()
    {
        if (_content is not { } content || SelectedItem?.Entry is not { } entry)
            return;
        var name = CurrentAssetName;
        try
        {
            var art = External.Start(entry.Kind, name, () => ArtFiles.ToPng(content, entry.Kind, name));
            ExternalArtEdits.Open(art.FilePath, ImageEditor);
            WatchingCount = External.Watched.Count;
            Status = $"Opened {art.FileName}: save it there, then bring it in here ({art.FilePath})";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Status = $"Couldn't open {name} in another program: {e.Message}";
        }
    }

    private async Task OnSavedOutside(ExternalArt art)
    {
        if (!SavedOutside.Contains(art))
            SavedOutside.Add(art);
        SavedOutsideCount = SavedOutside.Count;
        if (BringInAtOnce)
            await BringInAsync();
    }

    /// <summary>Brings the first picture saved in the other program into its mod (in place of the picture it was opened from)</summary>
    [RelayCommand]
    private async Task BringInAsync()
    {
        if (_content is not { } content || SavedOutside.Count == 0)
            return;
        var art = SavedOutside[0];
        SavedOutside.RemoveAt(0);
        SavedOutsideCount = SavedOutside.Count;

        byte[] data;
        try
        {
            data = ExternalArtEdits.Read(art);
            ArtFiles.Check(data);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or IndexOutOfRangeException)
        {
            Status = $"{art.FileName} can't be read: {e.Message}";
            return;
        }

        var inMod = ArtFiles.InMod(content, art.Kind, art.AssetName) != null;
        await Make(mod => ArtFiles.Replace(content, art.Kind, art.AssetName, data, mod),
            inMod ? null : $"A mod to put the {art.AssetName} drawn in the other program in (it goes in its {ArtFiles.Folder(art.Kind)} folder)");
    }

    /// <summary>Leaves the pictures saved in the other program as they are (they're still watched)</summary>
    [RelayCommand]
    private void NotNow()
    {
        SavedOutside.Clear();
        SavedOutsideCount = 0;
    }

    /// <summary>Stops watching every picture open in another program</summary>
    [RelayCommand]
    private void StopWatching()
    {
        foreach (var art in External.Watched)
            External.Stop(art);
        NotNow();
        WatchingCount = 0;
        Status = "Stopped watching the pictures open in other programs";
    }

    private static string UsesText(ArtEntry entry) => entry.Uses.Count == 1 ? entry.Uses[0].Label : $"{entry.Uses.Count} things";

    // Works out a change (with a mod picked first when it asks for one), makes it and says so;
    // false when it isn't made
    private async Task<bool> Make(Func<string?, ArtChange> plan, string? pickModPrompt)
    {
        string? mod = null;
        if (pickModPrompt != null)
        {
            mod = PickMod == null ? null : await PickMod(pickModPrompt);
            if (mod == null)
                return false;
        }

        try
        {
            var change = plan(mod);
            var where = change.Apply();
            Status = $"{change.Summary}: {(change.Writes.Count > 0 ? where : change.ModPath)}";
            Changed?.Invoke(this, change);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or InvalidOperationException)
        {
            Status = $"Couldn't do it: {e.Message}";
            return false;
        }
    }
}
