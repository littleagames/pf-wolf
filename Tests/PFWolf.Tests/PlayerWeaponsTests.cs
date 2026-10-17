using PFWolf.Enums;

namespace PFWolf.Tests;

public class PlayerWeaponsTests
{
    // Wolf3D's weapons (lower selectionorder is better), a second slot-4 weapon and a charging one
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
        Shells:
          parent: Ammo
          properties:
            "inventory.maxamount": 50
        Weapon:
          parent: Inventory
        WeaponGiver:
          parent: Weapon
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
            "weapon.ammotype1": Clip
            "weapon.ammouse1": 1
        MachineGun:
          parent: Weapon
          properties:
            "weapon.slot": 3
            "weapon.selectionorder": 1500
            "weapon.ammotype1": Clip
            "weapon.ammouse1": 1
        ChainGun:
          parent: Weapon
          properties:
            "weapon.slot": 4
            "weapon.selectionorder": 1300
            "weapon.ammotype1": Clip
            "weapon.ammouse1": 1
        Shotgun:
          parent: Weapon
          properties:
            "weapon.slot": 4
            "weapon.selectionorder": 1400
            "weapon.ammotype1": Shells
            "weapon.ammouse1": 2
        ChargePistol:
          parent: Weapon
          properties:
            "weapon.slot": 5
            "weapon.chargetics": 10
        RifleMag:
          parent: Ammo
          properties:
            "inventory.maxamount": 5
        Rifle:
          parent: Weapon
          properties:
            "weapon.slot": 6
            "weapon.selectionorder": 1000
            "weapon.ammotype1": Clip
            "weapon.ammouse1": 1
            "weapon.magazine": RifleMag
          states:
            Reload:
              - sprite: RIFR
                frames: [A]
                tics-per-frame: 6
        GatlingUpgrade:
          parent: WeaponGiver
          properties:
            "weapongiver.weapon": ChainGun
        """;

    private sealed class Rig
    {
        public readonly GameSession Session = new();
        public readonly InventoryManager Inventory = new(ActorDefs.Metadata(Yaml));
        public readonly List<int> SlotItemsUsed = [];
        public bool AmmoCheat { get => Session.AmmoCheat != 0; set => Session.AmmoCheat = (byte)(value ? 1 : 0); }
        public readonly PlayerWeapons Weapons;

        public Rig(params string[] held)
        {
            Inventory.SetHolder(Session.PlayerState.Items);
            foreach (var item in held)
                Inventory.Give(item, 1);
            // With no status bar layout nothing is drawn, so no graphics are needed (the weapon's
            // picture is still looked up in the inventory)
            var bar = new StatusBarDrawing(null!, null, null!, null!, Inventory, null!, null!, null!, Session,
                new Configuration.GameSettings(), new StatusBarHooks(map => map, (text, _, _) => [text]));
            Weapons = new PlayerWeapons(Inventory, null!, bar, Session, new PlayerWeaponsHooks(SlotItemsUsed.Add));
        }

        public string? InHand
        {
            get => Session.PlayerState.Weapon;
            set => Session.PlayerState.Weapon = Session.PlayerState.ChosenWeapon = value;
        }

        public void Press(object button)
        {
            Session.PlayerInput.SetPressed((ButtonType)button, true);
            Weapons.CheckWeaponChange();
            Session.PlayerInput.SetPressed((ButtonType)button, false);
        }
    }

    [Test]
    public void The_Best_Weapon_Is_The_Lowest_Selection_Order()
    {
        // Arrange
        var rig = new Rig("Knife", "Pistol", "MachineGun");

        // Act / Assert
        Assert.That(rig.Weapons.BestWeapon(), Is.EqualTo("MachineGun"));
        Assert.That(rig.Weapons.BestWeapon(canFire: true), Is.EqualTo("Knife"));     // no ammo yet
    }

    [Test]
    public void Held_Weapons_Go_In_Slot_Order_And_Worst_To_Best_Within_A_Slot()
    {
        Assert.That(new Rig("ChainGun", "Knife", "Shotgun", "Pistol").Weapons.OwnedWeapons(),
            Is.EqualTo(new[] { "Knife", "Pistol", "Shotgun", "ChainGun" }));
    }

    [Test]
    public void A_Weapon_Giver_Isnt_A_Held_Weapon()
    {
        Assert.That(new Rig("Knife", "GatlingUpgrade").Weapons.OwnedWeapons(), Is.EqualTo(new[] { "Knife" }));
    }

    [Test]
    public void Next_Weapon_Skips_Those_Without_Ammo_And_Wraps()
    {
        // Arrange
        var rig = new Rig("Knife", "Pistol", "MachineGun", "Shotgun");
        rig.Inventory.Give("Clip", 10);
        rig.InHand = "Pistol";

        // Act / Assert
        rig.Press(ButtonType.NextWeapon);
        Assert.That(rig.InHand, Is.EqualTo("MachineGun"));
        rig.Press(ButtonType.NextWeapon);
        Assert.That(rig.InHand, Is.EqualTo("Knife"));       // the shotgun has no shells
        rig.Press(ButtonType.PrevWeapon);
        Assert.That(rig.InHand, Is.EqualTo("MachineGun"));
    }

    [Test]
    public void A_Slot_Key_Takes_Its_Best_Weapon_Then_The_Next_In_The_Slot()
    {
        // Arrange
        var rig = new Rig("Knife", "ChainGun", "Shotgun");
        rig.Inventory.Give("Clip", 10);
        rig.Inventory.Give("Shells", 10);
        rig.InHand = "Knife";

        // Act / Assert
        rig.Press(ButtonType.Slot4);
        Assert.That(rig.InHand, Is.EqualTo("ChainGun"));
        rig.Press(ButtonType.Slot4);
        Assert.That(rig.InHand, Is.EqualTo("Shotgun"));
        Assert.That(rig.Session.PlayerState.ChosenWeapon, Is.EqualTo("Shotgun"));
    }

    [Test]
    public void An_Empty_Slot_Key_Uses_An_Item_Held_For_The_Slot()
    {
        // Arrange
        var rig = new Rig("Knife", "Pistol");
        rig.InHand = "Knife";

        // Act
        rig.Press(ButtonType.Slot7);

        // Assert
        Assert.That(rig.SlotItemsUsed, Is.EqualTo(new[] { 7 }));
        Assert.That(rig.InHand, Is.EqualTo("Knife"));
    }

    [Test]
    public void A_Shot_Costs_The_Weapons_Ammo_Use()
    {
        // Arrange
        var rig = new Rig("Shotgun");
        rig.Inventory.Give("Shells", 5);
        rig.InHand = "Shotgun";

        // Act
        rig.Weapons.UseAmmo();

        // Assert
        Assert.That(rig.Inventory.GetCount("Shells"), Is.EqualTo(3));
        Assert.That(rig.Weapons.CanFire("Shotgun"), Is.True);
        rig.Weapons.UseAmmo();
        Assert.That(rig.Weapons.CanFire("Shotgun"), Is.False);      // 1 shell left
    }

    [Test]
    public void The_Ammo_Cheat_Makes_Shots_Free()
    {
        // Arrange
        var rig = new Rig("Pistol") { AmmoCheat = true };
        rig.Inventory.Give("Clip", 5);
        rig.InHand = "Pistol";

        // Act
        rig.Weapons.UseAmmo();

        // Assert
        Assert.That(rig.Inventory.GetCount("Clip"), Is.EqualTo(5));
    }

    [Test]
    public void A_Weapon_With_No_Ammo_Type_Needs_No_Ammo()
    {
        var weapons = new Rig().Weapons;
        Assert.That(weapons.WeaponAmmoType("Knife"), Is.Null);
        Assert.That(weapons.CanFire("Knife"), Is.True);
        Assert.That(weapons.CanFire(null), Is.False);
    }

    [Test]
    public void A_Better_Weapon_Picked_Up_Is_Taken_In_Hand_With_Its_Ammo()
    {
        // Arrange
        var rig = new Rig("Knife", "Pistol");
        rig.InHand = "Pistol";

        // Act
        bool took = rig.Weapons.TryGiveWeapon("MachineGun", 6);

        // Assert
        Assert.That(took, Is.True);
        Assert.That(rig.InHand, Is.EqualTo("MachineGun"));
        Assert.That(rig.Inventory.GetCount("Clip"), Is.EqualTo(6));
    }

    [Test]
    public void A_Worse_Weapon_Picked_Up_Leaves_The_One_In_Hand()
    {
        // Arrange
        var rig = new Rig("MachineGun");
        rig.InHand = "MachineGun";

        // Act
        rig.Weapons.TryGiveWeapon("Pistol", 6);

        // Assert
        Assert.That(rig.InHand, Is.EqualTo("MachineGun"));
        Assert.That(rig.Inventory.Has("Pistol"), Is.True);
    }

    [Test]
    public void A_Weapon_Already_Held_With_No_Room_For_Its_Ammo_Isnt_Taken()
    {
        // Arrange
        var rig = new Rig("Pistol");
        rig.Inventory.Give("Clip", 99);

        // Act / Assert
        Assert.That(rig.Weapons.TryGiveWeapon("Pistol", 6), Is.False);
    }

    [Test]
    public void Give_All_Ammo_Fills_Every_Type_And_Says_How_Much()
    {
        // Arrange
        var rig = new Rig();
        rig.Inventory.Give("Shells", 45);

        // Act
        int given = rig.Weapons.GiveAllAmmo(10);

        // Assert: 10 clips, 5 shells and the rifle's magazine's 5 (a magazine is ammo too)
        Assert.That(given, Is.EqualTo(20));
        Assert.That((rig.Inventory.GetCount("Clip"), rig.Inventory.GetCount("Shells")), Is.EqualTo((10, 50)));
    }

    [Test]
    public void A_Magazine_Weapons_Shots_Come_From_Its_Magazine()
    {
        // Arrange
        var rig = new Rig("Rifle");
        rig.Inventory.Give("Clip", 10);
        rig.Inventory.Give("RifleMag", 1);
        rig.InHand = "Rifle";

        // Act / Assert
        Assert.That(rig.Weapons.IsLoaded("Rifle"), Is.True);
        rig.Weapons.UseAmmo();
        Assert.That((rig.Inventory.GetCount("RifleMag"), rig.Inventory.GetCount("Clip")), Is.EqualTo((0, 10)));
        Assert.That(rig.Weapons.IsLoaded("Rifle"), Is.False);
        Assert.That(rig.Weapons.CanFire("Rifle"), Is.True);          // it can still be taken up, to reload
        Assert.That(rig.Weapons.CanReload("Rifle"), Is.True);
    }

    [Test]
    public void A_Magazine_Weapon_With_Rounds_Loaded_But_No_Ammo_Can_Still_Fire()
    {
        // Arrange
        var rig = new Rig("Rifle");
        rig.Inventory.Give("RifleMag", 2);

        // Act / Assert
        Assert.That(rig.Weapons.CanFire("Rifle"), Is.True);
        Assert.That(rig.Weapons.CanReload("Rifle"), Is.False);
    }

    [Test]
    public void Reloading_Fills_The_Magazine_Or_Loads_Some_Rounds()
    {
        // Arrange
        var rig = new Rig("Rifle");
        rig.Inventory.Give("Clip", 3);
        rig.InHand = "Rifle";

        // Act / Assert
        Assert.That(rig.Weapons.Reload(1), Is.EqualTo(1));
        Assert.That(rig.Weapons.Reload(), Is.EqualTo(2));            // all the ammo there was
        Assert.That((rig.Inventory.GetCount("RifleMag"), rig.Inventory.GetCount("Clip")), Is.EqualTo((3, 0)));
        Assert.That(rig.Weapons.Reload(), Is.Zero);
    }

    [Test]
    public void A_Magazine_Weapon_Picked_Up_Comes_Loaded_The_Rest_Going_To_Its_Ammo()
    {
        // Arrange
        var rig = new Rig("Knife");

        // Act / Assert
        Assert.That(rig.Weapons.TryGiveWeapon("Rifle", 8), Is.True);
        Assert.That((rig.Inventory.GetCount("RifleMag"), rig.Inventory.GetCount("Clip")), Is.EqualTo((5, 3)));

        // with one loaded already it's all ammo
        Assert.That(rig.Weapons.TryGiveWeapon("Rifle", 5), Is.True);
        Assert.That((rig.Inventory.GetCount("RifleMag"), rig.Inventory.GetCount("Clip")), Is.EqualTo((5, 8)));
    }

    [Test]
    public void No_Weapon_Change_While_Reloading()
    {
        // Arrange
        var rig = new Rig("Knife", "Rifle");
        rig.Inventory.Give("Clip", 5);
        rig.InHand = "Rifle";
        var rifle = rig.Inventory.CreateActor("Rifle")!;
        rifle.ArmState(rifle.ResolvedStates[WeaponInHand.ReloadState]);
        rig.Session.PlayerState.WeaponSprite = rifle;

        // Act / Assert
        rig.Press(ButtonType.Slot1);
        Assert.That(rig.InHand, Is.EqualTo("Rifle"));
    }

    [Test]
    public void A_Charging_Weapon_Shoots_Once_Then_Waits_Its_Charge()
    {
        // Arrange
        var rig = new Rig("ChargePistol");
        rig.InHand = "ChargePistol";
        rig.Session.Tics = 4;

        // Act / Assert
        Assert.That(rig.Weapons.TakeCharge(), Is.True);
        Assert.That(rig.Weapons.TakeCharge(), Is.False);
        rig.Weapons.TickWeaponCharge();
        rig.Weapons.TickWeaponCharge();
        Assert.That(rig.Session.PlayerState.WeaponCharge, Is.EqualTo(2));
        rig.Weapons.TickWeaponCharge();
        Assert.That(rig.Session.PlayerState.WeaponCharge, Is.Zero);     // never below
        Assert.That(rig.Weapons.TakeCharge(), Is.True);
    }

    [Test]
    public void A_Weapon_That_Doesnt_Charge_Can_Always_Shoot()
    {
        // Arrange
        var rig = new Rig("Pistol");
        rig.InHand = "Pistol";

        // Act / Assert
        Assert.That(rig.Weapons.TakeCharge(), Is.True);
        Assert.That(rig.Weapons.TakeCharge(), Is.True);
        Assert.That(rig.Session.PlayerState.WeaponCharge, Is.Zero);
    }
}
