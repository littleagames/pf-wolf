using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Editor.Rendering;
using PFWolf.Entities.Actors;
using PFWolf.Shading;

namespace PFWolf.Editor.ViewModels;

/// <summary>Where the preview's shading comes from: a level's game-info (null: the default map's)</summary>
public sealed record ShadingSource(string? MapName, string Label)
{
    public override string ToString() => Label;
}

/// <summary>A light zone the preview can stand the frame in (0: none, the level's light)</summary>
public sealed record ZoneChoice(int Id, string Label)
{
    public override string ToString() => Label;
}

// "Seen from the player": the preview's frame shaded as the game shades it at a distance
// (docs/dev/lighting-in-common-plan.md phases 1 and 2): a level's shading (the default map's to
// begin with) and one of its light zones, resolved by the game's own rules, with values set by
// hand over them (never saved)
public sealed partial class ActorBrowserViewModel
{
    private readonly ShadePreview _shadePreview = new();
    private readonly LightenLevelTable _lightenLevels = new();
    private readonly List<string> _lightenWarnings = [];
    private readonly Dictionary<(string Name, byte[]? Row, int Offset), Bitmap?> _shadedBitmaps = [];

    // The level's zones as the game sets them up, ticked with the preview (their own Random, as the game's)
    private Dictionary<int, ZoneState> _previewZones = [];
    private readonly Random _zoneRandom = new();

    /// <summary>The preview shows the frame shaded as the player would see it, at <see cref="Distance"/></summary>
    [ObservableProperty] private bool _seenFromPlayer;

    /// <summary>How far away (tiles, to its middle along the view) the frame is seen from</summary>
    [ObservableProperty] private double _distance = 8;

    [ObservableProperty] private string _shadeText = "";

    public const double NearestDistance = 0.75, FarthestDistance = 32;

    public static readonly ShadingSource DefaultMapSource = new(null, "Default map (levels without their own)");
    public static readonly ZoneChoice NoZone = new(0, "No zone (the level's light)");

    public ObservableCollection<ShadingSource> ShadingSources { get; } = [DefaultMapSource];
    public ObservableCollection<ZoneChoice> PreviewZones { get; } = [NoZone];

    [ObservableProperty] private ShadingSource _selectedShadingSource = DefaultMapSource;
    [ObservableProperty] private ZoneChoice _selectedPreviewZone = NoZone;

    // Set by hand over the level's (empty: the level's), as a map's own game-info shading would
    [ObservableProperty] private string _handLight = "";
    [ObservableProperty] private string _handFadeColor = "";
    [ObservableProperty] private string _handFadeStart = "";
    [ObservableProperty] private string _handFadeEnd = "";
    [ObservableProperty] private string _handMaxFade = "";
    [ObservableProperty] private string _handFadeCurve = "";

    public IReadOnlyList<string> FadeCurveNames { get; } = ["linear", "inverse"];

    partial void OnSeenFromPlayerChanged(bool value) => DrawFrame();
    partial void OnDistanceChanged(double value) => DrawFrame();
    partial void OnSelectedPreviewZoneChanged(ZoneChoice value) => DrawFrame();
    partial void OnHandLightChanged(string value) => DrawFrame();
    partial void OnHandFadeColorChanged(string value) => DrawFrame();
    partial void OnHandFadeStartChanged(string value) => DrawFrame();
    partial void OnHandFadeEndChanged(string value) => DrawFrame();
    partial void OnHandMaxFadeChanged(string value) => DrawFrame();
    partial void OnHandFadeCurveChanged(string value) => DrawFrame();

    partial void OnSelectedShadingSourceChanged(ShadingSource value)
    {
        LoadPreviewZones();
        DrawFrame();
    }

    [RelayCommand]
    private void ClearHandShading()
    {
        (HandLight, HandFadeColor, HandFadeStart, HandFadeEnd, HandMaxFade, HandFadeCurve) = ("", "", "", "", "", "");
    }

