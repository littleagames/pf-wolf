using PFWolf.Assets;
using PFWolf.Configuration;
using PFWolf.Constants;
using PFWolf.Entities;
using PFWolf.Entities.Actors;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>
/// Actors' emitters (Rendering/Particles.cs): actordefs `particles`, states' `particles:`, and
/// A_SpawnParticles / A_StartEmitter / A_StopEmitter, which saves keep
/// </summary>
public class ParticleEmitterTests
{
    private const int Tile = 1 << MapConstants.TileShift;

    private MapManager _map = null!;
    private ParticleDetail _detail;
    private (int X, int Y) _view;
    private Particles _particles = null!;

    [SetUp]
    public void MakeLevel()
    {
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, new GameSession(), null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        _detail = ParticleDetail.High;
        _view = (10 * Tile, 10 * Tile);
        _particles = new Particles(null!, null!, _map, null!, null!, null!,
            new ParticleHooks(() => _detail, (_, _) => true, _ => true, () => 1, () => _view));

        // Smoke: 70 a second (one a tic), long-lived; Steam: 140 a second; Spray: a burst of 5
        _particles.UseDefinitions(YamlDataEntryLoader.Deserialize<ParticlesAsset>("""
            types:
              Bit: { life: 1000, collide: none }
            emitters:
              Smoke: { type: Bit, rate: 70, speed: 0, z: 0 }
              Steam: { type: Bit, rate: 140, speed: 0, z: 0, range: 4 }
              Spray: { type: Bit, count: 5, speed: 0, z: 0 }
            """));
    }

    // An actor in the middle of tile (x, y), on the map, with its class's particles property if given
    private Monster Thing(int x, int y, string? particles = null, ActorStateFrame? state = null)
    {
        var thing = new Monster { Name = $"Thing{particles}{state?.StateName}", X = x * Tile + Tile / 2, Y = y * Tile + Tile / 2, TileX = (byte)x, TileY = (byte)y };
        if (particles != null)
            thing.Properties["particles"] = particles;
        if (state != null)
        {
            thing.ResolvedStates[state.StateName] = state;
            thing.CurrentState = state;
        }
        _map.GetActors().AddLast(thing);
        return thing;
    }

    private static ActorStateFrame State(string name, List<string>? particles) =>
        new() { StateName = name, Sprite = "TNT1", FrameLetter = "A", TicTime = -1, Particles = particles };

    [TestCase("Smoke", new[] { "Smoke" })]
    [TestCase("Smoke, Steam", new[] { "Smoke", "Steam" })]
    [TestCase(" none ", new string[0])]
    [TestCase("off", new string[0])]
    [TestCase("", new string[0])]
    public void Emitter_Names_Are_One_Or_Several_Or_None(string text, string[] names) =>
        Assert.That(StateDataNodeDeserializer.EmitterNames(text), Is.EqualTo(names));

    [Test]
    public void Emitter_Names_Can_Be_A_List() =>
        Assert.That(StateDataNodeDeserializer.EmitterNames(new List<object> { "Smoke", "Steam" }), Is.EqualTo(new[] { "Smoke", "Steam" }));

    [Test]
    public void A_States_Particles_Are_Read_From_Yaml()
    {
        // Act
        var states = ActorStateResolver.Resolve(ActorDefs.Parse("""
            Barrel:
              states:
                Spawn:
                  - sprite: BARL
                    frames: [A]
                    tics-per-frame: -1
                Hit:
                  - sprite: BARL
                    frames: [A]
                    tics-per-frame: -1
                    particles: Smoke, Steam
                Out:
                  - sprite: BARL
                    frames: [A]
                    tics-per-frame: -1
                    particles: none
            """)["Barrel"].States);

        // Assert
        Assert.That(states["Spawn"].Particles, Is.Null);
        Assert.That(states["Hit"].Particles, Is.EqualTo(new[] { "Smoke", "Steam" }));
        Assert.That(states["Out"].Particles, Is.Empty);
    }

    [Test]
    public void A_Class_With_Particles_Throws_Them_Out_At_Its_Rate()
    {
        // Arrange
        Thing(10, 10, "Smoke");

        // Act
        _particles.Tick(10);

        // Assert: one a tic, where it is
        Assert.That(_particles.Count, Is.EqualTo(10));
        var (x, y, _, _) = _particles.Position(0);
        Assert.That((x, y), Is.EqualTo((10.5f * 64, 10.5f * 64)));
    }

    [Test]
    public void A_Rate_Below_One_A_Tic_Builds_Up()
    {
        // Arrange: 70 a second at Low is a quarter a tic
        _detail = ParticleDetail.Low;
        Thing(10, 10, "Smoke");

        // Act & Assert
        _particles.Tick(3);
        Assert.That(_particles.Count, Is.Zero);
        _particles.Tick(1);
        Assert.That(_particles.Count, Is.EqualTo(1));
    }

    [Test]
    public void Two_Emitters_Both_Run()
    {
        // Arrange
        Thing(10, 10, "Smoke, Steam");

        // Act
        _particles.Tick(2);

        // Assert
        Assert.That(_particles.Count, Is.EqualTo(2 + 4));
    }

    [Test]
    public void One_Out_Of_Range_Of_The_View_Throws_None_Out()
    {
        // Arrange: Steam's range is 4 tiles, the view 6 away
        Thing(16, 10, "Steam");

        // Act
        _particles.Tick(5);

        // Assert
        Assert.That(_particles.Count, Is.Zero);
    }

