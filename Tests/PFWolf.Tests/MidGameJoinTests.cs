using PFWolf.Networking;

namespace PFWolf.Tests;

public class MidGameJoinTests
{
    [Test]
    public void A_Game_Sent_In_Another_Form_Is_Refused_With_Nothing_Changed()
    {
        // Arrange: a state from a newer (or older) version
        var session = new GameSession();
        var players = session.players.ToList();
        bool? netgame = null;
        var join = new MidGameJoin(null!, null!, null!, null!, null!, null!, null!, null!, null!, session,
            new MidGameJoinHooks(() => 0, _ => { }, _ => { }, on => netgame = on, () => { }, _ => null, () => { }));
        var state = BitConverter.GetBytes(MidGameJoin.NETSTATEVERSION + 1);

        // Act
        var why = join.BeginJoinedGame(null!, new JoinStateInfo(null!, 1, 0, state));

        // Assert
        Assert.That(why, Does.Contain("can't read"));
        Assert.That(session.players, Is.EqualTo(players));
        Assert.That(netgame, Is.Null);
    }

    [Test]
    public void A_Garbled_Game_Is_Refused_And_Isnt_Played_With_Others()
    {
        // Arrange: the right version, then nothing
        var session = new GameSession();
        bool? netgame = null, loaded = null;
        var join = new MidGameJoin(null!, null!, null!, null!, null!, null!, null!, null!, new NetPlay(null!, null!, null!, null!, null!, null!, session, null!), session,
            new MidGameJoinHooks(() => 0, _ => { }, on => loaded = on, on => netgame = on, () => { }, _ => null, () => { }));

        // Act
        var why = join.BeginJoinedGame(null!, new JoinStateInfo(null!, 1, 0, BitConverter.GetBytes(MidGameJoin.NETSTATEVERSION)));

        // Assert
        Assert.That(why, Does.StartWith("The game couldn't be taken up"));
        Assert.That((netgame, loaded), Is.EqualTo(((bool?)false, (bool?)false)));
    }
}
