using PFWolf.Assets;
using PFWolf.Configuration;
using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Loaders;
using PFWolf.Scripting;

namespace PFWolf.Tests;

/// <summary>
/// Particles from Lua (Scripting/GameScripts.Particles.cs): game.particles, actor:particles,
/// game.emitter and its handle, game.defineparticle / defineemitter, game.particlecount
/// </summary>
public class ParticleScriptTests
{
    private const int Tile = 1 << MapConstants.TileShift;

    private MapManager _map = null!;
    private GameSession _session = null!;
    private ConsoleManager _console = null!;
    private Particles _particles = null!;
    private GameScripts _scripts = null!;
    private ScriptHost _host = null!;

    [SetUp]
    public void MakeScripts()
    {
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session, null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        _particles = new Particles(null!, null!, _map, null!, null!, null!,
            new ParticleHooks(() => ParticleDetail.High, (_, _) => true, _ => true, () => 1, () => (10 * Tile, 10 * Tile)));
        _particles.UseDefinitions(YamlDataEntryLoader.Deserialize<ParticlesAsset>("""
            types:
              Bit: { life: 1000, collide: none, color: "#FFFFFF" }
            emitters:
              Spray: { type: Bit, count: 5, rate: 70, speed: 0, z: 0 }
            """));
        _console = new ConsoleManager();
        _scripts = new GameScripts(null!, _map, null!, null!, _console, null, null!, _session, (_, _) => { })
        {
            ParticleSystem = () => _particles,
        };
        _scripts.LuaCommand(["x = 1"]);
        _host = _scripts.Scripts!;
    }

    private List<string> Printed() => [.. _console.Scrollback];

    private Monster Thing(int x, int y)
    {
        var thing = new Monster { Name = "Thing", X = x * Tile + Tile / 2, Y = y * Tile + Tile / 2, TileX = (byte)x, TileY = (byte)y };
        _map.GetActors().AddLast(thing);
        return thing;
    }

    [Test]
    public void Game_Particles_Bursts_At_A_Spot_In_Tiles()
    {
        // Act
        var error = _host.Execute("print(game.particles('Spray', 12.5, 20.5, 10))");

        // Assert: its count of 5, z added to the emitter's own
        Assert.That(error, Is.Null);
        Assert.That(Printed(), Has.Some.EqualTo("5"));
        Assert.That(_particles.Position(0), Is.EqualTo((12.5f * 64, 20.5f * 64, 10f, 0f)));
    }

    [Test]
    public void Changes_Change_The_Emitter_And_Its_Type()
    {
        // Act: a count, and the type's life (as particles.yaml writes it, and with an underscore)
        _host.Execute("print(game.particles('Spray', 10, 10, 0, { count = 3, life = 2, size_end = 4 }))");
        _particles.Tick(2);

        // Assert: three, gone after their 2 tics
        Assert.That(Printed(), Has.Some.EqualTo("3"));
        Assert.That(_particles.Count, Is.Zero);
    }

    [Test]
    public void A_Change_There_Is_No_Field_For_Is_An_Error()
    {
        // Act
        var unknown = _host.Execute("game.particles('Spray', 10, 10, 0, { colour = 'red' })");
        var wrong = _host.Execute("game.particles('Spray', 10, 10, 0, { count = 'lots' })");
        var nothing = _host.Execute("game.particles('Nothing', 10, 10)");

        // Assert
        Assert.That(unknown, Does.Contain("has no colour"));
        Assert.That(wrong, Does.Contain("count should be a whole number"));
        Assert.That(nothing, Does.Contain("no emitter or particle type Nothing"));
    }

    [Test]
    public void Actor_Particles_Bursts_Where_It_Is()
    {
        // Arrange
        var thing = Thing(20, 30);
        thing.Z = 6;
        _host.Globals["thing"] = LuaActor.Of(thing);

        // Act
        _host.Execute("print(thing:particles('Spray', { count = 2 }))");

        // Assert
        Assert.That(Printed(), Has.Some.EqualTo("2"));
        Assert.That(_particles.Position(1), Is.EqualTo((20.5f * 64, 30.5f * 64, 6f, 0f)));
    }

    [Test]
    public void An_Emitter_Runs_Until_Stopped_And_Starts_Again()
    {
        // Arrange: 70 a second, one a tic
        _host.Execute("e = game.emitter('Spray', 10, 10)");

        // Act & Assert
        _particles.Tick(3);
        Assert.That(_particles.Count, Is.EqualTo(3));
        _host.Execute("e:stop() print(e:running())");
        _particles.Tick(3);
        Assert.That(_particles.Count, Is.EqualTo(3));
        _host.Execute("e:start() e:set{ rate = 140 }");
        _particles.Tick(1);
        Assert.That(_particles.Count, Is.EqualTo(5));
        Assert.That(Printed(), Has.Some.EqualTo("false"));
    }

