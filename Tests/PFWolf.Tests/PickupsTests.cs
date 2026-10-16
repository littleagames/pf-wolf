using PFWolf.Entities.Actors;
using PFWolf.Enums;

namespace PFWolf.Tests;

public class PickupsTests
{
    private const string Yaml = """
        Inventory:
          properties:
            "inventory.amount": 1
            "inventory.maxamount": 1
        Health:
          parent: Inventory
          properties:
            "inventory.maxamount": 0
        Medikit:
          parent: Health
          properties:
            "inventory.amount": 25
        CustomInventory:
          parent: Inventory
        Cross:
          parent: CustomInventory
          flags: [COUNTITEM]
        Clue:
          parent: CustomInventory
          flags: [INVENTORY.ALWAYSPICKUP]
          properties:
            "inventory.amount": 0
        Weapon:
          parent: Inventory
        Pistol:
          parent: Weapon
        OfficerCap:
          parent: CustomInventory
          properties:
            "inventory.restrictedto": [Officer]
        Player:
          properties:
            "player.maxhealth": 100
        Officer:
          parent: Player
        """;

    private sealed class Rig
    {
        public readonly GameSession Session = new();
        public readonly InventoryManager Inventory = new(ActorDefs.Metadata(Yaml));
        public readonly MapManager Map;
        public readonly Pickups Pickups;
        public bool Died, HasWeapon, ItemRespawn;
        public readonly List<string> Sounds = [];
        public readonly List<Inventory> Respawning = [];

        public Rig()
        {
            Map = new(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, Session);
            // Acting as a second player: no pickup flash or message, which only the local player has
            Session.players.Add(new Entities.PlayerState());
            Session.ActingChanged = p => Inventory.SetHolder(p.Items);
            Session.SetActing(Session.players[1]);
            Session.playerstate.playerclass = "Player";
            Session.playerstate.health = 50;

            var bar = new StatusBarDrawing(null!, null, null!, null!, Inventory, null!, null!, null!, Session,
                new StatusBarHooks(() => false, map => map, (text, _, _) => [text]));
            var classes = new PlayerClassRules(null!, Inventory, Session);
            var weapons = new PlayerWeapons(Inventory, null!, bar, Session, new PlayerWeaponsHooks(() => false, _ => { }));
            var stats = new PlayerStats(null!, null!, null!, Inventory, bar, classes, weapons, Session,
                new PlayerStatsHooks(() => 0, () => false, () => false, () => 1, () => { }, () => { }, _ => { }));
            Pickups = new Pickups(Map, null!, null!, classes, stats, Session,
                new PickupHooks(() => Died, Sounds.Add, _ => HasWeapon, () => ItemRespawn, Respawning.Add));
        }

        // An item lying on the level
        public Inventory Item(string name)
        {
            var item = (Inventory)Inventory.CreateActor(name)!;
            Map.GetActors().AddLast(item);
            return item;
        }

        public bool OnTheFloor(Inventory item) => Map.GetActors().Contains(item);
    }

    [Test]
    public void An_Item_Taken_Gives_What_It_Gives_And_Goes()
    {
        // Arrange
        var rig = new Rig();
        var medikit = rig.Item("Medikit");

        // Act
        rig.Pickups.GetBonus(medikit);

        // Assert
        Assert.That(rig.Session.playerstate.health, Is.EqualTo(75));
        Assert.That(rig.OnTheFloor(medikit), Is.False);
    }

    [Test]
    public void One_The_Player_Cant_Take_Stays()
    {
        // Arrange: health already full
        var rig = new Rig();
        rig.Session.playerstate.health = 100;
        var medikit = rig.Item("Medikit");

        // Act
        rig.Pickups.GetBonus(medikit);

        // Assert
        Assert.That(rig.OnTheFloor(medikit), Is.True);
    }

    [Test]
    public void One_For_Another_Class_Stays()
    {
        var rig = new Rig();
        var cap = rig.Item("OfficerCap");
        rig.Pickups.GetBonus(cap);
        Assert.That(rig.OnTheFloor(cap), Is.True);
    }

    [Test]
    public void An_Always_Pickup_Item_Goes_Even_When_It_Gives_Nothing()
    {
        var rig = new Rig();
        var clue = rig.Item("Clue");
        rig.Pickups.GetBonus(clue);
        Assert.That(rig.OnTheFloor(clue), Is.False);
    }

    [Test]
    public void Treasure_Counts_Toward_The_Levels_Ratio()
    {
        var rig = new Rig();
        rig.Pickups.GetBonus(rig.Item("Cross"));
        Assert.That(rig.Session.gamestate.treasurecount, Is.EqualTo(1));
    }

    [Test]
    public void Nothing_Is_Picked_Up_The_Tic_The_Player_Dies()
    {
        // Arrange
        var rig = new Rig { Died = true };
        var medikit = rig.Item("Medikit");

        // Act
        rig.Pickups.GetBonus(medikit);

        // Assert
        Assert.That(rig.Session.playerstate.health, Is.EqualTo(50));
        Assert.That(rig.OnTheFloor(medikit), Is.True);
    }

    [Test]
    public void In_Deathmatch_A_Weapon_Stays_For_Everyone_Else()
    {
        // Arrange
        var rig = new Rig();
        rig.Session.SetGameMode(GameMode.Deathmatch);
        var pistol = rig.Item("Pistol");

        // Act
        rig.Pickups.GetBonus(pistol);

        // Assert
        Assert.That(rig.Inventory.Has("Pistol"), Is.True);
        Assert.That(rig.OnTheFloor(pistol), Is.True);
    }

    [Test]
    public void In_Deathmatch_A_Weapon_Already_Held_Isnt_Picked_Up_Again()
    {
        // Arrange
        var rig = new Rig { HasWeapon = true };
        rig.Session.SetGameMode(GameMode.Deathmatch);

        // Act
        rig.Pickups.GetBonus(rig.Item("Pistol"));

        // Assert
        Assert.That(rig.Inventory.Has("Pistol"), Is.False);
    }

    [Test]
    public void In_Deathmatch_Other_Items_Come_Back_When_The_Rules_Say()
    {
        // Arrange
        var rig = new Rig { ItemRespawn = true };
        rig.Session.SetGameMode(GameMode.Deathmatch);
        var medikit = rig.Item("Medikit");

        // Act
        rig.Pickups.GetBonus(medikit);

        // Assert
        Assert.That(rig.Respawning, Is.EqualTo(new[] { medikit }));
        Assert.That(rig.OnTheFloor(medikit), Is.False);
    }
}
