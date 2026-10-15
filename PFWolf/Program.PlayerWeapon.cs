namespace PFWolf;

// The weapon in hand's states (Gameplay/WeaponInHand.cs) and the player's knife and gun
// (Gameplay/PlayerAttacks.cs)
internal partial class Program
{
    internal const string WeaponReadyState = WeaponInHand.WeaponReadyState;

    static PlayerAttacks playerAttacks = null!;     // made in InitServices
    static WeaponInHand weaponInHand = null!;

    static PlayerAttacks MakePlayerAttacks() =>
        new(_mapManager, _inventoryManager, _factionManager, lineOfSight, session,
            new PlayerAttackHooks(PlayerViewOrigin, () => centerx, () => shootdelta, TakeDamage, PlayPlayerSound,
                () => DamageDealt, () => madenoise = true));

    static WeaponInHand MakeWeaponInHand() =>
        new(_inventoryManager, _mapManager, session, playerWeapons, playerAttacks, RegisterMissileActions);

    static void RegisterWeaponActions() => (weaponInHand ??= MakeWeaponInHand()).Register();

    static Entities.Actors.Actor? SyncWeaponSprite() => weaponInHand.SyncWeaponSprite();
    static bool IsWeaponReady() => weaponInHand.IsWeaponReady();
    static void TickWeapon() => weaponInHand.TickWeapon();
    static string? WeaponShapeName() => weaponInHand.WeaponShapeName();

    internal static bool PlayerWeaponIsSilent() => playerAttacks.PlayerWeaponIsSilent();
    static void PlayAttackSound() => playerAttacks.PlayAttackSound();
    static uint PlayerDamage(int damage) => playerAttacks.PlayerDamage(damage);
    internal static void KnifeAttack(Entities.Actors.Actor ob) => playerAttacks.KnifeAttack(ob);
    internal static void GunAttack(Entities.Actors.Actor ob, int near = 4, int mid = 6, int far = 6) => playerAttacks.GunAttack(ob, near, mid, far);
}
