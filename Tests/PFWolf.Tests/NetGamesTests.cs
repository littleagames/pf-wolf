using PFWolf.Networking;

namespace PFWolf.Tests;

public class NetGamesTests
{
    private GameSession _session = null!;
    private NetPlay _netPlay = null!;
    private NetGames _games = null!;

    [SetUp]
    public void Make()
    {
        _session = new GameSession();
        _netPlay = new NetPlay(null!, null!, null!, null!, null!, null!, _session, null!);
        _games = new NetGames(null!, null!, null!, new TranslationManager(new Lazy<AssetManager>(() => new AssetManager()), null!), null!, null!,
            new NetStartRequest(), null!, null!, null!, null!, null!, _netPlay, _session,
            new NetGamesHooks((_, _, _, _) => Assert.Fail("no game"), () => 0, _ => { }, () => "MENU"));
    }

    [Test]
    public void With_No_Host_Or_Join_Asked_For_Nothing_Starts()
    {
        Assert.That(_games.HasPendingNetStart, Is.False);
        Assert.That(_games.RunPendingNetStart(), Is.False);
    }

    [Test]
    public void A_Color_This_Game_Doesnt_Have_Is_None()
    {
        // No translations.yaml here: no color is one this game has
        Assert.That(_games.PlayerColor("Red"), Is.Null);
        Assert.That(_games.PlayerColor(""), Is.Null);
    }

    [Test]
    public void Ending_A_Game_With_Others_Goes_Back_To_Playing_Alone()
    {
        // Arrange
        _netPlay.netLeft = true;
        _session.NetGame = true;

        // Act: no session open (nothing to say why)
        _games.EndNetGame();

        // Assert
        Assert.That((_session.NetGame, _netPlay.netLeft), Is.EqualTo((false, false)));
    }
}
