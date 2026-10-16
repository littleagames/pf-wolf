using PFWolf.Assets;
using PFWolf.Gameplay;
using PFWolf.Loaders;
using PFWolf.Rendering;

namespace PFWolf.Tests;

public class ZoneAmbienceTests
{
    private const string GameInfo = """
        default-map:
          zones:
            9: { ambient: "ambient/wind" }
        maps:
          MAP01:
            zones:
              1: { light: 64 }
              2: { light: 64, ambient: "ambient/crickets" }
              3: { ambient: "ambient/rain_quiet", ambient-disguised: "ambient/pianobar" }
              4: { ambient: none }
          MAP02:
            zones:
              1: { light: 200 }
        """;

    private GameInfoAsset _info = null!;
    private string? _map;
    private (int X, int Y)? _tile;
    private int[,] _zones = null!;
    private bool _disguised;
    private List<string?> _asked = null!;
    private ZoneAmbience _ambience = null!;

    [SetUp]
    public void Make()
    {
        _info = YamlDataEntryLoader.Deserialize<GameInfoAsset>(GameInfo);
        _map = "MAP01";
        _tile = (0, 0);
        _zones = new int[8, 1];
        for (int x = 0; x < 8; x++)
            _zones[x, 0] = x;           // tile x is in zone x
        _disguised = false;
        _asked = [];
        _ambience = new ZoneAmbience(new ZoneAmbienceHooks(() => _map,
            () => _map != null && _info.Maps.TryGetValue(_map, out var map) ? map : null, () => _info.DefaultMap,
            () => _tile, (x, y) => _zones[x, y], () => _disguised, _asked.Add));
    }

    private string? StepTo(int x)
    {
        _tile = (x, 0);
        _ambience.Tick();
        return _asked[^1];
    }

    [Test]
    public void Each_Zone_Plays_Its_Sound_And_One_Without_Leaves_It()
    {
        Assert.That(StepTo(0), Is.Null);                          // zone 0: nothing yet
        Assert.That(StepTo(2), Is.EqualTo("ambient/crickets"));
        Assert.That(StepTo(1), Is.EqualTo("ambient/crickets"));   // a light zone with no sound: still crickets
        Assert.That(StepTo(3), Is.EqualTo("ambient/rain_quiet"));
        Assert.That(StepTo(4), Is.Null);                          // zone 4: none is silence
        Assert.That(StepTo(0), Is.Null);
    }

    [Test]
    public void In_Disguise_A_Zone_Plays_Its_Disguised_Sound_And_The_Default_Maps_Zones_Count()
    {
        _disguised = true;
        Assert.That(StepTo(3), Is.EqualTo("ambient/pianobar"));
        Assert.That(StepTo(2), Is.EqualTo("ambient/crickets"));   // no disguised one: its own

        var zones = ZoneAmbience.Resolve(_info.Maps["MAP01"], _info.DefaultMap);
        Assert.That(zones[9].Sound, Is.EqualTo("ambient/wind"));
        Assert.That(zones.ContainsKey(1), Is.False);
    }

    [Test]
    public void A_New_Level_Stops_The_Last_Ones_Sound()
    {
        StepTo(2);
        _zones[2, 0] = 1;               // MAP02's zones have no sound but the default map's 9

        _map = "MAP02";
        _ambience.Tick();

        Assert.That(_asked[^2], Is.Null);       // stopped as the level changed
    }

    [Test]
    public void A_Zone_With_Only_Ambient_Sound_Is_Not_A_Light_Zone()
    {
        var lightZones = Lighting.ResolveZones(_info.Maps["MAP01"], _info.DefaultMap);
        Assert.That(lightZones.Keys.Order(), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(lightZones[2].Light, Is.EqualTo(64));
    }

    [Test]
    public void The_Editor_Keeps_A_Zones_Ambient_Sounds()
    {
        var properties = PFWolf.Editor.Editing.MapProperties.From(_info.Maps["MAP01"]);
        var text = PFWolf.Editor.Editing.GameInfoFile.Apply(null, "MAP01", properties, ["zones"]);

        var again = YamlDataEntryLoader.Deserialize<GameInfoAsset>(text).Maps["MAP01"].Zones!;
        Assert.That(again[3].Ambient, Is.EqualTo("ambient/rain_quiet"));
        Assert.That(again[3].AmbientDisguised, Is.EqualTo("ambient/pianobar"));
        Assert.That(again[4].Ambient, Is.EqualTo("none"));
        Assert.That(again[2].Light, Is.EqualTo(64));
    }
}
