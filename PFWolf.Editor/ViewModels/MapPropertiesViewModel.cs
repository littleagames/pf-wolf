using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets;
using PFWolf.Editor.Editing;

namespace PFWolf.Editor.ViewModels;

/// <summary>A light zone as a row of the properties dialog</summary>
public sealed partial class ZoneRow : ObservableObject
{
    [ObservableProperty] private string _id = "";
    [ObservableProperty] private string _light = "";
    [ObservableProperty] private string _color = "";
    [ObservableProperty] private string _effect = "";
    [ObservableProperty] private string _low = "";
    [ObservableProperty] private string _tics = "";
    [ObservableProperty] private string _brightTics = "";

    public static IReadOnlyList<string> Effects { get; } = ["", "flicker", "pulse", "strobe"];
}

/// <summary>
/// The level properties dialog: a level's game-info entry as text fields, each left empty to
/// use the default map's. The default map's values show as the fields' placeholders.
/// </summary>
public sealed partial class MapPropertiesViewModel : ObservableObject
{
    public MapPropertiesViewModel(string mapName, MapProperties properties, DefaultMapInfo defaults, IReadOnlyList<string> music,
        IReadOnlyList<string> maps)
    {
        MapName = mapName;
        Defaults = defaults;
        MusicNames = music;
        MapNames = maps;

        _name = properties.Name ?? "";
        _music = properties.Music ?? "";
        _next = properties.Next ?? "";
        _secretNext = properties.SecretNext ?? "";
        _parTime = Text(properties.ParTime);
        _floorColor = properties.FloorColor ?? "";
        _ceilingColor = properties.CeilingColor ?? "";
        _wallHeight = Text(properties.WallHeight);
        _sky = properties.Sky ?? "";
        _defaultFloor = properties.DefaultFloor ?? "";
        _defaultCeiling = properties.DefaultCeiling ?? "";
        _fadeColor = properties.FadeColor ?? "";
        _fadeStart = Text(properties.FadeStart);
        _fadeEnd = Text(properties.FadeEnd);
        _maxFade = Text(properties.MaxFade);
        _light = Text(properties.Light);
        foreach (var zone in properties.Zones)
        {
            Zones.Add(new ZoneRow
            {
                Id = zone.Id.ToString(CultureInfo.InvariantCulture), Light = Text(zone.Light), Color = zone.Color ?? "", Effect = zone.Effect ?? "",
                Low = Text(zone.Low), Tics = Text(zone.Tics), BrightTics = Text(zone.BrightTics),
            });
        }
    }

    public string MapName { get; }
    public string Title => $"{MapName} properties";
    public DefaultMapInfo Defaults { get; }
    public IReadOnlyList<string> MusicNames { get; }
    public IReadOnlyList<string> MapNames { get; }

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _music;
    [ObservableProperty] private string _next;
    [ObservableProperty] private string _secretNext;
    [ObservableProperty] private string _parTime;
    [ObservableProperty] private string _floorColor;
    [ObservableProperty] private string _ceilingColor;
    [ObservableProperty] private string _wallHeight;
    [ObservableProperty] private string _sky;
    [ObservableProperty] private string _defaultFloor;
    [ObservableProperty] private string _defaultCeiling;
    [ObservableProperty] private string _fadeColor;
    [ObservableProperty] private string _fadeStart;
    [ObservableProperty] private string _fadeEnd;
    [ObservableProperty] private string _maxFade;
    [ObservableProperty] private string _light;

    public ObservableCollection<ZoneRow> Zones { get; } = [];

    [ObservableProperty] private string _error = "";

