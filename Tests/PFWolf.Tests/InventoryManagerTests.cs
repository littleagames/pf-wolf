namespace PFWolf.Tests;

public class InventoryManagerTests
{
    // A cut-down native.yaml plus some Wolf3D-style items and a player class
    private const string Yaml = """
        Inventory:
          properties:
            "inventory.amount": 1
            "inventory.maxamount": 1
            "inventory.interhubamount": 1
        Ammo:
          parent: Inventory
          properties:
            "inventory.interhubamount": 9999
        Health:
          parent: Inventory
          properties:
            "inventory.maxamount": 0
        Key:
          parent: Inventory
          properties:
            "inventory.interhubamount": 0
        GoldKey:
          parent: Key
        Weapon:
          parent: Inventory
        Pistol:
          parent: Weapon
        Clip:
          parent: Ammo
          properties:
            "inventory.maxamount": 99
        ClipBox:
          parent: Clip
          properties:
            "inventory.type": Clip
            "inventory.amount": 25
        Player:
          properties:
            "player.speed": 1
        Packrat:
          parent: Player
          properties:
            "player.maxamount":
              ClipBox: 200
        """;

    private static InventoryManager Create() => new(ActorDefs.Metadata(Yaml));

    [Test]
    public void Give_Adds_Up_To_The_Max_And_Says_How_Many()
    {
        // Arrange
        var inventory = Create();

        // Act
        var first = inventory.Give("Clip", 90);
        var second = inventory.Give("Clip", 20);

        // Assert
        Assert.That((first, second), Is.EqualTo((90, 9)));
        Assert.That(inventory.GetCount("Clip"), Is.EqualTo(99));
        Assert.That(inventory.IsFull("Clip"), Is.True);
    }

    [Test]
    public void Give_Of_Nothing_Or_Less_Adds_Nothing()
    {
        // Arrange
        var inventory = Create();

        // Act / Assert
        Assert.That(inventory.Give("Clip", 0), Is.Zero);
        Assert.That(inventory.Give("Clip", -5), Is.Zero);
        Assert.That(inventory.Has("Clip"), Is.False);
    }

    [Test]
    public void Variants_Fold_Into_Their_Type()
    {
        // Arrange
        var inventory = Create();

        // Act
        inventory.Give("ClipBox", 25);
        inventory.Give("Clip", 5);

        // Assert
        Assert.That(inventory.GetItemType("ClipBox"), Is.EqualTo("Clip"));
        Assert.That(inventory.GetCount("Clip"), Is.EqualTo(30));
        Assert.That(inventory.GetCount("ClipBox"), Is.EqualTo(30));
        Assert.That(inventory.Items.Keys, Is.EqualTo(new[] { "Clip" }));
    }

    [Test]
    public void Max_Amount_Is_Inherited_From_The_Nearest_Parent()
    {
        // Arrange
        var inventory = Create();

        // Act / Assert
        Assert.That(inventory.GetMaxAmount("Clip"), Is.EqualTo(99));
        Assert.That(inventory.GetMaxAmount("GoldKey"), Is.EqualTo(1));
        Assert.That(inventory.GetMaxAmount("Pistol"), Is.EqualTo(1));
    }

