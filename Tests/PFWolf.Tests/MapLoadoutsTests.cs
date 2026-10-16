using PFWolf.Assets;

namespace PFWolf.Tests;

public class MapLoadoutsTests
{
    // A knife, a pistol with its magazine and ammo, a shotgun's shells, grenades, and a medkit
    private const string Yaml = """
        Inventory:
          properties:
            "inventory.amount": 1
            "inventory.maxamount": 99
        Ammo:
          parent: Inventory
        Weapon:
          parent: Inventory
        Ammo45:
          parent: Ammo
        AmmoShells:
          parent: Ammo
        PistolMag:
          parent: Ammo
        Knife:
          parent: Weapon
          properties:
            "weapon.slot": 1
            "weapon.selectionorder": 3700
        Pistol:
          parent: Weapon
          properties:
            "weapon.slot": 2
            "weapon.selectionorder": 1900
            "weapon.ammotype1": Ammo45
            "weapon.ammouse1": 1
            "weapon.magazine": PistolMag
        Shotgun:
          parent: Weapon
          properties:
            "weapon.slot": 4
            "weapon.selectionorder": 1200
            "weapon.ammotype1": AmmoShells
            "weapon.ammouse1": 1
        Medkit:
          parent: Inventory
        """;

    private GameSession _session = null!;
    private InventoryManager _inventory = null!;
    private MapLoadouts _loadouts = null!;

    [SetUp]
    public void Make()
    {
        _session = new GameSession();
        _inventory = new InventoryManager(ActorDefs.Metadata(Yaml));
        _session.ActingChanged = p => _inventory.SetHolder(p.Items);
        _session.SetActing(_session.players[0]);
        foreach (var (item, amount) in new[] { ("Knife", 1), ("Pistol", 1), ("Shotgun", 1), ("Ammo45", 40), ("AmmoShells", 12), ("PistolMag", 6), ("Medkit", 2) })
            _inventory.Give(item, amount);
        _session.playerstate.weapon = _session.playerstate.chosenweapon = "Shotgun";
        _session.playerstate.health = 80;

        var bar = new StatusBarDrawing(null!, null, null!, null!, _inventory, null!, null!, null!, _session,
            new StatusBarHooks(() => false, map => map, (text, _, _) => [text]));
        var weapons = new PlayerWeapons(_inventory, null!, bar, _session, new PlayerWeaponsHooks(() => false, _ => { }));
        _loadouts = new MapLoadouts(_inventory, weapons, _session);
    }

    private static MapLoadout Loadout(string yaml) => PFWolf.Loaders.YamlDataEntryLoader.Deserialize<MapLoadout>(yaml);

    [Test]
    public void Hidden_Tunnels_Leaves_The_Knife_And_Pistol_And_Caps_Health()
    {
        // Act
        _loadouts.Apply(Loadout("take-weapons: true\ngive: { Knife: 1, Pistol: 1 }\nhealth-cap: 50\n"));

        // Assert: the magazine's rounds stay, the rest of the ammo goes, the medkits stay
        Assert.That(_inventory.Items.Where(i => i.Value > 0).ToDictionary(i => i.Key, i => i.Value),
            Is.EquivalentTo(new Dictionary<string, int> { ["Knife"] = 1, ["Pistol"] = 1, ["PistolMag"] = 6, ["Medkit"] = 2 }));
        Assert.That(_session.playerstate.health, Is.EqualTo(50));
        Assert.That(_session.playerstate.weapon, Is.EqualTo("Pistol"));
    }

    [Test]
    public void A_Health_Cap_Doesnt_Raise_It()
    {
        _session.playerstate.health = 30;
        _loadouts.Apply(Loadout("health-cap: 50\n"));
        Assert.That(_session.playerstate.health, Is.EqualTo(30));
        Assert.That(_inventory.Has("Shotgun"), Is.True);
    }

    [Test]
    public void POW_Takes_Everything_And_Sets_Health_To_One()
    {
        _loadouts.Apply(Loadout("take-weapons: true\nhealth: 1\n"));
        Assert.That(_session.playerstate.health, Is.EqualTo(1));
        Assert.That(_inventory.Items.Where(i => i.Value > 0).Select(i => i.Key), Is.EquivalentTo(new[] { "PistolMag", "Medkit" }));
        Assert.That(_session.playerstate.weapon, Is.Null);
    }

    [Test]
    public void Knife_Only_Puts_The_Knife_In_Hand()
    {
        _loadouts.Apply(Loadout("take-weapons: true\ngive: { Knife: 1 }\n"));
        Assert.That(_session.playerstate.weapon, Is.EqualTo("Knife"));
        Assert.That(_session.playerstate.WeaponSprite, Is.Null);
    }

    [Test]
    public void Every_Player_Gets_It()
    {
        // Arrange: a second player, with a shotgun
        var second = new Entities.PlayerState { health = 100 };
        _session.players.Add(second);
        using (_session.ActAs(second))
            _inventory.Give("Shotgun", 1);

        // Act
        _loadouts.Apply(Loadout("take-weapons: true\ngive: { Knife: 1 }\n"));

        // Assert
        Assert.That(second.Items.GetValueOrDefault("Shotgun"), Is.Zero);
        Assert.That(second.Items.GetValueOrDefault("Knife"), Is.EqualTo(1));
    }

    [Test]
    public void No_Loadout_Changes_Nothing()
    {
        _loadouts.Apply(null);
        Assert.That(_inventory.Has("Shotgun") && _session.playerstate.health == 80, Is.True);
    }
}
