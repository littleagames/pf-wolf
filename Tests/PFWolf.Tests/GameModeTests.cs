using PFWolf.Assets;
using PFWolf.Entities.Actors;
using PFWolf.Enums;
using PFWolf.Gameplay.MatchModes;

namespace PFWolf.Tests;

public class GameModeTests
{
    [Test]
    public void Every_Net_Mode_Reads_Back_From_Its_Id_And_The_Short_Names_Work()
    {
        foreach (var mode in GameModes.NetModes)
            Assert.That(GameModes.Parse(mode.Id()), Is.EqualTo(mode), mode.Id());
        Assert.That(GameModes.Parse("tdm"), Is.EqualTo(GameMode.TeamDeathmatch));
        Assert.That(GameModes.Parse("KOTH"), Is.EqualTo(GameMode.KingOfTheHill));
        Assert.That(GameModes.Parse("dom"), Is.EqualTo(GameMode.Domination));
        Assert.That(GameModes.Parse("ph"), Is.EqualTo(GameMode.PropHunt));
        Assert.That(GameModes.Parse("nonsense"), Is.Null);
    }

    [TestCase((int)GameMode.Coop, false, false, false)]
    [TestCase((int)GameMode.Deathmatch, true, false, true)]
    [TestCase((int)GameMode.TeamDeathmatch, true, true, true)]
    [TestCase((int)GameMode.CaptureTheFlag, true, true, true)]
    [TestCase((int)GameMode.Heist, true, true, false)]
    [TestCase((int)GameMode.Domination, true, true, true)]
    [TestCase((int)GameMode.KingOfTheHill, true, true, true)]
    [TestCase((int)GameMode.Horde, false, false, false)]
    [TestCase((int)GameMode.PropHunt, true, true, true)]
    public void What_Each_Mode_Is(int modeNumber, bool versus, bool teams, bool allKeys)
    {
        var mode = (GameMode)modeNumber;
        Assert.That((mode.IsVersus(), mode.IsTeamGame(), mode.GivesAllKeys()), Is.EqualTo((versus, teams, allKeys)));
    }

    [Test]
    public void Horde_Shares_Keys_And_Keeps_Items_As_A_Deathmatch_Does()
    {
        Assert.That(GameMode.Horde.IsCoopLike(), Is.True);
        Assert.That(GameMode.Horde.KeepsItems(), Is.True);
        Assert.That(GameMode.Coop.KeepsItems(), Is.False);
    }

    [Test]
    public void Teammates_Arent_Rivals_But_Everyone_Is_In_A_Deathmatch()
    {
        // Arrange: red, red, blue
        var session = new GameSession();
        session.Players[0].Team = 0;
        session.Players.Add(new Entities.PlayerState { Number = 1, Team = 0 });
        session.Players.Add(new Entities.PlayerState { Number = 2, Team = 1 });
        var (red1, red2, blue) = (session.Players[0], session.Players[1], session.Players[2]);

        // Act / Assert
        session.SetGameMode(GameMode.TeamDeathmatch);
        Assert.That(session.AreRivals(red1, red2), Is.False);
        Assert.That(session.AreRivals(red1, blue), Is.True);
        Assert.That(session.AreRivals(red1, red1), Is.False);

        session.SetGameMode(GameMode.Deathmatch);
        Assert.That(session.AreRivals(red1, red2), Is.True);

        session.SetGameMode(GameMode.Horde);
        Assert.That(session.AreRivals(red1, blue), Is.False);
    }

    [Test]
    public void A_Joining_Player_Goes_On_The_Team_With_Fewer()
    {
        Assert.That(Deathmatch.SmallerTeam([]), Is.EqualTo(0));
        Assert.That(Deathmatch.SmallerTeam([0]), Is.EqualTo(1));
        Assert.That(Deathmatch.SmallerTeam([0, 1]), Is.EqualTo(0));
        Assert.That(Deathmatch.SmallerTeam([1, 1, 0]), Is.EqualTo(0));
    }

