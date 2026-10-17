namespace PFWolf.Tests;

public class PlayerActionsTests
{
    [Test]
    public void A_Change_Map_Without_A_Map_Leaves_The_Level_Going()
    {
        // Arrange
        var session = new GameSession();
        PendingMapChange? change = null;
        var actions = new PlayerActions(null!, null!, null!, session, new PlayerActionsHooks(c => change = c));

        // Act
        actions.ChangeMapAction(new Entities.Actors.Monster { Name = "Spear" }, []);

        // Assert
        Assert.That((change, session.playstate), Is.EqualTo(((PendingMapChange?)null, PlayState.ex_stillplaying)));
    }

    [Test]
    public void Giving_Nothing_Gives_Nothing()
    {
        // No assets or stats: giving anything would reach them
        var actions = new PlayerActions(null!, null!, null!, new GameSession(), new PlayerActionsHooks(_ => { }));

        Assert.DoesNotThrow(() => actions.GiveInventoryAction(new Entities.Actors.Monster { Name = "Guard" }, []));
    }
}