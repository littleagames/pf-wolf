using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Editor.Audio;
using PFWolf.Editor.Data;

namespace PFWolf.Editor.ViewModels;

/// <summary>A sound or song as the sound browser lists it</summary>
public sealed class SoundItem(SoundEntry entry)
{
    public SoundEntry Entry { get; } = entry;
    public string Name => Entry.Name;
    public string KindText => SoundCatalog.KindName(Entry.Kind) + (Entry.IsFile ? " file" : "");

    public string ToolTip => $"{Entry.Name}  ({KindText}, {Entry.Source})"
        + (Entry.Uses.Count > 0 ? "" : "\nNothing in actordefs, mapdefs, sound-seq or game-info plays it");
}

/// <summary>Which kinds of sound the browser lists</summary>
public sealed record SoundKindOption(SoundKind? Kind, string Label)
{
    public override string ToString() => Label;
}

/// <summary>How a sound-seq entry is played: as the game would (null), or on one device</summary>
public sealed record SoundVariantOption(SoundVariant? Variant, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// The sound browser: every sound-seq sound, digitized, AdLib and PC speaker sound and song of
/// the loaded game and mods, played as the game plays them, with where each came from and what
/// plays it
/// </summary>
public sealed partial class SoundBrowserViewModel : ObservableObject, IDisposable
{
    private readonly SoundPlayer _player = new();
    private readonly DispatcherTimer _timer;
    private readonly Random _random = new();
    private List<SoundItem> _all = [];
    private HashSet<string> _modSources = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, (PFWolf.Assets.Sounds.SoundProfile Profile, string From)> _sequences = [];
    private GameContent? _content;

    // What's playing: its length (null for a song not decoded ahead) and where it started
    private TimeSpan? _playingLength;
    private TimeSpan _playingFrom;
    private int _playCount;

    public SoundBrowserViewModel()
    {
        _selectedKind = Kinds[0];
        _selectedShow = Shows[0];
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Background, (_, _) => UpdatePosition());
        _player.Finished += (_, _) =>
        {
            // Unless something else has started playing since
            var playing = _playCount;
            Dispatcher.UIThread.Post(() =>
            {
                if (playing == _playCount)
                    Stopped();
            });
        };
        _player.Volume = (float)(_volume / 100);
    }

    private static readonly (SoundKind? Kind, string Label)[] KindLabels =
    [
        (null, "All"),
        (SoundKind.Sound, "Sounds (sound-seq)"),
        (SoundKind.Digitized, "Digitized"),
        (SoundKind.AdLib, "AdLib"),
        (SoundKind.PcSpeaker, "PC speaker"),
        (SoundKind.Music, "Music"),
    ];

    public ObservableCollection<SoundKindOption> Kinds { get; } = [.. KindLabels.Select(kind => new SoundKindOption(kind.Kind, kind.Label))];

    public IReadOnlyList<ArtShowOption> Shows { get; } =
    [
        new(ArtShow.Everything, "Everything"),
        new(ArtShow.InUse, "In use"),
        new(ArtShow.NotInUse, "Not in use"),
        new(ArtShow.FromMods, "From the mods"),
    ];

    public ObservableCollection<SoundItem> Items { get; } = [];
    public ObservableCollection<string> Origins { get; } = [];
    public ObservableCollection<SoundUse> Uses { get; } = [];
    public ObservableCollection<SoundVariantOption> Variants { get; } = [];

    [ObservableProperty] private SoundKindOption _selectedKind;
    [ObservableProperty] private ArtShowOption _selectedShow;
    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private string _countText = "";
    [ObservableProperty] private string _title = "Sounds and music";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(IsSequence))]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    private SoundItem? _selectedItem;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoToUseCommand))]
    private SoundUse? _selectedUse;

    [ObservableProperty] private SoundVariantOption? _selectedVariant;

    /// <summary>The samples of the sound picked, for the waveform; null for music</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWaveform))]
    private short[]? _samples;
    private int _sampleRate = 1;

    [ObservableProperty] private string _details = "";
    [ObservableProperty] private string _definition = "";
    [ObservableProperty] private double _progress = -1;
    [ObservableProperty] private string _positionText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayLabel))]
    private bool _isPlaying;

    [ObservableProperty] private bool _loop;
    [ObservableProperty] private double _volume = 80;

    public bool HasSelection => SelectedItem != null;
    public bool HasUses => Uses.Count > 0;
    public bool HasWaveform => Samples != null;
    public bool HasDefinition => Definition.Length > 0;

    /// <summary>A sound-seq entry, which can be played on each of its devices</summary>
    public bool IsSequence => SelectedItem?.Entry.Kind == SoundKind.Sound;

    public string PlayLabel => IsPlaying ? "■ Stop" : "▶ Play";

    /// <summary>The editor has no sound player here (not Windows)</summary>
    public static bool CannotPlay => !SoundPlayer.IsAvailable;

    /// <summary>The sound shown, as Export WAV saves it: a sound, not music</summary>
    public (short[] Samples, int SampleRate)? ExportableSound => Samples != null ? (Samples, _sampleRate) : null;

    partial void OnVolumeChanged(double value) => _player.Volume = (float)(Math.Clamp(value, 0, 100) / 100);
    partial void OnDefinitionChanged(string value) => OnPropertyChanged(nameof(HasDefinition));

    /// <summary>Lists the sounds of a newly loaded game, keeping the one picked if it's still there</summary>
    public void Load(GameContent? content)
    {
        Stop();
        _content = content;
        var picked = SelectedItem?.Entry;

        if (content == null)
        {
            _all = [];
            _modSources = [];
            _sequences = [];
            Title = "Sounds and music";
        }
        else
        {
            _sequences = SoundCatalog.Sequences(content);
            _all = SoundCatalog.Build(content).Select(entry => new SoundItem(entry)).ToList();
            _modSources = content.Assets.LoadedMods.Select(mod => mod.Source.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Title = $"Sounds and music - {content.Title}";
        }

        var kindPicked = SelectedKind?.Kind;
        Kinds.Clear();
        foreach (var (kind, label) in KindLabels)
            Kinds.Add(new SoundKindOption(kind, $"{label}  ({_all.Count(item => kind == null || item.Entry.Kind == kind)})"));
        SelectedKind = Kinds.First(option => option.Kind == kindPicked);

        Refilter();
        SelectedItem = picked != null ? Items.FirstOrDefault(item => item.Entry.Kind == picked.Kind && item.Name.Equals(picked.Name, StringComparison.OrdinalIgnoreCase)) : null;
        SelectedItem ??= Items.FirstOrDefault();
    }

    /// <summary>Shows a sound or song by name, of the kind asked for when there's one of each</summary>
    public void Show(SoundKind kind, string name)
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

    partial void OnSelectedKindChanged(SoundKindOption value) => Refilter();
    partial void OnSelectedShowChanged(ArtShowOption value) => Refilter();
    partial void OnFilterChanged(string value) => Refilter();

    private void Refilter()
    {
        var selected = SelectedItem;
        var words = Filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Items.Clear();
        foreach (var item in _all.Where(Matches))
            Items.Add(item);
        CountText = Items.Count == _all.Count ? $"{Items.Count} sounds" : $"{Items.Count} of {_all.Count} sounds";
        SelectedItem = selected != null && Items.Contains(selected) ? selected : Items.FirstOrDefault();

        bool Matches(SoundItem item)
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

            // Every word is in the name, in what it plays, or in what plays it ("guard", "door 1")
            return shown && words.All(word => entry.Name.Contains(word, StringComparison.OrdinalIgnoreCase)
                || entry.Profile is { } profile && ProfileNames(profile).Any(name => name.Contains(word, StringComparison.OrdinalIgnoreCase))
                || entry.Uses.Any(use => use.Label.Contains(word, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private static IEnumerable<string> ProfileNames(PFWolf.Assets.Sounds.SoundProfile profile)
        => new[] { profile.Digitized, profile.AdLib, profile.PC, profile.Alias }.OfType<string>().Concat(profile.Random);

    partial void OnSelectedItemChanged(SoundItem? value)
    {
        Stop();
        Origins.Clear();
        Uses.Clear();
        Variants.Clear();
        SelectedVariant = null;
        Definition = "";
        Details = "";
        Samples = null;
        PositionText = "";

        if (value == null || _content == null)
        {
            OnPropertyChanged(nameof(HasUses));
            return;
        }

        var entry = value.Entry;
        foreach (var origin in entry.Origins)
            Origins.Add(origin);
        foreach (var use in entry.Uses)
            Uses.Add(use);
        OnPropertyChanged(nameof(HasUses));
        SelectedUse = Uses.FirstOrDefault();

        if (entry.Profile is { } profile)
        {
            Definition = Describe(profile);
            Variants.Add(new SoundVariantOption(null, profile.Random.Count > 0 ? "As the game plays it (a random pick)" : "As the game plays it"));
            if (profile.Random.Count > 0)
            {
                foreach (var pick in profile.Random)
                {
                    if (SoundCatalog.Variants(_content, pick).FirstOrDefault() is { } best)
                        Variants.Add(new SoundVariantOption(best, $"{pick}: {best.Label}"));
                }
            }
            else
            {
                foreach (var variant in SoundCatalog.Variants(_content, entry.Name))
                    Variants.Add(new SoundVariantOption(variant, variant.Label));
            }
            SelectedVariant = Variants[0];
        }
        else
            ShowSound(entry.Kind, entry.Name);
    }

    partial void OnSelectedVariantChanged(SoundVariantOption? value)
    {
        if (SelectedItem?.Entry is not { Kind: SoundKind.Sound } entry || _content == null)
            return;
        Stop();
        // As the game plays it: the best device's sound, or nothing to show yet for a random pick
        var variant = value?.Variant ?? (entry.Profile?.Random.Count > 0 ? null : SoundCatalog.Variants(_content, entry.Name).FirstOrDefault());
        if (variant != null)
            ShowSound(variant.Kind, variant.AssetName);
        else
        {
            Samples = null;
            Details = Variants.Count > 1 || entry.Profile?.Random.Count > 0
                ? "Sound-seq sound: each play picks one of its random sounds"
                : "Sound-seq sound with nothing to play: none of its sounds are in the game";
            PositionText = "";
        }
    }

    // Shows a sound's waveform and details, or a song's
    private void ShowSound(SoundKind kind, string assetName)
    {
        if (_content == null)
            return;

        var prefix = SelectedItem?.Entry.Kind == SoundKind.Sound ? $"Plays {SoundCatalog.KindName(kind)} {assetName}: " : "";
        if (kind == SoundKind.Music)
        {
            Samples = null;
            var length = SoundCatalog.MusicLength(_content, assetName);
            var isFile = _content.Assets.Exists<PFWolf.Assets.Sounds.MusicFileAsset>(assetName);
            Details = isFile
                ? "Music file (OGG, MP3 or WAV) from a pack's music/ folder, played in place of the game's own song of its name"
                : $"IMF song (AdLib), {Time(length ?? TimeSpan.Zero)} before it loops";
            PositionText = length is { } songLength ? $"0:00 / {Time(songLength)}" : "";
            return;
        }

        (short[] Samples, int SampleRate)? sound;
        try
        {
            sound = SoundCatalog.Samples(_content, kind, assetName);
        }
        catch (Exception e) when (e is InvalidDataException or PFWolf.Exceptions.PfWolfAudioException)
        {
            Samples = null;
            Details = $"{prefix}{assetName} can't be read: {e.Message}";
            return;
        }

        if (sound is not { } found)
        {
            Samples = null;
            Details = $"{prefix}{assetName} isn't in the game";
            return;
        }

        _sampleRate = found.SampleRate;
        Samples = found.Samples;
        var seconds = TimeSpan.FromSeconds(found.Samples.Length / (double)found.SampleRate);
        var source = kind == SoundKind.Digitized && _content.Assets.Exists<PFWolf.Assets.Sounds.SoundFileAsset>(assetName)
            ? ", from a pack's sounds/ file, played in place of the game's own"
            : "";
        Details = $"{prefix}{SoundCatalog.KindName(kind)} sound, {seconds.TotalSeconds:0.00} s at {found.SampleRate} Hz{source}";
        PositionText = $"0:00.0 / {Time(seconds, tenths: true)}";
    }

    private string Describe(PFWolf.Assets.Sounds.SoundProfile profile)
    {
        var lines = new List<string>();
        void Line(string label, string? name, SoundKind kind)
        {
            if (!string.IsNullOrWhiteSpace(name))
                lines.Add($"{label}: {name}{(SoundCatalog.HasAsset(_content!, kind, name) ? "" : "  (not in the game)")}");
        }
        Line("digitized", profile.Digitized, SoundKind.Digitized);
        Line("adlib", profile.AdLib, SoundKind.AdLib);
        Line("pc", profile.PC, SoundKind.PcSpeaker);
        if (!string.IsNullOrWhiteSpace(profile.Alias))
            lines.Add($"alias: {profile.Alias}{(_sequences.ContainsKey(profile.Alias) ? "" : "  (no such sound)")}");
        if (profile.Random.Count > 0)
            lines.Add($"random: {string.Join(", ", profile.Random)}");
        return string.Join("\n", lines);
    }

    private bool CanPlay => SelectedItem != null && SoundPlayer.IsAvailable;

    /// <summary>Plays the sound or song picked, or stops it when it's playing</summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Play()
    {
        if (IsPlaying)
            Stop();
        else
            PlayFrom(0);
    }

    /// <summary>Plays from this far into the sound (0 to 1): a click on the waveform</summary>
    public void Seek(double fraction)
    {
        if (Samples != null)
            PlayFrom(fraction);
    }

    private void PlayFrom(double fraction)
    {
        if (_content == null || SelectedItem?.Entry is not { } entry)
            return;
        Stop();
        _playCount++;

        var (kind, name) = (entry.Kind, entry.Name);
        if (kind == SoundKind.Sound)
        {
            var variant = SelectedVariant?.Variant;
            if (variant == null && SoundCatalog.Resolve(_sequences, entry.Name, count => _random.Next(count)) is { } profile)
            {
                // As the game would: the best device that has the sound
                variant = new[]
                {
                    (SoundKind.Digitized, profile.Digitized),
                    (SoundKind.AdLib, profile.AdLib),
                    (SoundKind.PcSpeaker, profile.PC),
                }.Where(option => SoundCatalog.HasAsset(_content, option.Item1, option.Item2))
                    .Select(option => new SoundVariant(option.Item1, option.Item2!.ToUpperInvariant()))
                    .FirstOrDefault();
                // A random pick shows what it played
                if (variant != null && entry.Profile?.Random.Count > 0)
                    ShowSound(variant.Kind, variant.AssetName);
            }
            if (variant == null)
                return;
            (kind, name) = (variant.Kind, variant.AssetName);
        }

        try
        {
            if (kind == SoundKind.Music)
            {
                if (SoundCatalog.Open(_content, kind, name) is not { } song)
                    return;
                _playingLength = SoundCatalog.MusicLength(_content, name);
                _playingFrom = TimeSpan.Zero;
                _player.Play(song, Loop);
            }
            else
            {
                if (Samples == null)
                    return;
                var start = (int)(Samples.Length * Math.Clamp(fraction, 0, 1));
                _playingLength = TimeSpan.FromSeconds(Samples.Length / (double)_sampleRate);
                _playingFrom = TimeSpan.FromSeconds(start / (double)_sampleRate);
                _player.Play(new PFWolf.Assets.Sounds.SampleMusicSource(Samples, _sampleRate, start), Loop);
            }
        }
        catch (Exception e) when (e is InvalidDataException or IOException)
        {
            Details = $"{name} can't be played: {e.Message}";
            return;
        }

        IsPlaying = true;
        _timer.Start();
        UpdatePosition();
    }

    /// <summary>Stops what's playing</summary>
    public void Stop()
    {
        _player.Stop();
        Stopped();
    }

    private void Stopped()
    {
        _timer.Stop();
        IsPlaying = false;
        Progress = -1;
        if (_playingLength is { } length)
            PositionText = $"{Time(TimeSpan.Zero, Samples != null)} / {Time(length, Samples != null)}";
    }

    private void UpdatePosition()
    {
        if (!IsPlaying)
            return;
        var position = _playingFrom + _player.Position;
        var tenths = Samples != null;
        if (_playingLength is { TotalSeconds: > 0 } length)
        {
            // Looping comes round to the start again
            var at = TimeSpan.FromTicks(position.Ticks % length.Ticks);
            Progress = Samples != null ? at / length : -1;
            PositionText = $"{Time(at, tenths)} / {Time(length, tenths)}";
        }
        else
            PositionText = Time(position, tenths);
    }

    private static string Time(TimeSpan time, bool tenths = false)
        => tenths ? $"{(int)time.TotalMinutes}:{time.Seconds:00}.{time.Milliseconds / 100}" : $"{(int)time.TotalMinutes}:{time.Seconds:00}";

    private bool CanGoToUse => SelectedUse?.SoundName != null;

    /// <summary>Goes to the sound-seq sound that plays the one picked</summary>
    [RelayCommand(CanExecute = nameof(CanGoToUse))]
    private void GoToUse()
    {
        if (SelectedUse?.SoundName is { } name)
            Show(SoundKind.Sound, name);
    }

    /// <summary>Goes to one of the sounds a sound-seq entry plays (or its alias)</summary>
    public void GoToVariant()
    {
        if (SelectedVariant?.Variant is { } variant)
            Show(variant.Kind, variant.AssetName);
        else if (SelectedItem?.Entry.Profile?.Alias is { Length: > 0 } alias)
            Show(SoundKind.Sound, alias);
    }

    public void Dispose()
    {
        _timer.Stop();
        _player.Dispose();
    }
}
