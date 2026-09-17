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
    // Everything below is still legacy/objstruct-based on purpose: projectiles (Rocket/
    // Smoke/Boom/Needle/Fire) and the BJ-victory end-of-episode cutscene weren't part of
    // that migration, and SelectPathDir/CheckPosition (objstruct) are still used by the
    // BJ-victory think functions and by the new Program.EnemyAI.cs's A_StartDeathCam
    // respectively.

    internal static statestruct s_rocket = new (1, "ROCKA", 3, T_Projectile, A_Smoke, "s_rocket" );
    internal static statestruct s_smoke1 = new(0, "SMOKA", 3, null, null, "s_smoke2" );
    internal static statestruct s_smoke2 = new(0, "SMOKB", 3, null, null, "s_smoke3" );
    internal static statestruct s_smoke3 = new(0, "SMOKC", 3, null, null, "s_smoke4" );
    internal static statestruct s_smoke4 = new(0, "SMOKD", 3, null, null, null );

    internal static statestruct s_boom1 = new(0, "BOOMA", 6, null, null, "s_boom2" );
    internal static statestruct s_boom2 = new(0, "BOOMB", 6, null, null, "s_boom3" );
    internal static statestruct s_boom3 = new(0, "BOOMC", 6, null, null, null);

    // Thrown by Schabbs (T_SchabbThrow, Program.EnemyAI.cs).
    internal static statestruct s_needle1 = new(0, "HYPOA", 6, T_Projectile, null, "s_needle2");
    internal static statestruct s_needle2 = new(0, "HYPOB", 6, T_Projectile, null, "s_needle3");
    internal static statestruct s_needle3 = new(0, "HYPOC", 6, T_Projectile, null, "s_needle4");
    internal static statestruct s_needle4 = new(0, "HYPOD", 6, T_Projectile, null, "s_needle1");

    // Fake Hitler's flamethrower stream (T_FakeFire, Program.EnemyAI.cs).
    internal static statestruct s_fire1 = new(0, "FIREA", 6, T_Projectile, null, "s_fire2");
    internal static statestruct s_fire2 = new(0, "FIREB", 6, T_Projectile, null, "s_fire1");

    /*
    =================
    =
    = A_Smoke
    =
    =================
    */

    internal static void A_Smoke(objstruct ob)
    {
        objstruct newobj = null!;

        newobj = GetNewActor();
//# ifdef SPEAR
//        if (ob->obclass == hrocketobj)
//            newobj->state = &s_hsmoke1;
//        else
//#endif
        newobj.state = s_smoke1;
        newobj.ticcount = 6;

        newobj.tilex = ob.tilex;
        newobj.tiley = ob.tiley;
        newobj.x = ob.x;
        newobj.y = ob.y;
        newobj.obclass = classtypes.inertobj;
        newobj.active = activetypes.ac_yes;

        newobj.flags = objflags.FL_NEVERMARK;
    }


    /*
    ===================
    =
    = ProjectileTryMove
    =
    = returns true if move ok
    ===================
    */

    internal const int PROJSIZE = 0x2000;

    internal static bool ProjectileTryMove(objstruct ob)
    {
        int xl, yl, xh, yh, x, y;
        Actor? check;

        xl = (ob.x - PROJSIZE) >> MapConstants.TILESHIFT;
        yl = (ob.y - PROJSIZE) >> MapConstants.TILESHIFT;

        xh = (ob.x + PROJSIZE) >> MapConstants.TILESHIFT;
        yh = (ob.y + PROJSIZE) >> MapConstants.TILESHIFT;

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

    internal static void T_Projectile(objstruct ob)
    {
        long deltax, deltay;
        int damage = 0;
        int speed;

        speed = (int)(ob.speed * tics);

        deltax = MathUtils.FixedMul(speed, costable[ob.angle]);
        deltay = -MathUtils.FixedMul(speed, sintable[ob.angle]);

        if (deltax > 0x10000L)
            deltax = 0x10000L;
        if (deltay > 0x10000L)
            deltay = 0x10000L;

        ob.x += (int)deltax;
        ob.y += (int)deltay;

        deltax = Math.Abs(ob.x - player.x);
        deltay = Math.Abs(ob.y - player.y);

        if (!ProjectileTryMove(ob))
        {
            if (ob.obclass == classtypes.rocketobj)
            {
                PlaySoundLocActor("missile/hit", ob);
                ob.state = s_boom1;
            }
#if SPEAR
            else if (ob->obclass == hrocketobj)
            {
                PlaySoundLocActor(missile/hit", ob);
                ob->state = &s_hboom1;
            }
#endif
            else
                ob.state = null;               // mark for removal

            return;
        }

        if (deltax < PROJECTILESIZE && deltay < PROJECTILESIZE)
        {       // hit the player
            switch (ob.obclass)
            {
                case classtypes.needleobj:
                    damage = (US_RndT() >> 3) + 20;
                    break;
                case classtypes.rocketobj:
                case classtypes.hrocketobj:
                case classtypes.sparkobj:
                    damage = (US_RndT() >> 3) + 30;
                    break;
                case classtypes.fireobj:
                    damage = (US_RndT() >> 3);
                    break;
            }

            TakeDamage(damage, ob);
            ob.state = null;               // mark for removal
            return;
        }

        ob.tilex = (byte)(ob.x >> MapConstants.TILESHIFT);
        ob.tiley = (byte)(ob.y >> MapConstants.TILESHIFT);
    }

    /*
    ============================================================================

                                        BJ VICTORY

    ============================================================================
    */


    //
    // BJ victory
    //
    internal static statestruct s_bjrun1 =  new statestruct(0, "BLAZA", 12, T_BJRun, null, "s_bjrun1s" );
    internal static statestruct s_bjrun1s = new statestruct(0, "BLAZA", 3, null, null, "s_bjrun2" );
    internal static statestruct s_bjrun2 =  new statestruct(0, "BLAZB", 8, T_BJRun, null, "s_bjrun3" );
    internal static statestruct s_bjrun3 =  new statestruct(0, "BLAZC", 12, T_BJRun, null, "s_bjrun3s" );
    internal static statestruct s_bjrun3s = new statestruct(0, "BLAZC", 3, null, null, "s_bjrun4" );
    internal static statestruct s_bjrun4 =  new statestruct(0, "BLAZD", 8, T_BJRun, null, "s_bjrun1" );
    internal static statestruct s_bjjump1 = new statestruct(0, "BLAZE", 14, T_BJJump, null, "s_bjjump2" );
    internal static statestruct s_bjjump2 = new statestruct(0, "BLAZF", 14, T_BJJump, T_BJYell, "s_bjjump3" );
    internal static statestruct s_bjjump3 = new statestruct(0, "BLAZG", 14, T_BJJump, null, "s_bjjump4" );
    internal static statestruct s_bjjump4 = new statestruct(0, "BLAZH", 300, null, T_BJDone, "s_bjjump4" );


    internal static statestruct s_deathcam = new statestruct( 0, "DCAMA", 0, null, null, null );

    /*
    ===============
    =
    = T_BJRun
    =
    ===============
    */

    internal static void T_BJRun(objstruct ob)
    {
        int move;

        move = (int)(BJRUNSPEED * tics);

        while (move != 0)
        {
            if (move < ob.distance)
            {
                MoveObj(ob, move);
                break;
            }


            ob.x = (int)((ob.tilex << MapConstants.TILESHIFT) + MapConstants.TILEGLOBAL / 2);
            ob.y = (int)((ob.tiley << MapConstants.TILESHIFT) + MapConstants.TILEGLOBAL / 2);
            move -= ob.distance;

            SelectPathDir(ob);

            if ((--ob.temp1) == 0)
            {
                NewState(ob, s_bjjump1);
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

    internal static void T_BJJump(objstruct ob)
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

    internal static void T_BJYell(objstruct ob)
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

    internal static void T_BJDone(objstruct ob)
    {
        playstate = playstatetypes.ex_victorious;                              // exit castle tile
    }


    internal static void SpawnBJVictory()
    {
        objstruct newobj;

        newobj = SpawnNewObj(player.tilex, (uint)(player.tiley + 1), s_bjrun1);
        newobj.x = player.x;
        newobj.y = player.y;
        newobj.obclass = classtypes.bjobj;
        newobj.dir = objdirtypes.north;
        newobj.temp1 = 6;                      // tiles to run forward
    }

    // Named next-state lookup for the legacy statestruct chain (Program.WL_DEF.cs) --
    // still needed by Program.DoActor's `enemy_states.TryGetValue(ob.state.next, ...)`
    // resolution and by save/load's EnemyStateList indexing (Program.cs), both of which
    // predate this migration and serve every objlist2 actor, not just enemies. Now that
    // enemies themselves are gone from objlist2, this only needs to carry the states the
    // remaining legacy systems (projectiles, BJ victory) actually reference.
    internal static Dictionary<string, statestruct> enemy_states = new()
    {
        { "s_rocket", s_rocket},
        { "s_smoke1", s_smoke1},
        { "s_smoke2", s_smoke2},
        { "s_smoke3", s_smoke3},
        { "s_smoke4", s_smoke4},
        { "s_boom1", s_boom1},
        { "s_boom2", s_boom2},
        { "s_boom3", s_boom3},

        { "s_needle1", s_needle1},
        { "s_needle2", s_needle2},
        { "s_needle3", s_needle3},
        { "s_needle4", s_needle4},

        { "s_fire1", s_fire1},
        { "s_fire2", s_fire2},

        { "s_bjrun1", s_bjrun1 },
        { "s_bjrun1s",s_bjrun1s},
        { "s_bjrun2", s_bjrun2 },
        { "s_bjrun3", s_bjrun3 },
        { "s_bjrun3s",s_bjrun3s},
        { "s_bjrun4", s_bjrun4 },
        { "s_bjjump1",s_bjjump1},
        { "s_bjjump2",s_bjjump2},
        { "s_bjjump3",s_bjjump3},
        { "s_bjjump4",s_bjjump4},
    };

    internal static List<statestruct> EnemyStateList => enemy_states.Values.ToList();

    /*
    ===============
    =
    = SelectPathDir
    =
    ===============
    */

    internal static void SelectPathDir(objstruct ob)
    {
        uint spot;

        spot = (uint)(_mapManager.MAPSPOT(ob.tilex, ob.tiley, 1) - MapDataConstants.ICONARROWS);

        if (spot < 8)
        {
            // new direction
            ob.dir = (objdirtypes)spot;
        }

        ob.distance = (int)MapConstants.TILEGLOBAL;

        if (!TryWalk(ob))
            ob.dir = objdirtypes.nodir;
    }

    //===========================================================================


    /*
    ===============
    =
    = CheckPosition
    =
    ===============
    */
    internal static bool CheckPosition(objstruct ob)
    {
        int x, y, xl, yl, xh, yh;
        Actor? check;

        xl = (int)((ob.x - PLAYERSIZE) >> MapConstants.TILESHIFT);
        yl = (int)((ob.y - PLAYERSIZE) >> MapConstants.TILESHIFT);

        xh = (int)((ob.x + PLAYERSIZE) >> MapConstants.TILESHIFT);
        yh = (int)((ob.y + PLAYERSIZE) >> MapConstants.TILESHIFT);

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
