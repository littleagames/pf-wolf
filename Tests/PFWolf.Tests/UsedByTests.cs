using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

/// <summary>
/// Something a player uses thinks as them while it's in its Use state (Actor.UsedBy,
/// MapManager.DoActors): its actions (A_Board, A_Mount) are that player's on every machine, not
/// each machine's own player's
/// </summary>
public class UsedByTests
{
    private const string Yaml = """
        Lever:
          states:
            Spawn:
              - sprite: LEVR
                frames: [A]
                tics-per-frame: -1
            Use:
              - sprite: LEVR
                frames: [B]
                tics-per-frame: 1
                action: Test_RecordUser
                next-state: Spawn
        """;

    private static readonly List<Entities.PlayerState> Users = [];
    private static GameSession? acting;

    [OneTimeSetUp]
    public void RegisterAction() =>
        ActorActionRegistry.Register("Test_RecordUser", _ => Users.Add(acting!.PlayerState));

    [Test]
    public void A_Use_States_Action_Is_The_Users_Not_The_Local_Players()
    {
        // Arrange: two players, this machine's the first; the second uses the lever
        var session = new GameSession();
        acting = session;
        Users.Clear();
        session.Players.Add(new Entities.PlayerState { Number = 1 });
        foreach (var p in session.Players)
            p.Pawn = new PlayerPawn { State = p };
        var map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, session, null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        var lever = new InventoryManager(ActorDefs.Metadata(Yaml)).CreateActor("Lever")!;
        lever.SetState("Spawn");
        map.GetActors().AddLast(lever);
        var second = session.Players[1];

        // Act: as TryUseActor does, then the lever's think a tic later
        lever.UsedBy = second.Pawn;
        lever.SetState("Use");
        map.DoActors(1);

        // Assert: it ran as the second player, and it's theirs no longer
        Assert.That(Users, Is.EqualTo(new[] { second }));
        Assert.That(session.PlayerState, Is.SameAs(session.Players[0]), "the local player acts again after");
        Assert.That(lever.UsedBy, Is.Null);
    }
}
