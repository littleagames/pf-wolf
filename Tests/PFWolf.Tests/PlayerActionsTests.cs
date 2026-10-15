namespace PFWolf.Tests;

public class PlayerActionsTests
{
    [Test]
    public void A_Change_Map_Without_A_Map_Leaves_The_Level_Going()
    {
        // Arrange
        var session = new GameSession();
        playstatetypes? ended = null;
        PendingMapChange? change = null;
        var actions = new PlayerActions(null!, null!, null!, session, new PlayerActionsHooks(c => change = c, s => ended = s));

        // Act
        actions.ChangeMapAction(new Entities.Actors.Monster { Name = "Spear" }, []);

        // Assert
        Assert.That((change, ended), Is.EqualTo(((PendingMapChange?)null, (playstatetypes?)null)));
    }

    [Test]
    public void Giving_Nothing_Gives_Nothing()
    {
        // No assets or stats: giving anything would reach them
        var actions = new PlayerActions(null!, null!, null!, new GameSession(), new PlayerActionsHooks(_ => { }, _ => { }));

        Assert.DoesNotThrow(() => actions.GiveInventoryAction(new Entities.Actors.Monster { Name = "Guard" }, []));
    }
}