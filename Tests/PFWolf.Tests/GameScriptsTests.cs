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
        _scripts.LuaCommand(["x = 1"]);
        return _scripts.Scripts!;
    }

    private List<string> Printed() => [.. _console.Scrollback];

    [Test]
    public void The_Game_Table_Reads_The_Game_Being_Played()
    {
        // Arrange
        var host = Host();
        _session.GameState.TimeCount = 1234;
        _session.GameState.MapOn = "MAP07";
        _session.GameState.Difficulty = 2;
        _session.Tics = 3;

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
        int start = _session.Random.RndIndex;

        // Act
        host.Execute("for i = 1, 200 do local n = game.random(3, 5) if n < 3 or n > 5 then print('out', n) end end print('done')");

        // Assert
        Assert.That(Printed(), Has.None.StartsWith("out"));
        Assert.That(Printed(), Has.Some.EqualTo("done"));
        Assert.That(_session.Random.RndIndex, Is.Not.EqualTo(start));      // the game's own numbers moved on
    }

    [Test]
    public void A_Level_Hook_Added_As_A_Script_Loads_Runs_With_The_Reason_And_Map()
    {
        // Arrange
        var host = Host();
        host.Load("game.onlevel(function(reason, map) print('level', reason, map) end)", "test.lua");
        _session.GameState.MapOn = "MAP02";

        // Act
        _scripts.RunLevelHooks("restarted");

        // Assert
        Assert.That(Printed(), Has.Some.EqualTo("level\trestarted\tMAP02"));
    }

    [Test]
    public void A_Script_Can_Raise_An_Actor()
    {
        // Arrange
        var host = Host();
        var lamp = new Entities.Actors.Monster { Name = "Lamp" };
        host.Globals["lamp"] = LuaActor.Of(lamp);

        // Act
        host.Execute("lamp.z = 24.5 print(lamp.z)");

        // Assert
        Assert.That(lamp.Z, Is.EqualTo(24.5));
        Assert.That(Printed(), Has.Some.EqualTo("24.5"));
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
    public void A_Script_Reads_A_Level_Objective()
    {
        // Arrange: a map with 2 of 5 sabotage targets done
        var map = new MapManager(new Lazy<AssetManager>(() => null!), null!, _session, null);
        map.ObjectiveCounts["sabotage"] = new ObjectiveCount { Done = 2, Total = 5 };
        _scripts = new GameScripts(null!, map, null!, null!, _console, null, null!, _session, (_, _) => { });
        var host = Host();

        // Act
        host.Execute("print(game.objective('Sabotage')) print(game.objective('intel'))");

        // Assert
        Assert.That(Printed(), Has.Some.EqualTo("2\t5"));
        Assert.That(Printed(), Has.Some.EqualTo("0\t0"));
    }

    [Test]
    public void A_Tick_Hook_Is_Given_The_Tics()
    {
        // Arrange
        var host = Host();
        host.Load("game.ontick(function(tics) print('tick', tics) end)", "test.lua");
        _session.Tics = 2;

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
    public void Damage_Hooks_Pass_The_Amount_On_And_Nil_Keeps_It()
    {
        // Arrange: one halves what it's given, the next only looks, the last adds 1
        var host = Host();
        host.Load("""
            game.ondamage(function(victim, amount, attacker, type) return amount / 2 end)
            game.ondamage(function(victim, amount, attacker, type) print('saw', victim.name, amount, attacker.name, type) end)
            game.ondamage(function(victim, amount) return amount + 1 end)
            """, "test.lua");
        var guard = new Entities.Actors.Monster { Name = "Guard" };
        var player = new Entities.Actors.PlayerPawn { Name = "Player" };

        // Act
        int amount = _scripts.RunDamageHooks(guard, 21, player, "bullet");

        // Assert: 10.5 rounds to even, then 1 more
        Assert.That(amount, Is.EqualTo(11));
        Assert.That(Printed(), Has.Some.EqualTo("saw\tGuard\t10\tPlayer\tbullet"));
    }

    [Test]
    public void A_Damage_Hook_Cant_Make_It_Negative()
    {
        var host = Host();
        host.Load("game.ondamage(function(victim, amount) return -50 end)", "test.lua");
        Assert.That(_scripts.RunDamageHooks(new Entities.Actors.Monster { Name = "Guard" }, 10, null, null), Is.Zero);
    }

    [Test]
    public void Without_Hooks_The_Damage_And_Pickups_Are_As_They_Were()
    {
        Host();
        var item = new Entities.Actors.Monster { Name = "Clip" };
        Assert.That(_scripts.RunDamageHooks(item, 7, null, null), Is.EqualTo(7));
        Assert.That(_scripts.RunTryPickupHooks(item, item), Is.True);
        Assert.That(_scripts.RunUseHooks(item, 1, 2, ControlDir.North), Is.False);
    }

    [Test]
    public void A_Pickup_Refused_Stops_The_Asking_And_Nil_Lets_It_Be_Taken()
    {
        // Arrange
        var host = Host();
        host.Load("""
            game.ontrypickup(function(player, item) print('first', item.name) end)
            game.ontrypickup(function(player, item) return item.name ~= 'Medikit' end)
            game.ontrypickup(function(player, item) print('last', item.name) end)
            """, "test.lua");
        var player = new Entities.Actors.PlayerPawn { Name = "Player" };

        // Act / Assert
        Assert.That(_scripts.RunTryPickupHooks(player, new Entities.Actors.Monster { Name = "Medikit" }), Is.False);
        Assert.That(Printed(), Has.None.EqualTo("last\tMedikit"));
        Assert.That(_scripts.RunTryPickupHooks(player, new Entities.Actors.Monster { Name = "Clip" }), Is.True);
        Assert.That(Printed(), Has.Some.EqualTo("last\tClip"));
    }

    [Test]
    public void A_Use_Hook_That_Uses_The_Tile_Stops_The_Rest()
    {
        // Arrange
        var host = Host();
        host.Load("""
            game.onuse(function(player, x, y, dir) print('use', x, y, dir) return x == 5 end)
            game.onuse(function(player, x, y, dir) print('second', x) end)
            """, "test.lua");
        var player = new Entities.Actors.PlayerPawn { Name = "Player" };

        // Act / Assert
        Assert.That(_scripts.RunUseHooks(player, 5, 9, ControlDir.East), Is.True);
        Assert.That(Printed(), Has.Some.EqualTo("use\t5\t9\teast").And.None.EqualTo("second\t5"));
        Assert.That(_scripts.RunUseHooks(player, 6, 9, ControlDir.West), Is.False);
        Assert.That(Printed(), Has.Some.EqualTo("second\t6"));
    }

    [Test]
    public void The_Telling_Hooks_Get_Their_Arguments()
    {
        // Arrange
        var host = Host();
        host.Load("""
            game.onkill(function(victim, killer, type) print('kill', victim.name, killer and killer.name, type) end)
            game.onpickup(function(player, item, amount) print('pickup', item.name, amount) end)
            game.onsecret(function(player, x, y) print('secret', player.name, x, y) end)
            game.onplayerdeath(function(player, killer) print('died', player.name, killer) end)
            game.onsight(function(monster, target) print('sight', monster.name, target.name) end)
            game.onspawn(function(player, reason) print('spawn', player.name, reason) end)
            """, "test.lua");
        var guard = new Entities.Actors.Monster { Name = "Guard" };
        var player = new Entities.Actors.PlayerPawn { Name = "Player" };

        // Act
        _scripts.RunKillHooks(guard, player, null);
        _scripts.RunPickupHooks(player, new Entities.Actors.Monster { Name = "Key" }, null);
        _scripts.RunSecretHooks(player, 3, 4);
        _scripts.RunPlayerDeathHooks(player, null);
        _scripts.RunSightHooks(guard, player);
        _scripts.RunSpawnHooks(player, "respawn");

        // Assert
        Assert.That(Printed(), Is.SupersetOf(new[]
        {
            "kill\tGuard\tPlayer\tnil", "pickup\tKey\tnil", "secret\tPlayer\t3\t4", "died\tPlayer\tnil", "sight\tGuard\tPlayer", "spawn\tPlayer\trespawn",
        }));
    }

    [Test]
    public void Each_Player_Is_Spawned_Before_The_Level_Hooks_But_Not_In_A_Loaded_Game()
    {
        // Arrange: a level with the player on it
        var map = new MapManager(new Lazy<AssetManager>(() => null!), null!, _session, null);
        _session.Players.Add(new Entities.PlayerState());
        _session.Players[0].Pawn = new Entities.Actors.PlayerPawn { Name = "Player", State = _session.Players[0] };
        _scripts = new GameScripts(null!, map, null!, null!, _console, null, null!, _session, (_, _) => { });
        var host = Host();
        host.Load("""
            game.onlevel(function(reason) print('level', reason) end)
            game.onspawn(function(player, reason) print('spawn', player.name, reason) end)
            """, "test.lua");

        // Act
        _scripts.RunLevelHooks("new");
        _scripts.RunLevelHooks("loaded");

        // Assert
        Assert.That(Printed().Where(line => line.StartsWith("spawn") || line.StartsWith("level")),
            Is.EqualTo(new[] { "spawn\tPlayer\tnew", "level\tnew", "level\tloaded" }));
    }

    [Test]
    public void The_Scripts_Command_Says_When_None_Are_Loaded()
    {
        _scripts.ScriptsCommand([]);
        Assert.That(Printed(), Has.Some.Contains("No scripts are loaded"));
    }
}
