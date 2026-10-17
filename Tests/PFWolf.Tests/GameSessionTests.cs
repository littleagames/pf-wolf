using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

public class GameSessionTests
{
    /// <summary>A session with a second player, each with a pawn</summary>
    private static GameSession TwoPlayers()
    {
        var session = new GameSession();
        session.Players.Add(new Entities.PlayerState());
        foreach (var p in session.Players)
            p.Pawn = new PlayerPawn { State = p };
        return session;
    }

    [Test]
    public void Starts_With_One_Local_Player_Acting()
    {
        // Arrange
        var session = new GameSession();

        // Assert
        Assert.That(session.Players, Has.Count.EqualTo(1));
        Assert.That(session.PlayerState, Is.SameAs(session.LocalPlayer));
        Assert.That(session.ActingIsLocal, Is.True);
        Assert.That(session.GameMode, Is.EqualTo(GameMode.Single));
    }

    [Test]
    public void ActAs_Makes_Another_Player_Act_Until_Disposed()
    {
        // Arrange
        var session = TwoPlayers();
        var other = session.Players[1];

        // Act / Assert
        using (session.ActAs(other))
        {
            Assert.That(session.PlayerState, Is.SameAs(other));
            Assert.That(session.Player, Is.SameAs(other.Pawn));
            Assert.That(session.PlayerInput, Is.SameAs(other.Input));
            Assert.That(session.ActingIsLocal, Is.False);
        }
        Assert.That(session.PlayerState, Is.SameAs(session.Players[0]));
        Assert.That(session.ActingIsLocal, Is.True);
    }

    [Test]
    public void ActingChanged_Hears_Each_Change()
    {
        // Arrange
        var session = TwoPlayers();
        var heard = new List<Entities.PlayerState>();
        session.ActingChanged = heard.Add;

        // Act
        using (session.ActAs(session.Players[1]))
        {
        }

        // Assert
        Assert.That(heard, Is.EqualTo(new[] { session.Players[1], session.Players[0] }));
    }

    [Test]
    public void The_Console_Player_Is_The_Local_One()
    {
        // Arrange
        var session = TwoPlayers();

        // Act
        session.ConsolePlayer = 1;

        // Assert
        Assert.That(session.LocalPlayer, Is.SameAs(session.Players[1]));
        Assert.That(session.ActingIsLocal, Is.False);       // the first is still acting
    }

    [Test]
    public void No_Pawn_Means_No_Player()
    {
        Assert.Throws<InvalidOperationException>(() => _ = new GameSession().Player);
    }

    [Test]
    public void SetGameMode_Is_Heard()
    {
        // Arrange
        var session = new GameSession();
        GameMode? heard = null;
        session.GameModeChanged = mode => heard = mode;

        // Act
        session.SetGameMode(GameMode.Coop);

        // Assert
        Assert.That(session.GameMode, Is.EqualTo(GameMode.Coop));
        Assert.That(heard, Is.EqualTo(GameMode.Coop));
    }
}

public class CameraTests
{
    private static (GameSession Session, PlayerPawn Pawn) WithPlayer()
    {
        var session = new GameSession();
        var pawn = new PlayerPawn { State = session.Players[0], X = 0x18000, Y = 0x28000, Angle = 45 };
        session.Players[0].Pawn = pawn;
        session.Players[0].Pitch = 10;
        return (session, pawn);
    }

    [Test]
    public void It_Looks_Through_The_Player_By_Default()
    {
        // Arrange
        var (session, pawn) = WithPlayer();
        var camera = session.Camera;

        // Assert
        Assert.That(camera.Target, Is.SameAs(pawn));
        Assert.That(camera.OnPlayer, Is.True);
        Assert.That(camera.Spectating, Is.False);
        Assert.That((camera.X, camera.Y, camera.TileX, camera.TileY), Is.EqualTo((0x18000, 0x28000, 1, 2)));
        Assert.That(camera.Angle, Is.EqualTo((short)45));
        Assert.That(camera.Pitch, Is.EqualTo(10));
    }

