using PFWolf.Enums;

namespace PFWolf.Tests;

public class WeaponInHandTests
{
    // A knife and two guns with Wolf3D's Ready/Fire states (no actions run until a frame ends,
    // and the tests never tick the weapon); the machine gun holds while fire stays down
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
            "weapon.slot": 1
            "weapon.selectionorder": 3700
          states:
            Ready:
              - sprite: KNFG
                frames: [A]
                tics-per-frame: 1
                next-state: Ready
            Fire:
              - sprite: KNFG
                frames: [B, C, D, E]
                tics-per-frame: 6
                next-state: Ready
        Pistol:
          parent: Weapon
          properties:
            "weapon.slot": 2
            "weapon.selectionorder": 1900
            "weapon.ammotype1": Clip
            "weapon.ammouse1": 1
          states:
            Ready:
              - sprite: PISG
                frames: [A]
                tics-per-frame: 1
                next-state: Ready
            Fire:
              - sprite: PISG
                frames: [B, C, D, E]
                tics-per-frame: 6
                next-state: Ready
        MachineGun:
          parent: Weapon
          properties:
            "weapon.slot": 3
            "weapon.selectionorder": 1500
            "weapon.ammotype1": Clip
            "weapon.ammouse1": 1
          states:
            Ready:
              - sprite: MCHG
                frames: [A]
                tics-per-frame: 1
                next-state: Ready
            Fire:
              - sprite: MCHG
                frames: [B, C]
                tics-per-frame: 6
            Hold:
              - sprite: MCHG
                frames: [D, E]
                tics-per-frame: 6
                next-state: Ready
        RifleMag:
          parent: Ammo
          properties:
            "inventory.maxamount": 5
        Rifle:
          parent: Weapon
          properties:
            "weapon.slot": 4
            "weapon.selectionorder": 1400
            "weapon.ammotype1": Clip
            "weapon.ammouse1": 1
            "weapon.magazine": RifleMag
          states:
            Ready:
              - sprite: RIFG
                frames: [A]
                tics-per-frame: 1
                next-state: Ready
            Fire:
              - sprite: RIFG
                frames: [B, C]
                tics-per-frame: 6
                next-state: Ready
            ZoomFire:
              - sprite: RIFZ
                frames: [A]
                tics-per-frame: 6
                next-state: Ready
            Reload:
              - sprite: RIFR
                frames: [A]
                tics-per-frame: 6
            ReloadLoop:
              - sprite: RIFR
                frames: [B, C]
                tics-per-frame: 6
                next-state: Ready
        """;

    private sealed class Rig
    {
        public readonly GameSession Session = new();
        public readonly InventoryManager Inventory = new(ActorDefs.Metadata(Yaml));
        public readonly WeaponInHand Hand;

        public Rig(params string[] held)
        {
            Inventory.SetHolder(Session.playerstate.Items);
            foreach (var item in held)
                Inventory.Give(item, 1);
            var bar = new StatusBarDrawing(null!, null, null!, null!, Inventory, null!, null!, null!, Session,
                new Configuration.GameSettings(), new StatusBarHooks(map => map, (text, _, _) => [text]));
            var weapons = new PlayerWeapons(Inventory, null!, bar, Session, new PlayerWeaponsHooks(_ => { }));
            Hand = new WeaponInHand(Inventory, null!, Session, weapons, null!, () => { });
        }

        public string? InHand
        {
            get => Session.playerstate.weapon;
            set => Session.playerstate.weapon = Session.playerstate.chosenweapon = value;
        }

        public Entities.Actors.Actor Sprite => Hand.SyncWeaponSprite()!;
    }

    [Test]
    public void No_Weapon_In_Hand_Means_No_Sprite()
    {
        var rig = new Rig();
        Assert.That(rig.Hand.SyncWeaponSprite(), Is.Null);
        Assert.That(rig.Hand.WeaponShapeName(), Is.Null);
        Assert.That(rig.Hand.IsWeaponReady(), Is.True);
    }

    [Test]
    public void A_Weapon_Taken_Up_Starts_Ready_And_Keeps_Its_Sprite()
    {
        // Arrange
        var rig = new Rig("Pistol") { InHand = "Pistol" };

        // Act
        var sprite = rig.Sprite;

        // Assert
        Assert.That(sprite.CurrentState!.StateName, Is.EqualTo(WeaponInHand.WeaponReadyState));
        Assert.That(rig.Hand.SyncWeaponSprite(), Is.SameAs(sprite));
        Assert.That(rig.Hand.WeaponShapeName(), Is.EqualTo("PISGA0"));
        Assert.That(rig.Session.playerstate.WeaponSprite, Is.SameAs(sprite));
    }

    [Test]
    public void Attack_Steps_Count_The_Timed_Frames_From_Fires_First()
    {
        // Arrange
        var rig = new Rig("Pistol") { InHand = "Pistol" };
        var sprite = rig.Sprite;

        // Act / Assert
        Assert.That(WeaponInHand.AttackStep(sprite), Is.Null);      // Ready isn't attacking
        WeaponInHand.JumpWeaponState(sprite, "Fire");
        Assert.That(WeaponInHand.AttackStep(sprite), Is.EqualTo(0));
        sprite.CurrentState = sprite.CurrentState!.Next;
        Assert.That(WeaponInHand.AttackStep(sprite), Is.EqualTo(1));
        Assert.That(WeaponInHand.AttackFrame(sprite, 1), Is.SameAs(sprite.CurrentState));
        Assert.That(WeaponInHand.AttackFrame(sprite, 9), Is.Null);
        Assert.That(rig.Hand.IsWeaponReady(), Is.False);
    }

    [Test]
    public void A_Weapon_Picked_Up_Mid_Attack_Carries_On_Where_The_Attack_Had_Got_To()
    {
        // Arrange: the pistol two frames into its attack, 4 tics left on the frame
        var rig = new Rig("Pistol", "MachineGun") { InHand = "Pistol" };
        var pistol = rig.Sprite;
        WeaponInHand.JumpWeaponState(pistol, "Fire");
        pistol.CurrentState = pistol.CurrentState!.Next;
        pistol.TicCount = 4;

        // Act
        rig.InHand = "MachineGun";
        var gun = rig.Sprite;

        // Assert
        Assert.That(gun.Name, Is.EqualTo("MachineGun"));
        Assert.That(gun.CurrentState!.FrameLetter, Is.EqualTo("C"));
        Assert.That(gun.TicCount, Is.EqualTo(4));
    }

    [Test]
    public void A_Fresh_Press_Of_Fire_Starts_The_Attack_And_Holds_The_Button()
    {
        // Arrange
        var rig = new Rig("Pistol") { InHand = "Pistol" };
        rig.Inventory.Give("Clip", 8);
        var sprite = rig.Sprite;
        rig.Session.playerinput.SetPressed(ButtonType.Attack, true);

        // Act
        rig.Hand.WeaponReady(sprite);

        // Assert
        Assert.That(sprite.CurrentState!.StateName, Is.EqualTo("Fire"));
        Assert.That(rig.Session.playerinput.IsFreshPress(ButtonType.Attack), Is.False);
    }

    [Test]
    public void Without_A_Press_The_Weapon_Stays_Ready()
    {
        // Arrange
        var rig = new Rig("Pistol") { InHand = "Pistol" };
        rig.Inventory.Give("Clip", 8);
        var sprite = rig.Sprite;

        // Act
        rig.Hand.WeaponReady(sprite);

        // Assert
        Assert.That(sprite.CurrentState!.StateName, Is.EqualTo(WeaponInHand.WeaponReadyState));
    }

    [Test]
    public void Out_Of_Ammo_The_Knife_Comes_Out_And_The_Pick_Stands()
    {
        // Arrange
        var rig = new Rig("Knife", "Pistol") { InHand = "Pistol" };

        // Act
        rig.Hand.WeaponReady(rig.Sprite);

        // Assert
        Assert.That(rig.InHand, Is.EqualTo("Knife"));
        Assert.That(rig.Session.playerstate.chosenweapon, Is.EqualTo("Pistol"));
        Assert.That(rig.Sprite.Name, Is.EqualTo("Knife"));

        // and with ammo again the pistol is taken back up
        rig.Inventory.Give("Clip", 8);
        rig.Hand.WeaponReady(rig.Sprite);
        Assert.That(rig.InHand, Is.EqualTo("Pistol"));
    }

    [Test]
    public void Fire_Held_Goes_Back_To_Hold_Or_Fire()
    {
        // Arrange
        var rig = new Rig("Pistol", "MachineGun") { InHand = "MachineGun" };
        rig.Inventory.Give("Clip", 8);
        var gun = rig.Sprite;
        rig.Session.playerinput.SetPressed(ButtonType.Attack, true);

        // Act / Assert: the machine gun has a Hold state
        rig.Hand.ReFire(gun);
        Assert.That(gun.CurrentState!.StateName, Is.EqualTo("Hold"));

        rig.InHand = "Pistol";
        var pistol = rig.Sprite;
        rig.Hand.ReFire(pistol);
        Assert.That(pistol.CurrentState!.StateName, Is.EqualTo("Fire"));
    }

    [Test]
    public void Fire_Let_Go_Or_No_Ammo_Doesnt_Refire()
    {
        // Arrange
        var rig = new Rig("MachineGun") { InHand = "MachineGun" };
        var gun = rig.Sprite;
        rig.Session.playerinput.SetPressed(ButtonType.Attack, true);

        // Act: pressed, but no ammo
        rig.Hand.ReFire(gun);

        // Assert
        Assert.That(gun.CurrentState!.StateName, Is.EqualTo(WeaponInHand.WeaponReadyState));
    }

    [Test]
    public void Holding_Fire_Cooks_A_Throw_Up_To_Its_Most_And_Letting_Go_Carries_On()
    {
        // Arrange: the rifle stands in for a grenade, cooking on its Fire state
        var rig = new Rig("Rifle") { InHand = "Rifle" };
        var weapon = rig.Sprite;
        WeaponInHand.JumpWeaponState(weapon, "Reload");
        rig.Session.tics = 3;
        rig.Session.playerinput.SetPressed(ButtonType.Attack, true);

        // Act / Assert: held, it goes up a tic's worth each time and back to Fire
        rig.Hand.HoldCharge(weapon, ["Fire"]);
        Assert.That(rig.Session.playerstate.WeaponPower, Is.EqualTo(3));
        Assert.That(weapon.CurrentState!.StateName, Is.EqualTo("Fire"));
        for (int i = 0; i < 50; i++)
            rig.Hand.HoldCharge(weapon, ["Fire"]);
        Assert.That(rig.Session.playerstate.WeaponPower, Is.EqualTo(100));

        // let go: it stays where it is
        WeaponInHand.JumpWeaponState(weapon, "Reload");
        rig.Session.playerinput.SetPressed(ButtonType.Attack, false);
        rig.Hand.HoldCharge(weapon, ["Fire"]);
        Assert.That(weapon.CurrentState!.StateName, Is.EqualTo("Reload"));
    }

    [Test]
    public void Zoomed_In_A_Weapon_Fires_Its_ZoomFire_State()
    {
        // Arrange: the rifle loaded, zoomed in
        var rig = new Rig("Rifle") { InHand = "Rifle" };
        rig.Inventory.Give("RifleMag", 5);
        var rifle = rig.Sprite;
        rig.Session.playerstate.ZoomMode = ZoomMode.Zoomed;
        rig.Session.playerinput.SetPressed(ButtonType.Attack, true);

        // Act
        rig.Hand.WeaponReady(rifle);

        // Assert
        Assert.That(rifle.CurrentState!.StateName, Is.EqualTo("ZoomFire"));
    }

    [Test]
    public void An_Empty_Magazine_Reloads_On_Its_Own_When_There_Is_Ammo()
    {
        // Arrange
        var rig = new Rig("Rifle") { InHand = "Rifle" };
        rig.Inventory.Give("Clip", 3);
        var rifle = rig.Sprite;

        // Act
        rig.Hand.WeaponReady(rifle);

        // Assert
        Assert.That(rifle.CurrentState!.StateName, Is.EqualTo(WeaponInHand.ReloadState));
        Assert.That(WeaponInHand.IsReloading(rifle), Is.True);
    }

    [Test]
    public void The_Reload_Key_Reloads_A_Magazine_With_Room_And_Fire_Shoots_A_Full_One()
    {
        // Arrange: 4 of 5 loaded
        var rig = new Rig("Rifle") { InHand = "Rifle" };
        rig.Inventory.Give("Clip", 3);
        rig.Inventory.Give("RifleMag", 4);
        var rifle = rig.Sprite;
        rig.Session.playerinput.SetPressed(ButtonType.Reload, true);

        // Act / Assert
        rig.Hand.WeaponReady(rifle);
        Assert.That(rifle.CurrentState!.StateName, Is.EqualTo(WeaponInHand.ReloadState));
        Assert.That(rig.Session.playerinput.IsFreshPress(ButtonType.Reload), Is.False);

        // full: reload does nothing, fire fires
        rig.Inventory.Give("RifleMag", 1);
        WeaponInHand.JumpWeaponState(rifle, WeaponInHand.WeaponReadyState);
        rig.Session.playerinput.SetHeld(ButtonType.Reload, false);
        rig.Session.playerinput.SetPressed(ButtonType.Attack, true);
        rig.Hand.WeaponReady(rifle);
        Assert.That(rifle.CurrentState!.StateName, Is.EqualTo("Fire"));
    }

    [Test]
    public void Nothing_To_Load_Leaves_An_Empty_Magazine_Weapon_For_The_Knife()
    {
        // Arrange
        var rig = new Rig("Knife", "Rifle") { InHand = "Rifle" };

        // Act
        rig.Hand.WeaponReady(rig.Sprite);

        // Assert
        Assert.That(rig.InHand, Is.EqualTo("Knife"));
    }

    [Test]
    public void A_Reload_Fills_The_Magazine_From_The_Ammo()
    {
        // Arrange
        var rig = new Rig("Rifle") { InHand = "Rifle" };
        rig.Inventory.Give("Clip", 7);
        rig.Inventory.Give("RifleMag", 1);

        // Act
        rig.Hand.Reload(rig.Sprite, []);

        // Assert
        Assert.That((rig.Inventory.GetCount("RifleMag"), rig.Inventory.GetCount("Clip")), Is.EqualTo((5, 3)));
    }

    [Test]
    public void A_Reload_Of_One_Round_Goes_Round_Its_Loop_Until_Full_Or_Out()
    {
        // Arrange: room for 2, ammo for 1
        var rig = new Rig("Rifle") { InHand = "Rifle" };
        rig.Inventory.Give("Clip", 2);
        rig.Inventory.Give("RifleMag", 3);
        var rifle = rig.Sprite;
        WeaponInHand.JumpWeaponState(rifle, WeaponInHand.ReloadState);

        // Act / Assert
        rig.Hand.Reload(rifle, ["1", "ReloadLoop"]);
        Assert.That(rig.Inventory.GetCount("RifleMag"), Is.EqualTo(4));
        Assert.That(rifle.CurrentState!.StateName, Is.EqualTo("ReloadLoop"));      // still room, and a round left

        WeaponInHand.JumpWeaponState(rifle, WeaponInHand.ReloadState);
        rig.Hand.Reload(rifle, ["1", "ReloadLoop"]);
        Assert.That(rig.Inventory.GetCount("RifleMag"), Is.EqualTo(5));
        Assert.That(rifle.CurrentState!.StateName, Is.EqualTo(WeaponInHand.ReloadState));     // full
    }
}
