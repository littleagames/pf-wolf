namespace PFWolf;

internal static partial class GameHost
{
    // Projectiles and jumps for any actor (Gameplay/ProjectileActions.cs, made in InitServices).
    // Registering only binds the handlers, so it can happen before the services exist (ScriptApiTests).
    static ProjectileActions projectileActions = null!;
    static ProjectileActions MakeProjectileActions() => new(_mapManager, session, lineOfSight, PlayActorSound);

    static void RegisterProjectileActions() => (projectileActions ??= MakeProjectileActions()).Register();

    /// <summary>A_SpawnProjectile's work (scripts' actor:spawnprojectile too): the projectile, or null if none was fired</summary>
    internal static Entities.Actors.Actor? SpawnProjectile(Entities.Actors.Actor ob, string className, int angle, string mode)
        => projectileActions.SpawnProjectile(ob, className, angle, mode);

    internal static bool CanSee(Entities.Actors.Actor ob, Entities.Actors.PlayerPawn pawn, long reach = long.MaxValue)
        => projectileActions.CanSee(ob, pawn, reach);

    // The scripts' actor API uses these too (GameHost.Scripting.cs)
    static (int X, int Y) LookoutPoint(Entities.Actors.Actor ob) => projectileActions.LookoutPoint(ob);
}