    // The game's named lighten levels, unknown names said in the frame text rather than the
    // console; the levels with shading or zones of their own to pick from
    private void LoadShading(GameContent content)
    {
        _lightenLevels.Warn = _lightenWarnings.Add;
        _lightenLevels.Set(content.GameInfo?.LightenLevels ?? []);
        DisposeShaded();

        var picked = SelectedShadingSource.MapName;
        ShadingSources.Clear();
        ShadingSources.Add(DefaultMapSource);
        foreach (var (name, info) in (content.GameInfo?.Maps ?? []).OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (info.Shading == null && info.Zones is not { Count: > 0 })
                continue;
            ShadingSources.Add(new ShadingSource(name, string.IsNullOrEmpty(info.Name) ? name : $"{name}: {info.Name}"));
        }
        SelectedShadingSource = ShadingSources.FirstOrDefault(source => source.MapName == picked) ?? DefaultMapSource;
        LoadPreviewZones();
    }

    // The picked level's light zones, as the game resolves them when the level starts
    private void LoadPreviewZones()
    {
        var picked = SelectedPreviewZone.Id;
        _previewZones = _content == null ? [] : ShadingResolver.ResolveZones(PreviewMapInfo(), _content.DefaultMapInfo);
        PreviewZones.Clear();
        PreviewZones.Add(NoZone);
        foreach (var (id, zone) in _previewZones.OrderBy(pair => pair.Key))
        {
            var parts = new List<string> { $"light {zone.Light}" };
            if (zone.Color != null)
                parts.Add(zone.Color);
            if (zone.Effect != ZoneEffect.None)
                parts.Add(zone.Effect.ToString().ToLowerInvariant());
            PreviewZones.Add(new ZoneChoice(id, $"Zone {id} ({string.Join(", ", parts)})"));
        }
        SelectedPreviewZone = PreviewZones.FirstOrDefault(zone => zone.Id == picked) ?? NoZone;
    }

    private MapInfo? PreviewMapInfo() =>
        SelectedShadingSource.MapName is { } name ? _content?.GameInfo?.Maps.GetValueOrDefault(name) : null;

    /// <summary>Runs the picked zone's effect (flicker, pulse, strobe) on; true when the light it shows moved a level</summary>
    private bool TickPreviewZone(int tics)
    {
        if (!SeenFromPlayer || !_previewZones.TryGetValue(SelectedPreviewZone.Id, out var zone))
            return false;
        int before = ShadeMath.LightLevelOf(zone.Current);
        zone.Tick(tics, _zoneRandom);
        return ShadeMath.LightLevelOf(zone.Current) != before;
    }

    private void DisposeShaded()
    {
        foreach (var bitmap in _shadedBitmaps.Values)
            bitmap?.Dispose();
        _shadedBitmaps.Clear();
    }

