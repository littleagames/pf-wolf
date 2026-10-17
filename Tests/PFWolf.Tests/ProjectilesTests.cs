using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Enums;

namespace PFWolf.Tests;

public class ProjectilesTests
{
    private const int Tile = 1 << MapConstants.TILESHIFT;

    private MapManager _map = null!;
    private GameSession _session = null!;
    private Projectiles _projectiles = null!;
    private bool _wallSpriteInTheWay;

    [OneTimeSetUp]
    public void Tables() => TrigTables.BuildTables();      // the sines a projectile flies by

    [SetUp]
    public void MakeMap()
    {
        // An open map with nothing on it; the tests put walls where they need them
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session)
        {
            tilemap = new ushort[MapManager.MAPSIZE, MapManager.MAPSIZE],
            actorat = new Actor?[MapManager.MAPSIZE, MapManager.MAPSIZE],
        };
        _wallSpriteInTheWay = false;
        _projectiles = new Projectiles(_map, null!, _session, new ProjectileHooks(
            (_, _, _, _, _, _, _) => _wallSpriteInTheWay ? new WallSpriteSpan(0, 0, 1, 0) : null,
            (_, _) => { }, (_, _, _) => { }));
    }

    // In the middle of tile (10, 10), plus dx
    private static Monster Needle(int dx = 0) => new() { Name = "Needle", X = 10 * Tile + Tile / 2 + dx, Y = 10 * Tile + Tile / 2 };

    [Test]
    public void A_Projectile_Flies_Over_Open_Floor()
    {
        Assert.That(_projectiles.ProjectileTryMove(Needle()), Is.True);
    }

    [Test]
    public void A_Wall_Its_Box_Reaches_Stops_It()
    {
        // Arrange: a wall east, the needle close to it
        _map.actorat[11, 10] = new Wall(1);

        // Act / Assert
        Assert.That(_projectiles.ProjectileTryMove(Needle(Tile / 2 - 0x1000)), Is.False);
        Assert.That(_projectiles.ProjectileTryMove(Needle()), Is.True);           // not yet
    }

    [Test]
    public void An_Enemys_Mark_Doesnt_Stop_It()
    {
        // Arrange: enemies are hit by the projectile's own check, not by the map
        _map.actorat[10, 10] = new Monster { Name = "Guard" }.Mark;

        // Act / Assert
        Assert.That(_projectiles.ProjectileTryMove(Needle()), Is.True);
    }

    [Test]
    public void A_Wall_Sprite_Stops_It_Only_Along_Its_Panel()
    {
        // Arrange
        _map.actorat[10, 10] = new WallSpriteBlocker();

        // Act / Assert
        Assert.That(_projectiles.ProjectileTryMove(Needle()), Is.True);
        _wallSpriteInTheWay = true;
        Assert.That(_projectiles.ProjectileTryMove(Needle()), Is.False);
    }

    [Test]
    public void A_Projectile_Without_A_Death_State_Vanishes_Against_A_Wall()
    {
        // Arrange: flying east into a wall
        _map.actorat[11, 10] = new Wall(1);
        var needle = Needle(Tile / 2 - 0x1000);
        needle.Angle = 0;
        needle.Speed = 0x800;
        _session.tics = 1;

        // Act
        _projectiles.A_Projectile(needle, ["10"]);

        // Assert
        Assert.That(needle.IsRemoved, Is.True);
    }

    [Test]
    public void A_Projectile_In_The_Open_Moves_Along_Its_Angle_And_Keeps_Its_Tile_Up()
    {
        // Arrange: north, a tile a tic
        var needle = Needle();
        needle.Angle = 90;
        needle.Speed = Tile;
        _session.tics = 1;
        int y = needle.Y;

        // Act
        _projectiles.A_Projectile(needle, ["10"]);

        // Assert
        Assert.That(needle.Y, Is.EqualTo(y - Tile));
        Assert.That((needle.TileX, needle.TileY), Is.EqualTo(((byte)10, (byte)9)));
        Assert.That(needle.IsRemoved, Is.False);
    }

    [Test]
    public void The_Death_Cam_Can_Stand_Only_Clear_Of_Walls_And_Solid_Things()
    {
        // Arrange
        _map.actorat[11, 10] = new BlockingActor();

        // Act / Assert
        Assert.That(_projectiles.CheckPosition(10 * Tile + Tile / 2, 10 * Tile + Tile / 2), Is.True);
        Assert.That(_projectiles.CheckPosition(11 * Tile - 0x1000, 10 * Tile + Tile / 2), Is.False);
    }

    [Test]
    public void A_Remove_Takes_The_Effect_Away()
    {
        var smoke = Needle();
        _projectiles.A_Remove(smoke);
        Assert.That(smoke.IsRemoved, Is.True);
    }
}

public class ActorSoundsTests
{
    private readonly List<string> _played = [];

    [SetUp]
    public void Quiet() => _played.Clear();

    private ActorSounds Sounds() => new(null!, null!, new GameSession(), (sound, _) => _played.Add(sound));

    [Test]
    public void An_Actor_With_One_Sound_Always_Makes_It()
    {
        // Arrange
        var guard = new Monster { Name = "Guard" };
        guard.Properties["deathsound"] = "guard/death";

        // Act
        Sounds().A_DeathScream(guard);

        // Assert
        Assert.That(_played, Is.EqualTo(new[] { "guard/death" }));
    }

    [Test]
    public void One_With_A_List_Picks_From_It()
    {
        // Arrange
        var guard = new Monster { Name = "Guard" };
        guard.Properties["deathsound"] = new List<object> { "a", "b", "c" };
        var sounds = Sounds();

        // Act
        for (int i = 0; i < 30; i++)
            sounds.A_DeathScream(guard);

        // Assert
        Assert.That(_played, Has.All.AnyOf("a", "b", "c"));
        Assert.That(_played.Distinct().Count(), Is.GreaterThan(1));
    }

    [Test]
    public void One_Without_The_Sound_Is_Quiet()
    {
        Sounds().A_DeathScream(new Monster { Name = "Ghost" });
        Assert.That(_played, Is.Empty);
    }

    [Test]
    public void A_Positional_Sound_Comes_From_The_Actor()
    {
        Sounds().A_PlaySound(new Monster { Name = "BJ" }, ["misc/yeah", "positional"]);
        Assert.That(_played, Is.EqualTo(new[] { "misc/yeah" }));
    }
}

public class BJVictoryTests
{
    [Test]
    public void BJ_Done_And_The_Angels_Last_Frame_Win_The_Game()
    {
        // Arrange
        var session = new GameSession();
        var victory = new BJVictory(null!, session);

        // Act / Assert
        victory.T_BJDone(new Monster { Name = "BJVictory" });
        Assert.That(session.playstate, Is.EqualTo(PlayState.ex_victorious));

        session.playstate = PlayState.ex_stillplaying;
        victory.A_Victory(new Monster { Name = "Angel" });
        Assert.That(session.playstate, Is.EqualTo(PlayState.ex_victorious));
    }
}