    [Test]
    public void A_Deathmatch_Arena_Is_One_For_Every_Mode_But_Heist_Unless_It_Lists_Its_Own()
    {
        var arena = new MapInfo { Deathmatch = true };
        Assert.That(GameModes.NetModes.Where(m => arena.IsArenaFor(m.Id())),
            Is.EquivalentTo(new[] { GameMode.Deathmatch, GameMode.TeamDeathmatch, GameMode.CaptureTheFlag, GameMode.Domination, GameMode.KingOfTheHill, GameMode.Horde, GameMode.PropHunt }));

        var heist = new MapInfo { Modes = ["heist", "team-deathmatch"] };
        Assert.That(heist.IsArenaFor("heist"), Is.True);
        Assert.That(heist.IsArenaFor("deathmatch"), Is.False);
        Assert.That(new MapInfo().IsArenaFor("deathmatch"), Is.False);
    }
}

public class MatchScoringTests
{
    // Two red players and a blue one, playing the mode; the second red acting
    private sealed class Rig
    {
        public readonly GameSession Session = new();
        public readonly InventoryManager Inventory = new(ActorDefs.Metadata("""
            Inventory:
              properties:
                "inventory.amount": 1
            """));
        public readonly MapManager Map;
        public readonly Deathmatch Match;
        public readonly HudMessageManager Hud;

        public Rig(GameMode mode)
        {
            Map = new(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, Session, null);
            Session.Players[0].Team = 0;
            Session.Players.Add(new Entities.PlayerState { Number = 1, Team = 0 });
            Session.Players.Add(new Entities.PlayerState { Number = 2, Team = 1 });
            foreach (var p in Session.Players)
                p.Pawn = new PlayerPawn { State = p };
            Session.ActingChanged = p => Inventory.SetHolder(p.Items);
            Session.SetGameMode(mode);
            Session.SetActing(Session.Players[1]);

            var bar = new StatusBarDrawing(null!, null, null!, null!, Inventory, null!, null!, null!, Session,
                new Configuration.GameSettings(), new StatusBarHooks(map => map, (text, _, _) => [text]));
            Match = new Deathmatch(Map, Inventory, bar, Session, new DeathmatchHooks((_, _) => true, () => { }));
            Match.NetRules = Match.NetRules with { ScoreLimit = mode.DefaultScoreLimit() };
            var console = new ConsoleManager();
            Hud = new HudMessageManager(new Lazy<AssetManager>(() => new AssetManager()), new Lazy<ConsoleManager>(console));
        }

        public MatchModeHooks Hooks => new((_, _) => true, (_, _) => true, name => name, _ => { });

        public Entities.PlayerState Red1 => Session.Players[0];
        public Entities.PlayerState Red2 => Session.Players[1];
        public Entities.PlayerState Blue => Session.Players[2];
    }

    [Test]
    public void In_Team_Deathmatch_A_Frag_Counts_For_The_Team_And_The_Limit_Ends_It()
    {
        // Arrange
        var rig = new Rig(GameMode.TeamDeathmatch);
        rig.Match.NetRules = rig.Match.NetRules with { ScoreLimit = 2 };

        // Act / Assert
        rig.Match.PlayerKilled(rig.Blue, rig.Red1.Pawn);
        rig.Match.PlayerKilled(rig.Red2, rig.Blue.Pawn);
        Assert.That(rig.Match.TeamScores, Is.EqualTo(new[] { 1, 1 }));
        Assert.That(rig.Match.MatchOver, Is.False);

        rig.Match.PlayerKilled(rig.Blue, rig.Red2.Pawn);
        Assert.That(rig.Match.TeamScores, Is.EqualTo(new[] { 2, 1 }));
        Assert.That((rig.Red1.Frags, rig.Red2.Frags, rig.Blue.Frags), Is.EqualTo((1, 1, 1)));
        Assert.That(rig.Match.MatchOver, Is.True);
        Assert.That(rig.Match.LeadingTeam, Is.EqualTo(0));
    }

