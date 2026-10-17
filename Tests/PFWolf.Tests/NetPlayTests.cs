using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Enums;
using PFWolf.Networking;

namespace PFWolf.Tests;

public class NetPlayTests
{
    private const int Tile = 1 << MapConstants.TileShift;

    private MapManager _map = null!;
    private Doors _doors = null!;
    private PushWalls _pushWalls = null!;
    private GameSession _game = null!;
    private bool _netGame { get => _game.NetGame; set => _game.NetGame = value; }
    private int _areasConnected;

    private NetPlay NetPlay() =>
        new(_map, null!, null!, null!, _doors, _pushWalls, _game,
            new NetPlayHooks(() => default, () => [], () => { }, () => { }, _ => { }, () => _areasConnected++));

    [SetUp]
    public void MakeLevel()
    {
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, new GameSession())
        {
            TileMap = new ushort[MapManager.MapSize, MapManager.MapSize],
            ActorAt = new Actor?[MapManager.MapSize, MapManager.MapSize],
        };
        _game = new GameSession();
        _doors = new Doors(null!, null!, _map, null!, null!, _game, null!);
        _pushWalls = new PushWalls(null!, null!, new GameSession());
        _netGame = false;
        _areasConnected = 0;
    }

    private Monster Guard(int x)
    {
        var guard = new Monster { Name = "Guard", X = x, Y = 10 * Tile };
        _map.GetActors().AddLast(guard);
        return guard;
    }

    [Test]
    public void The_Same_Game_Sums_Up_The_Same()
    {
        // Arrange
        Guard(5 * Tile);
        var netPlay = NetPlay();

        // Act / Assert
        Assert.That(netPlay.GameChecksum(), Is.EqualTo(netPlay.GameChecksum()));
    }

    [Test]
    public void Anything_The_Game_Depends_On_Changes_The_Sum()
    {
        // Arrange
        var guard = Guard(5 * Tile);
        var netPlay = NetPlay();
        var sums = new HashSet<uint> { netPlay.GameChecksum() };

        // Act: each change makes a sum not seen before
        guard.X += 1;
        Assert.That(sums.Add(netPlay.GameChecksum()), Is.True, "an actor moving");
        guard.Hitpoints -= 1;
        Assert.That(sums.Add(netPlay.GameChecksum()), Is.True, "an actor hurt");
        _game.PlayerState.Health -= 1;
        Assert.That(sums.Add(netPlay.GameChecksum()), Is.True, "a player hurt");
        _game.Random.RndT();
        Assert.That(sums.Add(netPlay.GameChecksum()), Is.True, "the random numbers moving on");
        _doors.DoorNum = 1;
        _doors.DoorObjList[0] = new DoorObject { Position = 0x8000 };
        Assert.That(sums.Add(netPlay.GameChecksum()), Is.True, "a door opening");
    }

    [Test]
    public void An_Actor_Gone_From_The_Level_Isnt_Summed()
    {
        // Arrange
        var netPlay = NetPlay();
        var before = netPlay.GameChecksum();
        var guard = Guard(5 * Tile);

        // Act
        guard.IsRemoved = true;

        // Assert
        Assert.That(netPlay.GameChecksum(), Is.EqualTo(before));
    }

    [Test]
    public void A_Player_Who_Leaves_Is_Gone_From_The_Level_And_The_Camera_Moves_Off_Them()
    {
        // Arrange
        var p = new Entities.PlayerState { Number = 1 };
        p.Pawn = new PlayerPawn { State = p };
        _game.Players.Add(p);
        _game.Camera.Follow(p.Pawn);

        // Act
        var pawn = p.Pawn;
        NetPlay().RemovePlayer(p);

        // Assert
        Assert.That(p.Gone, Is.True);
        Assert.That(p.Pawn, Is.Null);
        Assert.That(pawn.IsRemoved, Is.True);
        Assert.That(_game.Camera.Target, Is.Not.SameAs(pawn));
        Assert.That(_areasConnected, Is.EqualTo(1));
    }

    [Test]
    public void Without_A_Network_Game_A_Frame_Doesnt_Play_And_A_Level_Isnt_Counted()
    {
        // Arrange
        var netPlay = NetPlay();
        netPlay.NetLeavePrompt = true;
        netPlay.NetWaiting = "Waiting for Drew...";
        netPlay.PendingJoinState = (1, 5);

        // Act
        netPlay.NetLevelStart();

        // Assert
        Assert.That(netPlay.NetFrame(), Is.False);
        Assert.That((netPlay.NetLevel, netPlay.NetLeavePrompt, netPlay.NetWaiting, netPlay.PendingJoinState), Is.EqualTo((0, false, (string?)null, ((int, int)?)null)));
    }

    [Test]
    public void A_Network_Game_Is_Over_For_A_Machine_That_Left()
    {
        // Arrange
        var netPlay = NetPlay();

        // Act / Assert
        Assert.That(netPlay.NetGameOver, Is.False);         // playing alone
        _netGame = true;
        Assert.That(netPlay.NetGameOver, Is.True);          // no session: the host is lost
    }
}

public class NetOverlayTests
{
    private static (NetOverlay Overlay, NetPlay NetPlay, GameSession Game) Overlay()
    {
        var game = new GameSession { NetGame = true };
        var netPlay = new NetPlay(null!, null!, null!, null!, null!, null!, game,
            new NetPlayHooks(() => default, () => [], () => { }, () => { }, _ => { }, () => { }));
        var input = new InputManager(new Lazy<ConsoleManager>(() => new ConsoleManager()));
        return (new NetOverlay(input, null!, null!, null!, null!, null!, netPlay, game), netPlay, game);
    }

    [Test]
    public void Esc_Asks_Whether_To_Leave()
    {
        var (overlay, netPlay, _) = Overlay();
        overlay.NetCheckKeys(ScanCodes.Escape);
        Assert.That(netPlay.NetLeavePrompt, Is.True);
    }

    [Test]
    public void Then_Y_Leaves()
    {
        // Arrange
        var (overlay, netPlay, game) = Overlay();
        netPlay.NetLeavePrompt = true;

        // Act
        overlay.NetCheckKeys(ScanCodes.Y);

        // Assert
        Assert.That(netPlay.NetLeft, Is.True);
        Assert.That(netPlay.NetLeavePrompt, Is.False);
        Assert.That(game.PlayState, Is.EqualTo(PlayState.Abort));
    }

    [TestCase("N")]
    [TestCase("Escape")]
    public void And_N_Or_Esc_Plays_On(string key)
    {
        // Arrange
        var (overlay, netPlay, game) = Overlay();
        netPlay.NetLeavePrompt = true;

        // Act
        overlay.NetCheckKeys(Enum.Parse<ScanCodes>(key));

        // Assert
        Assert.That((netPlay.NetLeft, netPlay.NetLeavePrompt), Is.EqualTo((false, false)));
        Assert.That(game.PlayState, Is.EqualTo(PlayState.StillPlaying));
    }

    [Test]
    public void T_Starts_A_Line_To_Say_But_Not_While_Asking_About_Leaving()
    {
        // Arrange
        var (overlay, netPlay, _) = Overlay();

        // Act / Assert
        overlay.NetCheckKeys(ScanCodes.T);
        Assert.That(overlay.NetChatting, Is.True);

        overlay.NetChatting = false;
        netPlay.NetLeavePrompt = true;
        overlay.NetCheckKeys(ScanCodes.T);
        Assert.That(overlay.NetChatting, Is.False);
    }
}
