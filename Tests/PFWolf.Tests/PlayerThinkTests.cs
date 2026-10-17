using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

public class PlayerThinkTests
{
    private const int Tile = 1 << MapConstants.TileShift;

    // VictorySpin and the victory part of T_Player only use the session
    private static (PlayerThink Think, GameSession Session, PlayerPawn Player) Won(bool runnerOnItsWay = false)
    {
        var session = new GameSession();
        var pawn = new PlayerPawn { State = session.playerstate, X = 10 * Tile + Tile / 2, Y = 30 * Tile + Tile / 2, TileX = 10, TileY = 30 };
        session.playerstate.Pawn = pawn;
        session.gamestate.victoryflag = true;
        session.tics = 1;
        var think = new PlayerThink(null!, null!, null!, null!, null!, null!, null!, null!, null!, session,
            new PlayerThinkHooks(() => { }, () => runnerOnItsWay, () => { }, _ => { }));
        return (think, session, pawn);
    }

    [TestCase(300, 297)]
    [TestCase(271, 270)]        // not past it
    [TestCase(200, 203)]
    [TestCase(270, 270)]
    public void Once_Won_The_Player_Turns_To_Face_South_A_Few_Degrees_A_Tic(int from, int to)
    {
        // Arrange
        var (think, _, player) = Won();
        player.Angle = (short)from;

        // Act
        think.VictorySpin();

        // Assert
        Assert.That(player.Angle, Is.EqualTo(to));
    }

    [Test]
    public void And_Backs_Away_North_Until_Five_Tiles_Back()
    {
        // Arrange
        var (think, session, player) = Won();
        int dest = (25 << MapConstants.TileShift) - 0x3000;
        session.playerstate.Pitch = 12;

        // Act / Assert: 4096 a tic
        int y = player.Y;
        think.VictorySpin();
        Assert.That(player.Y, Is.EqualTo(y - 4096));
        Assert.That(session.playerstate.Pitch, Is.Zero);        // watching BJ straight on

        session.tics = 1000;
        think.VictorySpin();
        Assert.That(player.Y, Is.EqualTo(dest));
    }

    [Test]
    public void Once_Won_The_Think_Only_Watches()
    {
        // Arrange
        var (think, _, player) = Won();
        player.Angle = 0;

        // Act: nothing else it needs is there, so this would throw if it did anything more
        think.ThinkPlayer(player);

        // Assert
        Assert.That(player.Angle, Is.Not.EqualTo(0));
    }

    [Test]
    public void With_The_Runner_On_Its_Way_The_Player_Doesnt_Turn()
    {
        // Arrange
        var (think, _, player) = Won(runnerOnItsWay: true);
        player.Angle = 0;
        int y = player.Y;

        // Act
        think.ThinkPlayer(player);

        // Assert
        Assert.That((player.Angle, player.Y), Is.EqualTo(((short)0, y)));
    }
}
