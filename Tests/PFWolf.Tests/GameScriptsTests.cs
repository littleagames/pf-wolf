using PFWolf.Scripting;

namespace PFWolf.Tests;

public class GameScriptsTests
{
    private GameSession _session = null!;
    private ConsoleManager _console = null!;
    private GameScripts _scripts = null!;

    [SetUp]
    public void MakeScripts()
    {
        _session = new GameSession();
        _console = new ConsoleManager();
        _scripts = new GameScripts(null!, null!, null!, null!, _console, null, null!, _session, (_, _) => { });
    }

    // The lua command makes the Lua state; a script then loads into it as one would at startup
    private ScriptHost Host()
    {
        _scripts.Cmd_Lua(["x = 1"]);
        return _scripts.Scripts!;
    }

    private List<string> Printed() => [.. _console.Scrollback];

    [Test]
    public void The_Game_Table_Reads_The_Game_Being_Played()
    {
        // Arrange
        var host = Host();
        _session.gamestate.TimeCount = 1234;
        _session.gamestate.mapon = "MAP07";
        _session.gamestate.difficulty = 2;
        _session.tics = 3;

        // Act
        host.Execute("print(game.time(), game.map(), game.skill(), game.tics())");

        // Assert
        Assert.That(Printed(), Has.Some.EqualTo("1234\tMAP07\t2\t3"));
    }

    [Test]
    public void Game_Random_Stays_In_Its_Range_And_Comes_From_The_Games_Numbers()
    {
        // Arrange
        var host = Host();
        int start = _session.random.rndindex;

        // Act
        host.Execute("for i = 1, 200 do local n = game.random(3, 5) if n < 3 or n > 5 then print('out', n) end end print('done')");

        // Assert
        Assert.That(Printed(), Has.None.StartsWith("out"));
        Assert.That(Printed(), Has.Some.EqualTo("done"));
        Assert.That(_session.random.rndindex, Is.Not.EqualTo(start));      // the game's own numbers moved on
    }

    [Test]
    public void A_Level_Hook_Added_As_A_Script_Loads_Runs_With_The_Reason_And_Map()
    {
        // Arrange
        var host = Host();
        host.Load("game.onlevel(function(reason, map) print('level', reason, map) end)", "test.lua");
        _session.gamestate.mapon = "MAP02";

        // Act
        _scripts.RunLevelHooks("restarted");

        // Assert
        Assert.That(Printed(), Has.Some.EqualTo("level\trestarted\tMAP02"));
    }

    [Test]
    public void A_Script_Sets_And_Stops_The_Ambient_Loop()
    {
        // Arrange: paused, so no sound is looked up whether or not there's an audio device
        var audio = new AudioManager(new Lazy<AssetManager>(() => null!), _session);
        audio.SetPaused(true);
        _scripts = new GameScripts(null!, null!, audio, null!, _console, null, null!, _session, (_, _) => { });
        var host = Host();

        // Act / Assert
        host.Execute("game.ambient('ambient/rain_quiet')");
        Assert.That(audio.Ambient, Is.EqualTo("ambient/rain_quiet"));
        host.Execute("game.ambient(nil)");
        Assert.That(audio.Ambient, Is.Null);
    }

    [Test]
    public void A_Tick_Hook_Is_Given_The_Tics()
    {
        // Arrange
        var host = Host();
        host.Load("game.ontick(function(tics) print('tick', tics) end)", "test.lua");
        _session.tics = 2;

        // Act
        _scripts.RunTickHooks();

        // Assert
        Assert.That(Printed(), Has.Some.EqualTo("tick\t2"));
    }

    [Test]
    public void A_Hook_That_Keeps_Failing_Is_Switched_Off()
    {
        // Arrange
        var host = Host();
        host.Load("game.ontick(function(tics) error('broken') end)", "test.lua");

        // Act
        for (int i = 0; i < 5; i++)
            _scripts.RunTickHooks();

        // Assert
        Assert.That(Printed().Count(line => line.Contains("switched off")), Is.EqualTo(1));
    }

    [Test]
    public void The_Scripts_Command_Says_When_None_Are_Loaded()
    {
        _scripts.Cmd_Scripts([]);
        Assert.That(Printed(), Has.Some.Contains("No scripts are loaded"));
    }
}
