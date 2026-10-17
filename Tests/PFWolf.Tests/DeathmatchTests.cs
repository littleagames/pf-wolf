using PFWolf.Entities.Actors;
using PFWolf.Enums;

namespace PFWolf.Tests;

public class DeathmatchTests
{
    private const string Yaml = """
        Inventory:
          properties:
            "inventory.amount": 1
            "inventory.maxamount": 1
        Weapon:
          parent: Inventory
        WeaponGiver:
          parent: Weapon
        ChainGun:
          parent: Weapon
        GatlingUpgrade:
          parent: WeaponGiver
          properties:
            "weapongiver.weapon": ChainGun
        """;

    private sealed class Rig
    {
        public readonly GameSession Session = new();
        public readonly InventoryManager Inventory = new(ActorDefs.Metadata(Yaml));
        public readonly MapManager Map;
        public readonly Deathmatch Deathmatch;

        public Rig()
        {
            Map = new(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, Session);
            // Two players, acting as the second: the status bar is only the local player's
            Session.Players.Add(new Entities.PlayerState { Number = 1 });
            foreach (var p in Session.Players)
                p.Pawn = new PlayerPawn { State = p };
            Session.ActingChanged = p => Inventory.SetHolder(p.Items);
            Session.SetGameMode(GameMode.Deathmatch);
            Session.SetActing(Session.Players[1]);

            var bar = new StatusBarDrawing(null!, null, null!, null!, Inventory, null!, null!, null!, Session,
                new Configuration.GameSettings(), new StatusBarHooks(map => map, (text, _, _) => [text]));
            Deathmatch = new Deathmatch(Map, Inventory, bar, Session, new DeathmatchHooks((_, _) => true, () => { }));
        }

        public Entities.PlayerState First => Session.Players[0];
        public Entities.PlayerState Second => Session.Players[1];
    }

    [Test]
    public void Killing_Another_Player_Is_A_Frag()
    {
        var rig = new Rig();
        rig.Deathmatch.CountFrag(rig.Second, rig.First.Pawn);
        Assert.That((rig.First.Frags, rig.Second.Frags), Is.EqualTo((1, 0)));
    }

    [Test]
    public void A_Players_Missile_Counts_For_Them()
    {
        // Arrange
        var rig = new Rig();
        var rocket = new Monster { Name = "Rocket" };
        rocket.Shooter = rig.First.Pawn;

        // Act
        rig.Deathmatch.CountFrag(rig.Second, rocket);

        // Assert
        Assert.That(rig.First.Frags, Is.EqualTo(1));
    }

    [Test]
    public void Killing_Yourself_Takes_One_Away_And_An_Enemy_Kill_Counts_For_Nobody()
    {
        var rig = new Rig();
        rig.Deathmatch.CountFrag(rig.Second, rig.Second.Pawn);
        rig.Deathmatch.CountFrag(rig.Second, new Monster { Name = "Guard" });
        rig.Deathmatch.CountFrag(rig.Second, null);
        Assert.That((rig.First.Frags, rig.Second.Frags), Is.EqualTo((0, -1)));
    }

    [Test]
    public void The_Frag_Limit_Ends_The_Match_Once()
    {
        // Arrange
        var rig = new Rig();
        rig.Deathmatch.NetRules = rig.Deathmatch.NetRules with { ScoreLimit = 2 };

        // Act / Assert: the second frag ends it
        for (int i = 0; i < 2; i++)
            rig.Deathmatch.CountFrag(rig.Second, rig.First.Pawn);
        Assert.That(rig.Deathmatch.MatchOver, Is.True);
        Assert.That(rig.Session.PlayState, Is.EqualTo(PlayState.Abort));

        // and another doesn't end it again
        rig.Session.PlayState = PlayState.StillPlaying;
        rig.Deathmatch.CountFrag(rig.Second, rig.First.Pawn);
        Assert.That(rig.Session.PlayState, Is.EqualTo(PlayState.StillPlaying));
    }

    [Test]
    public void The_Time_Limit_Ends_It_Too()
    {
        // Arrange: a minute
        var rig = new Rig();
        rig.Deathmatch.NetRules = rig.Deathmatch.NetRules with { TimeLimitMinutes = 1 };

        // Act / Assert
        rig.Session.GameState.TimeCount = 60 * 70 - 1;
        rig.Deathmatch.TickDeathmatch();
        Assert.That(rig.Deathmatch.MatchOver, Is.False);
        rig.Session.GameState.TimeCount = 60 * 70;
        rig.Deathmatch.TickDeathmatch();
        Assert.That(rig.Deathmatch.MatchOver, Is.True);
    }

