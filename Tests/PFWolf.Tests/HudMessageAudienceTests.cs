using PFWolf.Assets;

namespace PFWolf.Tests;

/// <summary>With others playing, each machine shows only the messages for its own player</summary>
public class HudMessageAudienceTests
{
    private GameSession _session = null!;
    private HudMessageManager _messages = null!;

    private Entities.PlayerState Local => _session.Players[0];
    private Entities.PlayerState Other => _session.Players[1];

    [SetUp]
    public void Make()
    {
        // This machine is player 0, with a second player in the game
        _session = new GameSession();
        _session.Players.Add(new Entities.PlayerState { Number = 1 });
        _messages = new HudMessageManager(new Lazy<AssetManager>(() => new AssetManager()), new Lazy<ConsoleManager>(new ConsoleManager()),
            session: _session)
        {
            EnabledSetting = true,
        };
    }

    private IEnumerable<string> Shown => _messages.Messages.Select(m => m.Text);

    [Test]
    public void The_Local_Players_Own_Message_Is_Shown()
    {
        _messages.Show(HudMessageKind.Lock, "This door is locked.");

        Assert.That(Shown, Is.EqualTo(new[] { "This door is locked." }));
    }

    [Test]
    public void Another_Players_Message_Is_Not_Shown_Here()
    {
        // Act: the other player bumps a locked door and picks something up
        using (_session.ActAs(Other))
        {
            _messages.Show(HudMessageKind.Lock, "This door is locked.");
            _messages.Show(HudMessageKind.Pickup, "Picked up a clip.");
            _messages.ShowKeyed("god", "0:30");
        }

        Assert.That(_messages.Messages, Is.Empty);
    }

    [Test]
    public void A_Message_To_Everyone_Is_Shown_Whoever_Is_Acting()
    {
        using (_session.ActAs(Other))
            _messages.ShowToAll(HudMessageKind.Other, "Red team took the flag");

        Assert.That(Shown, Is.EqualTo(new[] { "Red team took the flag" }));
    }

    [Test]
    public void A_Message_To_One_Player_Is_Shown_Only_On_Their_Machine()
    {
        // Act: while the other player acts (their shot), one message for each of them
        using (_session.ActAs(Other))
        {
            _messages.ShowTo(Local, HudMessageKind.Other, "for the local player");
            _messages.ShowTo(Other, HudMessageKind.Other, "for the other player");
        }

        Assert.That(Shown, Is.EqualTo(new[] { "for the local player" }));
    }

    [Test]
    public void A_Message_To_Some_Players_Is_Shown_When_The_Local_One_Is_Among_Them()
    {
        Local.Team = 1;
        Other.Team = 0;

        _messages.ShowToPlayers(p => p.Team == 0, HudMessageKind.Other, "blue team");
        _messages.ShowToPlayers(p => p.Team == 1, HudMessageKind.Other, "red team");

        Assert.That(Shown, Is.EqualTo(new[] { "red team" }));
    }

    [Test]
    public void Without_A_Session_Every_Message_Is_Shown()
    {
        var messages = new HudMessageManager(new Lazy<AssetManager>(() => new AssetManager()), new Lazy<ConsoleManager>(new ConsoleManager()));

        messages.ShowTo(Other, HudMessageKind.Other, "anyone");

        Assert.That(messages.Messages.Select(m => m.Text), Is.EqualTo(new[] { "anyone" }));
    }
}
