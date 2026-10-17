using PFWolf.Assets;
using PFWolf.Entities.Actors;
using PFWolf.Enums;

namespace PFWolf.Tests;

public class TimedSwitchesTests
{
    private const int SwitchOff = 40, SwitchOn = 41;

    private MapManager _map = null!;
    private GameSession _session = null!;
    private TimedSwitches _timed = null!;
    private StringWriter _console = null!;
    private TextWriter _realConsole = null!;

    [SetUp]
    public void MakeLevel()
    {
        _realConsole = Console.Out;
        _console = new StringWriter();
        Console.SetOut(_console);
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session)
        {
            tilemap = new ushort[MapManager.MAPSIZE, MapManager.MAPSIZE],
            actorat = new Actor?[MapManager.MAPSIZE, MapManager.MAPSIZE],
        };
        var pawn = new PlayerPawn { State = _session.playerstate };
        pawn.SetPosition(10, 10);
        _session.playerstate.Pawn = pawn;
        _timed = new TimedSwitches(_map, _session);
    }

    [TearDown]
    public void PutConsoleBack() => Console.SetOut(_realConsole);

    // The reset action is left unregistered (the registry is shared, and other tests check every
    // name in it), so it running shows as the registry saying it has no handler for it
    private int Resets => _console.ToString().Split('\n').Count(line => line.Contains("'A_TestReset'"));

    // A switch wall at 11,10, thrown from off to on
    private void Throw(int resetTics)
    {
        _map.tilemap[11, 10] = SwitchOn;
        _timed.Thrown(new MapSwitchTranslation { To = SwitchOn, ResetTics = resetTics, ResetActions = ["A_TestReset"] },
            11, 10, patch: false, SwitchOff, SwitchOn, 0, ControlDir.di_east);
    }

    [Test]
    public void A_Timed_Switch_Turns_Back_And_Runs_Its_Reset_Actions_When_Time_Is_Up()
    {
        // Arrange: 2 seconds, as the original's first timed switch
        Throw(140);

        // Act / Assert
        _timed.Tick(139);
        Assert.That(_map.tilemap[11, 10], Is.EqualTo(SwitchOn));
        Assert.That(Resets, Is.Zero);
        _timed.Tick(1);
        Assert.That(_map.tilemap[11, 10], Is.EqualTo(SwitchOff));
        Assert.That(Resets, Is.EqualTo(1));
        Assert.That(_timed.Pending, Is.Empty);
    }

    [Test]
    public void One_Without_Reset_Tics_Stays_Thrown()
    {
        Throw(0);
        _timed.Tick(1000);
        Assert.That(_map.tilemap[11, 10], Is.EqualTo(SwitchOn));
        Assert.That(_timed.Pending, Is.Empty);
    }

    [Test]
    public void A_Wall_Changed_Meanwhile_Is_Left_Alone()
    {
        // Arrange: something else made it another wall
        Throw(10);
        _map.tilemap[11, 10] = 7;

        // Act
        _timed.Tick(10);

        // Assert: the wall stays, the actions still run
        Assert.That(_map.tilemap[11, 10], Is.EqualTo(7));
        Assert.That(Resets, Is.EqualTo(1));
    }

    [Test]
    public void A_Reloaded_Level_Forgets_Them()
    {
        // Arrange: the level loads again (a death, a saved game)
        Throw(10);
        _map.tilemap = new ushort[MapManager.MAPSIZE, MapManager.MAPSIZE];
        _map.tilemap[11, 10] = SwitchOn;

        // Act
        _timed.Tick(10);

        // Assert
        Assert.That(_map.tilemap[11, 10], Is.EqualTo(SwitchOn));
        Assert.That(Resets, Is.Zero);
        Assert.That(_timed.Pending, Is.Empty);
    }

    [Test]
    public void Thrown_Again_It_Starts_Its_Time_Over()
    {
        Throw(10);
        _timed.Tick(5);
        Throw(10);
        _timed.Tick(5);
        Assert.That(_timed.Pending, Has.Count.EqualTo(1));
        Assert.That(_map.tilemap[11, 10], Is.EqualTo(SwitchOn));
    }

    [Test]
    public void A_Switch_Reads_Its_Reset_From_Yaml()
    {
        var sw = PFWolf.Loaders.YamlDataEntryLoader.Deserialize<MapSwitchTranslation>(
            "to: 125\nreset-tics: 280\nactions: [\"A_OpenDoor(\\\"hold\\\")\"]\nreset-actions: [A_CloseDoor]\n");
        Assert.That((sw.To, sw.ResetTics, sw.ResetActions.Single()), Is.EqualTo((125, 280, "A_CloseDoor")));
    }
}