    [Test]
    public void A_Suicide_In_Team_Deathmatch_Costs_The_Team_A_Point()
    {
        var rig = new Rig(GameMode.TeamDeathmatch);
        rig.Match.PlayerKilled(rig.Blue, rig.Blue.Pawn);
        Assert.That((rig.Match.TeamScores[1], rig.Blue.Frags), Is.EqualTo((-1, -1)));
    }

    [Test]
    public void Heist_Is_A_Tug_Of_War_Won_By_A_Lead_Not_A_Total()
    {
        // Arrange: a lead of 10 wins
        var rig = new Rig(GameMode.Heist);
        rig.Match.NetRules = rig.Match.NetRules with { ScoreLimit = 10 };

        // Act / Assert: both past 10, nobody's won
        rig.Match.AddTeamScore(0, 8);
        rig.Match.AddTeamScore(1, 12);
        rig.Match.AddTeamScore(0, 8);
        Assert.That(rig.Match.MatchOver, Is.False);

        rig.Match.AddTeamScore(0, 6);       // 22 to 12
        Assert.That(rig.Match.MatchOver, Is.True);
        Assert.That(rig.Match.LeadingTeam, Is.EqualTo(0));
    }

    [Test]
    public void Treasure_Is_Worth_A_Hundredth_Of_Its_Score_Unless_It_Says()
    {
        ScoreItem Treasure(int points)
        {
            var item = new ScoreItem { Name = "Treasure" };
            item.Properties["inventory.amount"] = points;
            return item;
        }
        var crown = Treasure(5000);
        var priced = Treasure(100);
        priced.Properties["heist.value"] = "7";

        Assert.That(Heist.LootValue(Treasure(100)), Is.EqualTo(1));
        Assert.That(Heist.LootValue(Treasure(1000)), Is.EqualTo(10));
        Assert.That(Heist.LootValue(crown), Is.EqualTo(50));
        Assert.That(Heist.LootValue(Treasure(10)), Is.EqualTo(1), "never worth nothing");
        Assert.That(Heist.LootValue(priced), Is.EqualTo(7));
        Assert.That(Heist.LootValue(new CustomInventory { Name = "Medikit" }), Is.Zero);
    }

    [Test]
    public void Loot_Taken_Pulls_For_The_Takers_Team()
    {
        // Arrange: the second red player picks up a chest
        var rig = new Rig(GameMode.Heist);
        var heist = new Heist(rig.Map, rig.Session, rig.Match, rig.Hud, rig.Hooks);
        var chest = new ScoreItem { Name = "ChestOfJewels" };
        chest.Properties["inventory.amount"] = 1000;

        // Act
        ((IMatchObjective)heist).ItemTaken(chest);

        // Assert
        Assert.That(rig.Match.TeamScores, Is.EqualTo(new[] { 10, 0 }));
        Assert.That(rig.Red2.Objective, Is.EqualTo(10));
        Assert.That(((IMatchObjective)heist).StatusLine(), Is.EqualTo("Red leads by 10"));
    }

    [Test]
    public void The_Status_Bar_Shows_The_Teams_Score_In_A_Team_Game()
    {
        var rig = new Rig(GameMode.CaptureTheFlag);
        rig.Match.AddTeamScore(1, 2);
        rig.Red1.Score = 1234;
        Assert.That((rig.Match.ScoreShown(rig.Blue), rig.Match.ScoreShown(rig.Red1)), Is.EqualTo((2, 0)));
    }

    [Test]
    public void A_New_Match_Starts_From_Nothing()
    {
        var rig = new Rig(GameMode.Domination);
        rig.Match.AddTeamScore(0, 5);
        rig.Red1.Frags = rig.Blue.Objective = 3;
        rig.Match.MatchOver = true;

        rig.Match.NewMatch();

        Assert.That(rig.Match.TeamScores, Is.EqualTo(new[] { 0, 0 }));
        Assert.That((rig.Red1.Frags, rig.Blue.Objective, rig.Match.MatchOver), Is.EqualTo((0, 0, false)));
    }

