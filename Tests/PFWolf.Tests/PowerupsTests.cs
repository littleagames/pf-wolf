namespace PFWolf.Tests;

public class PowerupsTests
{
    private const string Yaml = """
        Inventory: {}
        Weapon:
          parent: Inventory
        Knife:
          parent: Weapon
        Powerup:
          parent: Inventory
          properties:
            "inventory.maxamount": 1000000
            "powerup.duration": 2100
        GodArtifact:
          parent: Powerup
          properties:
            "powerup.invulnerable": true
            "powerup.face": [GODMODEFACE1, GODMODEFACE2, GODMODEFACE3]
        KnifeFury:
          parent: Powerup
          properties:
            "powerup.weapon": Knife
            "powerup.weaponkills": true
            "powerup.face": GOTGATLINGPIC
        """;

    private InventoryManager _inventory = null!;

    [SetUp]
    public void Make() => _inventory = new InventoryManager(ActorDefs.Metadata(Yaml));

    [Test]
    public void Nothing_Runs_Until_One_Is_Held()
    {
        Assert.That(Powerups.Running(_inventory), Is.Empty);
        Assert.That(Powerups.Invulnerable(_inventory), Is.False);
        Assert.That(Powerups.Face(_inventory, 0), Is.Null);
        Assert.That(Powerups.HoldsAWeapon(_inventory), Is.False);
    }

    [Test]
    public void God_Mode_Makes_The_Player_Unhurt_With_Its_Own_Face()
    {
        // Act: picked up twice, the time adds up
        _inventory.Give("GodArtifact", 2100);
        _inventory.Give("GodArtifact", 2100);

        // Assert
        Assert.That(_inventory.GetCount("GodArtifact"), Is.EqualTo(4200));
        Assert.That(Powerups.Invulnerable(_inventory), Is.True);
        Assert.That(Powerups.Face(_inventory, 1), Is.EqualTo("GODMODEFACE2"));
    }

    [Test]
    public void Knife_Fury_Holds_The_Knife()
    {
        _inventory.Give("KnifeFury", 2100);

        Assert.That(Powerups.HoldsAWeapon(_inventory), Is.True);
        Assert.That(Powerups.ForWeapon(_inventory, "Knife"), Is.EqualTo("KnifeFury"));
        Assert.That(Powerups.ForWeapon(_inventory, "Pistol"), Is.Null);
        Assert.That(Powerups.Flag(_inventory, "KnifeFury", "powerup.weaponkills"), Is.True);
        Assert.That(Powerups.Invulnerable(_inventory), Is.False);
        Assert.That(Powerups.Face(_inventory, 2), Is.EqualTo("GOTGATLINGPIC"));
    }

    [TestCase(2100, "0:30")]
    [TestCase(2099, "0:29")]
    [TestCase(69, "0:00")]
    [TestCase(4270, "1:01")]
    public void The_Timer_Shows_Minutes_And_Seconds(int tics, string shown) =>
        Assert.That(Powerups.TimerText(tics), Is.EqualTo(shown));
}
