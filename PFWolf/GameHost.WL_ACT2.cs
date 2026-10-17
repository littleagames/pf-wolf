using System.Data;
using PFWolf.Constants;
using PFWolf.Enums;
using PFWolf.Extensions;
using PFWolf.Managers;

namespace PFWolf;

internal static partial class GameHost
{
    /*
    =============================================================================

                                   LOCAL CONSTANTS

    =============================================================================
    */

    // Enemy state tables, spawn functions (SpawnStand/SpawnPatrol/SpawnBoss/etc.), and AI
    // think/action functions (T_Stand/T_Chase/T_Shoot/etc., along with starthitpoints and
    // the enemy_states/EnemyStateList lookup) were removed from this file -- enemies now
    // run on the new Entities.Actors.Actor type, spawned by MapManager.LoadMap from
    // mapdefs/wolf3d/enemies.yaml and driven by the AI on Entities.Actors.Monster.
    //
    // Projectiles (Rocket/Smoke/Needle/Fire, actordefs/wolf3d/projectiles.yaml) and the
    // BJ-victory end-of-episode cutscene (actordefs/wolf3d/victory.yaml) run on the same type,
    // spawned into MapManager._actors by MapManager.SpawnAtActor. CheckPosition's only caller
    // is A_StartDeathCam (Gameplay/PlayerDeath.cs), placing the camera.

    // Enemies' projectiles and their effects (Gameplay/Projectiles.cs), actors' sounds
    // (Gameplay/ActorSounds.cs) and BJ's victory run (Gameplay/BJVictory.cs)
    static Projectiles projectiles = null!;     // made in InitServices
    static ActorSounds actorSounds = null!;
    static BJVictory bjVictory = null!;

    static Projectiles MakeProjectiles() =>
        new(_mapManager, _factionManager, session, new ProjectileHooks(
            (xl, yl, xh, yh, x, y, size) => WallSpriteHitByBox(xl, yl, xh, yh, x, y, size, player: false),
            PlaySoundLocActor, TakeDamage));

    internal static void A_SpawnThing(Entities.Actors.Actor ob, string[] args) => projectiles.A_SpawnThing(ob, args);
    internal static void A_Remove(Entities.Actors.Actor ob) => projectiles.A_Remove(ob);
    internal static bool ProjectileTryMove(Entities.Actors.Actor ob) => projectiles.ProjectileTryMove(ob);
    internal static void A_Projectile(Entities.Actors.Actor ob, string[] args) => projectiles.A_Projectile(ob, args);

    internal static void A_DeathScream(Entities.Actors.Actor ob) => actorSounds.A_DeathScream(ob);
    internal static void PlayActorSound(Entities.Actors.Actor ob, string key) => actorSounds.PlayActorSound(ob, key);
    internal static void A_ActiveSound(Entities.Actors.Actor ob) => actorSounds.A_ActiveSound(ob);
    internal static void A_PlaySound(Entities.Actors.Actor ob, string[] args) => actorSounds.A_PlaySound(ob, args);

    internal static void VictoryTile() => bjVictory.VictoryTile();
    internal static void A_Victory(Entities.Actors.Actor ob) => bjVictory.A_Victory(ob);
    internal static void T_BJRun(Entities.Actors.Monster ob) => bjVictory.T_BJRun(ob);
    internal static void T_BJJump(Entities.Actors.Monster ob) => bjVictory.T_BJJump(ob);
    internal static void T_BJDone(Entities.Actors.Actor ob) => bjVictory.T_BJDone(ob);
}
