using PFWolf.Entities.Actors;
using PFWolf.Enums;

namespace PFWolf.Tests;

public class PlayersTests
{
    private const string Yaml = """
        Inventory:
          properties:
            "inventory.amount": 1
            "inventory.maxamount": 1
        Player:
          properties:
            "player.maxhealth": 100
            "deathsound": "player/death"
        """;

    private sealed class Rig
    {
        public readonly GameSession Session = new();
        public readonly InventoryManager Inventory = new(ActorDefs.Metadata(Yaml));
        public readonly Players Players;
        public readonly List<string> SoundsAtPawns = [];
        public readonly List<(Entities.PlayerState Victim, Entities.Actors.Actor? Killer)> Frags = [];
        public int Obituaries;
        public bool NetGameCleared => !Session.netgame;
        public int? DropTo;

        public Rig()
        {
            // Two players in a network game, acting as the second: no flash or status bar, which only the local player has
            Session.netgame = true;
            Session.players.Add(new Entities.PlayerState { Number = 1 });
            Session.ActingChanged = p => Inventory.SetHolder(p.Items);
            Session.SetGameMode(GameMode.Coop);
            foreach (var p in Session.players)
            {
                p.playerclass = "Player";
                p.health = 100;
                p.Pawn = new PlayerPawn { State = p };
            }
            Session.SetActing(Session.players[1]);

            var bar = new StatusBarDrawing(null!, null, null!, null!, Inventory, null!, null!, null!, Session,
                new Configuration.GameSettings(), new StatusBarHooks(map => map, (text, _, _) => [text]));
            var classes = new PlayerClassRules(null!, Inventory, Session);
            var weapons = new PlayerWeapons(Inventory, null!, bar, Session, new PlayerWeaponsHooks(_ => { }));
            var stats = new PlayerStats(null!, null!, null!, Inventory, bar, classes, weapons, Session,
                new Configuration.GameSettings(), new PlayerStatsHooks(() => 1, () => Players!.PlayerDies(), _ => { }));
            Players = new Players(null!, null!, null!, Inventory, null!, bar, classes, stats, weapons, null!, null!, Session,
                new PlayersHooks((sound, _) => SoundsAtPawns.Add(sound), _ => (0, 0, 0), () => { }, (victim, killer) => Frags.Add((victim, killer)), _ => true,
                    () => Obituaries++, () => { }, () => 5, () => DropTo, () => 4, color => color));
        }

        public Entities.PlayerState Second => Session.players[1];
    }

    private static readonly Monster Guard = new() { Name = "Guard", X = 0, Y = 0 };

    [Test]
    public void A_New_Game_Has_Just_The_Local_Player_Alone()
    {
        // Arrange
        var rig = new Rig();

        // Act
        rig.Players.ResetPlayers();

        // Assert
        Assert.That(rig.Session.players, Has.Count.EqualTo(1));
        Assert.That(rig.Session.playerstate, Is.SameAs(rig.Session.localplayer));
        Assert.That(rig.Session.gamemode, Is.EqualTo(GameMode.Single));
        Assert.That(rig.NetGameCleared, Is.True);
    }

    [Test]
    public void Hurting_A_Player_Hurts_Them_Whoever_Is_Acting()
    {
        // Arrange
        var rig = new Rig();
        using var _ = rig.Session.ActAs(rig.Session.players[0]);

        // Act
        rig.Players.TakeDamage(rig.Second.Pawn!, 30, Guard);

        // Assert
        Assert.That(rig.Second.health, Is.EqualTo(70));
        Assert.That(rig.Session.players[0].health, Is.EqualTo(100));
        Assert.That(rig.Session.playerstate, Is.SameAs(rig.Session.players[0]));
    }

    [Test]
    public void Another_Players_Sound_Comes_From_Where_They_Stand()
    {
        var rig = new Rig();
        rig.Players.PlayPlayerSound("misc/pickup");
        Assert.That(rig.SoundsAtPawns, Is.EqualTo(new[] { "misc/pickup" }));
    }

