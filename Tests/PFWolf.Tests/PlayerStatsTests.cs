using PFWolf.Entities.Actors;
using PFWolf.Enums;

namespace PFWolf.Tests;

public class PlayerStatsTests
{
    private const string Yaml = """
        Inventory:
          properties:
            "inventory.amount": 1
            "inventory.maxamount": 1
        Ammo:
          parent: Inventory
        Clip:
          parent: Ammo
          properties:
            "inventory.maxamount": 99
        Weapon:
          parent: Inventory
        Knife:
          parent: Weapon
          properties:
            "weapon.selectionorder": 3700
        Pistol:
          parent: Weapon
          properties:
            "weapon.selectionorder": 1900
            "weapon.ammotype1": Clip
            "weapon.ammouse1": 1
        Key:
          parent: Inventory
        GoldKey:
          parent: Key
        Health:
          parent: Inventory
          properties:
            "inventory.maxamount": 0
        Medikit:
          parent: Health
        Blood:
          parent: Health
          properties:
            "health.takenbelow": 10
        BasicArmor:
          parent: Inventory
        GreenArmor:
          parent: BasicArmor
          properties:
            "armor.saveamount": 100
            "armor.savepercent": 33
        BlueArmor:
          parent: BasicArmor
          properties:
            "armor.saveamount": 200
            "armor.savepercent": 50
        ArmorBonus:
          parent: BasicArmor
          properties:
            "armor.saveamount": 1
            "armor.savepercent": 33
            "armor.maxsaveamount": 200
        Player:
          properties:
            "player.maxhealth": 100
            "player.maxlives": 4
            "player.maxarmor": 150
            "player.startitem":
              Knife: 1
              Pistol: 1
              Clip: 8
              GreenArmor: 1
        Fragile:
          parent: Player
          properties:
            "player.damagetaken": 2
        """;

    private sealed class Rig
    {
        public readonly GameSession Session = new();
        public readonly InventoryManager Inventory = new(ActorDefs.Metadata(Yaml));
        public readonly PlayerStats Stats;
        public int GodMode { get => Session.GodMode; set => Session.GodMode = (byte)value; }
        public bool Demo { get => Session.DemoPlayback; set => Session.DemoPlayback = value; }
        public double Skill = 1;
        public int NetDeaths;
        public Func<int, Entities.Actors.Actor?, string?, int>? DamageHook;
        public readonly List<Entities.Actors.Actor?> Killers = [];
        public int Deaths => Session.PlayState == PlayState.Died ? 1 : 0;

        public Rig(string playerClass = "Player")
        {
            // Acting as a second player: no damage flash or status bar, which only the local player has
            Session.Players.Add(new Entities.PlayerState());
            Session.ActingChanged = p => Inventory.SetHolder(p.Items);
            Session.SetActing(Session.Players[1]);
            Player.PlayerClass = playerClass;
            Player.Health = 100;
            Player.Lives = 3;

            var bar = new StatusBarDrawing(null!, null, null!, null!, Inventory, null!, null!, null!, Session,
                new Configuration.GameSettings(), new StatusBarHooks(map => map, (text, _, _) => [text]));
            var classes = new PlayerClassRules(null!, Inventory, Session);
            var weapons = new PlayerWeapons(Inventory, null!, bar, Session, new PlayerWeaponsHooks(_ => { }));
            Stats = new PlayerStats(null!, null!, null!, Inventory, bar, classes, weapons, Session,
                new Configuration.GameSettings(), new PlayerStatsHooks(() => Skill, () => NetDeaths++, _ => { },
                    Damage: (points, attacker, type) => DamageHook?.Invoke(points, attacker, type) ?? points,
                    Died: Killers.Add));
        }

        public Entities.PlayerState Player => Session.PlayerState;
    }

    private static readonly Monster Guard = new() { Name = "Guard" };

    [Test]
    public void Damage_Comes_Off_Health_And_The_Attacker_Is_Kept()
    {
        // Arrange
        var rig = new Rig();

        // Act
        rig.Stats.TakeDamage(30, Guard);

        // Assert
        Assert.That(rig.Player.Health, Is.EqualTo(70));
        Assert.That(rig.Player.LastAttacker, Is.SameAs(Guard));
        Assert.That(rig.Player.PainTics, Is.EqualTo(10));
    }

    [Test]
    public void A_Damage_Hook_Gets_The_Scaled_Amount_And_Changes_It()
    {
        // Arrange: the easiest skill's quarter, and a script that doubles what it's given
        var rig = new Rig { Skill = 0.25 };
        var seen = new List<(int, Entities.Actors.Actor?)>();
        rig.DamageHook = (points, attacker, _) => { seen.Add((points, attacker)); return points * 2; };

        // Act
        rig.Stats.TakeDamage(40, Guard);

        // Assert
        Assert.That(seen, Is.EqualTo(new[] { (10, (Entities.Actors.Actor?)Guard) }));
        Assert.That(rig.Player.Health, Is.EqualTo(80));
    }