    [Test]
    public void Max_Amount_Of_Zero_Is_Uncapped()
    {
        Assert.That(Create().GetMaxAmount("Health"), Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void Player_Class_Can_Raise_A_Cap_Named_By_Any_Variant()
    {
        // Arrange
        var inventory = Create();
        inventory.PlayerClass = () => "Packrat";

        // Act
        var added = inventory.Give("Clip", 150);

        // Assert
        Assert.That(inventory.GetMaxAmount("Clip"), Is.EqualTo(200));
        Assert.That(added, Is.EqualTo(150));
    }

    [Test]
    public void Take_Removes_Up_To_What_Is_Held_And_Drops_Empty_Items()
    {
        // Arrange
        var inventory = Create();
        inventory.Give("Clip", 10);

        // Act
        var first = inventory.Take("Clip", 4);
        var second = inventory.Take("Clip", 100);

        // Assert
        Assert.That((first, second), Is.EqualTo((4, 6)));
        Assert.That(inventory.Items, Is.Empty);
        Assert.That(inventory.Take("Clip", 1), Is.Zero);
    }

    [Test]
    public void FindClass_Matches_Case_And_Checks_The_Base_Class()
    {
        // Arrange
        var inventory = Create();

        // Act / Assert
        Assert.That(inventory.FindClass("goldkey", "Key"), Is.EqualTo("GoldKey"));
        Assert.That(inventory.FindClass("goldkey", "Weapon"), Is.Null);
        Assert.That(inventory.FindClass("goldkey", "Weapon", "Inventory"), Is.EqualTo("GoldKey"));
        Assert.That(inventory.FindClass("Nothing", "Inventory"), Is.Null);
    }

    [Test]
    public void GetClassesDerivedFrom_Leaves_Out_The_Base_Itself()
    {
        // Act
        var keys = Create().GetClassesDerivedFrom("Key");

        // Assert
        Assert.That(keys, Is.EqualTo(new[] { "GoldKey" }));
    }

    [Test]
    public void Shared_Keys_Go_In_The_Teams_Bag()
    {
        // Arrange
        var inventory = Create();
        inventory.ShareKeys = true;

        // Act
        inventory.Give("GoldKey", 1);
        inventory.Give("Clip", 8);

        // Assert
        Assert.That(inventory.SharedItems.Keys, Is.EqualTo(new[] { "GoldKey" }));
        Assert.That(inventory.Items.Keys, Is.EqualTo(new[] { "Clip" }));
        Assert.That(inventory.Has("GoldKey"), Is.True);
    }

    [Test]
    public void Shared_Keys_Are_Seen_By_Every_Holder()
    {
        // Arrange
        var inventory = Create();
        inventory.ShareKeys = true;
        var alice = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var bob = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Act
        inventory.SetHolder(alice);
        inventory.Give("GoldKey", 1);
        inventory.Give("Clip", 8);
        inventory.SetHolder(bob);

        // Assert
        Assert.That(inventory.Has("GoldKey"), Is.True);
        Assert.That(inventory.Has("Clip"), Is.False);
    }

    [Test]
    public void Without_Sharing_Keys_Are_The_Players_Own()
    {
        // Arrange
        var inventory = Create();

        // Act
        inventory.Give("GoldKey", 1);

        // Assert
        Assert.That(inventory.Items.Keys, Is.EqualTo(new[] { "GoldKey" }));
        Assert.That(inventory.SharedItems, Is.Empty);
    }

    [Test]
    public void Clear_Keeps_Shared_Keys()
    {
        // Arrange
        var inventory = Create();
        inventory.ShareKeys = true;
        inventory.Give("GoldKey", 1);
        inventory.Give("Clip", 8);

        // Act
        inventory.Clear();

        // Assert
        Assert.That(inventory.Items, Is.Empty);
        Assert.That(inventory.Has("GoldKey"), Is.True);
    }

    [Test]
    public void ResetForNextLevel_Trims_To_Each_Items_Interhub_Amount()
    {
        // Arrange
        var inventory = Create();
        inventory.Give("Clip", 50);
        inventory.Give("Pistol", 1);
        inventory.Give("GoldKey", 1);

        // Act
        inventory.ResetForNextLevel();

        // Assert: ammo keeps everything, a weapon keeps the default 1, keys go
        Assert.That(inventory.GetCount("Clip"), Is.EqualTo(50));
        Assert.That(inventory.GetCount("Pistol"), Is.EqualTo(1));
        Assert.That(inventory.Has("GoldKey"), Is.False);
    }

    [Test]
    public void ResetForNextLevel_Drops_Shared_Keys()
    {
        // Arrange
        var inventory = Create();
        inventory.ShareKeys = true;
        inventory.Give("GoldKey", 1);

        // Act
        inventory.ResetForNextLevel();

        // Assert
        Assert.That(inventory.SharedItems, Is.Empty);
    }

    [Test]
    public void Restore_Replaces_Everything_And_Skips_Empty_Counts()
    {
        // Arrange
        var inventory = Create();
        inventory.Give("Pistol", 1);

        // Act
        inventory.Restore(new Dictionary<string, int> { ["Clip"] = 12, ["GoldKey"] = 0 });

        // Assert
        Assert.That(inventory.Items, Is.EqualTo(new Dictionary<string, int> { ["Clip"] = 12 }));
    }

    [Test]
    public void Properties_Come_Through_The_Parent_Chain()
    {
        // Arrange
        var inventory = Create();

        // Act / Assert
        Assert.That(inventory.GetIntProperty("ClipBox", "inventory.amount", -1), Is.EqualTo(25));
        Assert.That(inventory.GetIntProperty("Clip", "inventory.amount", -1), Is.EqualTo(1));
        Assert.That(inventory.GetIntProperty("Clip", "no.such.property", -1), Is.EqualTo(-1));
        Assert.That(inventory.GetStringProperty("ClipBox", "inventory.type"), Is.EqualTo("Clip"));
    }

    [Test]
    public void A_Parent_Cycle_Does_Not_Hang()
    {
        // Arrange
        var inventory = new InventoryManager(ActorDefs.Metadata("""
            A:
              parent: B
            B:
              parent: A
            """));

        // Act / Assert
        Assert.That(inventory.GetMaxAmount("A"), Is.EqualTo(1));
        Assert.That(inventory.FindClass("A", "Inventory"), Is.Null);
    }
}
