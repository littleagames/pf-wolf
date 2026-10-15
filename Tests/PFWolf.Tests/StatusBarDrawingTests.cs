namespace PFWolf.Tests;

public class StatusBarDrawingTests
{
    // With no assets there's no layout, so nothing is drawn and no manager is needed
    private static StatusBarDrawing Bar(GameSession session, bool fullScreen = false) =>
        new(null!, null, null!, null!, null!, null!, null!, null!, session,
            new StatusBarHooks(() => fullScreen, map => map, (text, _, _) => [text]));

    [Test]
    public void Shows_For_The_Local_Player_With_A_View_That_Leaves_Room()
    {
        Assert.That(Bar(new GameSession()).StatusBarHidden, Is.False);
    }

    [Test]
    public void Hidden_With_The_View_Full_Screen()
    {
        Assert.That(Bar(new GameSession(), fullScreen: true).StatusBarHidden, Is.True);
    }

    [Test]
    public void Hidden_While_Another_Player_Acts()
    {
        // Arrange: the bar is this machine's player's
        var session = new GameSession();
        session.players.Add(new Entities.PlayerState());
        var bar = Bar(session);

        // Act / Assert
        using (session.ActAs(session.players[1]))
            Assert.That(bar.StatusBarHidden, Is.True);
        Assert.That(bar.StatusBarHidden, Is.False);
    }

    [Test]
    public void Without_A_Layout_It_Is_Empty_And_Draws_Nothing()
    {
        // Arrange
        var session = new GameSession();
        session.playerstate.health = 50;
        var bar = Bar(session);

        // Act / Assert: the managers it's given are null, so drawing anything would throw
        Assert.That(bar.StatusBar, Is.SameAs(StatusBarDrawing.NoStatusBar));
        Assert.DoesNotThrow(() =>
        {
            bar.DrawFace();
            bar.DrawHealth();
            bar.DrawArmor();
            bar.DrawKeys();
            bar.DrawLives();
            bar.DrawScore();
            bar.DrawWeaponPic();
            bar.DrawTopBar();
            bar.DrawLocation();
            bar.DrawRadarGauge();
            bar.DrawInfoArea(force: true);
            bar.UpdateHeartMonitor();
            bar.DrawHeartMonitor(force: true);
        });
        Assert.That(bar.HasInfoArea, Is.False);
    }

    [Test]
    public void Doesnt_Grin_Without_A_Pickup_Sound()
    {
        Assert.That(Bar(new GameSession()).Grinning, Is.False);
    }
}
