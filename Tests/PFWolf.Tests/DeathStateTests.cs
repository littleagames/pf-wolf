using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

/// <summary>Which death state a monster dies into (Monster.ChooseDeathState: DEATH STATES)</summary>
public class DeathStateTests
{
    private static readonly string[] AllStates = ["Spawn", "Death", "XDeath", "Death.Fire", "Death.Stealth"];
    private static readonly string[] NoTypes = [];

    private static string Choose(string? type, int blow, int healthLeft = -1, int gibDamage = 0, int gibHealth = -1,
        string[]? gibTypes = null, string[]? states = null) =>
        Monster.ChooseDeathState(states ?? AllStates, type, blow, healthLeft, gibDamage, gibHealth, gibTypes ?? NoTypes);

    [Test]
    public void A_Plain_Kill_Is_Death() =>
        Assert.That(Choose(null, 20), Is.EqualTo("Death"));

    [TestCase("Fire")]
    [TestCase("fire")]          // any case
    public void A_Damage_Type_With_Its_Own_State_Dies_That_Way(string type) =>
        Assert.That(Choose(type, 500, gibDamage: 100), Is.EqualTo("Death.Fire"));

    [Test]
    public void A_Damage_Type_Without_A_State_Dies_As_Usual() =>
        Assert.That(Choose("Melee", 20), Is.EqualTo("Death"));

    [TestCase(99, "Death")]
    [TestCase(100, "XDeath")]   // End of Destiny: over 99 gibs
    public void A_Big_Enough_Blow_Gibs(int blow, string state) =>
        Assert.That(Choose(null, blow, gibDamage: 100), Is.EqualTo(state));

    [Test]
    public void A_Stealth_Kill_Never_Gibs_When_The_Class_Has_A_Stealth_Death() =>
        Assert.That(Choose("Stealth", 1000, gibDamage: 100), Is.EqualTo("Death.Stealth"));

    [TestCase(-25, "Death")]
    [TestCase(-26, "XDeath")]   // Midnight Assault: below -25
    public void Health_Left_Far_Enough_Below_Nothing_Gibs(int healthLeft, string state) =>
        Assert.That(Choose("Explosive", 30, healthLeft, gibHealth: 25), Is.EqualTo(state));

    [TestCase("Explosive", "XDeath")]
    [TestCase("Bullet", "Death")]
    [TestCase(null, "Death")]
    public void Gib_Types_Limit_What_Gibs(string? type, string state) =>
        Assert.That(Choose(type, 30, -100, gibHealth: 25, gibTypes: ["explosive"]), Is.EqualTo(state));

    [Test]
    public void A_Class_Without_XDeath_Dies_As_Usual() =>
        Assert.That(Choose(null, 1000, gibDamage: 100, states: ["Spawn", "Death"]), Is.EqualTo("Death"));

    [Test]
    public void No_Gib_Settings_Never_Gib() =>
        Assert.That(Choose(null, 1000, -1000), Is.EqualTo("Death"));
}
