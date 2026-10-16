namespace PFWolf.Tests;

public class FactionCommandsTests
{
    private ConsoleManager _console = null!;
    private GameSession _session = null!;

    [SetUp]
    public void Make()
    {
        _console = new ConsoleManager { LevelLoaded = () => true, CheatsEnabled = () => true };
        _session = new GameSession();
        _session.players.Add(new Entities.PlayerState { Number = 1, Color = "Red" });
        var assets = new AssetManager();
        var map = new MapManager(new Lazy<AssetManager>(() => assets), null!, _session);
        var inventory = new InventoryManager(ActorDefs.Metadata("Player: {}"));
        new FactionCommands(_console, assets, map, new FactionManager(new Lazy<AssetManager>(() => assets), new Lazy<InventoryManager>(() => inventory)),
            new TranslationManager(new Lazy<AssetManager>(() => assets), null!), null!, _session).Register();
    }

    [Test]
    public void Playercolor_None_Puts_A_Player_Back_In_Their_Own_Colors()
    {
        _console.Execute("playercolor none 2");

        Assert.That(_session.players[1].Color, Is.Null);
        Assert.That(_console.Scrollback[^1], Is.EqualTo("Player 2 is in their class's own colors"));
    }

    [Test]
    public void A_Color_There_Isnt_Is_Refused()
    {
        _console.Execute("playercolor Purple");

        Assert.That(_session.players[0].Color, Is.Null);
        Assert.That(_console.Scrollback[^1], Does.Contain("no translation Purple"));
    }

    [Test]
    public void With_No_Translations_The_List_Says_So()
    {
        _console.Execute("translations");
        Assert.That(_console.Scrollback[^1], Is.EqualTo("(none)"));
    }

    [Test]
    public void Factions_Says_Whose_Side_Everyone_Is_On_By_Default()
    {
        _console.Execute("factions");
        Assert.That(_console.Scrollback.Any(line => line.StartsWith("Players are ") && line.EndsWith("(no factions.yaml)")), Is.True);
    }
}
