using PFWolf.Constants;
using PFWolf.Entities;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

public class PlayerMovementTests
{
    private const int Tile = 1 << MapConstants.TILESHIFT;

    private MapManager _map = null!;
    private PushWalls _pushWalls = null!;
    private GameSession _session = null!;
    private List<string> _sounds = null!;
    private bool _noclip { get => _session.noclip != 0; set => _session.noclip = (byte)(value ? 1 : 0); }
    private PlayerMovement _movement = null!;

    [SetUp]
    public void MakeMap()
    {
        // An open map with nothing on it; the tests put walls and actors where they need them
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session)
        {
            tilemap = new ushort[MapManager.MAPSIZE, MapManager.MAPSIZE],
            actorat = new Actor?[MapManager.MAPSIZE, MapManager.MAPSIZE],
            mapwidth = 64,
            mapheight = 64,
        };
        _pushWalls = new PushWalls(null!, null!, new GameSession());
        _session.playerstate.Pawn = new PlayerPawn { State = _session.playerstate };
        _sounds = [];
        _noclip = false;
        _movement = new PlayerMovement(_map, new AudioManager(new Lazy<AssetManager>(() => null!), _session), _pushWalls, _session,
            new PlayerMovementHooks(_ => 1, () => 20, _sounds.Add,
                (_, _, _, _, _, _, _) => null, (_, _) => { }, (_, _, _, _) => { }));
    }

    // In the middle of tile (10, 10)
    private PlayerPawn Player()
    {
        var pawn = _session.player;
        (pawn.X, pawn.Y) = (10 * Tile + Tile / 2, 10 * Tile + Tile / 2);
        return pawn;
    }

    [TestCase(0, "East")]
    [TestCase(44, "East")]
    [TestCase(46, "North")]
    [TestCase(90, "North")]
    [TestCase(180, "West")]
    [TestCase(270, "South")]
    [TestCase(359, "East")]
    public void Facing_Is_The_Nearest_Of_The_Four_Directions(int angle, string dir)
    {
        Assert.That(PlayerMovement.FacingDir(angle).ToString(), Is.EqualTo(dir));
    }

    [Test]
    public void Turning_Keeps_What_Doesnt_Make_A_Whole_Degree()
    {
        // Arrange: ANGLESCALE (20) of controlx turns a degree, to the right for positive
        var pawn = Player();
        pawn.Angle = 90;
        _session.playerinput.Begin(new TicCmd { ControlX = 45 });

        // Act
        _movement.ControlMovement(pawn);

        // Assert
        Assert.That(pawn.Angle, Is.EqualTo(88));
        Assert.That(_session.playerstate.AngleFrac, Is.EqualTo(5));
    }

    [Test]
    public void Turning_Wraps_Round()
    {
        // Arrange
        var pawn = Player();
        pawn.Angle = 0;
        _session.playerinput.Begin(new TicCmd { ControlX = 20 });

        // Act
        _movement.ControlMovement(pawn);

        // Assert
        Assert.That(pawn.Angle, Is.EqualTo(359));
    }

    [Test]
    public void Looking_Up_And_Down_Stops_At_The_Views_Limit_And_Centers_Again()
    {
        // Arrange
        var pawn = Player();

        // Act / Assert
        _session.playerinput.Begin(new TicCmd { Pitch = 15 });
        _movement.ControlMovement(pawn);
        _session.playerinput.Begin(new TicCmd { Pitch = 15 });
        _movement.ControlMovement(pawn);
        Assert.That(_session.playerstate.Pitch, Is.EqualTo(20));

        _session.playerinput.Begin(new TicCmd { CenterView = true });
        _movement.ControlMovement(pawn);
        Assert.That(_session.playerstate.Pitch, Is.Zero);
    }

    [Test]
    public void Open_Floor_Lets_The_Player_Stand()
    {
        Assert.That(_movement.TryMove(Player()), Is.True);
    }

    [Test]
    public void A_Wall_The_Players_Box_Reaches_Into_Blocks()
    {
        // Arrange: a wall east, and the player close to it
        _map.actorat[11, 10] = new Wall(1);
        var pawn = Player();
        pawn.X = 11 * Tile - 0x2000;

        // Act / Assert
        Assert.That(_movement.TryMove(pawn), Is.False);
    }

    [TestCase((byte)0, false)]      // just starting off: its back is still at the tile's edge
    [TestCase((byte)32, true)]      // half way across: there's room behind it
    public void A_Pushwalls_Back_Blocks_Only_Where_It_Has_Got_To(byte pos, bool room)
    {
        // Arrange: a pushwall moving away east, the player close behind it
        _map.tilemap[11, 10] = GameConstants.BIT_WALL;
        _map.actorat[11, 10] = new Wall(GameConstants.BIT_WALL);
        _pushWalls.pushwalls.Add(new MovingPushWall { X = 11, Y = 10, Dir = ControlDir.East, Pos = pos, Tile = 1 });
        var pawn = Player();
        pawn.X = 11 * Tile - 0x2000;

        // Act / Assert
        Assert.That(_movement.TryMove(pawn), Is.EqualTo(room));
    }

    [Test]
    public void A_Solid_Actor_Close_By_Blocks()
    {
        // Arrange: a guard marked on the next tile, within MINACTORDIST
        var guard = new Monster { Name = "Guard", X = 11 * Tile + Tile / 2, Y = 10 * Tile + Tile / 2 };
        guard.RuntimeFlags |= ObjFlags.Shootable;
        _map.actorat[11, 10] = guard.Mark;

        // Act / Assert
        Assert.That(_movement.TryMove(Player()), Is.False);

        guard.RuntimeFlags &= ~ObjFlags.Shootable;      // dead
        Assert.That(_movement.TryMove(Player()), Is.True);
    }

    [Test]
    public void Running_Into_A_Wall_Slides_Along_It_And_Bumps()
    {
        // Arrange: a wall east; the player moves east and a little south
        _map.actorat[11, 10] = new Wall(1);
        var pawn = Player();
        pawn.X = 11 * Tile - 0x6000;
        var (x, y) = (pawn.X, pawn.Y);

        // Act
        _movement.ClipMove(pawn, 0x2000, 0x1000);

        // Assert: only the move south was taken
        Assert.That((pawn.X, pawn.Y), Is.EqualTo((x, y + 0x1000)));
        Assert.That(_sounds, Is.EqualTo(new[] { "world/hitwall" }));
    }

    [Test]
    public void Boxed_In_The_Player_Stays_Put()
    {
        // Arrange: walls east and south, moving into the corner
        _map.actorat[11, 10] = new Wall(1);
        _map.actorat[10, 11] = new Wall(1);
        _map.actorat[11, 11] = new Wall(1);
        var pawn = Player();
        (pawn.X, pawn.Y) = (11 * Tile - 0x6000, 11 * Tile - 0x6000);
        var (x, y) = (pawn.X, pawn.Y);

        // Act
        _movement.ClipMove(pawn, 0x2000, 0x2000);

        // Assert
        Assert.That((pawn.X, pawn.Y), Is.EqualTo((x, y)));
    }

    [Test]
    public void Noclip_Walks_Through_Walls()
    {
        // Arrange
        _noclip = true;
        _map.actorat[11, 10] = new Wall(1);
        var pawn = Player();
        pawn.X = 11 * Tile - 0x6000;
        var x = pawn.X;

        // Act
        _movement.ClipMove(pawn, 0x2000, 0);

        // Assert
        Assert.That(pawn.X, Is.EqualTo(x + 0x2000));
        Assert.That(_sounds, Is.Empty);
    }
}
