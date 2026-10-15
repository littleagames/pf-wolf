namespace PFWolf.Tests;

public class PlayerClassRulesTests
{
    // Player and two classes: a tough soldier who only uses the knife and pistol, and an officer
    private const string Yaml = """
        Inventory:
          properties:
            "inventory.amount": 1
            "inventory.maxamount": 1
        Ammo:
          parent: Inventory
        Clip:
          parent: Ammo
        ClipBox:
          parent: Clip
          properties:
            "inventory.type": Clip
        Shells:
          parent: Ammo
        Weapon:
          parent: Inventory
        Knife:
          parent: Weapon
          properties:
            "weapon.ammotype1": Clip
            "weapon.ammouse1": 0
        Pistol:
          parent: Weapon
          properties:
            "weapon.ammotype1": Clip
            "weapon.ammouse1": 1
        BluePistol:
          parent: Pistol
        Shotgun:
          parent: Weapon
          properties:
            "weapon.ammotype1": Shells
            "weapon.ammouse1": 1
        SilverKey:
          parent: Inventory
          properties:
            "inventory.forbiddento": Soldier
        OfficerCap:
          parent: Inventory
          properties:
            "inventory.restrictedto": [Officer]
        Player:
          properties:
            "player.speed": 1
        Soldier:
          parent: Player
          properties:
            "player.damagedealt": 1.5
            "player.damagetaken": "-2"
            "player.maxhealth": 150
            "player.startlives": 5
            "player.eyeheight": 99999
            "player.weapons": [Knife, Pistol]
        Officer:
          parent: Player
        Captain:
          parent: Officer
        Dog:
          properties:
            "player.speed": 2
        """;

    private static (PlayerClassRules Rules, GameSession Session) Playing(string playerClass)
    {
        var session = new GameSession();
        session.playerstate.playerclass = playerClass;
        return (new PlayerClassRules(null!, new InventoryManager(ActorDefs.Metadata(Yaml)), session), session);
    }

    [TestCase("player", "Player")]
    [TestCase("soldier", "Soldier")]
    [TestCase("Captain", "Captain")]       // a class descended from one that's descended from Player
    [TestCase("Dog", null)]                 // not a player class at all
    [TestCase("Nobody", null)]
    public void A_Player_Class_Is_Player_Or_Descended_From_It(string name, string? found)
    {
        Assert.That(Playing("Player").Rules.FindPlayerClass(name), Is.EqualTo(found));
    }

    [Test]
    public void Left_Out_Values_Are_Wolf3Ds()
    {
        // Arrange
        var rules = Playing("Player").Rules;

        // Act / Assert
        Assert.That((rules.MaxHealth, rules.StartingHealth, rules.StartingLives, rules.MaxLives), Is.EqualTo(((short)100, (short)100, (short)3, (short)9)));
        Assert.That(rules.MaxArmor, Is.EqualTo(short.MaxValue));
        Assert.That((rules.DamageDealt, rules.DamageTakenFactor), Is.EqualTo((1.0, 1.0)));
    }

    [Test]
    public void A_Class_Has_Its_Own_Values()
    {
        // Arrange
        var rules = Playing("Soldier").Rules;

        // Act / Assert
        Assert.That(rules.MaxHealth, Is.EqualTo(150));
        Assert.That(rules.StartingLives, Is.EqualTo(5));
        Assert.That(rules.DamageDealt, Is.EqualTo(1.5));
        Assert.That(rules.DamageTakenFactor, Is.Zero);      // never below 0
    }

    [Test]
    public void Eye_Height_Stays_Within_What_The_Renderer_Can_Show()
    {
        // Arrange
        var (rules, session) = Playing("Soldier");

        // Act / Assert
        Assert.That(rules.StandingEyeZ(session.playerstate), Is.EqualTo(Program.MAXEYE));
        session.playerstate.playerclass = "Player";
        Assert.That(rules.StandingEyeZ(session.playerstate), Is.EqualTo(Program.EYEDEFAULT));
    }

    [TestCase("Pistol", true)]
    [TestCase("BluePistol", true)]          // descended from a weapon in the list
    [TestCase("Shotgun", false)]
    [TestCase("ClipBox", true)]             // the pistol's ammo, as a variant of it
    [TestCase("Shells", false)]             // only the shotgun uses them
    [TestCase("SilverKey", false)]          // forbidden to soldiers
    [TestCase("OfficerCap", false)]         // only for officers
    public void A_Soldier_Picks_Up_Only_What_It_May(string item, bool may)
    {
        Assert.That(Playing("Soldier").Rules.PlayerClassCanPickUp(item), Is.EqualTo(may));
    }

    [TestCase("Officer", true)]
    [TestCase("Captain", true)]             // a kind of officer
    [TestCase("Player", false)]
    public void An_Item_Restricted_To_A_Class_Is_For_It_And_Those_Descended_From_It(string playerClass, bool may)
    {
        Assert.That(Playing(playerClass).Rules.PlayerClassCanPickUp("OfficerCap"), Is.EqualTo(may));
    }

    [Test]
    public void A_Class_Without_A_Weapons_List_Uses_Every_Weapon()
    {
        var rules = Playing("Player").Rules;
        Assert.That(rules.PlayerClassCanPickUp("Shotgun"), Is.True);
        Assert.That(rules.PlayerClassCanPickUp("Shells"), Is.True);
    }
}
