namespace PFWolf.Tests;

public class StatusBarDrawingTests
{
    // With no assets there's no layout, so nothing is drawn and no manager is needed
    private static StatusBarDrawing Bar(GameSession session, bool fullScreen = false) =>
        new(null!, null, null!, null!, null!, null!, null!, null!, session,
            new Configuration.GameSettings { ViewSize = fullScreen ? 21 : 19 }, new StatusBarHooks(map => map, (text, _, _) => [text]));

    [Test]
    public void Shows_For_The_Local_Player_With_A_View_That_Leaves_Room()
    {
        Assert.That(Bar(new GameSession()).StatusBarHidden, Is.False);
    }

    [Test]
    public void Hidden_With_The_View_Full_Screen()
    {
        Assert.That(Bar(new GameSession { InGame = true }, fullScreen: true).StatusBarHidden, Is.True);
    }

    [Test]
    public void Hidden_While_Another_Player_Acts()
    {
        // Arrange: the bar is this machine's player's
        var session = new GameSession();
        session.Players.Add(new Entities.PlayerState());
        var bar = Bar(session);

        // Act / Assert
        using (session.ActAs(session.Players[1]))
            Assert.That(bar.StatusBarHidden, Is.True);
        Assert.That(bar.StatusBarHidden, Is.False);
    }

    [Test]
    public void Without_A_Layout_It_Is_Empty_And_Draws_Nothing()
    {
        // Arrange
        var session = new GameSession();
        session.PlayerState.Health = 50;
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
            bar.DrawLevelTimer();
            bar.DrawInfoArea(force: true);
            bar.UpdateHeartMonitor();
            bar.DrawHeartMonitor(force: true);
        });
        Assert.That(bar.HasInfoArea, Is.False);
    }

    [TestCase(0, 0)]
    [TestCase(15, 0)]
    [TestCase(16, 1)]
    [TestCase(48, 3)]       // the last picture
    [TestCase(64, 2)]       // then back down
    [TestCase(80, 1)]
    [TestCase(96, 0)]       // and round again
    public void A_Use_Hints_Pictures_Go_Back_And_Forth(int time, int frame)
    {
        Assert.That(StatusBarDrawing.HintFrame(time, 16, 4), Is.EqualTo(frame));
    }

    [Test]
    public void A_Single_Picture_Hint_Doesnt_Animate()
    {
        Assert.That(StatusBarDrawing.HintFrame(1234, 16, 1), Is.Zero);
    }

    [Test]
    public void Doesnt_Grin_Without_A_Pickup_Sound()
    {
        Assert.That(Bar(new GameSession()).Grinning, Is.False);
    }

    // Player has no face of its own; Guard does, Elite inherits it, Dog gives only a face
    private const string ClassesYaml = """
        Player:
          properties:
            "player.maxhealth": 100
        GuardPlayer:
          parent: Player
          properties:
            "player.face": [GUARDLEFT, GUARDAHEAD, GUARDRIGHT]
            "player.facedead": GUARDDEAD
            "player.faceouch": GUARDPAIN
        ElitePlayer:
          parent: GuardPlayer
        DogPlayer:
          parent: Player
          properties:
            "player.face": DOGHEAD
        """;

    private static (StatusBarDrawing Bar, Entities.PlayerState Player) FaceBar(string playerClass, int health = 100)
    {
        var session = new GameSession();
        var bar = new StatusBarDrawing(null!, null, null!, null!, new InventoryManager(ActorDefs.Metadata(ClassesYaml)), null!, null!, null!,
            session, new Configuration.GameSettings(), new StatusBarHooks(map => map, (text, _, _) => [text]));
        var face = new Assets.StatusBarElement
        {
            Faces = [new() { Health = 50, Pics = ["FACE1A", "FACE1B", "FACE1C"] }, new() { Health = 1, Pics = ["FACE2A"] }],
            Dead = "FACE8A",
            Ouch = "OUCH",
        };
        bar.StatusBar = new Assets.StatusBarAsset(new() { ["face"] = face });
        session.PlayerState.PlayerClass = playerClass;
        session.PlayerState.Health = (short)health;
        return (bar, session.PlayerState);
    }

    [Test]
    public void A_Class_Without_A_Face_Shows_The_Layouts_By_Health()
    {
        // Arrange
        var (bar, player) = FaceBar("Player", health: 20);
        player.FaceFrame = 2;

        // Act / Assert
        Assert.That(bar.FacePic(), Is.EqualTo("FACE2A"));
        player.Health = 0;
        Assert.That(bar.FacePic(), Is.EqualTo("FACE8A"));
        Assert.That(bar.OuchPic(), Is.EqualTo("OUCH"));
    }

    [TestCase(100, 0, "GUARDLEFT")]
    [TestCase(100, 2, "GUARDRIGHT")]
    [TestCase(5, 1, "GUARDAHEAD")]       // the same at any health
    [TestCase(0, 1, "GUARDDEAD")]
    public void A_Class_Face_Looks_Around_At_Every_Health(int health, int frame, string pic)
    {
        // Arrange
        var (bar, player) = FaceBar("GuardPlayer", health);
        player.FaceFrame = (short)frame;

        // Act / Assert
        Assert.That(bar.FacePic(), Is.EqualTo(pic));
    }

    [Test]
    public void A_Class_Face_Is_Inherited()
    {
        // Act / Assert
        Assert.That(FaceBar("ElitePlayer").Bar.FacePic(), Is.EqualTo("GUARDLEFT"));
    }

    [Test]
    public void A_Class_Face_Without_Its_Own_Dead_Or_Ouch_Keeps_Its_Face()
    {
        // Arrange
        var (bar, _) = FaceBar("DogPlayer", health: 0);

        // Act / Assert: not BJ's dead face or wince
        Assert.That(bar.FacePic(), Is.EqualTo("DOGHEAD"));
        Assert.That(bar.OuchPic(), Is.Null);
        Assert.That(FaceBar("GuardPlayer").Bar.OuchPic(), Is.EqualTo("GUARDPAIN"));
    }

    [Test]
    public void The_Part_In_The_Mode_Has_Its_Face_First()
    {
        // Arrange: a Guard playing the part of a dog
        var (bar, player) = FaceBar("GuardPlayer");
        player.RoleClass = "DogPlayer";

        // Act / Assert
        Assert.That(bar.FacePic(), Is.EqualTo("DOGHEAD"));
    }
}
