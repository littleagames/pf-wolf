namespace PFWolf;

// The weapon in hand's states (Gameplay/WeaponInHand.cs) and the player's knife and gun
// (Gameplay/PlayerAttacks.cs)
internal static partial class GameHost
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

    static string? WeaponShapeName() => weaponInHand.WeaponShapeName();

    internal static bool PlayerWeaponIsSilent() => playerAttacks.PlayerWeaponIsSilent();
}
