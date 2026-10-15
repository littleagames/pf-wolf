namespace PFWolf.Tests;

public class LevelLoopTests
{
    [Test]
    public void The_Level_Starts_With_Each_Player_Still_In_The_Game_At_The_Head_Of_The_Actors()
    {
        // Arrange: three players, the second gone; a guard already on the map
        var map = new MapManager(new Lazy<AssetManager>(() => new AssetManager()));
        var guard = new Entities.Actors.Monster { Name = "Guard" };
        map.GetActors().AddLast(guard);
        var session = new GameSession();      // it starts with its first player
        session.players.Add(new Entities.PlayerState { Number = 2, Gone = true });
        session.players.Add(new Entities.PlayerState { Number = 3 });
        var loop = new LevelLoop(null!, null!, null!, null!, null!, map, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, session, null!);

        // CreatePlayer gives each pawn its side from the game's FactionManager
        var factions = Program._factionManager;
        Program._factionManager = new FactionManager(new Lazy<AssetManager>(() => new AssetManager()), new Lazy<InventoryManager>(() => new InventoryManager(ActorDefs.Metadata("Player: {}"))));
        try
        {
            // Act
            loop.InitActorList();
        }
        finally
        {
            Program._factionManager = factions;
        }

        // Assert: the players think first, in order; whoever has gone has no pawn
        Assert.That(map.GetActors(), Is.EqualTo(new Entities.Actors.Actor?[] { session.players[0].Pawn, session.players[2].Pawn, guard }));
        Assert.That(session.players[0].Pawn, Is.Not.Null);
        Assert.That(session.players[1].Pawn, Is.Null);
    }
}
