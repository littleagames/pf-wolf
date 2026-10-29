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
        Assert.That<(PendingMapChange change, PlayState playstate)>((change, playstate:session.PlayState), Is.EqualTo<(PendingMapChange, PlayState StillPlaying)>(((PendingMapChange?)null, PlayState.StillPlaying)));
    }

    [TestCase(new string[0], false, "")]
    [TestCase(new[] { "keep-position" }, true, "")]
    [TestCase(new[] { "portal1" }, false, "portal1")]
    [TestCase(new[] { " portal1 " }, false, "portal1")]
    [TestCase(new[] { "KEEP-POSITION", "portal1" }, true, "portal1")]
    [TestCase(new[] { "portal1", "keep-position" }, true, "portal1")]
    [TestCase(new[] { "", "portal1" }, false, "portal1")]       // an empty word isn't an entrance
    public void A_Change_Maps_Other_Word_Is_The_Entrance(string[] words, bool keepPosition, string entrance)
    {
        Assert.That(PlayerActions.ChangeMapOptions(words), Is.EqualTo((keepPosition, entrance)));
    }

    [Test]
    public void A_Switch_Can_Change_The_Map()
    {
        // Arrange: the engine's actions, the switch and trigger ones among them
        GameHost.RegisterActorActions();

        // Assert
        Assert.That(Entities.MapTriggerRegistry.IsRegistered("A_ChangeMap"), Is.True);
    }

    [Test]
    public void Giving_Nothing_Gives_Nothing()
    {
        // No assets or stats: giving anything would reach them
        var actions = new PlayerActions(null!, null!, null!, new GameSession(), new PlayerActionsHooks(_ => { }));

        Assert.DoesNotThrow(() => actions.GiveInventoryAction(new Entities.Actors.Monster { Name = "Guard" }, []));
    }
}