    [Test]
    public void Killed_With_Others_Playing_They_Lose_Their_Weapon_And_Lie_There()
    {
        // Arrange
        var rig = new Rig();
        rig.Second.weapon = "Pistol";

        // Act
        rig.Players.TakeDamage(rig.Second.Pawn!, 500, Guard);

        // Assert
        Assert.That(rig.Second.health, Is.Zero);
        Assert.That(rig.Second.weapon, Is.Null);
        Assert.That(rig.Second.Pawn!.CurrentState!.StateName, Is.EqualTo(PlayerPawn.DeadState));
        Assert.That(rig.SoundsAtPawns, Is.EqualTo(new[] { "player/death" }));
        Assert.That(rig.Obituaries, Is.EqualTo(1));
        Assert.That(rig.Frags, Is.Empty);           // co-op
    }

    [Test]
    public void In_Deathmatch_A_Death_Is_A_Frag()
    {
        // Arrange
        var rig = new Rig();
        rig.Session.SetGameMode(GameMode.Deathmatch);

        // Act
        rig.Players.TakeDamage(rig.Second.Pawn!, 500, Guard);

        // Assert
        Assert.That(rig.Frags, Is.EqualTo(new[] { (rig.Second, (Entities.Actors.Actor?)Guard) }));
    }

    [TestCase(0, 0, 10, 0, 0)]          // killer due east
    [TestCase(0, 0, 0, -10, 90)]        // due north: y grows southward
    [TestCase(0, 0, -10, 0, 180)]
    public void A_Dead_Player_Turns_Toward_Their_Killer(int fromX, int fromY, int toX, int toY, int angle)
    {
        var from = new PlayerPawn { X = fromX, Y = fromY };
        var to = new Monster { Name = "Guard", X = toX, Y = toY };
        Assert.That(Players.FacingAngle(from, to), Is.EqualTo(angle));
    }

    [Test]
    public void Killed_By_Nothing_They_Turn_Round()
    {
        Assert.That(Players.FacingAngle(new PlayerPawn { Angle = 90 }, null), Is.EqualTo(270));
    }

    [Test]
    public void Lying_Dead_They_Turn_The_Shorter_Way_And_Their_View_Sinks()
    {
        // Arrange: facing east, the killer north-ish (turn left), 5 degrees a tic
        var rig = new Rig { DropTo = 10 };
        var pawn = rig.Second.Pawn!;
        pawn.Angle = 0;
        rig.Second.DeathAngle = 80;
        rig.Second.EyeZ = 32;
        rig.Session.tics = 2;

        // Act
        rig.Players.T_PlayerDead(pawn);

        // Assert
        Assert.That(pawn.Angle, Is.EqualTo(10));
        Assert.That(rig.Second.EyeZ, Is.EqualTo(24));       // 4 a tic
        Assert.That(rig.Second.DeadTics, Is.EqualTo(2));

        // the other way round
        rig.Second.DeathAngle = 350;
        pawn.Angle = 0;
        rig.Players.T_PlayerDead(pawn);
        Assert.That(pawn.Angle, Is.EqualTo(350));
    }

    [Test]
    public void Without_A_Death_Drop_Height_The_View_Stays()
    {
        var rig = new Rig();
        rig.Second.EyeZ = 32;
        rig.Session.tics = 2;
        rig.Players.T_PlayerDead(rig.Second.Pawn!);
        Assert.That(rig.Second.EyeZ, Is.EqualTo(32));
    }

    [Test]
    public void Pressing_Fire_Too_Soon_Doesnt_Bring_Them_Back()
    {
        // Arrange
        var rig = new Rig();
        rig.Second.health = 0;
        rig.Session.tics = 1;
        rig.Session.playerinput.SetPressed(ButtonType.Attack, true);

        // Act: Respawn would need the map, which isn't there
        Assert.DoesNotThrow(() => rig.Players.T_PlayerDead(rig.Second.Pawn!));

        // Assert
        Assert.That(rig.Second.DeadTics, Is.LessThan(Players.RESPAWNTICS));
        Assert.That(rig.Second.health, Is.Zero);
    }

    [Test]
    public void Messages_Name_Players_Only_With_Others_Playing()
    {
        // Arrange
        var rig = new Rig();
        rig.Session.players[0].Name = "Drew";

        // Act / Assert
        Assert.That(rig.Players.PlayerName(rig.Session.players[0].Pawn!), Is.EqualTo("Drew"));
        Assert.That(rig.Players.PlayerName(rig.Second.Pawn!), Is.EqualTo("Player 2"));
        rig.Session.SetGameMode(GameMode.Single);
        Assert.That(rig.Players.PlayerName(rig.Second.Pawn!), Is.Null);
    }
}
