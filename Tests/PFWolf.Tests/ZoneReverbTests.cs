using PFWolf.Assets;
using PFWolf.Gameplay;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>A zone's or a map's reverb, as the camera goes from zone to zone (Gameplay/ZoneReverb.cs)</summary>
public class ZoneReverbTests
{
    private const string GameInfo = """
        default-map:
          reverb-fade: 20
          zones:
            9: { reverb: cave }
        maps:
          MAP01:
            reverb: hallway
            zones:
              1: { light: 64 }
              2: { reverb: sewer-pipe }
              3: { reverb: STONE_ROOM, reverb-fade: 70 }
              4: { reverb: none }
              5: { reverb: no-such-room }
          MAP02:
            zones:
              1: { light: 200 }
        """;

    private GameInfoAsset _info = null!;
    private GameSession _session = null!;
    private int[,] _zones = null!;
    private List<(string? Reverb, int Fade)> _asked = null!;
    private ZoneReverb _reverb = null!;

    [SetUp]
    public void Make()
    {
        _info = YamlDataEntryLoader.Deserialize<GameInfoAsset>(GameInfo);
        _session = new GameSession();
        _session.GameState.MapOn = "MAP01";
        _session.PlayerState.Pawn = new Entities.Actors.PlayerPawn { State = _session.PlayerState };
        _session.Player.SetPosition(0, 0);
        _zones = new int[10, 1];
        for (int x = 0; x < 10; x++)
            _zones[x, 0] = x;           // tile x is in zone x
        _asked = [];
        _reverb = new ZoneReverb(_session, new CurrentMapInfo(_session, () => _info), new ZoneReverbHooks(
            (x, y) => _zones[x, y], (reverb, fade) => _asked.Add((reverb, fade))));
    }

    private (string? Reverb, int Fade) StepTo(int x)
    {
        _session.Player.SetPosition(x, 0);
        _reverb.Tick();
        return _asked[^1];
    }

    [Test]
    public void The_Level_Starts_With_The_Maps_Reverb_At_Once()
    {
        Assert.That(StepTo(0), Is.EqualTo(("hallway", 0)));
        Assert.That(StepTo(0), Is.EqualTo(("hallway", 20)));      // after, the default map's fade
    }

    [Test]
    public void A_Zone_Sets_It_And_One_Without_Keeps_It()
    {
        StepTo(0);
        Assert.That(StepTo(2), Is.EqualTo(("sewer-pipe", 20)));
        Assert.That(StepTo(1), Is.EqualTo(("sewer-pipe", 20)));   // a light zone: still the sewer's
        Assert.That(StepTo(0), Is.EqualTo(("sewer-pipe", 20)));   // a doorway (no zone): still
        Assert.That(StepTo(3), Is.EqualTo(("stone-room", 70)));   // its own fade; the name as the list has it
        Assert.That(StepTo(4), Is.EqualTo(((string?)null, 20)));  // none: dry
        Assert.That(StepTo(9), Is.EqualTo(("cave", 20)));         // the default map's zone
    }

    [Test]
    public void An_Unknown_Reverb_Is_Dry_With_A_Warning()
    {
        var warnings = new List<string>();
        var sink = WarningLog.Sink;
        WarningLog.Sink = warnings.Add;
        try
        {
            StepTo(0);
            Assert.That(StepTo(5).Reverb, Is.Null);
            StepTo(5);
        }
        finally
        {
            WarningLog.Sink = sink;
        }
        Assert.That(warnings, Has.Count.EqualTo(1));
        Assert.That(warnings[0], Does.Contain("no-such-room"));
    }

    [Test]
    public void A_Map_With_None_Is_Dry()
    {
        _session.GameState.MapOn = "MAP02";
        Assert.That(StepTo(1), Is.EqualTo(((string?)null, 0)));
    }

    [Test]
    public void The_Editor_Keeps_The_Maps_And_Zones_Reverbs()
    {
        var properties = PFWolf.Editor.Editing.MapProperties.From(_info.Maps["MAP01"]);
        var text = PFWolf.Editor.Editing.GameInfoFile.Apply(null, "MAP01", properties, ["reverb", "zones"]);

        var again = YamlDataEntryLoader.Deserialize<GameInfoAsset>(text).Maps["MAP01"];
        Assert.That(again.Reverb, Is.EqualTo("hallway"));
        Assert.That(again.Zones![2].Reverb, Is.EqualTo("sewer-pipe"));
        Assert.That(again.Zones[3].ReverbFade, Is.EqualTo(70));
        Assert.That(again.Zones[4].Reverb, Is.EqualTo("none"));
    }

    [Test]
    public void A_Script_Holds_It_Over_The_Zones_Until_It_Asks_For_Nil()
    {
        StepTo(2);
        _reverb.SetScriptReverb("Underwater", 10);
        Assert.That(_asked[^1], Is.EqualTo(("underwater", 10)));      // at once
        Assert.That(StepTo(3), Is.EqualTo(("underwater", 10)));       // the zones don't take it back

        var count = _asked.Count;
        _reverb.SetScriptReverb(null, 0);
        Assert.That(_asked[^1], Is.EqualTo(("stone-room", 0)));       // given back: the zone's
        _reverb.SetScriptReverb(null, 0);                              // nil again, holding nothing: nothing
        Assert.That(_asked, Has.Count.EqualTo(count + 1));
    }
}