    [Test]
    public void A_States_Particles_Replace_The_Classes()
    {
        // Arrange
        Thing(10, 10, "Smoke", State("Hit", ["Steam"]));

        // Act
        _particles.Tick(1);

        // Assert: Steam's two, not Smoke's one
        Assert.That(_particles.Count, Is.EqualTo(2));
    }

    [Test]
    public void A_State_With_None_Stops_Them()
    {
        // Arrange
        var thing = Thing(10, 10, "Smoke", State("Dead", []));

        // Act
        _particles.Tick(5);

        // Assert
        Assert.That(_particles.Count, Is.Zero);
        Assert.That(_particles.CurrentEmitters(thing), Is.Empty);
    }

    [Test]
    public void A_Class_Without_Particles_Has_A_State_Run_Them()
    {
        // Arrange
        Thing(10, 10, state: State("Burning", ["Smoke"]));

        // Act
        _particles.Tick(3);

        // Assert
        Assert.That(_particles.Count, Is.EqualTo(3));
    }

    [Test]
    public void A_StartEmitter_Adds_One_And_A_StopEmitter_Takes_It_Away()
    {
        // Arrange
        var thing = Thing(10, 10, "Smoke");

        // Act & Assert
        _particles.StartEmitter(thing, "Steam");
        Assert.That(_particles.CurrentEmitters(thing), Is.EqualTo(new[] { "Smoke", "Steam" }));
        _particles.StopEmitter(thing, "Smoke");
        Assert.That(_particles.CurrentEmitters(thing), Is.EqualTo(new[] { "Steam" }));
        _particles.StartEmitter(thing, null);
        Assert.That(_particles.CurrentEmitters(thing), Is.EqualTo(new[] { "Smoke" }), "no name: its class's back");
    }

    [Test]
    public void A_StopEmitter_With_No_Name_Stops_Everything_States_Too()
    {
        // Arrange
        var thing = Thing(10, 10, "Smoke", State("Hit", ["Steam"]));

        // Act
        _particles.StopEmitter(thing, null);
        _particles.Tick(5);

        // Assert
        Assert.That(_particles.Count, Is.Zero);

        // ...until one's started again: its class's and that one
        _particles.StartEmitter(thing, "Spray");
        Assert.That(_particles.StateOf(thing).Override, Is.EqualTo(new ParticleOverride(false, ["Smoke", "Spray"])).Using<ParticleOverride>(Same));
    }

    [Test]
    public void A_Switch_Starts_And_Stops_The_Tagged_Actors_Emitters()
    {
        // Arrange
        Particles.RegisterActions(() => _particles, () => _map);
        var tagged = Thing(10, 10);
        tagged.Tag = 7;
        var other = Thing(12, 10);
        var activation = new TriggerActivation(5, 5, ControlDir.East, other, 7);

        // Act
        MapTriggerRegistry.Invoke("A_StartEmitter(\"Smoke\")", activation);
        _particles.Tick(4);

        // Assert
        Assert.That(_particles.CurrentEmitters(tagged), Is.EqualTo(new[] { "Smoke" }));
        Assert.That(_particles.CurrentEmitters(other), Is.Empty);
        Assert.That(_particles.Count, Is.EqualTo(4));

        // ...and stops them
        MapTriggerRegistry.Invoke("A_StopEmitter", activation);
        _particles.Tick(4);
        Assert.That(_particles.Count, Is.EqualTo(4));
    }

    [Test]
    public void A_SpawnParticles_Bursts_Where_The_Actor_Is()
    {
        // Arrange
        var thing = Thing(20, 30);
        thing.Z = 12;

        // Act
        bool done = _particles.SpawnAction(thing, ["Spray", "3", "8"]);

        // Assert: count 3 in place of 5, 8 texels above the actor
        Assert.That(done, Is.True);
        Assert.That(_particles.Count, Is.EqualTo(3));
        Assert.That(_particles.Position(0), Is.EqualTo((20.5f * 64, 30.5f * 64, 20f, 0f)));
        Assert.That(_particles.SpawnAction(thing, ["Nothing"]), Is.False);
    }

    [Test]
    public void A_Saved_Actor_Keeps_Its_Started_And_Stopped_Emitters()
    {
        // Arrange
        var thing = Thing(10, 10, "Smoke");
        _particles.StartEmitter(thing, "Steam");

        // Act: written and read back, onto a fresh actor of the class
        var stream = new MemoryStream();
        ActorSnapshot.Capture(thing).Write(new BinaryWriter(stream));
        stream.Position = 0;
        var loaded = new Monster { Name = thing.Name, Properties = new() { ["particles"] = "Smoke" } };
        ActorSnapshot.Read(new BinaryReader(stream)).ApplyTo(loaded);

        // Assert
        Assert.That(_particles.CurrentEmitters(loaded), Is.EqualTo(new[] { "Smoke", "Steam" }));
    }

    [Test]
    public void An_Actor_That_Never_Had_Its_Emitters_Changed_Saves_None()
    {
        // Arrange
        var thing = Thing(10, 10, "Smoke");
        _particles.Tick(1);

        // Act & Assert
        Assert.That(ActorSnapshot.Capture(thing).Particles, Is.Null);
    }

    [Test]
    public void It_Faces_Its_Direction_Else_Its_Angle()
    {
        Assert.That(Particles.Facing(new Monster { Name = "A", Dir = ObjDir.North, Angle = 10 }), Is.EqualTo(90));
        Assert.That(Particles.Facing(new Monster { Name = "B", Angle = 200 }), Is.EqualTo(200));
    }

    private static bool Same(ParticleOverride a, ParticleOverride b) => a.Off == b.Off && a.Names.SequenceEqual(b.Names);
}