    // A Horde with these waves
    private static Horde HordeOf(Rig rig, HordeInfo info) => new(rig.Map, rig.Session, rig.Match, rig.Hud, rig.Hooks, () => info);

    [Test]
    public void Horde_Waves_Past_The_List_Grow_And_More_Players_Bring_More()
    {
        // Arrange
        var rig = new Rig(GameMode.Horde);
        var horde = HordeOf(rig, new HordeInfo
        {
            Waves = [new HordeWave { Enemies = new() { ["Mutant"] = 4 } }, new HordeWave { Enemies = new() { ["Mutant"] = 6, ["Dog"] = 2 } }],
            Grow = 3,
        });

        // Act / Assert
        Assert.That(horde.WaveEnemies(1, 1), Is.EqualTo(new[] { ("Mutant", 4) }));
        Assert.That(horde.WaveEnemies(2, 1), Is.EqualTo(new[] { ("Mutant", 6), ("Dog", 2) }));
        Assert.That(horde.WaveEnemies(4, 1), Is.EqualTo(new[] { ("Mutant", 12), ("Dog", 8) }), "two past the list: 6 more of each");
        Assert.That(horde.WaveEnemies(1, 3), Is.EqualTo(new[] { ("Mutant", 8) }), "half as many again for each player past the first");
    }

    [Test]
    public void In_Horde_The_Dead_Come_Back_Only_Between_Waves_And_Everyone_Dead_Ends_It()
    {
        // Arrange: a wave being fought
        var rig = new Rig(GameMode.Horde);
        var horde = HordeOf(rig, new HordeInfo());
        horde.CurrentPhase = Horde.Phase.Wave;
        horde.Survived = 3;
        IMatchObjective objective = horde;

        // Act / Assert
        Assert.That(objective.MayRespawn(rig.Red1), Is.False);
        rig.Red1.Health = rig.Red2.Health = 0;
        rig.Blue.Health = 50;
        objective.PlayerDied(rig.Red2);
        Assert.That(rig.Match.MatchOver, Is.False, "one still standing");

        rig.Blue.Health = 0;
        objective.PlayerDied(rig.Blue);
        Assert.That(rig.Match.MatchOver, Is.True);
        Assert.That(objective.FinalTitle(), Is.EqualTo("Survived 3 waves"));

        horde.CurrentPhase = Horde.Phase.Break;
        Assert.That(objective.MayRespawn(rig.Red1), Is.True);
    }

    [Test]
    public void Prop_Hunt_Props_Are_The_Team_Not_Hunting_And_Come_In_Disguised_With_A_Props_Loadout()
    {
        // Arrange: red hunts, blue hides; everyone alive
        var rig = new Rig(GameMode.PropHunt);
        var rearmed = new List<Entities.PlayerState>();
        var hooks = rig.Hooks with { Rearm = rearmed.Add };
        var hunt = new PropHunt(rig.Map, rig.Session, rig.Match, rig.Hud, hooks);
        hunt.LevelSetup();
        hunt.Disguises.AddRange(["FloorLamp", "Plant"]);
        foreach (var p in rig.Session.Players)
            p.Health = 100;

        // Act
        hunt.Tick();

        // Assert
        Assert.That((hunt.IsProp(rig.Red1), hunt.IsProp(rig.Blue)), Is.EqualTo((false, true)));
        Assert.That(rig.Blue.Disguise, Is.AnyOf("FloorLamp", "Plant"));
        Assert.That(rig.Blue.RoleClass, Is.EqualTo(PropHunt.PropClass));
        Assert.That((rig.Red1.Disguise, rig.Red1.RoleClass), Is.EqualTo(((string?)null, (string?)null)));
        Assert.That(rearmed, Is.EqualTo(new[] { rig.Blue }), "only a new prop gets a new loadout");

        hunt.Tick();
        Assert.That(rearmed, Has.Count.EqualTo(1), "once");
    }

