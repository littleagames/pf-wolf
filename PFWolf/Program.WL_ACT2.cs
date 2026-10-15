using System.Data;
using PFWolf.Constants;
using PFWolf.Enums;
using PFWolf.Extensions;
using PFWolf.Managers;

namespace PFWolf;

internal partial class Program
{
    /*
    =============================================================================

                                   LOCAL CONSTANTS

    =============================================================================
    */

    internal const long PROJECTILESIZE = 0xc000L;

    internal const int BJRUNSPEED = 2048;
    internal const int BJJUMPSPEED = 680;

    // Enemy state tables, spawn functions (SpawnStand/SpawnPatrol/SpawnBoss/etc.), and AI
    // think/action functions (T_Stand/T_Chase/T_Shoot/etc., along with starthitpoints and
    // the enemy_states/EnemyStateList lookup) were removed from this file -- enemies now
    // run on the new Entities.Actors.Actor type, spawned by MapManager.LoadMap from
    // mapdefs/wolf3d/enemies.yaml and driven by the AI on Entities.Actors.Monster.
    //
    // Projectiles (Rocket/Smoke/Needle/Fire, actordefs/wolf3d/projectiles.yaml) and the
    // BJ-victory end-of-episode cutscene (actordefs/wolf3d/victory.yaml) run on the same type,
    // spawned into MapManager._actors by MapManager.SpawnAtActor. CheckPosition's only caller
    // is A_StartDeathCam below, placing the camera.

    internal const int PROJSIZE = 0x2000;        // a projectile's box, either way (ProjectileTryMove)

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
    internal static bool CheckPosition(int obx, int oby) => projectiles.CheckPosition(obx, oby);

    internal static void A_DeathScream(Entities.Actors.Actor ob) => actorSounds.A_DeathScream(ob);
    internal static void PlayActorSound(Entities.Actors.Actor ob, string key) => actorSounds.PlayActorSound(ob, key);
    internal static void A_ActiveSound(Entities.Actors.Actor ob) => actorSounds.A_ActiveSound(ob);
    internal static void A_PlaySound(Entities.Actors.Actor ob, string[] args) => actorSounds.A_PlaySound(ob, args);

    internal static void VictoryTile() => bjVictory.VictoryTile();
    internal static void A_Victory(Entities.Actors.Actor ob) => bjVictory.A_Victory(ob);
    internal static void T_BJRun(Entities.Actors.Monster ob) => bjVictory.T_BJRun(ob);
    internal static void T_BJJump(Entities.Actors.Monster ob) => bjVictory.T_BJJump(ob);
    internal static void T_BJDone(Entities.Actors.Actor ob) => bjVictory.T_BJDone(ob);
    internal static void SpawnBJVictory() => bjVictory.SpawnBJVictory();
    internal static void A_StartDeathCam(Entities.Actors.Actor ob)
    {
        var language = _assetManager.GetText("en-us");
        int dx, dy;
        float fangle;
        int xmove, ymove;
        int dist;

        _videoManager.FinishPaletteShifts();

        // intermission.yaml deathcam
        var deathcam = Intermission.DeathCam ?? new Assets.DeathCamScreen();
        GameEngineManager.DelayMs((uint)Math.Max(deathcam.PauseMs, 0));

        if (gamestate.victoryflag)
        {
            playstate = playstatetypes.ex_victorious;
            return;
        }

        // With others there's no replay: it would move one player's view, on one machine
        if (gamemode != GameMode.Single)
        {
            playstate = playstatetypes.ex_victorious;
            return;
        }

        gamestate.victoryflag = true;
        uint fadeheight = (uint)(viewsize != 21 ? _videoManager.ScreenYAboveBottom(STATUSLINES) : _videoManager.screenHeight);
        _videoManager.BarScaledCoord(0, 0, _videoManager.screenWidth, (int)fadeheight, bordercol);
        _videoManager.Transition(deathFadeStyle, 0, 0, _videoManager.screenWidth, (int)fadeheight, (uint)deathFadeTics);

        foreach (var label in deathcam.Labels)
            TextAt(label.X, label.Y, IntermissionTextStyle).Print(label.Text.ToLanguageText(language));

        _videoManager.Update();

        _inputManager.UserInput((uint)Math.Max(deathcam.HoldTics, 0));

        deathCamSprite = null;      // T_DeathCam builds a fresh one, so the flash starts from its first frame
        player.SetState(Entities.Actors.PlayerPawn.DeathCamState);

        // The camera, not the player, moves: from where the killing shot came, back from the
        // boss until it's clear of walls
        dx = ob.X - playerstate.killx;
        dy = playerstate.killy - ob.Y;

        fangle = (float)Math.Atan2((float)dy, (float)dx);
        if (fangle < 0)
            fangle = (float)(M_PI * 2 + fangle);

        var camangle = (short)(fangle / (M_PI * 2) * ANGLES);
        int camx, camy;

        dist = 0x14000;
        do
        {
            xmove = MathUtils.FixedMul(dist, costable[camangle]);
            ymove = -MathUtils.FixedMul(dist, sintable[camangle]);

            camx = ob.X - xmove;
            camy = ob.Y - ymove;
            dist += 0x1000;

        } while (!CheckPosition(camx, camy));
        camera.SetFixed(camx, camy, camangle);

        DrawPlayBorder();

        fizzlein = true;

        if (ob.ResolvedStates.ContainsKey("DeathCam"))
            ob.SetState("DeathCam");
    }

    // The death cam's "LET'S SEE THAT AGAIN!" banner (actordefs/deathcam.yaml), drawn by
    // DrawPlayerWeapon. Like the weapon in hand it's never placed in the world: the player's
    // DeathCam state runs its states instead.
    static Entities.Actors.Actor? deathCamSprite;

    /// <summary>The player's think while on its DeathCam state: animates the banner.</summary>
    internal static void T_DeathCam(Entities.Actors.Actor ob)
    {
        deathCamSprite ??= _inventoryManager.CreateActor("DeathCam");
        if (deathCamSprite != null)
            _mapManager.DoActor(deathCamSprite, tics);
    }
}