    [Test]
    public void A_Damage_Hook_Of_Nothing_Leaves_The_Player_Unhurt()
    {
        // Arrange
        var rig = new Rig { DamageHook = (_, _, _) => 0 };

        // Act
        rig.Stats.TakeDamage(200, Guard);

        // Assert
        Assert.That(rig.Player.Health, Is.EqualTo(100));
        Assert.That(rig.Player.PainTics, Is.Zero);
        Assert.That(rig.Killers, Is.Empty);
    }

    [Test]
    public void A_Death_Is_Told_Once_With_The_Killer()
    {
        // Arrange: with others playing, so the dead player lies there and isn't hurt again
        var rig = new Rig();
        rig.Session.SetGameMode(GameMode.Coop);

        // Act
        rig.Stats.TakeDamage(150, Guard);
        rig.Stats.TakeDamage(10, Guard);

        // Assert
        Assert.That(rig.Killers, Is.EqualTo(new Entities.Actors.Actor?[] { Guard }));
        Assert.That(rig.NetDeaths, Is.EqualTo(1));
    }

    [Test]
    public void Killed_Outright_Is_A_Death_With_The_Killer_Told()
    {
        // Arrange
        var rig = new Rig();

        // Act: time's up
        rig.Stats.Kill(null);

        // Assert
        Assert.That(rig.Player.Health, Is.Zero);
        Assert.That(rig.Deaths, Is.EqualTo(1));
        Assert.That(rig.Killers, Is.EqualTo(new Entities.Actors.Actor?[] { null }));
    }

    [Test]
    public void The_Skill_And_The_Class_Scale_The_Damage()
    {
        // Arrange: the easiest skill's quarter, a class that takes double
        var rig = new Rig("Fragile") { Skill = 0.25 };

        // Act
        rig.Stats.TakeDamage(40, Guard);

        // Assert
        Assert.That(rig.Player.Health, Is.EqualTo(80));
    }

    [Test]
    public void Armor_Takes_Its_Share_As_Far_As_Its_Points_Go()
    {
        // Arrange: 50% armor with 10 points left
        var rig = new Rig();
        (rig.Player.Armor, rig.Player.ArmorPercent) = (10, 50);

        // Act
        rig.Stats.TakeDamage(40, Guard);

        // Assert: 10 of the 20 it would have saved
        Assert.That(rig.Player.Health, Is.EqualTo(70));
        Assert.That((rig.Player.Armor, rig.Player.ArmorPercent), Is.EqualTo(((short)0, (short)0)));
    }

    [Test]
    public void God_Mode_Takes_No_Damage()
    {
        var rig = new Rig { GodMode = 1 };
        rig.Stats.TakeDamage(40, Guard);
        Assert.That(rig.Player.Health, Is.EqualTo(100));
    }

    [Test]
    public void Dying_Alone_Starts_The_Level_Again()
    {
        // Arrange
        var rig = new Rig();

        // Act
        rig.Stats.TakeDamage(500, Guard);

        // Assert
        Assert.That(rig.Player.Health, Is.Zero);
        Assert.That((rig.Deaths, rig.NetDeaths), Is.EqualTo((1, 0)));
    }

    [Test]
    public void Dying_With_Others_Playing_Leaves_Them_To_Play_On_And_Isnt_Hurt_Again()
    {
        // Arrange
        var rig = new Rig();
        rig.Session.SetGameMode(GameMode.Coop);

        // Act
        rig.Stats.TakeDamage(500, Guard);
        rig.Stats.TakeDamage(500, Guard);

        // Assert
        Assert.That((rig.Deaths, rig.NetDeaths), Is.EqualTo((0, 1)));
    }

    [Test]
    public void Nothing_Hurts_Once_The_Game_Is_Won()
    {
        var rig = new Rig();
        rig.Session.GameState.VictoryFlag = true;
        rig.Stats.TakeDamage(40, Guard);
        Assert.That(rig.Player.Health, Is.EqualTo(100));
    }

    [Test]
    public void Better_Armor_Replaces_What_Is_Worn_And_Worse_Doesnt()
    {
        // Arrange
        var rig = new Rig();

        // Act / Assert
        Assert.That(rig.Stats.TryGiveArmor("GreenArmor"), Is.True);
        Assert.That((rig.Player.Armor, rig.Player.ArmorPercent), Is.EqualTo(((short)100, (short)33)));
        Assert.That(rig.Stats.TryGiveArmor("BlueArmor"), Is.True);
        Assert.That((rig.Player.Armor, rig.Player.ArmorPercent), Is.EqualTo(((short)150, (short)50)));    // the class's max armor
        Assert.That(rig.Stats.TryGiveArmor("GreenArmor"), Is.False);
    }