    // A Prop Hunt round on: everyone alive and on the level; the players brought back in a new round noted
    private static PropHunt PropHuntOf(Rig rig, List<Entities.PlayerState> respawned)
    {
        var hunt = new PropHunt(rig.Map, rig.Session, rig.Match, rig.Hud, rig.Hooks with { Respawn = respawned.Add });
        foreach (var p in rig.Session.Players)
        {
            p.Health = 100;
            p.Pawn!.Active = ActiveType.Yes;
        }
        hunt.LevelSetup();
        return hunt;
    }

    [Test]
    public void A_Prop_Hunt_Round_Hides_Then_Hunts_And_Finding_Every_Prop_Wins_It_For_The_Hunters()
    {
        // Arrange
        var rig = new Rig(GameMode.PropHunt);
        var respawned = new List<Entities.PlayerState>();
        var hunt = PropHuntOf(rig, respawned);
        IMatchObjective objective = hunt;
        var clock = rig.Session.GameState;

        // Act / Assert: the hunters (red) wait while the props hide
        Assert.That(hunt.CurrentPhase, Is.EqualTo(PropHunt.Phase.Hide));
        Assert.That((objective.IsWaiting(rig.Red1), objective.IsWaiting(rig.Blue)), Is.EqualTo((true, false)));

        clock.TimeCount += hunt.HideTics;
        hunt.Tick();
        Assert.That(hunt.CurrentPhase, Is.EqualTo(PropHunt.Phase.Hunt));
        Assert.That(objective.IsWaiting(rig.Red1), Is.False);

        // the last prop's found
        rig.Blue.Health = 0;
        objective.PlayerDied(rig.Blue);
        hunt.Tick();
        Assert.That((hunt.CurrentPhase, hunt.RoundWinner), Is.EqualTo((PropHunt.Phase.RoundOver, 0)));
        Assert.That(rig.Match.TeamScores, Is.EqualTo(new[] { 1, 0 }));
        Assert.That((rig.Red1.Objective, rig.Red2.Objective, rig.Blue.Objective), Is.EqualTo((1, 1, 0)), "a round won each");
        Assert.That(objective.MayRespawn(rig.Blue), Is.False, "back next round, not before");

        // the next round: sides swap, everyone comes back in their new part
        clock.TimeCount += PropHunt.RoundOverTics;
        hunt.Tick();
        Assert.That((hunt.Round, hunt.HuntingTeam, hunt.CurrentPhase), Is.EqualTo((2, 1, PropHunt.Phase.Hide)));
        Assert.That(respawned, Is.EquivalentTo(rig.Session.Players));
        Assert.That((rig.Red1.RoleClass, rig.Blue.RoleClass), Is.EqualTo(((string?)PropHunt.PropClass, (string?)null)));
        Assert.That((objective.IsWaiting(rig.Red1), objective.IsWaiting(rig.Blue)), Is.EqualTo((false, true)));
    }

    [Test]
    public void Props_Still_Hidden_When_The_Hunt_Runs_Out_Win_The_Round()
    {
        var rig = new Rig(GameMode.PropHunt);
        var hunt = PropHuntOf(rig, []);
        rig.Session.GameState.TimeCount += hunt.HideTics;
        hunt.Tick();

        rig.Session.GameState.TimeCount += hunt.HuntTics - 1;
        hunt.Tick();
        Assert.That(hunt.CurrentPhase, Is.EqualTo(PropHunt.Phase.Hunt), "a tic to go");

        rig.Session.GameState.TimeCount++;
        hunt.Tick();
        Assert.That((hunt.CurrentPhase, hunt.RoundWinner), Is.EqualTo((PropHunt.Phase.RoundOver, 1)));
        Assert.That(rig.Match.TeamScores, Is.EqualTo(new[] { 0, 1 }));
    }

