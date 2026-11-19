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
              5: { ambient: "ambient/hum", ambient-fade: 35 }
              6: { ambient: "ambient/calm", ambient-undetected: "ambient/tense", ambient-fade: 70 }
          MAP02:
            zones:
              1: { light: 200 }
        """;

    private GameInfoAsset _info = null!;
    private GameSession _session = null!;

    // The level being played (the session's map)
    private string? _map { get => _session.GameState.MapOn; set => _session.GameState.MapOn = value!; }
    private int[,] _zones = null!;
    private bool _disguised;
    private List<string?> _asked = null!;
    private ZoneAmbience _ambience = null!;

    [SetUp]
    public void Make()
    {
        _info = YamlDataEntryLoader.Deserialize<GameInfoAsset>(GameInfo);
        _session = new GameSession();
        _map = "MAP01";
        _session.PlayerState.Pawn = new Entities.Actors.PlayerPawn { State = _session.PlayerState };
        _session.Player.SetPosition(0, 0);
        _zones = new int[8, 1];
        for (int x = 0; x < 8; x++)
            _zones[x, 0] = x;           // tile x is in zone x
        _disguised = false;
        _asked = [];
        _fades = [];
        _ambience = new ZoneAmbience(_session, new CurrentMapInfo(_session, () => _info), new ZoneAmbienceHooks(
            (x, y) => _zones[x, y], () => _disguised, (sound, fade) => { _asked.Add(sound); _fades.Add(fade); }));
    }

    private List<int> _fades = null!;

    private string? StepTo(int x)
    {
        _session.Player.SetPosition(x, 0);
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
    public void A_Zones_Fade_Is_The_One_Stepped_Into()
    {
        StepTo(2);
        Assert.That(_fades[^1], Is.EqualTo(0));
        Assert.That(StepTo(5), Is.EqualTo("ambient/hum"));
        Assert.That(_fades[^1], Is.EqualTo(35));
        Assert.That(StepTo(1), Is.EqualTo("ambient/hum"));        // no sound of its own: hum, fading as hum's zone said
        Assert.That(_fades[^1], Is.EqualTo(35));
        StepTo(4);
        Assert.That(_fades[^1], Is.EqualTo(0));                   // none, at once
    }

    [Test]
    public void Spotted_In_A_Zone_Its_Undetected_Sound_Fades_To_Its_Own()
    {
        _session.PlayerState.Undetected = true;
        Assert.That(StepTo(6), Is.EqualTo("ambient/tense"));
        _session.PlayerState.Undetected = false;
        _ambience.Tick();
        Assert.That(_asked[^1], Is.EqualTo("ambient/calm"));
        Assert.That(_fades[^1], Is.EqualTo(70));
    }

    [Test]
    public void A_Scripts_Ambient_Wins_Over_The_Zones_Until_It_Asks_For_Nil()
    {
        StepTo(2);
        _ambience.SetScriptAmbient("ambient/radio", 20);
        Assert.That(_asked[^1], Is.EqualTo("ambient/radio"));     // at once
        Assert.That(_fades[^1], Is.EqualTo(20));
        Assert.That(StepTo(3), Is.EqualTo("ambient/radio"));      // the zones don't take it back

        _ambience.SetScriptAmbient("none", 0);
        Assert.That(_asked[^1], Is.Null);                         // none: silence while it holds the channel
        Assert.That(StepTo(2), Is.Null);

        _ambience.SetScriptAmbient(null, 10);
        Assert.That(_asked[^1], Is.EqualTo("ambient/crickets"));  // given back: the zone's, fading as the script said
        Assert.That(_fades[^1], Is.EqualTo(10));
        Assert.That(StepTo(3), Is.EqualTo("ambient/rain_quiet"));
    }

    [Test]
    public void A_Script_Asking_For_Nil_Each_Tic_Leaves_The_Zones_Fade_Alone()
    {
        StepTo(5);                                  // hum, fading in over 35
        var asked = _asked.Count;
        _ambience.SetScriptAmbient(null, 0);
        Assert.That(_asked, Has.Count.EqualTo(asked));
    }

    [Test]
    public void On_A_Map_With_No_Zone_Sounds_A_Scripts_Ambient_Plays_And_Nil_Stops_It()
    {
        _info = YamlDataEntryLoader.Deserialize<GameInfoAsset>("maps: { MAP02: { zones: { 1: { light: 200 } } } }");
        _map = "MAP02";
        _ambience.Tick();
        Assert.That(_asked, Is.Empty);                            // the channel's left alone

        _ambience.SetScriptAmbient("ambient/hum", 0);
        _ambience.Tick();
        Assert.That(_asked, Is.EqualTo(new[] { "ambient/hum", "ambient/hum" }));   // asked each tic, so it comes back after StopAll
        _ambience.SetScriptAmbient(null, 0);
        Assert.That(_asked[^1], Is.Null);
        _ambience.Tick();
        Assert.That(_asked, Has.Count.EqualTo(3));
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
        Assert.That(again[5].AmbientFade, Is.EqualTo(35));
        Assert.That(again[6].AmbientUndetected, Is.EqualTo("ambient/tense"));
    }
}