    [Test]
    public void An_Item_Taken_Comes_Back_Where_It_Was_After_A_While()
    {
        // Arrange
        var rig = new Rig();
        rig.Session.GameState.TimeCount = 100;
        var medikit = new CustomInventory { Name = "Medikit" };
        medikit.SetPosition(12, 34);

        // Act
        rig.Deathmatch.QueueItemRespawn(medikit);

        // Assert
        Assert.That(rig.Deathmatch.ItemRespawns, Is.EqualTo(new[] { new ItemRespawn("Medikit", 12, 34, 100 + Deathmatch.ItemRespawnTics) }));
    }

    [Test]
    public void Nothing_Comes_Back_Before_Its_Time()
    {
        // Arrange
        var rig = new Rig();
        rig.Deathmatch.ItemRespawns.Add(new ItemRespawn("Medikit", 1, 1, 500));
        rig.Session.GameState.TimeCount = 499;

        // Act
        rig.Deathmatch.TickDeathmatch();

        // Assert
        Assert.That(rig.Deathmatch.ItemRespawns, Has.Count.EqualTo(1));
    }

    [Test]
    public void A_New_Level_Forgets_What_Was_Coming_Back_And_Leaves_The_Enemies_Out_Unless_Monsters_Are_On()
    {
        // Arrange
        var rig = new Rig();
        rig.Deathmatch.ItemRespawns.Add(new ItemRespawn("Medikit", 1, 1, 500));

        // Act / Assert
        rig.Deathmatch.DeathmatchLevelSetup();
        Assert.That(rig.Deathmatch.ItemRespawns, Is.Empty);
        Assert.That(rig.Map.NoMonsters, Is.True);

        rig.Deathmatch.NetRules = rig.Deathmatch.NetRules with { Monsters = true };
        rig.Deathmatch.DeathmatchLevelSetup();
        Assert.That(rig.Map.NoMonsters, Is.False);
    }

    [Test]
    public void A_Weapon_Already_Held_Or_Given_By_An_Upgrade_Already_Held_Is_Had()
    {
        // Arrange
        var rig = new Rig();
        rig.Inventory.Give("ChainGun", 1);

        // Act / Assert
        Assert.That(rig.Deathmatch.HasWeaponFrom(new Weapon { Name = "ChainGun" }), Is.True);
        var upgrade = new Weapon { Name = "GatlingUpgrade" };
        upgrade.Properties["weapongiver.weapon"] = "ChainGun";
        Assert.That(rig.Deathmatch.HasWeaponFrom(upgrade), Is.True);
        Assert.That(rig.Deathmatch.HasWeaponFrom(new Weapon { Name = "Pistol" }), Is.False);
    }
}

public class ScoreboardTests
{
    private static (Scoreboard Board, GameSession Session) Board()
    {
        var session = new GameSession();
        session.Players.Add(new Entities.PlayerState { Number = 1 });
        session.Players.Add(new Entities.PlayerState { Number = 2 });
        return (new Scoreboard(null!, null!, null!, null!, session, () => { }), session);
    }

    [Test]
    public void In_Deathmatch_Players_Rank_By_Frags_Ties_In_Player_Order()
    {
        // Arrange
        var (board, session) = Board();
        session.SetGameMode(GameMode.Deathmatch);
        (session.Players[0].Frags, session.Players[1].Frags, session.Players[2].Frags) = (3, 5, 5);

        // Act / Assert
        Assert.That(board.RankedPlayers().Select(p => p.Number), Is.EqualTo(new[] { 1, 2, 0 }));
    }

    [Test]
    public void In_Coop_They_Rank_By_Score_And_Those_Gone_Arent_On_It()
    {
        // Arrange
        var (board, session) = Board();
        session.SetGameMode(GameMode.Coop);
        (session.Players[0].Score, session.Players[1].Score, session.Players[2].Score) = (500, 100, 900);
        session.Players[2].Gone = true;

        // Act / Assert
        Assert.That(board.RankedPlayers().Select(p => p.Number), Is.EqualTo(new[] { 0, 1 }));
    }

    [Test]
    public void The_Command_Toggles_It_Or_Sets_It()
    {
        var (board, _) = Board();
        board.ScoreboardCommand([]);
        Assert.That(board.ShowScoreboard, Is.True);
        board.ScoreboardCommand([]);
        Assert.That(board.ShowScoreboard, Is.False);
        board.ScoreboardCommand(["1"]);
        board.ScoreboardCommand(["1"]);
        Assert.That(board.ShowScoreboard, Is.True);
    }
}
