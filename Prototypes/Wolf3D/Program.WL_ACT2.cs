using System.Data;
using static System.Runtime.InteropServices.JavaScript.JSType;

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


    /*
    =============================================================================

                                  LOCAL VARIABLES

    =============================================================================
    */

    internal static short[,] starthitpoints = new short[4, (int)enemytypes.NUMENEMIES]
//
// BABY MODE
//
{
    {
        25,   // guards
        50,   // officer
        100,  // SS
        1,    // dogs
        850,  // Hans
        850,  // Schabbs
        200,  // fake hitler
        800,  // mecha hitler
        45,   // mutants
        25,   // ghosts
        25,   // ghosts
        25,   // ghosts
        25,   // ghosts

        850,  // Gretel
        850,  // Gift
        850,  // Fat
        5,    // en_spectre,
        1450, // en_angel,
        850,  // en_trans,
        1050, // en_uber,
        950,  // en_will,
        1250  // en_death
    },
    //
    // DON'T HURT ME MODE
    //
    {
        25,   // guards
        50,   // officer
        100,  // SS
        1,    // dogs
        950,  // Hans
        950,  // Schabbs
        300,  // fake hitler
        950,  // mecha hitler
        55,   // mutants
        25,   // ghosts
        25,   // ghosts
        25,   // ghosts
        25,   // ghosts

        950,  // Gretel
        950,  // Gift
        950,  // Fat
        10,   // en_spectre,
        1550, // en_angel,
        950,  // en_trans,
        1150, // en_uber,
        1050, // en_will,
        1350  // en_death
    },
    //
    // BRING 'EM ON MODE
    //
    {
        25,   // guards
        50,   // officer
        100,  // SS
        1,    // dogs

        1050, // Hans
        1550, // Schabbs
        400,  // fake hitler
        1050, // mecha hitler

        55,   // mutants
        25,   // ghosts
        25,   // ghosts
        25,   // ghosts
        25,   // ghosts

        1050, // Gretel
        1050, // Gift
        1050, // Fat
        15,   // en_spectre,
        1650, // en_angel,
        1050, // en_trans,
        1250, // en_uber,
        1150, // en_will,
        1450  // en_death
    },
    //
    // DEATH INCARNATE MODE
    //
    {
        25,   // guards
        50,   // officer
        100,  // SS
        1,    // dogs

        1200, // Hans
        2400, // Schabbs
        500,  // fake hitler
        1200, // mecha hitler

        65,   // mutants
        25,   // ghosts
        25,   // ghosts
        25,   // ghosts
        25,   // ghosts

        1200, // Gretel
        1200, // Gift
        1200, // Fat
        25,   // en_spectre,
        2000, // en_angel,
        1200, // en_trans,
        1400, // en_uber,
        1300, // en_will,
        1600  // en_death
    }
};


    /*
    =============================================================================

    GUARD

    =============================================================================
    */

    //
    // guards
    //

    
    internal static statestruct s_grdstand = new statestruct(1, (short)spritenums.SPR_GRD_S_1, 0, T_Stand, null, "s_grdstand");

    internal static statestruct s_grdpath1 = new statestruct(1, (short)spritenums.SPR_GRD_W1_1, 20, T_Path, null, "s_grdpath1s");
    internal static statestruct s_grdpath1s = new statestruct(1, (short)spritenums.SPR_GRD_W1_1, 5, null, null, "s_grdpath2");
    internal static statestruct s_grdpath2 = new statestruct(1, (short)spritenums.SPR_GRD_W2_1, 15, T_Path, null, "s_grdpath3");
    internal static statestruct s_grdpath3 = new statestruct(1, (short)spritenums.SPR_GRD_W3_1, 20, T_Path, null, "s_grdpath3s");
    internal static statestruct s_grdpath3s = new statestruct(1, (short)spritenums.SPR_GRD_W3_1, 5, null, null, "s_grdpath4");
    internal static statestruct s_grdpath4 = new statestruct(1, (short)spritenums.SPR_GRD_W4_1, 15, T_Path, null, "s_grdpath1");
    
    internal static statestruct s_grdpain =  new statestruct( 2, (short)spritenums.SPR_GRD_PAIN_1, 10, null, null, "s_grdchase1" );
    internal static statestruct s_grdpain1 = new statestruct(2, (short)spritenums.SPR_GRD_PAIN_2, 10, null, null, "s_grdchase1" );

    internal static statestruct s_grdshoot1 = new statestruct(0, (short)spritenums.SPR_GRD_SHOOT1, 20, null, null, "s_grdshoot2" );
    internal static statestruct s_grdshoot2 = new statestruct(0, (short)spritenums.SPR_GRD_SHOOT2, 20, null, T_Shoot, "s_grdshoot3" );
    internal static statestruct s_grdshoot3 = new statestruct(0, (short)spritenums.SPR_GRD_SHOOT3, 20, null, null, "s_grdchase1" );

    internal static statestruct s_grdchase1 =  new statestruct( 1, (short)spritenums.SPR_GRD_W1_1, 10, T_Chase, null, "s_grdchase1s" );
    internal static statestruct s_grdchase1s = new statestruct( 1, (short)spritenums.SPR_GRD_W1_1, 3, null, null, "s_grdchase2" );
    internal static statestruct s_grdchase2 =  new statestruct( 1, (short)spritenums.SPR_GRD_W2_1, 8, T_Chase, null, "s_grdchase3" );
    internal static statestruct s_grdchase3 =  new statestruct( 1, (short)spritenums.SPR_GRD_W3_1, 10, T_Chase, null, "s_grdchase3s" );
    internal static statestruct s_grdchase3s = new statestruct( 1, (short)spritenums.SPR_GRD_W3_1, 3, null, null, "s_grdchase4" );
    internal static statestruct s_grdchase4 = new statestruct( 1, (short)spritenums.SPR_GRD_W4_1, 8, T_Chase, null, "s_grdchase1" );

    internal static statestruct s_grddie1 =  new statestruct(0, (short)spritenums.SPR_GRD_DIE_1, 15, null, A_DeathScream, "s_grddie2" );
    internal static statestruct s_grddie2 =  new statestruct(0, (short)spritenums.SPR_GRD_DIE_2, 15, null, null, "s_grddie3" );
    internal static statestruct s_grddie3 = new statestruct(0, (short)spritenums.SPR_GRD_DIE_3, 15, null, null, "s_grddie4" );
    internal static statestruct s_grddie4 = new statestruct(0, (short)spritenums.SPR_GRD_DEAD, 0, null, null, "s_grddie4");

    /*
    ============================================================================

                                        BJ VICTORY

    ============================================================================
    */


    //
    // BJ victory
    //
    internal static statestruct s_bjrun1 =  new statestruct(0, (short)spritenums.SPR_BJ_W1, 12, T_BJRun, null, "s_bjrun1s" );
    internal static statestruct s_bjrun1s = new statestruct(0, (short)spritenums.SPR_BJ_W1, 3, null, null, "s_bjrun2" );
    internal static statestruct s_bjrun2 =  new statestruct(0, (short)spritenums.SPR_BJ_W2, 8, T_BJRun, null, "s_bjrun3" );
    internal static statestruct s_bjrun3 =  new statestruct(0, (short)spritenums.SPR_BJ_W3, 12, T_BJRun, null, "s_bjrun3s" );
    internal static statestruct s_bjrun3s = new statestruct(0, (short)spritenums.SPR_BJ_W3, 3, null, null, "s_bjrun4" );
    internal static statestruct s_bjrun4 =  new statestruct(0, (short)spritenums.SPR_BJ_W4, 8, T_BJRun, null, "s_bjrun1" );
    internal static statestruct s_bjjump1 = new statestruct(0, (short)spritenums.SPR_BJ_JUMP1, 14, T_BJJump, null, "s_bjjump2" );
    internal static statestruct s_bjjump2 = new statestruct(0, (short)spritenums.SPR_BJ_JUMP2, 14, T_BJJump, T_BJYell, "s_bjjump3" );
    internal static statestruct s_bjjump3 = new statestruct(0, (short)spritenums.SPR_BJ_JUMP3, 14, T_BJJump, null, "s_bjjump4" );
    internal static statestruct s_bjjump4 = new statestruct(0, (short)spritenums.SPR_BJ_JUMP4, 300, null, T_BJDone, "s_bjjump4" );


    internal static statestruct s_deathcam = new statestruct( 0, 0, 0, null, null, null );

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


            ob.x = (int)((ob.tilex << TILESHIFT) + TILEGLOBAL / 2);
            ob.y = (int)((ob.tiley << TILESHIFT) + TILEGLOBAL / 2);
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
        PlaySoundLocActor((int)soundnames.YEAHSND, ob);  // JAB
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
        playstate = (byte)playstatetypes.ex_victorious;                              // exit castle tile
    }


    internal static void SpawnBJVictory()
    {
        objstruct newobj = null;

        newobj = SpawnNewObj(player.tilex, (uint)(player.tiley + 1), s_bjrun1);
        newobj.x = player.x;
        newobj.y = player.y;
        newobj.obclass = (byte)classtypes.bjobj;
        newobj.dir = (byte)objdirtypes.north;
        newobj.temp1 = 6;                      // tiles to run forward
    }


    internal static Dictionary<string, statestruct> enemy_states = new()
    {
        { "s_grdstand",   s_grdstand },
        { "s_grdpath1",   s_grdpath1 },
        { "s_grdpath1s",  s_grdpath1s },
        { "s_grdpath2",   s_grdpath2 },
        { "s_grdpath3",   s_grdpath3 },
        { "s_grdpath3s",  s_grdpath3s },
        { "s_grdpath4",   s_grdpath4 },

        { "s_grdpain",    s_grdpain },
        { "s_grdpain1",   s_grdpain1 },

        { "s_grdshoot1",  s_grdshoot1 },
        { "s_grdshoot2",  s_grdshoot2 },
        { "s_grdshoot3",  s_grdshoot3 },

        { "s_grdchase1",  s_grdchase1 },
        { "s_grdchase1s", s_grdchase1s },
        { "s_grdchase2",  s_grdchase2 },
        { "s_grdchase3",  s_grdchase3 },
        { "s_grdchase3s", s_grdchase3s },
        { "s_grdchase4",  s_grdchase4 },

        { "s_grddie1",    s_grddie1 },
        { "s_grddie2",    s_grddie2 },
        { "s_grddie3",    s_grddie3 },
        { "s_grddie4",    s_grddie4 },

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

    internal static void SpawnStand(int which, int tilex, int tiley, int dir)
    {
        objstruct? newobj = null;
        int tile;

        switch ((enemytypes)which)
        {
            case enemytypes.en_guard:
                newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_grdstand);
                newobj.speed = SPDPATROL;
                if (!loadedgame)
                    gamestate.killtotal++;
                break;

            //case en_officer:
            //    newobj = SpawnNewObj(tilex, tiley, &s_ofcstand);
            //    newobj->speed = SPDPATROL;
            //    if (!loadedgame)
            //        gamestate.killtotal++;
            //    break;

            //case en_mutant:
            //    newobj = SpawnNewObj(tilex, tiley, &s_mutstand);
            //    newobj->speed = SPDPATROL;
            //    if (!loadedgame)
            //        gamestate.killtotal++;
            //    break;

            //case en_ss:
            //    newobj = SpawnNewObj(tilex, tiley, &s_ssstand);
            //    newobj->speed = SPDPATROL;
            //    if (!loadedgame)
            //        gamestate.killtotal++;
            //    break;
        }


        tile = MAPSPOT(tilex, tiley, 0);
        if (tile == AMBUSHTILE)
        {
            if (VALIDAREA(MAPSPOT(tilex + 1, tiley, 0)))
                tile = MAPSPOT(tilex + 1, tiley, 0);
            if (VALIDAREA(MAPSPOT(tilex, tiley - 1, 0)))
                tile = MAPSPOT(tilex, tiley - 1, 0);
            if (VALIDAREA(MAPSPOT(tilex, tiley + 1, 0)))
                tile = MAPSPOT(tilex, tiley + 1, 0);
            if (VALIDAREA(MAPSPOT(tilex - 1, tiley, 0)))
                tile = MAPSPOT(tilex - 1, tiley, 0);

            SetMapSpot(tilex, tiley, 0, (ushort)tile);
            newobj.areanumber = (byte)(tile - AREATILE);

            newobj.flags |= (uint)objflags.FL_AMBUSH;
        }

        newobj.obclass = (byte)(classtypes.guardobj + which);
        newobj.hitpoints = starthitpoints[gamestate.difficulty, which];
        newobj.dir = (byte)(dir * 2);
        newobj.flags |= (uint)objflags.FL_SHOOTABLE;
    }

    internal static void SpawnPatrol(int which, int tilex, int tiley, int dir)
    {
        objstruct newobj = null!;

        switch ((enemytypes)which)
        {
            case enemytypes.en_guard:
                newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_grdpath1);
                newobj.speed = SPDPATROL;
                if (!loadedgame)
                    gamestate.killtotal++;
                break;

            //case enemytypes.en_officer:
            //    newobj = SpawnNewObj(tilex, tiley, s_ofcpath1);
            //    newobj.speed = SPDPATROL;
            //    if (!loadedgame)
            //        gamestate.killtotal++;
            //    break;

            //case enemytypes.en_ss:
            //    newobj = SpawnNewObj(tilex, tiley, s_sspath1);
            //    newobj.speed = SPDPATROL;
            //    if (!loadedgame)
            //        gamestate.killtotal++;
            //    break;

            //case enemytypes.en_mutant:
            //    newobj = SpawnNewObj(tilex, tiley, s_mutpath1);
            //    newobj.speed = SPDPATROL;
            //    if (!loadedgame)
            //        gamestate.killtotal++;
            //    break;

            //case enemytypes.en_dog:
            //    newobj = SpawnNewObj(tilex, tiley, s_dogpath1);
            //    newobj.speed = SPDDOG;
            //    if (!loadedgame)
            //        gamestate.killtotal++;
            //    break;
            default:return; // temporary
        }

        newobj.obclass = (byte)(classtypes.guardobj + which);
        newobj.dir = (byte)(dir * 2);
        newobj.hitpoints = starthitpoints[gamestate.difficulty, which];
        newobj.distance = (int)TILEGLOBAL;
        newobj.flags |= (uint)objflags.FL_SHOOTABLE;
        newobj.active = (byte)activetypes.ac_yes;

        //actorat[newobj.tilex, newobj.tiley] = -1;           // don't use original spot

        switch (dir)
        {
            case 0:
                newobj.tilex++;
                break;
            case 1:
                newobj.tiley--;
                break;
            case 2:
                newobj.tilex--;
                break;
            case 3:
                newobj.tiley++;
                break;
        }

        //actorat[newobj.tilex, newobj.tiley] = newobj;
    }

    internal static void SpawnDeadGuard(int tilex, int tiley)
    {
        objstruct newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_grddie4);
        newobj.flags |= (uint)objflags.FL_NONMARK; // walk through moving enemy fix
        newobj.obclass = (byte)classtypes.inertobj;
    }

    internal static void SpawnBoss(int tilex, int tiley)
    {

    }

    internal static void SpawnGretel(int tilex, int tiley)
    {

    }

    internal static void SpawnFakeHitler(int tilex, int tiley)
    {

    }
    internal static void SpawnHitler(int tilex, int tiley)
    {

    }
    internal static void SpawnGift(int tilex, int tiley)
    {

    }
    internal static void SpawnGhosts(int which, int tilex, int tiley)
    {

    }


    internal static void SpawnSchabbs(int tilex, int tiley)
    {

    }

    internal static void SpawnFat(int tilex, int tiley)
    {

    }

    internal static void T_Stand(objstruct ob)
    {
        SightPlayer(ob);
    }

    internal static void T_Chase(objstruct ob)
    {
        int move, target;
        int dx, dy, dist, chance;
        bool dodge;

        if (gamestate.victoryflag != 0)
            return;

        dodge = false;
        if (CheckLine(ob))      // got a shot at player?
        {
            ob.hidden = false;
            dx = Math.Abs(ob.tilex - player.tilex);
            dy = Math.Abs(ob.tiley - player.tiley);
            dist = dx > dy ? dx : dy;

            {
                if (dist != 0)
                    chance = (int)((tics << 4) / dist);
                else
                    chance = 300;

                if (dist == 1)
                {
                    target = Math.Abs(ob.x - player.x);
                    if (target < 0x14000l)
                    {
                        target = Math.Abs(ob.y - player.y);
                        if (target < 0x14000l)
                            chance = 300;
                    }
                }
            }

            if (US_RndT() < chance)
            {
                //
                // go into attack frame
                //
                switch ((classtypes)ob.obclass)
                {
                    case classtypes.guardobj:
                        NewState(ob, s_grdshoot1);
                        break;
//                    case officerobj:
//                        NewState(ob, s_ofcshoot1);
//                        break;
//                    case mutantobj:
//                        NewState(ob, &s_mutshoot1);
//                        break;
//                    case ssobj:
//                        NewState(ob, &s_ssshoot1);
//                        break;
//# ifndef SPEAR
//                    case bossobj:
//                        NewState(ob, &s_bossshoot1);
//                        break;
//                    case gretelobj:
//                        NewState(ob, &s_gretelshoot1);
//                        break;
//                    case mechahitlerobj:
//                        NewState(ob, &s_mechashoot1);
//                        break;
//                    case realhitlerobj:
//                        NewState(ob, &s_hitlershoot1);
//                        break;
//#else
//                    case angelobj:
//                        NewState(ob, &s_angelshoot1);
//                        break;
//                    case transobj:
//                        NewState(ob, &s_transshoot1);
//                        break;
//                    case uberobj:
//                        NewState(ob, &s_ubershoot1);
//                        break;
//                    case willobj:
//                        NewState(ob, &s_willshoot1);
//                        break;
//                    case deathobj:
//                        NewState(ob, &s_deathshoot1);
//                        break;
//#endif
                }
                return;
            }
            dodge = true;
        }
        else
            ob.hidden = true;

        if (ob.dir == (byte)objdirtypes.nodir)
        {
            if (dodge)
                SelectDodgeDir(ob);
            else
                SelectChaseDir(ob);
            if (ob.dir == (byte)objdirtypes.nodir)
                return;                                                 // object is blocked in
        }

        move = (int)(ob.speed * tics);

        while (move != 0)
        {
            if (ob.distance < 0)
            {
                //
                // waiting for a door to open
                //
                OpenDoor(-ob.distance - 1);
                if (doorobjlist[-ob.distance - 1].action != (byte)dooractiontypes.dr_open)
                    return;
                ob.distance = (int)TILEGLOBAL;      // go ahead, the door is now open
                if (!((demorecord || demoplayback)))
                {
                    TryWalk(ob);
                }
            }

            if (move < ob.distance)
            {
                MoveObj(ob, move);
                break;
            }

            //
            // reached goal tile, so select another one
            //

            //
            // fix position to account for round off during moving
            //
            ob.x = (int)((ob.tilex << TILESHIFT) + TILEGLOBAL / 2);
            ob.y = (int)((ob.tiley << TILESHIFT) + TILEGLOBAL / 2);

            move -= ob.distance;

            if (dodge)
                SelectDodgeDir(ob);
            else
                SelectChaseDir(ob);

            if (ob.dir == (byte)objdirtypes.nodir)
                return;                                                 // object is blocked in
        }
    }

    internal static void T_Path(objstruct ob)
    {
        int move;

        if (SightPlayer(ob))
            return;

        if (ob.dir == (byte)objdirtypes.nodir)
        {
            SelectPathDir(ob);
            if (ob.dir == (byte)objdirtypes.nodir)
                return;                                 // all movement is blocked
        }


        move = (int)(ob.speed * tics);

        while (move != 0)
        {
            if (ob.distance < 0)
            {
                //
                // waiting for a door to open
                //
                OpenDoor(-ob.distance - 1);
                if (doorobjlist[-ob.distance - 1].action != (byte)dooractiontypes.dr_open)
                    return;
                ob.distance = (int)TILEGLOBAL;      // go ahead, the door is now open
                if (!((demorecord || demoplayback)))
                {
                    TryWalk(ob);
                }
            }

            if (move < ob.distance)
            {
                MoveObj(ob, move);
                break;
            }

            if (ob.tilex > MAPSIZE || ob.tiley > MAPSIZE)
                Quit($"T_Path hit a wall at {ob.tilex},{ob.tiley}, dir {ob.dir}");

            ob.x = (int)((ob.tilex << TILESHIFT) + TILEGLOBAL / 2);
            ob.y = (int)((ob.tiley << TILESHIFT) + TILEGLOBAL / 2);
            move -= ob.distance;

            SelectPathDir(ob);

            if (ob.dir == (byte)objdirtypes.nodir)
                return;                                 // all movement is blocked
        }
    }

    internal static void T_Shoot(objstruct ob)
    {
        int dx, dy, dist;
        int hitchance, damage;

        hitchance = 128;

        if (ob.areanumber < NUMAREAS && areabyplayer[ob.areanumber] == 0)
            return;

        if (CheckLine(ob))                    // player is not behind a wall
        {
            dx = Math.Abs(ob.tilex - player.tilex);
            dy = Math.Abs(ob.tiley - player.tiley);
            dist = dx > dy ? dx : dy;

            if (ob.obclass == (byte)classtypes.ssobj || ob.obclass == (byte)classtypes.bossobj)
                dist = dist * 2 / 3;                                        // ss are better shots

            if (thrustspeed >= RUNSPEED)
            {
                if ((ob.flags & (uint)objflags.FL_VISABLE) != 0)
                    hitchance = 160 - dist * 16;                // player can see to dodge
                else
                    hitchance = 160 - dist * 8;
            }
            else
            {
                if ((ob.flags & (uint)objflags.FL_VISABLE) != 0)
                    hitchance = 256 - dist * 16;                // player can see to dodge
                else
                    hitchance = 256 - dist * 8;
            }

            // see if the shot was a hit

            if (US_RndT() < hitchance)
            {
                if (dist < 2)
                    damage = US_RndT() >> 2;
                else if (dist < 4)
                    damage = US_RndT() >> 3;
                else
                    damage = US_RndT() >> 4;

                TakeDamage(damage, ob);
            }
        }

        switch ((classtypes)ob.obclass)
        {
            case classtypes.ssobj:
                PlaySoundLocActor((int)soundnames.SSFIRESND, ob);
                break;
            case classtypes.giftobj:
            case classtypes.fatobj:
                PlaySoundLocActor((int)soundnames.MISSILEFIRESND, ob);
                break;
            case classtypes.mechahitlerobj:
            case classtypes.realhitlerobj:
            case classtypes.bossobj:
                PlaySoundLocActor((int)soundnames.BOSSFIRESND, ob);
                break;
            case classtypes.schabbobj:
                PlaySoundLocActor((int)soundnames.SCHABBSTHROWSND, ob);
                break;
            case classtypes.fakeobj:
                PlaySoundLocActor((int)soundnames.FLAMETHROWERSND, ob);
                break;
            default:
                PlaySoundLocActor((int)soundnames.NAZIFIRESND, ob);
                break;
        }
    }

    internal static void A_DeathScream(objstruct ob)
    {
        if (gamestate.mapon == 9 && US_RndT() == 0)
        {
            switch ((classtypes)ob.obclass)
            {
                case classtypes.mutantobj:
                case classtypes.guardobj:
                case classtypes.officerobj:
                case classtypes.ssobj:
                case classtypes.dogobj:
                    PlaySoundLocActor((int)soundnames.DEATHSCREAM6SND, ob);
                    return;
            }
        }

        switch ((classtypes)ob.obclass)
        {
            case classtypes.mutantobj:
                PlaySoundLocActor((int)soundnames.AHHHGSND, ob);
                break;

            case classtypes.guardobj:
                {
                    soundnames[] sounds ={  soundnames.DEATHSCREAM1SND,
                                            soundnames.DEATHSCREAM2SND,
                                            soundnames.DEATHSCREAM3SND,
                                            soundnames.DEATHSCREAM4SND,
                                            soundnames.DEATHSCREAM5SND,
                                            soundnames.DEATHSCREAM7SND,
                                            soundnames.DEATHSCREAM8SND,
                                            soundnames.DEATHSCREAM9SND
                                };
                    PlaySoundLocActor((int)sounds[US_RndT() % 8], ob);
                    break;
                }
            case classtypes.officerobj:
                PlaySoundLocActor((int)soundnames.NEINSOVASSND, ob);
                break;
            case classtypes.ssobj:
                PlaySoundLocActor((int)soundnames.LEBENSND, ob); // JAB
                break;
            case classtypes.dogobj:
                PlaySoundLocActor((int)soundnames.DOGDEATHSND, ob);      // JAB
                break;
            case classtypes.bossobj:
                SD_PlaySound((int)soundnames.MUTTISND);                         // JAB
                break;
            case classtypes.schabbobj:
                SD_PlaySound((int)soundnames.MEINGOTTSND);
                break;
            case classtypes.fakeobj:
                SD_PlaySound((int)soundnames.HITLERHASND);
                break;
            case classtypes.mechahitlerobj:
                SD_PlaySound((int)soundnames.SCHEISTSND);
                break;
            case classtypes.realhitlerobj:
                SD_PlaySound((int)soundnames.EVASND);
                break;
            case classtypes.gretelobj:
                SD_PlaySound((int)soundnames.MEINSND);
                break;
            case classtypes.giftobj:
                SD_PlaySound((int)soundnames.DONNERSND);
                break;
            case classtypes.fatobj:
                SD_PlaySound((int)soundnames.ROSESND);
                break;

        }
    }

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

        spot = (uint)(MAPSPOT(ob.tilex, ob.tiley, 1) - ICONARROWS);

        if (spot < 8)
        {
            // new direction
            ob.dir = (byte)spot;
        }

        ob.distance = (int)TILEGLOBAL;

        if (!TryWalk(ob))
            ob.dir = (byte)objdirtypes.nodir;
    }

}
