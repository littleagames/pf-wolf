using System.Data;
using Wolf3D.Constants;
using Wolf3D.Extensions;
using Wolf3D.Managers;

namespace Wolf3D;

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
    // mapdefs/wolf3d/enemies.yaml and driven by the AI ported to Program.EnemyAI.cs.
    //
    // Projectiles (Rocket/Smoke/Boom/Needle/Fire, actordefs/wolf3d/projectiles.yaml) and the
    // BJ-victory end-of-episode cutscene (actordefs/wolf3d/victory.yaml) run on the same type,
    // spawned into MapManager._actors by MapManager.SpawnAtActor. Nothing in this file is
    // legacy/objstruct-based any more. CheckPosition's only caller is the player pawn in
    // Program.EnemyAI.cs's A_StartDeathCam.

    /*
    =================
    =
    = A_Smoke
    =
    =================
    */

    internal static void A_Smoke(Entities.Actors.Actor ob)
    {
        var smoke = _mapManager.SpawnAtActor("Smoke", ob);
        if (smoke != null)
            smoke.TicCount = 6;             // the first puff lingers longer than its YAML 3 tics
    }

    // Terminal action for one-shot effects (Smoke, Boom): the legacy chain ended on a null
    // next state, which removed the object once its last frame's tics ran out.
    internal static void A_Remove(Entities.Actors.Actor ob) => _mapManager.MarkForRemoval(ob);


    /*
    ===================
    =
    = ProjectileTryMove
    =
    = returns true if move ok
    ===================
    */

    internal const int PROJSIZE = 0x2000;

    internal static bool ProjectileTryMove(Entities.Actors.Actor ob)
    {
        int xl, yl, xh, yh, x, y;
        Actor? check;

        xl = (ob.X - PROJSIZE) >> MapConstants.TILESHIFT;
        yl = (ob.Y - PROJSIZE) >> MapConstants.TILESHIFT;

        xh = (ob.X + PROJSIZE) >> MapConstants.TILESHIFT;
        yh = (ob.Y + PROJSIZE) >> MapConstants.TILESHIFT;

        //
        // check for solid walls
        //
        for (y = yl; y <= yh; y++)
            for (x = xl; x <= xh; x++)
            {
                check = _mapManager.actorat[x, y];
                if (check != null && check is not objstruct)
                    return false;
            }

        return true;
    }

    /*
    =================
    =
    = T_Projectile
    =
    =================
    */

    internal static void T_Projectile(Entities.Actors.Actor ob)
    {
        long deltax, deltay;
        int damage = 0;
        int speed;

        speed = (int)(ob.Speed * tics);

        deltax = MathUtils.FixedMul(speed, costable[ob.Angle]);
        deltay = -MathUtils.FixedMul(speed, sintable[ob.Angle]);

        if (deltax > 0x10000L)
            deltax = 0x10000L;
        if (deltay > 0x10000L)
            deltay = 0x10000L;

        ob.X += (int)deltax;
        ob.Y += (int)deltay;

        deltax = Math.Abs(ob.X - player.X);
        deltay = Math.Abs(ob.Y - player.Y);

        if (!ProjectileTryMove(ob))
        {
            // Only rockets explode; needles and flames just vanish. (Spear's hrocket variant
            // is not ported.)
            if (ob.Name == "Rocket")
            {
                PlaySoundLocActor("missile/hit", ob);
                _mapManager.SpawnAtActor("Boom", ob);
            }

            _mapManager.MarkForRemoval(ob);
            return;
        }

        if (deltax < PROJECTILESIZE && deltay < PROJECTILESIZE)
        {       // hit the player
            switch (ob.Name)
            {
                case "Needle":
                    damage = (US_RndT() >> 3) + 20;
                    break;
                case "Rocket":
                    damage = (US_RndT() >> 3) + 30;
                    break;
                case "Fire":
                    damage = (US_RndT() >> 3);
                    break;
            }

            TakeDamage(damage, ob);
            _mapManager.MarkForRemoval(ob);
            return;
        }

        ob.TileX = (byte)(ob.X >> MapConstants.TILESHIFT);
        ob.TileY = (byte)(ob.Y >> MapConstants.TILESHIFT);
    }

    /*
    ============================================================================

                                        BJ VICTORY

    ============================================================================
    */

    // The BJVictory actor (actordefs/wolf3d/victory.yaml) runs along the map's arrow icons for
    // a few tiles, jumps, and then ends the level.

    /*
    ===============
    =
    = T_BJRun
    =
    ===============
    */

    internal static void T_BJRun(Entities.Actors.Actor ob)
    {
        int move;

        move = (int)(BJRUNSPEED * tics);

        while (move != 0)
        {
            if (move < ob.Distance)
            {
                MoveObj(ob, move);
                break;
            }

            RecenterOnTile(ob);
            move -= ob.Distance;

            SelectPathDir(ob);

            if ((--ob.Temp1) == 0)
            {
                NewActorState(ob, "Jump");
                return;
            }
        }
    }

    /*
    ===============
    =
    = T_BJJump
    =
    ===============
    */

    internal static void T_BJJump(Entities.Actors.Actor ob)
    {
        int move;

        move = (int)(BJJUMPSPEED * tics);
        MoveObj(ob, move);
    }


    /*
    ===============
    =
    = T_BJYell
    =
    ===============
    */

    internal static void T_BJYell(Entities.Actors.Actor ob)
    {
        PlaySoundLocActor("misc/yeah", ob);  // JAB
    }


    /*
    ===============
    =
    = T_BJDone
    =
    ===============
    */

    internal static void T_BJDone(Entities.Actors.Actor ob)
    {
        playstate = playstatetypes.ex_victorious;                              // exit castle tile
    }


    internal static void SpawnBJVictory()
    {
        // Same spot the legacy SpawnNewObj used: BJ's tile is the one south of the player's, but
        // he starts at the player's exact position (SpawnAtActor copies the player's x/y).
        var bj = _mapManager.SpawnAtActor("BJVictory", player);
        if (bj == null)
            return;

        bj.TileY = (byte)(player.TileY + 1);
        bj.SyncPosition();
        bj.AreaNumber = (byte)(_mapManager.MAPSPOT(bj.TileX, bj.TileY, 0) - MapDataConstants.AREATILE);

        // SpawnNewObj started every actor a random number of tics into its first frame.
        var firstTicTime = bj.CurrentState?.TicTime ?? 0;
        bj.TicCount = firstTicTime != 0 ? (short)(US_RndT() % firstTicTime + 1) : (short)0;

        bj.Dir = objdirtypes.north;
        bj.Temp1 = 6;                      // tiles to run forward
    }

    //===========================================================================


    /*
    ===============
    =
    = CheckPosition
    =
    ===============
    */
    internal static bool CheckPosition(Entities.Actors.Actor ob)
    {
        int x, y, xl, yl, xh, yh;
        Actor? check;

        xl = (int)((ob.X - PLAYERSIZE) >> MapConstants.TILESHIFT);
        yl = (int)((ob.Y - PLAYERSIZE) >> MapConstants.TILESHIFT);

        xh = (int)((ob.X + PLAYERSIZE) >> MapConstants.TILESHIFT);
        yh = (int)((ob.Y + PLAYERSIZE) >> MapConstants.TILESHIFT);

        //
        // check for solid walls
        //
        for (y = yl; y <= yh; y++)
        {
            for (x = xl; x <= xh; x++)
            {
                check = _mapManager.actorat[x, y];
                if (check != null && check is not objstruct)
                    return false;
            }
        }

        return true;
    }
}
