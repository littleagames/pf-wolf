namespace PFWolf;

internal static partial class GameHost
{
    // Projectiles and jumps for any actor (Gameplay/ProjectileActions.cs, made in InitServices).
    // Registering only binds the handlers, so it can happen before the services exist (ScriptApiTests).
    static ProjectileActions projectileActions = null!;
    static ProjectileActions MakeProjectileActions() => new(_mapManager, session, lineOfSight, PlayActorSound);

    static void RegisterProjectileActions() => (projectileActions ??= MakeProjectileActions()).Register();
}
