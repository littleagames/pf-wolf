using PFWolf.Assets;

namespace PFWolf.Tests;

public class HudMessageDrawingTests
{
    private const string Yaml = """
        Player: {}
        Guard:
          properties:
            tag: Guard
        Rocket: {}
        """;

    private GameSession _session = null!;
    private HudMessageManager _messages = null!;
    private HudMessageDrawing _drawing = null!;

    [SetUp]
    public void Make()
    {
        _session = new GameSession();
        _session.PlayerState.Pawn = new Entities.Actors.PlayerPawn { State = _session.PlayerState };
        var console = new ConsoleManager();
        _messages = new HudMessageManager(new Lazy<AssetManager>(() => new AssetManager()), new Lazy<ConsoleManager>(console), session: _session)
        {
            EnabledSetting = true,
        };
        var players = new Players(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, _session, null!);
        _drawing = new HudMessageDrawing(null!, null!, new InventoryManager(ActorDefs.Metadata(Yaml)), _messages, null!, null!, players,
            new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.NoShade), _session);
    }

    private static Entities.Actors.Monster Guard(string? obituary = null)
    {
        var guard = new Entities.Actors.Monster { Name = "Guard" };
        if (obituary != null)
            guard.Properties["obituary"] = obituary;
        return guard;
    }

    [Test]
    public void The_Obituary_Fills_In_Who_Died_And_Who_Did_It()
    {
        // Arrange
        _session.PlayerState.LastAttacker = Guard("%o was shot by %k");

        // Act
        _drawing.ShowObituary();

        // Assert: the player is their class's name, alone
        Assert.That(_messages.Messages.Single().Text, Is.EqualTo("Player was shot by Guard"));
    }

    [Test]
    public void A_Projectile_With_No_Obituary_Of_Its_Own_Is_Put_Down_To_Its_Shooter()
    {
        // Arrange
        var rocket = new Entities.Actors.Monster { Name = "Rocket" };
        rocket.Shooter = Guard("%o ate %k's rocket");
        _session.PlayerState.LastAttacker = rocket;

        // Act
        _drawing.ShowObituary();

        // Assert
        Assert.That(_messages.Messages.Single().Text, Is.EqualTo("Player ate Guard's rocket"));
    }

    [Test]
    public void The_Players_Own_Blast_Says_They_Blew_Themselves_Up()
    {
        // Arrange: a rocket of theirs, with no obituary of its own
        var rocket = new Entities.Actors.Monster { Name = "Rocket", Shooter = _session.Player };
        _session.PlayerState.LastAttacker = rocket;

        // Act
        _drawing.ShowObituary();

        // Assert: not "killed by Player" (no language file here, so the key itself)
        Assert.That(_messages.Messages.Single().Text, Does.Contain("OB_SELF"));
    }

    /// <summary>A deathmatch with a second player, acting as them (the one killed); this machine's is the first</summary>
    private Entities.PlayerState SecondPlayerActing()
    {
        var second = new Entities.PlayerState { Number = 1, Name = "Client" };
        second.Pawn = new Entities.Actors.PlayerPawn { State = second };
        _session.PlayerState.Name = "Host";
        _session.Players.Add(second);
        _session.SetGameMode(GameMode.Deathmatch);
        _session.SetActing(second);
        return second;
    }

    [Test]
    public void The_Kill_Feed_Reaches_Players_Other_Than_The_One_Killed()
    {
        // Arrange: the first player (this machine's) shot the second, with messages off
        SecondPlayerActing().LastAttacker = _session.Players[0].Pawn;
        _messages.EnabledSetting = false;

        // Act
        _drawing.ShowKillFeed();

        // Assert: shown here (no language file here, so the key itself)
        Assert.That(_messages.Messages.Single().Text, Is.EqualTo("$KF_KILLED"));
    }

    [Test]
    public void The_Kill_Feed_Says_How_They_Died()
    {
        var second = SecondPlayerActing();
        var feed = new List<string>();
        void Feed(Entities.Actors.Actor? killer)
        {
            second.LastAttacker = killer;
            _drawing.ShowKillFeed();
            feed.Add(_messages.Messages[^1].Text);
            _messages.Clear();
        }

        Feed(new Entities.Actors.Monster { Name = "Rocket", Shooter = _session.Players[0].Pawn });    // a player's rocket
        Feed(new Entities.Actors.Monster { Name = "Rocket", Shooter = second.Pawn });                // their own
        Feed(Guard());
        Feed(null);

        Assert.That(feed, Is.EqualTo(new[] { "$KF_KILLED", "$KF_SELF", "$KF_MONSTER", "$KF_DIED" }));
    }

    [Test]
    public void An_Actors_Tag_Is_Its_Own_Else_Its_Classes_Else_Its_Name()
    {
        var tagged = new Entities.Actors.Monster { Name = "Rocket" };
        tagged.Properties["tag"] = "Missile";

        Assert.That(_drawing.ActorTag(tagged), Is.EqualTo("Missile"));
        Assert.That(_drawing.ActorTag(Guard()), Is.EqualTo("Guard"));
        Assert.That(_drawing.ActorTag(new Entities.Actors.Monster { Name = "Rocket" }), Is.EqualTo("Rocket"));
    }

    [TestCase("TopLeft", 0, 0)]
    [TestCase("TopRight", 2, 0)]
    [TestCase("Bottom", 1, 2)]
    [TestCase("Left", 0, 1)]
    public void Anchors_Are_Read_Across_Then_Down(string name, int across, int down)
    {
        var anchor = Enum.Parse<HudAnchor>(name);
        Assert.That((_drawing.HorizontalPlace(anchor), _drawing.VerticalPlace(anchor)), Is.EqualTo((across, down)));
    }
}