    [Test]
    public void Chasing_Looks_From_Behind_The_Player_And_Higher_Short_Of_A_Wall()
    {
        // Arrange: facing east from the middle of tile 5,5; a wall at x 3 unless opened up
        var session = new GameSession();
        var pawn = new PlayerPawn { State = session.Players[0], X = 0x58000, Y = 0x58000, Angle = 0 };
        session.Players[0].Pawn = pawn;
        var camera = session.Camera;
        bool wall = false;
        camera.IsOpenTile = (x, _) => !(wall && x <= 3);

        // Act / Assert: off, the player's own view
        Assert.That((camera.Chasing, camera.X), Is.EqualTo((false, 0x58000)));

        camera.WantsChase = () => true;
        Assert.That(camera.Chasing, Is.True);
        Assert.That((camera.X, camera.Y), Is.EqualTo((0x58000 - Camera.ChaseDistance, 0x58000)), "a tile and a half behind");
        Assert.That(camera.EyeZ, Is.EqualTo(session.Players[0].EyeZ + Camera.ChaseRise));

        wall = true;
        Assert.That(camera.X, Is.InRange(0x40000 + 0x4000, 0x58000 - 1), "pulled in a quarter tile short of the wall");

        camera.WantsChase = () => false;
        Assert.That(camera.X, Is.EqualTo(0x58000));
    }

    [Test]
    public void There_Is_No_Target_Before_The_Player_Has_A_Pawn()
    {
        Assert.That(new GameSession().Camera.Target, Is.Null);
    }

    [Test]
    public void A_Fixed_Point_Has_No_Target()
    {
        // Arrange
        var (session, _) = WithPlayer();
        var camera = session.Camera;

        // Act
        camera.SetFixed(0x30000, 0x40000, 180, pitch: -5, eyez: 20);

        // Assert
        Assert.That(camera.Target, Is.Null);
        Assert.That(camera.OnPlayer, Is.False);
        Assert.That((camera.X, camera.Y, camera.Angle, camera.Pitch, camera.EyeZ), Is.EqualTo((0x30000, 0x40000, (short)180, -5.0, 20)));
    }

    [Test]
    public void FollowPlayer_Comes_Back_From_A_Fixed_Point()
    {
        // Arrange
        var (session, pawn) = WithPlayer();
        session.Camera.SetFixed(0, 0, 0);

        // Act
        session.Camera.FollowPlayer();

        // Assert
        Assert.That(session.Camera.Target, Is.SameAs(pawn));
    }

    [Test]
    public void Following_Another_Actor_Is_Spectating()
    {
        // Arrange
        var (session, _) = WithPlayer();
        var guard = new Monster { Name = "Guard", Dir = ObjDir.North };

        // Act
        session.Camera.Follow(guard);

        // Assert
        Assert.That(session.Camera.Spectating, Is.True);
        Assert.That(session.Camera.OnPlayer, Is.False);
        Assert.That(session.Camera.Angle, Is.EqualTo((short)90));
        Assert.That(session.Camera.Pitch, Is.EqualTo(0));
        Assert.That(session.Camera.EyeZ, Is.EqualTo(GameConstants.EyeDefault));
    }

    [TestCase(5u, 40)]          // 8 degrees a tic toward north
    [TestCase(100u, 90)]        // and no further
    public void A_Watched_Actor_Turns_Smoothly(uint tics, int expected)
    {
        // Arrange
        var (session, _) = WithPlayer();
        var guard = new Monster { Name = "Guard", Dir = ObjDir.East };
        session.Camera.Follow(guard);
        guard.Dir = ObjDir.North;

        // Act
        session.Camera.Tick(tics);

        // Assert
        Assert.That(session.Camera.Angle, Is.EqualTo((short)expected));
    }

    [Test]
    public void It_Turns_The_Shorter_Way_Round()
    {
        // Arrange: east (0) to south (270) is 90 degrees clockwise, not 270 the other way
        var (session, _) = WithPlayer();
        var guard = new Monster { Name = "Guard", Dir = ObjDir.East };
        session.Camera.Follow(guard);
        guard.Dir = ObjDir.South;

        // Act
        session.Camera.Tick(1);

        // Assert
        Assert.That(session.Camera.Angle, Is.EqualTo((short)352));
    }

    [Test]
    public void A_Removed_Actor_Gives_The_View_Back()
    {
        // Arrange
        var (session, pawn) = WithPlayer();
        var guard = new Monster { Name = "Guard" };
        session.Camera.Follow(guard);

        // Act
        guard.IsRemoved = true;

        // Assert
        Assert.That(session.Camera.Target, Is.SameAs(pawn));
        Assert.That(session.Camera.Spectating, Is.False);
    }
}
