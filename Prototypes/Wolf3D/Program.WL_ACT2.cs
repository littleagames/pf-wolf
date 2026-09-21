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
    // Projectiles (Rocket/Smoke/Boom/Needle/Fire, actordefs/wolf3d/projectiles.yaml) now run
    // on the new Entities.Actors.Actor type, spawned into MapManager._actors by
    // MapManager.SpawnAtActor. Only the BJ-victory end-of-episode cutscene is still
    // legacy/objstruct-based, along with SelectPathDir which its think functions use.
    // CheckPosition takes the new Actor type: its only caller is the player pawn in the new
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

        newobj = SpawnNewObj(player.TileX, (uint)(player.TileY + 1), s_bjrun1);
        newobj.x = player.X;
        newobj.y = player.Y;
        newobj.obclass = classtypes.bjobj;
        newobj.dir = objdirtypes.north;
        newobj.temp1 = 6;                      // tiles to run forward
    }

    // Named next-state lookup for the legacy statestruct chain (Program.WL_DEF.cs) --
    // still needed by Program.DoActor's `enemy_states.TryGetValue(ob.state.next, ...)`
    // resolution and by save/load's EnemyStateList indexing (Program.cs), both of which
    // predate this migration and serve every objlist2 actor, not just enemies. Now that
    // enemies themselves are gone from objlist2, this only needs to carry the states the
    // remaining legacy system (BJ victory) actually references.
    internal static Dictionary<string, statestruct> enemy_states = new()
    {
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