    [Test]
    public void The_Score_Limits_Rounds_Win_The_Prop_Hunt()
    {
        var rig = new Rig(GameMode.PropHunt);
        rig.Match.NetRules = rig.Match.NetRules with { ScoreLimit = 1 };
        var hunt = PropHuntOf(rig, []);
        rig.Session.GameState.TimeCount += hunt.HideTics;
        hunt.Tick();    // the hunt starts
        rig.Session.GameState.TimeCount += hunt.HuntTics;
        hunt.Tick();    // and runs out

        Assert.That(rig.Match.MatchOver, Is.True);
        Assert.That(rig.Match.LeadingTeam, Is.EqualTo(1));
    }

    [Test]
    public void A_Props_Health_Goes_By_The_Size_Of_What_It_Looks_Like()
    {
        // Arrange
        var metadata = ActorDefs.Metadata("""
            Lamp: {}
            Barrel:
              radius: 32
            Vase:
              properties:
                "prophunt.health": 30
            """);
        Entities.Actors.Actor Make(string name) => metadata.CreateActor(name, metadata.Actors[name]);
        var rig = new Rig(GameMode.PropHunt);
        var shown = new List<Entities.PlayerState>();
        var hunt = new PropHunt(rig.Map, rig.Session, rig.Match, rig.Hud, rig.Hooks with { ShowHealth = shown.Add });
        foreach (var name in new[] { "Lamp", "Barrel", "Vase" })
            hunt.DisguiseHealth[name] = PropHunt.HealthAs(Make(name));

        // Act / Assert
        Assert.That(hunt.DisguiseHealth, Is.EqualTo(new Dictionary<string, int> { ["Lamp"] = 50, ["Barrel"] = 100, ["Vase"] = 30 }));

        rig.Blue.Health = 80;
        hunt.Disguise(rig.Blue, "Lamp");
        Assert.That(rig.Blue.Health, Is.EqualTo(40), "the same share of the most");
        hunt.Disguise(rig.Blue, "Barrel");
        Assert.That(rig.Blue.Health, Is.EqualTo(80));
        Assert.That(shown, Has.Count.EqualTo(2));
    }

    [Test]
    public void Only_A_Hunters_Shot_In_The_Hunt_Can_Hit_A_Real_Decoration()
    {
        // Arrange: a lamp a prop could look like, and red hunting
        var metadata = ActorDefs.Metadata("""
            Lamp:
              states:
                Spawn:
                  - sprite: CLMP
                    frames: [A]
                    tics-per-frame: -1
            """);
        var lamp = metadata.CreateActor("Lamp", metadata.Actors["Lamp"]);
        var rig = new Rig(GameMode.PropHunt);
        var hunt = PropHuntOf(rig, []);
        hunt.DisguiseHealth["Lamp"] = 50;
        IMatchObjective objective = hunt;

        // Act / Assert: hiding, nobody shoots
        rig.Session.SetActing(rig.Red1);
        Assert.That(objective.IsDecoy(lamp), Is.False, "not while the props hide");

        rig.Session.GameState.TimeCount += hunt.HideTics;
        hunt.Tick();
        Assert.That(objective.IsDecoy(lamp), Is.True);

        rig.Session.SetActing(rig.Blue);
        Assert.That(objective.IsDecoy(lamp), Is.False, "a prop's shot");
    }

    [Test]
    public void A_Prop_Who_Dies_Drops_Their_Disguise()
    {
        var rig = new Rig(GameMode.PropHunt);
        var hunt = new PropHunt(rig.Map, rig.Session, rig.Match, rig.Hud, rig.Hooks);
        rig.Blue.Disguise = "FloorLamp";

        ((IMatchObjective)hunt).PlayerDied(rig.Blue);
        rig.Blue.Health = 0;
        hunt.Tick();

        Assert.That(rig.Blue.Disguise, Is.Null);
    }

