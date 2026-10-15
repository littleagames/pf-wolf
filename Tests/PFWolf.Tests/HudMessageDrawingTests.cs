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
        _session.playerstate.Pawn = new Entities.Actors.PlayerPawn { State = _session.playerstate };
        var console = new ConsoleManager();
        _messages = new HudMessageManager(new Lazy<AssetManager>(() => new AssetManager()), new Lazy<ConsoleManager>(console))
        {
            EnabledSetting = true,
        };
        var players = new Players(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, _session, null!);
        _drawing = new HudMessageDrawing(null!, null!, new InventoryManager(ActorDefs.Metadata(Yaml)), _messages, null!, null!, players,
            new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.noshade), _session);
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
        _session.playerstate.LastAttacker = Guard("%o was shot by %k");

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
        _session.playerstate.LastAttacker = rocket;

        // Act
        _drawing.ShowObituary();

        // Assert
        Assert.That(_messages.Messages.Single().Text, Is.EqualTo("Player ate Guard's rocket"));
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