    [Test]
    public void An_Emitter_Moves_And_Is_Removed()
    {
        // Arrange
        _host.Execute("e = game.emitter('Spray', 10, 10)");

        // Act
        _host.Execute("e:moveto(15.5, 15.5, 20)");
        _particles.Tick(1);
        _host.Execute("e:remove() print(e:running())");
        _particles.Tick(5);

        // Assert
        Assert.That(_particles.Count, Is.EqualTo(1));
        Assert.That(_particles.Position(0), Is.EqualTo((15.5f * 64, 15.5f * 64, 20f, 0f)));
        Assert.That(Printed(), Has.Some.EqualTo("false"));
        Assert.That(_particles.ScriptEmitterCount, Is.Zero);
    }

    [Test]
    public void An_Emitter_On_An_Actor_Follows_It_And_Goes_With_It()
    {
        // Arrange
        var thing = Thing(10, 10);
        _host.Globals["thing"] = LuaActor.Of(thing);
        _host.Execute("e = game.emitter('Spray', thing)");

        // Act: it moves a tile east, then it's gone
        thing.X += Tile;
        _particles.Tick(1);
        _map.MarkForRemoval(thing);
        _particles.Tick(1);

        // Assert
        Assert.That(_particles.Count, Is.EqualTo(1));
        Assert.That(_particles.Position(0).X, Is.EqualTo(11.5f * 64));
        Assert.That(_particles.ScriptEmitterCount, Is.Zero);
    }

    [Test]
    public void An_Emitter_Ends_With_Its_Level()
    {
        // Arrange
        _host.Execute("e = game.emitter('Spray', 10, 10)");

        // Act: a new level (or a death, or a loaded game) is a new LevelSerial
        _map.LevelSerial++;
        _particles.Tick(3);

        // Assert
        Assert.That(_particles.Count, Is.Zero);
        Assert.That(_particles.ScriptEmitterCount, Is.Zero);
    }

    [Test]
    public void A_Script_Defines_A_Type_From_A_Base_And_An_Emitter_Of_It()
    {
        // Act
        var error = _host.Execute("""
            game.defineparticle('Short', { base = 'Bit', life = 3, colors = { '#FF0000', '#000000' } })
            game.defineemitter('Shorts', { base = 'Spray', type = 'Short', count = 4 })
            print(game.particles('Shorts', 10, 10), game.particlecount())
            """);
        _particles.Tick(3);

        // Assert: four, which last 3 tics (Bit's collide: none kept)
        Assert.That(error, Is.Null);
        Assert.That(Printed(), Has.Some.EqualTo("4\t4"));
        Assert.That(_particles.Count, Is.Zero);
        Assert.That(_particles.Definitions.Types["Short"].Collide, Is.EqualTo("none"));
        Assert.That(_particles.Definitions.Types["Short"].Colors, Is.EqualTo(new[] { "#FF0000", "#000000" }));
    }

    [Test]
    public void A_Defined_Emitters_Type_Must_Be_One_There_Is()
    {
        // Act
        var error = _host.Execute("game.defineemitter('Odd', { type = 'Nothing' })");
        var badBase = _host.Execute("game.defineparticle('Odd', { base = 'Nothing' })");

        // Assert
        Assert.That(error, Does.Contain("its type Nothing isn't a particle type"));
        Assert.That(badBase, Does.Contain("base Nothing isn't a particle type"));
    }

    [Test]
    public void Particles_Can_Be_Thrown_Out_From_A_Hud_Hook()
    {
        // Arrange: game.onhud changes nothing in the game, and particles don't
        _host.Load("game.onhud(function() game.particles('Spray', 10, 10) end)", "test.lua");

        // Act
        _scripts.RunHudHooks();

        // Assert
        Assert.That(_particles.Count, Is.EqualTo(5));
        Assert.That(Printed(), Has.None.Contains("mustn't change the game"));
    }

    [Test]
    public void The_Handles_Methods_Are_The_Lists()
    {
        // Act
        _host.Execute("e = game.emitter('Spray', 10, 10) local names = {} for k in pairs(e) do names[#names + 1] = k end table.sort(names) print(table.concat(names, ','))");

        // Assert
        Assert.That(Printed(), Has.Some.EqualTo(string.Join(",", ScriptApi.EmitterMethods.Select(m => m.Name).Order(StringComparer.Ordinal))));
    }
}
