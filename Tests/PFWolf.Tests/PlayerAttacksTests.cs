using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

public class PlayerAttacksTests
{
    private const string Yaml = """
        Inventory:
          properties:
            "inventory.amount": 1
            "inventory.maxamount": 1
        Weapon:
          parent: Inventory
        Pistol:
          parent: Weapon
          properties:
            "attacksound": "weapon/pistol/attack"
        ChargePistol:
          parent: Weapon
          properties:
            "weapon.silent": true
        QuietKnife:
          parent: Weapon
          properties:
            "weapon.silent": "True"
        """;

    private sealed class Rig
    {
        public readonly GameSession Session = new();
        public readonly List<string> Sounds = [];
        public readonly List<(PlayerPawn Victim, int Points, Entities.Actors.Actor Attacker)> PlayersHurt = [];
        public double DamageDealt = 1;
        public readonly PlayerAttacks Attacks;

        public Rig(string? weapon)
        {
            Session.PlayerState.Weapon = weapon;
            Session.PlayerState.Pawn = new PlayerPawn { State = Session.PlayerState };
            var hooks = new PlayerAttackHooks(() => (0, 0, 0, 0x10000), () => 160, () => 10,
                (victim, points, attacker) => PlayersHurt.Add((victim, points, attacker)),
                Sounds.Add, () => DamageDealt);
            Attacks = new PlayerAttacks(null!, new InventoryManager(ActorDefs.Metadata(Yaml)), null!, null!, Session, hooks);
        }
    }

    [TestCase(10, 1.0, 10u)]
    [TestCase(10, 1.5, 15u)]
    [TestCase(15, 0.5, 8u)]         // 7.5 rounds to the even 8
    [TestCase(10, 0.0, 0u)]
    public void Damage_Is_Scaled_By_The_Classs_Damage_Dealt(int damage, double factor, uint dealt)
    {
        var rig = new Rig("Pistol") { DamageDealt = factor };
        Assert.That(rig.Attacks.PlayerDamage(damage), Is.EqualTo(dealt));
    }

    [TestCase("Pistol", false)]
    [TestCase("ChargePistol", true)]
    [TestCase("QuietKnife", true)]      // a "True" read from text
    [TestCase(null, false)]
    public void A_Weapon_Is_Silent_When_It_Says_So(string? weapon, bool silent)
    {
        Assert.That(new Rig(weapon).Attacks.PlayerWeaponIsSilent(), Is.EqualTo(silent));
    }

    [Test]
    public void The_Attack_Sound_Is_The_Weapons_Own()
    {
        // Arrange
        var rig = new Rig("Pistol");

        // Act
        rig.Attacks.PlayAttackSound();
        new Rig("ChargePistol").Attacks.PlayAttackSound();      // none

        // Assert
        Assert.That(rig.Sounds, Is.EqualTo(new[] { "weapon/pistol/attack" }));
    }

    [Test]
    public void A_Shot_That_Hits_Another_Player_Hurts_Them_As_Themselves()
    {
        // Arrange
        var rig = new Rig("Pistol");
        var rival = new PlayerPawn { State = new Entities.PlayerState() };

        // Act
        rig.Attacks.HitShotTarget(rival, 12);

        // Assert
        Assert.That(rig.PlayersHurt, Has.Count.EqualTo(1));
        Assert.That(rig.PlayersHurt[0].Victim, Is.SameAs(rival));
        Assert.That(rig.PlayersHurt[0].Points, Is.EqualTo(12));
        Assert.That(rig.PlayersHurt[0].Attacker, Is.SameAs(rig.Session.Player));
    }
}