    [Test]
    public void An_Armor_Bonus_Adds_Up_To_Its_Cap_Keeping_The_Worn_Armors_Share()
    {
        // Arrange
        var rig = new Rig();
        rig.Stats.TryGiveArmor("BlueArmor");     // 150 (the class's max), 50%

        // Act / Assert
        Assert.That(rig.Stats.TryGiveArmor("ArmorBonus"), Is.False);     // at the class's max already
        rig.Player.Armor = 20;
        Assert.That(rig.Stats.TryGiveArmor("ArmorBonus"), Is.True);
        Assert.That((rig.Player.Armor, rig.Player.ArmorPercent), Is.EqualTo(((short)21, (short)50)));
    }

    [Test]
    public void A_Health_Item_With_Taken_Below_Only_Helps_The_Nearly_Dead()
    {
        // Arrange
        var rig = new Rig();
        rig.Player.Health = 10;

        // Act / Assert: blood at 10 or less, healing as any health item (Wolf3D's gibs: +1 to 11)
        Assert.That(rig.Stats.CouldTakeInventory(new Health { Name = "Blood" }), Is.True);
        Assert.That(rig.Stats.TryApplyInventory(new Health { Name = "Blood" }, 1), Is.True);
        Assert.That(rig.Player.Health, Is.EqualTo(11));
        Assert.That(rig.Stats.CouldTakeInventory(new Health { Name = "Blood" }), Is.False);
        Assert.That(rig.Stats.TryApplyInventory(new Health { Name = "Blood" }, 1), Is.False);
        Assert.That(rig.Player.Health, Is.EqualTo(11));

        // a puddle's 5 from 15 goes past its taken-below (Blake Stone's water puddle)
        rig.Player.Health = 8;
        Assert.That(rig.Stats.TryApplyInventory(new Health { Name = "Blood" }, 5), Is.True);
        Assert.That(rig.Player.Health, Is.EqualTo(13));

        // without one, up to the max
        Assert.That(rig.Stats.TryApplyInventory(new Health { Name = "Medikit" }, 125), Is.True);
        Assert.That(rig.Player.Health, Is.EqualTo(100));
        Assert.That(rig.Stats.CouldTakeInventory(new Health { Name = "Medikit" }), Is.False);
    }

    [Test]
    public void A_Key_Already_Held_Isnt_Taken_Again()
    {
        var rig = new Rig();
        Assert.That(rig.Stats.TryApplyInventory(new Key { Name = "GoldKey" }, 1), Is.True);
        Assert.That(rig.Stats.TryApplyInventory(new Key { Name = "GoldKey" }, 1), Is.False);
    }

    [Test]
    public void Healing_Never_Lowers_Health_Over_The_Cap()
    {
        // Arrange: over the max (a cheat, a megasphere)
        var rig = new Rig();
        rig.Player.Health = 150;

        // Act
        rig.Stats.HealSelf(10);

        // Assert
        Assert.That(rig.Player.Health, Is.EqualTo(150));
    }

    [Test]
    public void An_Extra_Life_Stops_At_The_Classs_Max()
    {
        // Arrange
        var rig = new Rig();

        // Act
        for (int i = 0; i < 3; i++)
            rig.Stats.GiveExtraMan();

        // Assert
        Assert.That(rig.Player.Lives, Is.EqualTo(4));
    }

    [Test]
    public void A_New_Game_Starts_With_The_Classs_Items_Armor_On_And_The_Best_Weapon_In_Hand()
    {
        // Arrange: something left over from before
        var rig = new Rig();
        rig.Inventory.Give("GoldKey", 1);

        // Act
        rig.Stats.GiveStartingInventory();

        // Assert
        Assert.That(rig.Inventory.Has("GoldKey"), Is.False);
        Assert.That(rig.Inventory.GetCount("Clip"), Is.EqualTo(8));
        Assert.That(rig.Inventory.Has("GreenArmor"), Is.False);         // worn, not held
        Assert.That((rig.Player.Armor, rig.Player.ArmorPercent), Is.EqualTo(((short)100, (short)33)));
        Assert.That((rig.Player.Weapon, rig.Player.ChosenWeapon), Is.EqualTo(("Pistol", "Pistol")));
    }

    [Test]
    public void In_A_Demo_A_Grin_Lasts_Its_Count_Not_The_Sound()
    {
        // Arrange
        var rig = new Rig { Demo = true };
        rig.Player.FaceTimes = 2;

        // Act / Assert
        rig.Stats.UpdateFace();
        rig.Stats.UpdateFace();
        Assert.That(rig.Player.FaceTimes, Is.Zero);
    }
}