    // The default map's values, shown where a field is empty
    public string FloorColorDefault => Defaults.FloorColor ?? "";
    public string CeilingColorDefault => Defaults.CeilingColor ?? "";
    public string WallHeightDefault => Defaults.WallHeight.ToString(CultureInfo.InvariantCulture);
    public string SkyDefault => Defaults.Sky ?? "none";
    public string DefaultFloorDefault => Defaults.DefaultFloor ?? "the floor color";
    public string DefaultCeilingDefault => Defaults.DefaultCeiling ?? "the ceiling color";
    public string FadeColorDefault => Defaults.Shading?.FadeColor ?? "#000000";
    public string FadeStartDefault => Text(Defaults.Shading?.FadeStart) is { Length: > 0 } text ? text : "0";
    public string FadeEndDefault => Text(Defaults.Shading?.FadeEnd) is { Length: > 0 } text ? text : "16";
    public string MaxFadeDefault => Text(Defaults.Shading?.MaxFade) is { Length: > 0 } text ? text : "100";
    public string LightDefault => Text(Defaults.Shading?.Light) is { Length: > 0 } text ? text : "255";

    [RelayCommand]
    private void AddZone()
    {
        int next = Zones.Select(zone => int.TryParse(zone.Id, out var id) ? id : 0).DefaultIfEmpty(0).Max() + 1;
        Zones.Add(new ZoneRow { Id = next.ToString(CultureInfo.InvariantCulture), Light = "128" });
    }

    [RelayCommand]
    private void RemoveZone(ZoneRow zone) => Zones.Remove(zone);

    /// <summary>The properties as entered, or null (with <see cref="Error"/> saying why) when one won't do</summary>
    public MapProperties? Build()
    {
        try
        {
            var zones = new List<ZoneProperties>();
            foreach (var row in Zones)
            {
                var id = Int(row.Id, $"zone id '{row.Id}'", 1, 65535) ?? throw new FormatException("every zone needs an id, 1 or more");
                if (zones.Any(zone => zone.Id == id))
                    throw new FormatException($"there are two zone {id}s");
                zones.Add(new ZoneProperties(id, Int(row.Light, $"zone {id}'s light", 0, 255), Color(row.Color, $"zone {id}'s color"),
                    Optional(row.Effect), Int(row.Low, $"zone {id}'s low", 0, 255), Int(row.Tics, $"zone {id}'s tics", 1, 100000),
                    Int(row.BrightTics, $"zone {id}'s bright tics", 1, 100000)));
            }

            Error = "";
            return new MapProperties
            {
                Name = Optional(Name),
                Music = Optional(Music)?.ToUpperInvariant(),
                Next = Optional(Next)?.ToUpperInvariant(),
                SecretNext = Optional(SecretNext)?.ToUpperInvariant(),
                ParTime = Int(ParTime, "par time", 0, 100000),
                FloorColor = Color(FloorColor, "floor color"),
                CeilingColor = Color(CeilingColor, "ceiling color"),
                WallHeight = Int(WallHeight, "wall height", 1, 8),
                Sky = Optional(Sky),
                DefaultFloor = Optional(DefaultFloor),
                DefaultCeiling = Optional(DefaultCeiling),
                FadeColor = Color(FadeColor, "fade color"),
                FadeStart = Double(FadeStart, "fade start"),
                FadeEnd = Double(FadeEnd, "fade end"),
                MaxFade = Int(MaxFade, "max fade", 0, 100),
                Light = Int(Light, "light", 0, 255),
                Zones = zones.OrderBy(zone => zone.Id).ToList(),
            };
        }
        catch (FormatException e)
        {
            Error = char.ToUpperInvariant(e.Message[0]) + e.Message[1..] + ".";
            return null;
        }
    }

    private static string? Optional(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static int? Int(string text, string what, int min, int max)
    {
        if (Optional(text) is not { } value)
            return null;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) || number < min || number > max)
            throw new FormatException($"{what} is a whole number from {min} to {max}");
        return number;
    }

    private static double? Double(string text, string what)
    {
        if (Optional(text) is not { } value)
            return null;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || number < 0)
            throw new FormatException($"{what} is a number of tiles, 0 or more");
        return number;
    }

    private static string? Color(string text, string what)
    {
        if (Optional(text) is not { } value)
            return null;
        if (!Regex.IsMatch(value, "^#[0-9A-Fa-f]{6}$"))
            throw new FormatException($"{what} is a color written #RRGGBB");
        return value;
    }

    private static string Text(double? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";
}
