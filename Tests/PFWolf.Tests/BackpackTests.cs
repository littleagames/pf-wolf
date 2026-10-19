namespace PFWolf.Tests;

public class BackpackTests
{
    private const string Yaml = """
        Inventory: {}
        Ammo:
          parent: Inventory
        Clip:
          parent: Ammo
          properties:
            "inventory.maxamount": 100
            "ammo.backpackmaxamount": 200
            "ammo.backpackamount": 40
        ClipBox:
          parent: Clip
          properties:
            "inventory.type": Clip
        Rockets:
          parent: Ammo
          properties:
            "inventory.maxamount": 10
        BackpackItem:
          parent: Inventory
          properties:
            "inventory.maxamount": 1
        Backpack:
          parent: BackpackItem
        """;

    private InventoryManager _inventory = null!;

    [SetUp]
    public void Make() => _inventory = new InventoryManager(ActorDefs.Metadata(Yaml));

    [Test]
    public void Without_A_Backpack_The_Usual_Caps_Hold()
    {
        _inventory.Give("Clip", 500);

        Assert.That(_inventory.HasBackpack(), Is.False);
        Assert.That(_inventory.GetCount("Clip"), Is.EqualTo(100));
    }

    [Test]
    public void A_Backpack_Raises_The_Caps_That_Say_So()
    {
        // Act
        _inventory.Give("Backpack", 1);
        _inventory.Give("ClipBox", 500);
        _inventory.Give("Rockets", 50);

        // Assert: the clips' cap is their backpack one, by their type; the rockets have none
        Assert.That(_inventory.HasBackpack(), Is.True);
        Assert.That(_inventory.GetCount("Clip"), Is.EqualTo(200));
        Assert.That(_inventory.GetCount("Rockets"), Is.EqualTo(10));
    }

    [Test]
    public void Losing_The_Backpack_Brings_The_Caps_Back()
    {
        _inventory.Give("Backpack", 1);
        _inventory.Take("Backpack", 1);

        _inventory.Give("Clip", 500);

        Assert.That(_inventory.GetCount("Clip"), Is.EqualTo(100));
    }
}