    /// <summary>
    /// The level's shading as the game would resolve it if its game-info said what's set by hand
    /// (ShadingResolver.ResolveShading); bad values are left out, each said in <paramref name="notes"/>
    /// </summary>
    private ShadingSettings? PreviewSettings(List<string> notes)
    {
        var map = PreviewMapInfo();
        var own = map?.Shading;
        int? Int(string text, string what, int low, int high)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= low && value <= high)
                return value;
            notes.Add($"{what} '{text}' isn't {low}-{high}, left out");
            return null;
        }
        double? Double(string text, string what)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= 0)
                return value;
            notes.Add($"{what} '{text}' isn't a number of tiles, left out");
            return null;
        }
        string? Color(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            if (ShadingResolver.IsRgb(text.Trim()))
                return text.Trim();
            notes.Add($"fade color '{text}' isn't #RRGGBB, left out");
            return null;
        }
        string? Curve(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            var curve = text.Trim().ToLowerInvariant();
            if (curve is "linear" or "inverse")
                return curve;
            notes.Add($"fade curve '{text}' isn't linear or inverse, left out");
            return null;
        }

        var hand = new ShadingInfo
        {
            Light = Int(HandLight, "light", 0, 255),
            FadeColor = Color(HandFadeColor),
            FadeStart = Double(HandFadeStart, "fade start"),
            FadeEnd = Double(HandFadeEnd, "fade end"),
            MaxFade = Int(HandMaxFade, "max fade", 0, 100),
            FadeCurve = Curve(HandFadeCurve),
        };
        bool anyHand = hand.Light != null || hand.FadeColor != null || hand.FadeStart != null || hand.FadeEnd != null
                       || hand.MaxFade != null || hand.FadeCurve != null;
        if (!anyHand)
            return ShadingResolver.ResolveShading(map, _content!.DefaultMapInfo);

        notes.Add("set by hand");
        var overlaid = new ShadingInfo
        {
            Light = hand.Light ?? own?.Light,
            FadeColor = hand.FadeColor ?? own?.FadeColor,
            FadeStart = hand.FadeStart ?? own?.FadeStart,
            FadeEnd = hand.FadeEnd ?? own?.FadeEnd,
            MaxFade = hand.MaxFade ?? own?.MaxFade,
            FadeCurve = hand.FadeCurve ?? own?.FadeCurve,
        };
        return ShadingResolver.ResolveShading((map ?? new MapInfo()) with { Shading = overlaid }, _content!.DefaultMapInfo);
    }

    /// <summary>
    /// How the frame is shaded seen from the player: the picked level's shading (and zone), the
    /// class's `lighten` and bright flag with the frame's modifiers (ActorShadingRules, as the
    /// game's Lighting.IsBright and Lighten); a weapon's frames other than its Spawn are in hand
    /// </summary>
    internal FrameShade? FrameShading(ActorStateFrame frame)
    {
        if (_content == null || Shown is not { } entry)
            return null;
        var data = entry.Merged ?? entry.Own;
        bool hasOwn = data.Properties.TryGetValue("lighten", out var own);
        bool flag = data.Flags.Any(f => f.Equals("bright", StringComparison.OrdinalIgnoreCase));
        _lightenWarnings.Clear();
        bool bright = ActorShadingRules.IsBright(frame.Bright, flag, hasOwn, own?.ToString());
        int lighten = ActorShadingRules.Lighten(hasOwn, own?.ToString(), frame.LightenLevel, _lightenLevels);
        bool inHand = entry.Kind == ActorKind.Weapon && !frame.StateName.Equals("Spawn", StringComparison.OrdinalIgnoreCase);

        var notes = new List<string>();
        var settings = PreviewSettings(notes);
        PreviewZone? zone = _previewZones.TryGetValue(SelectedPreviewZone.Id, out var state)
            ? new PreviewZone(SelectedPreviewZone.Id, state.Current, ShadingResolver.ParseRgb(state.Color, ShadingResolver.NoTint, "zone color"))
            : null;
        var shade = _shadePreview.Shade(_content.Palette, settings, Math.Clamp(Distance, NearestDistance, FarthestDistance),
            inHand, bright, lighten, _previewZones.Count > 0, zone);
        notes.Insert(0, shade.Text);
        if (settings == null && _previewZones.Count == 0 && notes.Count == 1)
            notes.Add(SelectedShadingSource.MapName == null
                ? "the game's default map has no shading: pick a level, or set some by hand"
                : "this level has no shading");
        if (frame.Light != null)
            notes.Add("its own light isn't shown");
        notes.AddRange(_lightenWarnings);
        return shade with { Text = string.Join("; ", notes) };
    }

    // The sprite through the shade's light row, made once per row and step
    private Bitmap? ShadedPicture(string name, FrameShade shade)
    {
        if (shade.Row == null)
            return Picture(name);
        var key = (name, shade.Row, shade.Offset);
        if (_shadedBitmaps.TryGetValue(key, out var bitmap))
            return bitmap;
        if (_shadedBitmaps.Count > 512)
            DisposeShaded();
        if (Avalonia.Application.Current != null && _content != null && ArtCache.Indexed(_content, ArtKind.Sprite, name) is { } picture)
            bitmap = ArtCache.Render(ShadePreview.Remap(picture, shade.Row, shade.Offset), _content.Palette);
        return _shadedBitmaps[key] = bitmap;
    }
}