    [Test]
    public void Prop_Hunts_State_Goes_To_A_Player_Joining()
    {
        var rig = new Rig(GameMode.PropHunt);
        var hunt = new PropHunt(rig.Map, rig.Session, rig.Match, rig.Hud, rig.Hooks)
        {
            HuntingTeam = 1, Round = 3, CurrentPhase = PropHunt.Phase.Hunt, PhaseEnds = 1234, RoundWinner = 0,
        };
        hunt.Disguises.AddRange(["Barrel", "Table"]);
        using var stream = new MemoryStream();
        hunt.Write(new BinaryWriter(stream));
        stream.Position = 0;

        var joined = new PropHunt(rig.Map, rig.Session, rig.Match, rig.Hud, rig.Hooks);
        joined.Read(new BinaryReader(stream));

        Assert.That((joined.HuntingTeam, joined.Round, joined.CurrentPhase, joined.PhaseEnds, joined.RoundWinner),
            Is.EqualTo((1, 3, PropHunt.Phase.Hunt, 1234, 0)));
        Assert.That(joined.Disguises, Is.EqualTo(new[] { "Barrel", "Table" }));
    }

    [Test]
    public void Props_Can_Look_Like_Plain_Scenery_Only()
    {
        // Arrange
        var metadata = ActorDefs.Metadata("""
            Inventory: {}
            Health:
              parent: Inventory
            Monster: {}
            FloorLamp:
              states:
                Spawn:
                  - sprite: CLMP
                    frames: [A]
                    tics-per-frame: -1
            Puddle:
              parent: FloorLamp
              flags: [NODISGUISE]
            Marker:
              states:
                Spawn:
                  - sprite: TNT1
                    frames: [A]
                    tics-per-frame: -1
            Medikit:
              parent: Health
              states:
                Spawn:
                  - sprite: MEDI
                    frames: [A]
                    tics-per-frame: -1
            Guard:
              parent: Monster
              states:
                Spawn:
                  - sprite: GARD
                    frames: [A]
                    tics-per-frame: -1
            """);
        bool Can(string name) => PropHunt.CanDisguiseAs(metadata.CreateActor(name, metadata.Actors[name]));

        // Act / Assert
        Assert.That(Can("FloorLamp"), Is.True);
        Assert.That(Can("Puddle"), Is.False, "flagged");
        Assert.That(Can("Marker"), Is.False, "nothing to see");
        Assert.That(Can("Medikit"), Is.False, "an item");
        Assert.That(Can("Guard"), Is.False, "an enemy");
        Assert.That(PropHunt.LookName("TableWithChairs2"), Is.EqualTo("Table With Chairs2"));
    }
}

public class TeamScoreboardTests
{
    [Test]
    public void In_A_Team_Game_Players_Rank_By_Team_Then_What_They_Did_And_The_Leading_Team_Wins()
    {
        // Arrange: blue, red, red, with captures
        var session = new GameSession();
        session.Players[0].Team = 1;
        session.Players.Add(new Entities.PlayerState { Number = 1, Team = 0, Objective = 1 });
        session.Players.Add(new Entities.PlayerState { Number = 2, Team = 0, Objective = 2 });
        session.SetGameMode(GameMode.CaptureTheFlag);
        var match = new Deathmatch(null!, null!, null!, session, null!);
        var board = new Scoreboard(null!, null!, null!, null!, session, () => { }) { Match = match };
        match.TeamScores[1] = 3;

        // Act / Assert
        Assert.That(board.RankedPlayers().Select(p => p.Number), Is.EqualTo(new[] { 2, 1, 0 }));
        Assert.That(board.WinnerTitle(), Is.EqualTo("Blue team wins!"));
        match.TeamScores[0] = 3;
        Assert.That(board.WinnerTitle(), Is.EqualTo("It's a draw!"));
    }
}
