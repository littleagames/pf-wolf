namespace PFWolf;

internal static partial class GameHost
{
    // Missiles, blasts and barriers (Gameplay/Missiles.cs)
    static Missiles missiles = null!;   // made in InitServices

    static Missiles MakeMissiles() =>
        new(_mapManager, _factionManager, doors, playerWeapons, playerAttacks, playerStats, session,
            new MissileHooks(ProjectileTryMove, PlaySoundLocActor, TakeDamage));

    static void RegisterMissileActions() => (missiles ??= MakeMissiles()).Register();
}
