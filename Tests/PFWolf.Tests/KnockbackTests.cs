using System.Reflection;
using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Enums;

namespace PFWolf.Tests;

/// <summary>Hits that shove what they hit (Entities/Actors/Monster.Knockback.cs)</summary>
public class KnockbackTests
{
    private const string Yaml = """
        Monster: {}
        Guard:
          parent: Monster
          states:
            Spawn:
              - sprite: GARD
                frames: [A]
                tics-per-frame: -1
        HeavyGuard:
          parent: Guard
          properties:
            mass: 200
        Tree:
          parent: Guard
          flags: [NOKNOCKBACK]
        Window:
          parent: Guard
          flags: [WALLSPRITE]
        Rocket:
          properties:
            knockback: 16384
          states:
            Spawn:
              - sprite: ROCK
                frames: [A]
                tics-per-frame: -1
        FlyingRocket:
          parent: Rocket
          flags: [PROJECTILE]
        """;

    private const int Tile = (int)MapConstants.TileGlobal;
    private const int Half = Tile / 2;

    private MapManager _map = null!;
    private GameSession _session = null!;
    private InventoryManager _actors = null!;
    private ActorWorld _world = null!;

    [SetUp]
    public void MakeLevel()
    {
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => new AssetManager()), null!, _session, null)
        {
            MapWidth = MapConstants.ClassicMapSize, MapHeight = MapConstants.ClassicMapSize,
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        typeof(MapManager).GetField("mapsegs", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_map, Enumerable.Range(0, MapManager.LevelPlanes).Select(_ => new ushort[MapConstants.ClassicMapArea]).ToArray());
        _world = new ActorWorld(null!, null!, null!, null!, null!, _map, null!, null!, null!, null!, null!, null!,
            null!, null!, null!, null!, null!, null!, null!, null, null, _session);
        _actors = new InventoryManager(ActorDefs.Metadata(Yaml));
    }

    private Monster Make(string className, int x, int y)
    {
        var monster = (Monster)_actors.CreateActor(className)!;
        monster.World = _world;
        monster.SetPosition(x, y);
        monster.RuntimeFlags |= ObjFlags.Shootable;
        monster.Hitpoints = 25;
        monster.SetState("Spawn");
        _map.GetActors().AddLast(monster);
        return monster;
    }

    // A rocket (knockback 16384, a quarter tile) on a tile
    private Entities.Actors.Actor Rocket(int x, int y)
    {
        var rocket = _actors.CreateActor("Rocket")!;
        rocket.SetPosition(x, y);
        return rocket;
    }

    private void Wall(int x, int y)
    {
        _map.TileMap[x, y] = 1;
        _map.ActorAt[x, y] = new Wall(1);
    }

    private static int Center(int tile) => tile * Tile + Half;

    [Test]
    public void A_Hit_Shoves_It_Away_From_What_Hit_It()
    {
        // Arrange: the rocket two tiles west
        var guard = Make("Guard", 10, 10);

        // Act
        guard.Knock(Rocket(8, 10));

        // Assert
        Assert.That((guard.PushX, guard.PushY), Is.EqualTo((16384, 0)));
    }

    [Test]
    public void A_Projectiles_Hit_Shoves_It_On_The_Way_It_Flew_Even_From_Past_Its_Middle()
    {
        // Arrange: a rocket flying east, already a little past the guard's middle
        var guard = Make("Guard", 10, 10);
        var rocket = _actors.CreateActor("FlyingRocket")!;
        rocket.SetPosition(10, 10);
        rocket.X += 0x2000;
        rocket.Angle = 0;

        // Act
        guard.Knock(rocket);

        // Assert
        Assert.That((guard.PushX, guard.PushY), Is.EqualTo((16384, 0)));
    }

    [Test]
    public void A_Projectiles_Blast_Shoves_It_Away_From_Where_It_Went_Off()
    {
        // Arrange: the same rocket blowing up there (A_Explode sets its blast's knockback)
        var guard = Make("Guard", 10, 10);
        var rocket = _actors.CreateActor("FlyingRocket")!;
        rocket.SetPosition(10, 10);
        rocket.X += 0x2000;
        rocket.Angle = 0;
        guard.BlowKnockback = 16384;

        // Act
        guard.Knock(rocket);

        // Assert
        Assert.That((guard.PushX, guard.PushY), Is.EqualTo((-16384, 0)));
    }

    [Test]
    public void The_Shove_Eases_Out_Over_A_Few_Tics_And_Is_Done()
    {
        // Arrange
        var guard = Make("Guard", 10, 10);
        guard.Knock(Rocket(10, 12));        // from the south: shoved north

        // Act
        guard.PushTick(1);
        int afterOne = guard.Y;
        for (int i = 0; i < 30 && guard.Pushing; i++)
            guard.PushTick(1);

        // Assert: part of the way at first, all of it in the end, and still on its walk's tile
        Assert.That(afterOne, Is.LessThan(Center(10)).And.GreaterThan(Center(10) - 16384));
        Assert.That(guard.Pushing, Is.False);
        Assert.That((guard.X, guard.Y), Is.EqualTo((Center(10), Center(10) - 16384)));
        Assert.That((guard.DispX, guard.DispY), Is.EqualTo((0, -16384)));
        Assert.That((guard.TileX, guard.TileY), Is.EqualTo(((byte)10, (byte)10)));
    }

    [Test]
    public void Twice_The_Mass_Is_Shoved_Half_As_Far()
    {
        var guard = Make("HeavyGuard", 10, 10);

        guard.Knock(Rocket(8, 10));

        Assert.That(guard.PushX, Is.EqualTo(8192));
    }

    [TestCase("Tree")]
    [TestCase("Window")]
    public void Trees_And_Wall_Sprites_Stay_Put(string className)
    {
        var thing = Make(className, 10, 10);

        thing.Knock(Rocket(8, 10));

        Assert.That(thing.Pushing, Is.False);
    }

    [Test]
    public void A_Blow_With_No_Knockback_Shoves_Nothing()
    {
        var guard = Make("Guard", 10, 10);
        var other = Make("Guard", 8, 10);

        guard.Knock(other);

        Assert.That(guard.Pushing, Is.False);
    }

    [Test]
    public void A_Blasts_Own_Knockback_Takes_The_Attackers_Place()
    {
        // Arrange: a blast weakened to a quarter of the rocket's
        var guard = Make("Guard", 10, 10);
        guard.BlowKnockback = 4096;

        // Act
        guard.Knock(Rocket(8, 10));

        // Assert
        Assert.That(guard.PushX, Is.EqualTo(4096));
    }

    [Test]
    public void A_Living_One_Is_Shoved_No_Further_Off_Its_Walk_Than_The_Most()
    {
        // Arrange: shoved twice over
        var guard = Make("Guard", 10, 10);
        guard.Knock(Rocket(8, 10));
        guard.Knock(Rocket(8, 10));

        // Act
        guard.PushTick(70);

        // Assert
        Assert.That(guard.DispX, Is.EqualTo(Monster.MaxDisplacement));
        Assert.That(guard.X, Is.EqualTo(Center(10) + Monster.MaxDisplacement));
        Assert.That(guard.Pushing, Is.False);
    }

    [Test]
    public void A_Body_Slides_The_Whole_Way_Onto_Another_Tile()
    {
        // Arrange: dead, shoved more than half a tile east
        var guard = Make("Guard", 10, 10);
        guard.Knock(Rocket(8, 10));
        guard.Knock(Rocket(8, 10));
        guard.Knock(Rocket(8, 10));
        guard.Hitpoints = 0;
        guard.RuntimeFlags |= ObjFlags.NonMark;

        // Act
        guard.PushTick(70);

        // Assert
        Assert.That(guard.X, Is.EqualTo(Center(10) + 3 * 16384));
        Assert.That(guard.TileX, Is.EqualTo((byte)11));
        Assert.That((guard.DispX, guard.DispY), Is.EqualTo((0, 0)));
    }

    [Test]
    public void A_Wall_Stops_The_Shove()
    {
        // Arrange: a wall just east
        Wall(11, 10);
        var guard = Make("Guard", 10, 10);
        guard.Knock(Rocket(8, 10));

        // Act
        guard.PushTick(70);

        // Assert: its middle no nearer the wall than the player's box
        Assert.That(guard.X, Is.GreaterThan(Center(10)).And.LessThanOrEqualTo(11 * Tile - (int)Gameplay.GameConstants.MinDist));
        Assert.That(guard.Pushing, Is.False);
    }

    [Test]
    public void Shoved_Into_A_Wall_At_An_Angle_It_Slides_Along_It()
    {
        // Arrange: a wall just north, the rocket to the south-west
        Wall(10, 9);
        var guard = Make("Guard", 10, 10);
        guard.Knock(Rocket(8, 12));

        // Act
        guard.PushTick(70);

        // Assert: it went east all the same, and north only as far as the wall let it
        Assert.That(guard.X, Is.GreaterThan(Center(10) + 8192));
        Assert.That(guard.Y, Is.LessThan(Center(10)).And.GreaterThanOrEqualTo(10 * Tile + (int)Gameplay.GameConstants.MinDist));
    }

    [Test]
    public void Walking_On_It_Drifts_Back_Onto_Its_Walk()
    {
        // Arrange: shoved a quarter tile north, walking east to the next tile
        var guard = Make("Guard", 10, 10);
        guard.Knock(Rocket(10, 12));
        guard.PushTick(70);
        guard.TileX = 11;
        guard.Dir = ObjDir.East;
        guard.Distance = Tile;

        // Act
        guard.MoveObj(0x2000);

        // Assert: on east, and half that back towards its walk
        Assert.That(guard.X, Is.EqualTo(Center(10) + 0x2000));
        Assert.That(guard.DispY, Is.EqualTo(-16384 + 0x1000));
        Assert.That(guard.Y, Is.EqualTo(Center(10) - 16384 + 0x1000));
    }

    [Test]
    public void Arriving_On_A_Tile_It_Keeps_What_It_Is_Still_Off_Its_Walk()
    {
        // Arrange
        var guard = Make("Guard", 10, 10);
        guard.DispX = 0;
        guard.DispY = -0x2000;

        // Act
        guard.RecenterOnTile(keepDisplacement: true);

        // Assert: no snapping onto the middle
        Assert.That((guard.X, guard.Y), Is.EqualTo((Center(10), Center(10) - 0x2000)));
    }

    [Test]
    public void Put_On_A_Tile_It_Is_No_Longer_Shoved()
    {
        var guard = Make("Guard", 10, 10);
        guard.Knock(Rocket(8, 10));
        guard.DispY = -0x2000;

        guard.RecenterOnTile();

        Assert.That((guard.X, guard.Y), Is.EqualTo((Center(10), Center(10))));
        Assert.That(guard.Pushing, Is.False);
        Assert.That(guard.DispY, Is.Zero);
    }

    [Test]
    public void A_Saved_Game_Keeps_The_Shove()
    {
        // Arrange
        var guard = Make("Guard", 10, 10);
        guard.Knock(Rocket(8, 10));
        guard.PushTick(1);
        using var stream = new MemoryStream();
        using (var bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            ActorSnapshot.Capture(guard).Write(bw);
        stream.Position = 0;

        // Act
        var loaded = (Monster)_actors.CreateActor("Guard")!;
        using (var br = new BinaryReader(stream))
            ActorSnapshot.Read(br).ApplyTo(loaded);

        // Assert
        Assert.That((loaded.PushX, loaded.PushY, loaded.DispX, loaded.DispY), Is.EqualTo((guard.PushX, guard.PushY, guard.DispX, guard.DispY)));
        Assert.That(loaded.PushX, Is.GreaterThan(0));
    }
}
