namespace PFWolf.Tests;

public class LevelLoopTests
{
    [Test]
    public void The_Level_Starts_With_Each_Player_Still_In_The_Game_At_The_Head_Of_The_Actors()
    {
        // Arrange: three players, the second gone; a guard already on the map
        // CreatePlayer gives each pawn its side from the map's FactionManager
        var factions = new FactionManager(new Lazy<AssetManager>(() => new AssetManager()), new Lazy<InventoryManager>(() => new InventoryManager(ActorDefs.Metadata("Player: {}"))));
        var session = new GameSession();      // it starts with its first player
        var map = new MapManager(new Lazy<AssetManager>(() => new AssetManager()), new Lazy<FactionManager>(() => factions), session, null);
        var guard = new Entities.Actors.Monster { Name = "Guard" };
        map.GetActors().AddLast(guard);
        session.Players.Add(new Entities.PlayerState { Number = 2, Gone = true });
        session.Players.Add(new Entities.PlayerState { Number = 3 });
        var loop = new LevelLoop(null!, null!, null!, null!, null!, map, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, session, null!);


        // Act
        loop.InitActorList();

        // Assert: the players think first, in order; whoever has gone has no pawn
        Assert.That(map.GetActors(), Is.EqualTo(new Entities.Actors.Actor?[] { session.Players[0].Pawn, session.Players[2].Pawn, guard }));
        Assert.That(session.Players[0].Pawn, Is.Not.Null);
        Assert.That(session.Players[1].Pawn, Is.Null);
    }
}
