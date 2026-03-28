using System.Data;
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


    internal static statestruct s_rocket = new (1, "ROCKA1", 3, T_Projectile, A_Smoke, "s_rocket" );
    internal static statestruct s_smoke1 = new(0, "SMOKA0", 3, null, null, "s_smoke2" );
    internal static statestruct s_smoke2 = new(0, "SMOKB0", 3, null, null, "s_smoke3" );
    internal static statestruct s_smoke3 = new(0, "SMOKC0", 3, null, null, "s_smoke4" );
    internal static statestruct s_smoke4 = new(0, "SMOKD0", 3, null, null, null );

    internal static statestruct s_boom1 = new(0, "BOOMA0", 6, null, null, "s_boom2" );
    internal static statestruct s_boom2 = new(0, "BOOMB0", 6, null, null, "s_boom3" );
    internal static statestruct s_boom3 = new(0, "BOOMC0", 6, null, null, null);

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

        xl = (ob.x - PROJSIZE) >> TILESHIFT;
        yl = (ob.y - PROJSIZE) >> TILESHIFT;

        xh = (ob.x + PROJSIZE) >> TILESHIFT;
        yh = (ob.y + PROJSIZE) >> TILESHIFT;

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

        deltax = FixedMul(speed, costable[ob.angle]);
        deltay = -FixedMul(speed, sintable[ob.angle]);

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
                PlaySoundLocActor((int)soundnames.MISSILEHITSND, ob);
                ob.state = s_boom1;
            }
#if SPEAR
            else if (ob->obclass == hrocketobj)
            {
                PlaySoundLocActor(MISSILEHITSND, ob);
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

        ob.tilex = (byte)(ob.x >> TILESHIFT);
        ob.tiley = (byte)(ob.y >> TILESHIFT);
    }

    /*
    =============================================================================

    GUARD

    =============================================================================
    */

    //
    // guards
    //


    internal static statestruct s_grdstand = new statestruct(1, "GARDA", 0, T_Stand, null, "s_grdstand");

    internal static statestruct s_grdpath1 = new statestruct(1, "GARDB", 20, T_Path, null, "s_grdpath1s");
    internal static statestruct s_grdpath1s = new statestruct(1, "GARDB", 5, null, null, "s_grdpath2");
    internal static statestruct s_grdpath2 = new statestruct(1, "GARDC", 15, T_Path, null, "s_grdpath3");
    internal static statestruct s_grdpath3 = new statestruct(1, "GARDD", 20, T_Path, null, "s_grdpath3s");
    internal static statestruct s_grdpath3s = new statestruct(1, "GARDD", 5, null, null, "s_grdpath4");
    internal static statestruct s_grdpath4 = new statestruct(1, "GARDE", 15, T_Path, null, "s_grdpath1");
    
    internal static statestruct s_grdpain =  new statestruct(2, "GARDI", 10, null, null, "s_grdchase1" );
    internal static statestruct s_grdpain1 = new statestruct(2, "GARDJ", 10, null, null, "s_grdchase1" );

    internal static statestruct s_grdshoot1 = new statestruct(0, "GARDF", 20, null, null, "s_grdshoot2" );
    internal static statestruct s_grdshoot2 = new statestruct(0, "GARDG", 20, null, T_Shoot, "s_grdshoot3" );
    internal static statestruct s_grdshoot3 = new statestruct(0, "GARDH", 20, null, null, "s_grdchase1" );

    internal static statestruct s_grdchase1 =  new statestruct(1, "GARDB", 10, T_Chase, null, "s_grdchase1s" );
    internal static statestruct s_grdchase1s = new statestruct(1, "GARDB", 3, null, null, "s_grdchase2" );
    internal static statestruct s_grdchase2 =  new statestruct(1, "GARDC", 8, T_Chase, null, "s_grdchase3" );
    internal static statestruct s_grdchase3 =  new statestruct(1, "GARDD", 10, T_Chase, null, "s_grdchase3s" );
    internal static statestruct s_grdchase3s = new statestruct(1, "GARDD", 3, null, null, "s_grdchase4" );
    internal static statestruct s_grdchase4 =  new statestruct(1, "GARDE", 8, T_Chase, null, "s_grdchase1" );

    internal static statestruct s_grddie1 =  new statestruct(0, "GARDK", 15, null, A_DeathScream, "s_grddie2" );
    internal static statestruct s_grddie2 =  new statestruct(0, "GARDL", 15, null, null, "s_grddie3" );
    internal static statestruct s_grddie3 = new statestruct(0, "GARDM", 15, null, null, "s_grddie4" );
    internal static statestruct s_grddie4 = new statestruct(0, "GARDN", 0, null, null, "s_grddie4");

    //
    // ghosts
    //
    internal static statestruct s_blinkychase1 = new (0, "BLKYA", 10, T_Ghosts, null, "s_blinkychase2" );
    internal static statestruct s_blinkychase2 = new (0, "BLKYB", 10, T_Ghosts, null, "s_blinkychase1" );

    internal static statestruct s_inkychase1 =  new (0, "INKYA", 10, T_Ghosts, null, "s_inkychase2" );
    internal static statestruct s_inkychase2 = new(0, "INKYB", 10, T_Ghosts, null, "s_inkychase1" );

    internal static statestruct s_pinkychase1 = new (0, "PNKYA", 10, T_Ghosts, null, "s_pinkychase2" );
    internal static statestruct s_pinkychase2 = new(0, "PNKYB", 10, T_Ghosts, null, "s_pinkychase1" );

    internal static statestruct s_clydechase1 = new (0, "CLYDA", 10, T_Ghosts, null, "s_clydechase2" );
    internal static statestruct s_clydechase2 = new(0, "CLYDB", 10, T_Ghosts, null, "s_clydechase1" );

    /*
    =================
    =
    = T_Ghosts
    =
    =================
    */

    internal static void T_Ghosts(objstruct ob)
    {
        int move;

        if (ob.dir == objdirtypes.nodir)
        {
            SelectChaseDir(ob);
            if (ob.dir == objdirtypes.nodir)
                return;                                                 // object is blocked in
        }

        move = (int)(ob.speed * tics);

        while (move != 0)
        {
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

            SelectChaseDir(ob);

            if (ob.dir == objdirtypes.nodir)
                return;                                                 // object is blocked in
        }
    }


    internal static statestruct s_dogpath1 = new(1, "DOGYA", 20, T_Path, null, "s_dogpath1s" );
    internal static statestruct s_dogpath1s = new(1, "DOGYA", 5, null, null, "s_dogpath2" );
    internal static statestruct s_dogpath2 = new(1, "DOGYB", 15, T_Path, null, "s_dogpath3" );
    internal static statestruct s_dogpath3 = new(1, "DOGYC", 20, T_Path, null, "s_dogpath3s" );
    internal static statestruct s_dogpath3s = new(1, "DOGYC", 5, null, null, "s_dogpath4" );
    internal static statestruct s_dogpath4 = new(1, "DOGYD", 15, T_Path, null, "s_dogpath1" );

    internal static statestruct s_dogjump1 = new(0, "DOGYE", 10, null, null, "s_dogjump2" );
    internal static statestruct s_dogjump2 = new(0, "DOGYF", 10, null, T_Bite, "s_dogjump3" );
    internal static statestruct s_dogjump3 = new(0, "DOGYG", 10, null, null, "s_dogjump4" );
    internal static statestruct s_dogjump4 = new(0, "DOGYE", 10, null, null, "s_dogjump5" );
    internal static statestruct s_dogjump5 = new(1, "DOGYA", 10, null, null, "s_dogchase1" );

    internal static statestruct s_dogchase1 = new(1, "DOGYA", 10, T_DogChase, null, "s_dogchase1s" );
    internal static statestruct s_dogchase1s = new(1, "DOGYA", 3, null, null, "s_dogchase2" );
    internal static statestruct s_dogchase2 = new(1, "DOGYB", 8, T_DogChase, null, "s_dogchase3" );
    internal static statestruct s_dogchase3 = new(1, "DOGYC", 10, T_DogChase, null, "s_dogchase3s" );
    internal static statestruct s_dogchase3s = new(1, "DOGYC", 3, null, null, "s_dogchase4" );
    internal static statestruct s_dogchase4 = new(1, "DOGYD", 8, T_DogChase, null, "s_dogchase1" );

    internal static statestruct s_dogdie1 = new(0, "DOGYH", 15, null, A_DeathScream, "s_dogdie2" );
    internal static statestruct s_dogdie2 = new(0, "DOGYI", 15, null, null, "s_dogdie3" );
    internal static statestruct s_dogdie3 = new(0, "DOGYJ", 15, null, null, "s_dogdead" );
    internal static statestruct s_dogdead = new(0, "DOGYK", 15, null, null, "s_dogdead" );


    /*
    ===============
    =
    = T_Bite
    =
    ===============
    */

    internal static void T_Bite(objstruct ob)
    {
        int dx, dy;

        PlaySoundLocActor((int)soundnames.DOGATTACKSND, ob);     // JAB

        dx = player.x - ob.x;
        if (dx < 0)
            dx = -dx;
        dx -= (int)TILEGLOBAL;
        if (dx <= MINACTORDIST)
        {
            dy = player.y - ob.y;
            if (dy < 0)
                dy = -dy;
            dy -= (int)TILEGLOBAL;
            if (dy <= MINACTORDIST)
            {
                if (US_RndT() < 180)
                {
                    TakeDamage(US_RndT() >> 4, ob);
                    return;
                }
            }
        }
    }


    //
    // officers
    //

    internal static statestruct s_ofcstand = new (1, "OFFIA", 0, T_Stand, null, "s_ofcstand" );

    internal static statestruct s_ofcpath1 = new(1, "OFFIB", 20, T_Path, null, "s_ofcpath1s");
    internal static statestruct s_ofcpath1s = new(1, "OFFIB", 5, null, null, "s_ofcpath2");
    internal static statestruct s_ofcpath2 = new(1, "OFFIC", 15, T_Path, null, "s_ofcpath3");
    internal static statestruct s_ofcpath3 = new(1, "OFFID", 20, T_Path, null, "s_ofcpath3s");
    internal static statestruct s_ofcpath3s = new(1, "OFFID", 5, null, null, "s_ofcpath4");
    internal static statestruct s_ofcpath4 = new(1, "OFFIE", 15, T_Path, null, "s_ofcpath1");

    internal static statestruct s_ofcpain = new(2, "OFFII", 10, null, null, "s_ofcchase1");
    internal static statestruct s_ofcpain1 = new(2, "OFFIJ", 10, null, null, "s_ofcchase1");

    internal static statestruct s_ofcshoot1 = new(0, "OFFIF", 6, null, null, "s_ofcshoot2");
    internal static statestruct s_ofcshoot2 = new(0, "OFFIG", 20, null, T_Shoot, "s_ofcshoot3");
    internal static statestruct s_ofcshoot3 = new(0, "OFFIH", 10, null, null, "s_ofcchase1");

    internal static statestruct s_ofcchase1 = new(1, "OFFIB", 10, T_Chase, null, "s_ofcchase1s");
    internal static statestruct s_ofcchase1s = new(1, "OFFIB", 3, null, null, "s_ofcchase2");
    internal static statestruct s_ofcchase2 = new(1, "OFFIC", 8, T_Chase, null, "s_ofcchase3");
    internal static statestruct s_ofcchase3 = new(1, "OFFID", 10, T_Chase, null, "s_ofcchase3s");
    internal static statestruct s_ofcchase3s = new(1, "OFFID", 3, null, null, "s_ofcchase4");
    internal static statestruct s_ofcchase4 = new(1, "OFFIE", 8, T_Chase, null, "s_ofcchase1");

    internal static statestruct s_ofcdie1 = new(0, "OFFIK", 11, null, A_DeathScream, "s_ofcdie2");
    internal static statestruct s_ofcdie2 = new(0, "OFFIL", 11, null, null, "s_ofcdie3");
    internal static statestruct s_ofcdie3 = new(0, "OFFIM", 11, null, null, "s_ofcdie4");
    internal static statestruct s_ofcdie4 = new(0, "OFFIN", 11, null, null, "s_ofcdie5");
    internal static statestruct s_ofcdie5 = new(0, "OFFIO", 0, null, null, "s_ofcdie5");


    //
    // mutant
    //

    internal static statestruct s_mutstand = new(1, "MTNTA", 0, T_Stand, null, "s_mutstand" );

    internal static statestruct s_mutpath1 = new(1, "MTNTB", 20, T_Path, null, "s_mutpath1s" );
    internal static statestruct s_mutpath1s = new(1, "MTNTB", 5, null, null, "s_mutpath2" );
    internal static statestruct s_mutpath2 = new(1, "MTNTC", 15, T_Path, null, "s_mutpath3" );
    internal static statestruct s_mutpath3 = new(1, "MTNTD", 20, T_Path, null, "s_mutpath3s" );
    internal static statestruct s_mutpath3s = new(1, "MTNTD", 5, null, null, "s_mutpath4" );
    internal static statestruct s_mutpath4 = new(1, "MTNTE", 15, T_Path, null, "s_mutpath1" );

    internal static statestruct s_mutpain = new(2, "MTNTJ", 10, null, null, "s_mutchase1" );
    internal static statestruct s_mutpain1 = new(2, "MTNTK", 10, null, null, "s_mutchase1" );

    internal static statestruct s_mutshoot1 = new(0, "MTNTF", 6, null, T_Shoot, "s_mutshoot2" );
    internal static statestruct s_mutshoot2 = new(0, "MTNTG", 20, null, null, "s_mutshoot3" );
    internal static statestruct s_mutshoot3 = new(0, "MTNTH", 10, null, T_Shoot, "s_mutshoot4" );
    internal static statestruct s_mutshoot4 = new(0, "MTNTI", 20, null, null, "s_mutchase1" );

    internal static statestruct s_mutchase1 = new(1, "MTNTB", 10, T_Chase, null, "s_mutchase1s" );
    internal static statestruct s_mutchase1s = new(1, "MTNTB", 3, null, null, "s_mutchase2" );
    internal static statestruct s_mutchase2 = new(1, "MTNTC", 8, T_Chase, null, "s_mutchase3" );
    internal static statestruct s_mutchase3 = new(1, "MTNTD", 10, T_Chase, null, "s_mutchase3s" );
    internal static statestruct s_mutchase3s = new(1, "MTNTD", 3, null, null, "s_mutchase4" );
    internal static statestruct s_mutchase4 = new(1, "MTNTE", 8, T_Chase, null, "s_mutchase1" );

    internal static statestruct s_mutdie1 = new(0, "MTNTL", 7, null, A_DeathScream, "s_mutdie2" );
    internal static statestruct s_mutdie2 = new(0, "MTNTM", 7, null, null, "s_mutdie3" );
    internal static statestruct s_mutdie3 = new(0, "MTNTN", 7, null, null, "s_mutdie4" );
    internal static statestruct s_mutdie4 = new(0, "MTNTO", 7, null, null, "s_mutdie5" );
    internal static statestruct s_mutdie5 = new(0, "MTNTP", 0, null, null, "s_mutdie5" );

    //
    // SS
    //

    internal static statestruct s_ssstand = new(1, "SSWVW", 0, T_Stand, null, "s_ssstand" );

    internal static statestruct s_sspath1 = new(1, "SSWVA", 20, T_Path, null, "s_sspath1s" );
    internal static statestruct s_sspath1s = new(1, "SSWVA", 5, null, null, "s_sspath2" );
    internal static statestruct s_sspath2 = new(1, "SSWVB", 15, T_Path, null, "s_sspath3" );
    internal static statestruct s_sspath3 = new(1, "SSWVC", 20, T_Path, null, "s_sspath3s" );
    internal static statestruct s_sspath3s = new(1, "SSWVC", 5, null, null, "s_sspath4" );
    internal static statestruct s_sspath4 = new(1, "SSWVD", 15, T_Path, null, "s_sspath1" );

    internal static statestruct s_sspain = new(2, "SSWVI", 10, null, null, "s_sschase1" );
    internal static statestruct s_sspain1 = new(2, "SSWVH", 10, null, null, "s_sschase1" );

    internal static statestruct s_ssshoot1 = new(0, "SSWVE", 20, null, null, "s_ssshoot2" );
    internal static statestruct s_ssshoot2 = new(0, "SSWVF", 20, null, T_Shoot, "s_ssshoot3" );
    internal static statestruct s_ssshoot3 = new(0, "SSWVG", 10, null, null, "s_ssshoot4" );
    internal static statestruct s_ssshoot4 = new(0, "SSWVF", 10, null, T_Shoot, "s_ssshoot5" );
    internal static statestruct s_ssshoot5 = new(0, "SSWVG", 10, null, null, "s_ssshoot6" );
    internal static statestruct s_ssshoot6 = new(0, "SSWVF", 10, null, T_Shoot, "s_ssshoot7" );
    internal static statestruct s_ssshoot7 = new(0, "SSWVG", 10, null, null, "s_ssshoot8" );
    internal static statestruct s_ssshoot8 = new(0, "SSWVF", 10, null, T_Shoot, "s_ssshoot9" );
    internal static statestruct s_ssshoot9 = new(0, "SSWVG", 10, null, null, "s_sschase1" );

    internal static statestruct s_sschase1 = new(1, "SSWVA", 10, T_Chase, null, "s_sschase1s" );
    internal static statestruct s_sschase1s = new(1, "SSWVA", 3, null, null, "s_sschase2" );
    internal static statestruct s_sschase2 = new(1, "SSWVB", 8, T_Chase, null, "s_sschase3" );
    internal static statestruct s_sschase3 = new(1, "SSWVC", 10, T_Chase, null, "s_sschase3s" );
    internal static statestruct s_sschase3s = new(1, "SSWVC", 3, null, null, "s_sschase4" );
    internal static statestruct s_sschase4 = new(1, "SSWVD", 8, T_Chase, null, "s_sschase1" );

    internal static statestruct s_ssdie1 = new(0, "SSWVJ", 15, null, A_DeathScream, "s_ssdie2" );
    internal static statestruct s_ssdie2 = new(0, "SSWVK", 15, null, null, "s_ssdie3" );
    internal static statestruct s_ssdie3 = new(0, "SSWVL", 15, null, null, "s_ssdie4" );
    internal static statestruct s_ssdie4 = new(0, "SSWVM", 0, null, null, "s_ssdie4" );

    //
    // hans
    //

    internal static statestruct s_bossstand = new(0, "HANSA", 0, T_Stand, null, "s_bossstand" );

    internal static statestruct s_bosschase1 = new(0, "HANSA", 10, T_Chase, null, "s_bosschase1s" );
    internal static statestruct s_bosschase1s = new(0, "HANSA", 3, null, null, "s_bosschase2" );
    internal static statestruct s_bosschase2 = new(0, "HANSB", 8, T_Chase, null, "s_bosschase3" );
    internal static statestruct s_bosschase3 = new(0, "HANSC", 10, T_Chase, null, "s_bosschase3s" );
    internal static statestruct s_bosschase3s = new(0, "HANSC", 3, null, null, "s_bosschase4" );
    internal static statestruct s_bosschase4 = new(0, "HANSD", 8, T_Chase, null, "s_bosschase1" );

    internal static statestruct s_bossdie1 = new(0, "HANSH", 15, null, A_DeathScream, "s_bossdie2" );
    internal static statestruct s_bossdie2 = new(0, "HANSI", 15, null, null, "s_bossdie3" );
    internal static statestruct s_bossdie3 = new(0, "HANSJ", 15, null, null, "s_bossdie4" );
    internal static statestruct s_bossdie4 = new(0, "HANSK", 0, null, null, "s_bossdie4" );

    internal static statestruct s_bossshoot1 = new(0, "HANSE", 30, null, null, "s_bossshoot2" );
    internal static statestruct s_bossshoot2 = new(0, "HANSF", 10, null, T_Shoot, "s_bossshoot3" );
    internal static statestruct s_bossshoot3 = new(0, "HANSG", 10, null, T_Shoot, "s_bossshoot4" );
    internal static statestruct s_bossshoot4 = new(0, "HANSF", 10, null, T_Shoot, "s_bossshoot5" );
    internal static statestruct s_bossshoot5 = new(0, "HANSG", 10, null, T_Shoot, "s_bossshoot6" );
    internal static statestruct s_bossshoot6 = new(0, "HANSF", 10, null, T_Shoot, "s_bossshoot7" );
    internal static statestruct s_bossshoot7 = new(0, "HANSG", 10, null, T_Shoot, "s_bossshoot8" );
    internal static statestruct s_bossshoot8 = new(0, "HANSE", 10, null, null, "s_bosschase1" );


    //
    // gretel
    //
    internal static statestruct s_gretelstand = new(0, "GRETA", 0, T_Stand, null, "s_gretelstand" );

    internal static statestruct s_gretelchase1 = new(0, "GRETA", 10, T_Chase, null, "s_gretelchase1s" );
    internal static statestruct s_gretelchase1s = new(0, "GRETA", 3, null, null, "s_gretelchase2" );
    internal static statestruct s_gretelchase2 = new(0, "GRETB", 8, T_Chase, null, "s_gretelchase3" );
    internal static statestruct s_gretelchase3 = new(0, "GRETC", 10, T_Chase, null, "s_gretelchase3s" );
    internal static statestruct s_gretelchase3s = new(0, "GRETC", 3, null, null, "s_gretelchase4" );
    internal static statestruct s_gretelchase4 = new(0, "GRETD", 8, T_Chase, null, "s_gretelchase1" );

    internal static statestruct s_greteldie1 = new(0, "GRETH", 15, null, A_DeathScream, "s_greteldie2" );
    internal static statestruct s_greteldie2 = new(0, "GRETI", 15, null, null, "s_greteldie3" );
    internal static statestruct s_greteldie3 = new(0, "GRETJ", 15, null, null, "s_greteldie4" );
    internal static statestruct s_greteldie4 = new(0, "GRETK", 0, null, null, "s_greteldie4" );

    internal static statestruct s_gretelshoot1 = new(0, "GRETE", 30, null, null, "s_gretelshoot2" );
    internal static statestruct s_gretelshoot2 = new(0, "GRETF", 10, null, T_Shoot, "s_gretelshoot3" );
    internal static statestruct s_gretelshoot3 = new(0, "GRETG", 10, null, T_Shoot, "s_gretelshoot4" );
    internal static statestruct s_gretelshoot4 = new(0, "GRETF", 10, null, T_Shoot, "s_gretelshoot5" );
    internal static statestruct s_gretelshoot5 = new(0, "GRETG", 10, null, T_Shoot, "s_gretelshoot6" );
    internal static statestruct s_gretelshoot6 = new(0, "GRETF", 10, null, T_Shoot, "s_gretelshoot7" );
    internal static statestruct s_gretelshoot7 = new(0, "GRETG", 10, null, T_Shoot, "s_gretelshoot8" );
    internal static statestruct s_gretelshoot8 = new(0, "GRETE", 10, null, null, "s_gretelchase1" );


    /*
    =============================================================================

                                        HITLERS

    =============================================================================
    */


    //
    // fake
    //

    internal static statestruct s_fakestand = new (0, "FAKEA", 0, T_Stand, null, "s_fakestand" );

    internal static statestruct s_fakechase1 = new(0, "FAKEA", 10, T_Fake, null, "s_fakechase1s" );
    internal static statestruct s_fakechase1s = new(0, "FAKEA", 3, null, null, "s_fakechase2" );
    internal static statestruct s_fakechase2 = new(0, "FAKEB", 8, T_Fake, null, "s_fakechase3" );
    internal static statestruct s_fakechase3 = new(0, "FAKEC", 10, T_Fake, null, "s_fakechase3s" );
    internal static statestruct s_fakechase3s = new(0, "FAKEC", 3, null, null, "s_fakechase4" );
    internal static statestruct s_fakechase4 = new(0, "FAKED", 8, T_Fake, null, "s_fakechase1" );

    internal static statestruct s_fakedie1 = new (0, "FAKEF", 10, null, A_DeathScream, "s_fakedie2" );
    internal static statestruct s_fakedie2 = new (0, "FAKEG", 10, null, null, "s_fakedie3" );
    internal static statestruct s_fakedie3 = new (0, "FAKEH", 10, null, null, "s_fakedie4" );
    internal static statestruct s_fakedie4 = new (0, "FAKEI", 10, null, null, "s_fakedie5" );
    internal static statestruct s_fakedie5 = new (0, "FAKEJ", 10, null, null, "s_fakedie6" );
    internal static statestruct s_fakedie6 = new(0, "FAKEK", 0, null, null, "s_fakedie6" );

    internal static statestruct s_fakeshoot1 = new (0, "FAKEE", 8, null, T_FakeFire, "s_fakeshoot2" );
    internal static statestruct s_fakeshoot2 = new (0, "FAKEE", 8, null, T_FakeFire, "s_fakeshoot3" );
    internal static statestruct s_fakeshoot3 = new (0, "FAKEE", 8, null, T_FakeFire, "s_fakeshoot4" );
    internal static statestruct s_fakeshoot4 = new (0, "FAKEE", 8, null, T_FakeFire, "s_fakeshoot5" );
    internal static statestruct s_fakeshoot5 = new (0, "FAKEE", 8, null, T_FakeFire, "s_fakeshoot6" );
    internal static statestruct s_fakeshoot6 = new (0, "FAKEE", 8, null, T_FakeFire, "s_fakeshoot7" );
    internal static statestruct s_fakeshoot7 = new (0, "FAKEE", 8, null, T_FakeFire, "s_fakeshoot8" );
    internal static statestruct s_fakeshoot8 = new (0, "FAKEE", 8, null, T_FakeFire, "s_fakeshoot9" );
    internal static statestruct s_fakeshoot9 = new(0, "FAKEE", 8, null, null, "s_fakechase1" );

    internal static statestruct s_fire1 = new(0, "FIREA", 6, T_Projectile, null, "s_fire2" );
    internal static statestruct s_fire2 = new (0, "FIREB", 6, T_Projectile, null, "s_fire1" );

    //
    // hitler
    //
    internal static statestruct s_mechastand = new(0, "MECHA", 0, T_Stand, null, "s_mechastand" );

    internal static statestruct s_mechachase1 = new(0, "MECHA", 10, T_Chase, A_MechaSound, "s_mechachase1s" );
    internal static statestruct s_mechachase1s = new(0, "MECHA", 6, null, null, "s_mechachase2" );
    internal static statestruct s_mechachase2 = new(0, "MECHB", 8, T_Chase, null, "s_mechachase3" );
    internal static statestruct s_mechachase3 = new(0, "MECHC", 10, T_Chase, A_MechaSound, "s_mechachase3s" );
    internal static statestruct s_mechachase3s = new(0, "MECHC", 6, null, null, "s_mechachase4" );
    internal static statestruct s_mechachase4 = new(0, "MECHD", 8, T_Chase, null, "s_mechachase1" );

    internal static statestruct s_mechadie1 = new(0, "MECHH", 10, null, A_DeathScream, "s_mechadie2" );
    internal static statestruct s_mechadie2 = new (0, "MECHI", 10, null, null, "s_mechadie3" );
    internal static statestruct s_mechadie3 = new (0, "MECHJ", 10, null, A_HitlerMorph, "s_mechadie4" );
    internal static statestruct s_mechadie4 = new (0, "MECHK", 0, null, null, "s_mechadie4" );

    internal static statestruct s_mechashoot1 = new(0, "MECHE", 30, null, null, "s_mechashoot2" );
    internal static statestruct s_mechashoot2 = new (0, "MECHF", 10, null, T_Shoot, "s_mechashoot3" );
    internal static statestruct s_mechashoot3 = new (0, "MECHG", 10, null, T_Shoot, "s_mechashoot4" );
    internal static statestruct s_mechashoot4 = new (0, "MECHF", 10, null, T_Shoot, "s_mechashoot5" );
    internal static statestruct s_mechashoot5 = new (0, "MECHG", 10, null, T_Shoot, "s_mechashoot6" );
    internal static statestruct s_mechashoot6 = new (0, "MECHF", 10, null, T_Shoot, "s_mechachase1" );


    internal static statestruct s_hitlerchase1 = new(0, "HTLRA", 6, T_Chase, null, "s_hitlerchase1s" );
    internal static statestruct s_hitlerchase1s = new(0, "HTLRA", 4, null, null, "s_hitlerchase2" );
    internal static statestruct s_hitlerchase2 = new(0, "HTLRB", 2, T_Chase, null, "s_hitlerchase3" );
    internal static statestruct s_hitlerchase3 = new(0, "HTLRC", 6, T_Chase, null, "s_hitlerchase3s" );
    internal static statestruct s_hitlerchase3s = new(0, "HTLRC", 4, null, null, "s_hitlerchase4" );
    internal static statestruct s_hitlerchase4 = new(0, "HTLRD", 2, T_Chase, null, "s_hitlerchase1" );

    internal static statestruct s_hitlerdeathcam = new(0, "HTLRA", 10, null, null, "s_hitlerdie1" );

    internal static statestruct s_hitlerdie1 = new(0, "HTLRA", 1, null, A_DeathScream, "s_hitlerdie2" );
    internal static statestruct s_hitlerdie2 = new (0, "HTLRA", 10, null, null, "s_hitlerdie3" );
    internal static statestruct s_hitlerdie3 = new (0, "HTLRH", 10, null, A_Slurpie, "s_hitlerdie4" );
    internal static statestruct s_hitlerdie4 = new (0, "HTLRI", 10, null, null, "s_hitlerdie5" );
    internal static statestruct s_hitlerdie5 = new (0, "HTLRJ", 10, null, null, "s_hitlerdie6" );
    internal static statestruct s_hitlerdie6 = new (0, "HTLRK", 10, null, null, "s_hitlerdie7" );
    internal static statestruct s_hitlerdie7 = new (0, "HTLRL", 10, null, null, "s_hitlerdie8" );
    internal static statestruct s_hitlerdie8 = new (0, "HTLRM", 10, null, null, "s_hitlerdie9" );
    internal static statestruct s_hitlerdie9 = new (0, "HTLRN", 10, null, null, "s_hitlerdie10" );
    internal static statestruct s_hitlerdie10 = new(0, "HTLRO", 20, null, A_StartDeathCam, "s_hitlerdie10" );

    internal static statestruct s_hitlershoot1 = new(0, "HTLRE", 30, null, null, "s_hitlershoot2" );
    internal static statestruct s_hitlershoot2 = new (0, "HTLRF", 10, null, T_Shoot, "s_hitlershoot3" );
    internal static statestruct s_hitlershoot3 = new (0, "HTLRG", 10, null, T_Shoot, "s_hitlershoot4" );
    internal static statestruct s_hitlershoot4 = new (0, "HTLRF", 10, null, T_Shoot, "s_hitlershoot5" );
    internal static statestruct s_hitlershoot5 = new (0, "HTLRG", 10, null, T_Shoot, "s_hitlershoot6" );
    internal static statestruct s_hitlershoot6 = new (0, "HTLRF", 10, null, T_Shoot, "s_hitlerchase1" );

    //
    // schabb
    //
    internal static statestruct s_schabbstand = new(0, "SHABA", 0, T_Stand, null, "s_schabbstand" );

    internal static statestruct s_schabbchase1 = new(0, "SHABA", 10, T_Schabb, null, "s_schabbchase1s" );
    internal static statestruct s_schabbchase1s = new(0, "SHABA", 3, null, null, "s_schabbchase2" );
    internal static statestruct s_schabbchase2 = new(0, "SHABB", 8, T_Schabb, null, "s_schabbchase3" );
    internal static statestruct s_schabbchase3 = new(0, "SHABC", 10, T_Schabb, null, "s_schabbchase3s" );
    internal static statestruct s_schabbchase3s = new(0, "SHABC", 3, null, null, "s_schabbchase4" );
    internal static statestruct s_schabbchase4 = new(0, "SHABD", 8, T_Schabb, null, "s_schabbchase1" );

    internal static statestruct s_schabbdeathcam = new(0, "SHABA", 1, null, null, "s_schabbdie1" );

    internal static statestruct s_schabbdie1 = new(0, "SHABA", 10, null, A_DeathScream, "s_schabbdie2" );
    internal static statestruct s_schabbdie2 = new (0, "SHABA", 10, null, null, "s_schabbdie3" );
    internal static statestruct s_schabbdie3 = new (0, "SHABG", 10, null, null, "s_schabbdie4" );
    internal static statestruct s_schabbdie4 = new (0, "SHABH", 10, null, null, "s_schabbdie5" );
    internal static statestruct s_schabbdie5 = new (0, "SHABI", 10, null, null, "s_schabbdie6" );
    internal static statestruct s_schabbdie6 = new (0, "SHABJ", 20, null, A_StartDeathCam, "s_schabbdie6" );

    internal static statestruct s_schabbshoot1 = new(0, "SHABE", 30, null, null, "s_schabbshoot2" );
    internal static statestruct s_schabbshoot2 = new (0, "SHABF", 10, null, T_SchabbThrow, "s_schabbchase1" );

    internal static statestruct s_needle1 = new(0, "HYPOA", 6, T_Projectile, null, "s_needle2" );
    internal static statestruct s_needle2 = new (0, "HYPOB", 6, T_Projectile, null, "s_needle3" );
    internal static statestruct s_needle3 = new (0, "HYPOC", 6, T_Projectile, null, "s_needle4" );
    internal static statestruct s_needle4 = new (0, "HYPOD", 6, T_Projectile, null, "s_needle1" );

    internal static void T_Schabb(objstruct ob)
    {

        int move;
        int dx, dy, dist;
        bool dodge;

        dodge = false;
        dx = Math.Abs(ob.tilex - player.tilex);
        dy = Math.Abs(ob.tiley - player.tiley);
        dist = dx > dy ? dx : dy;

        if (CheckLine(ob))                                              // got a shot at player?
        {
            ob.hidden = false;
            if (US_RndT() < (tics << 3))
            {
                //
                // go into attack frame
                //
                NewState(ob, s_schabbshoot1);
                return;
            }
            dodge = true;
        }
        else
            ob.hidden = true;

        if (ob.dir == objdirtypes.nodir)
        {
            if (dodge)
                SelectDodgeDir(ob);
            else
                SelectChaseDir(ob);
            if (ob.dir == objdirtypes.nodir)
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
                TryWalk(ob);
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

            if (dist < 4)
                SelectRunDir(ob);
            else if (dodge)
                SelectDodgeDir(ob);
            else
                SelectChaseDir(ob);

            if (ob.dir == objdirtypes.nodir)
                return;                                                 // object is blocked in
        }
    }

    internal static void T_SchabbThrow(objstruct ob)
    {
        var deltax = player.x - ob.x;
        var deltay = ob.y - player.y;
        var angle = (float)Math.Atan2((float)deltay, (float)deltax);
        if (angle < 0)
            angle = (float)(M_PI * 2 + angle);
        var iangle = (int)(angle / (M_PI * 2) * ANGLES);

        var newobj = GetNewActor();
        newobj.state = s_needle1;
        newobj.ticcount = 1;

        newobj.tilex = ob.tilex;
        newobj.tiley = ob.tiley;
        newobj.x = ob.x;
        newobj.y = ob.y;
        newobj.obclass = classtypes.needleobj;
        newobj.dir = objdirtypes.nodir;
        newobj.angle = (short)iangle;
        newobj.speed = (int)0x2000L;

        newobj.flags = objflags.FL_NEVERMARK;
        newobj.active = activetypes.ac_yes;

        PlaySoundLocActor((int)soundnames.SCHABBSTHROWSND, newobj);
    }

    //
    // gift
    //

    internal static statestruct s_giftstand = new(0, "GIFTA", 0, T_Stand, null, "s_giftstand" );

    internal static statestruct s_giftchase1 = new(0, "GIFTA", 10, T_Gift, null, "s_giftchase1s" );
    internal static statestruct s_giftchase1s = new(0, "GIFTA", 3, null, null, "s_giftchase2" );
    internal static statestruct s_giftchase2 = new(0, "GIFTB", 8, T_Gift, null, "s_giftchase3" );
    internal static statestruct s_giftchase3 = new(0, "GIFTC", 10, T_Gift, null, "s_giftchase3s" );
    internal static statestruct s_giftchase3s = new(0, "GIFTC", 3, null, null, "s_giftchase4" );
    internal static statestruct s_giftchase4 = new(0, "GIFTD", 8, T_Gift, null, "s_giftchase1" );

    internal static statestruct s_giftdeathcam = new(0, "GIFTA", 1, null, null, "s_giftdie1" );

    internal static statestruct s_giftdie1 = new(0, "GIFTA", 1, null, A_DeathScream, "s_giftdie2" );
    internal static statestruct s_giftdie2 = new (0, "GIFTA", 10, null, null, "s_giftdie3" );
    internal static statestruct s_giftdie3 = new (0, "GIFTG", 10, null, null, "s_giftdie4" );
    internal static statestruct s_giftdie4 = new (0, "GIFTH", 10, null, null, "s_giftdie5" );
    internal static statestruct s_giftdie5 = new (0, "GIFTI", 10, null, null, "s_giftdie6" );
    internal static statestruct s_giftdie6 = new (0, "GIFTJ", 20, null, A_StartDeathCam, "s_giftdie6" );

    internal static statestruct s_giftshoot1 = new(0, "GIFTE", 30, null, null, "s_giftshoot2" );
    internal static statestruct s_giftshoot2 = new (0, "GIFTF", 10, null, T_GiftThrow, "s_giftchase1" );

    internal static void T_Gift(objstruct ob)
    {
        int move;
        int dx, dy, dist;
        bool dodge;

        dodge = false;
        dx = Math.Abs(ob.tilex - player.tilex);
        dy = Math.Abs(ob.tiley - player.tiley);
        dist = dx > dy ? dx : dy;

        if (CheckLine(ob))                                              // got a shot at player?
        {
            ob.hidden = false;
            if (US_RndT() < (tics << 3))
            {
                //
                // go into attack frame
                //
                NewState(ob, s_giftshoot1);
                return;
            }
            dodge = true;
        }
        else
            ob.hidden = true;

        if (ob.dir == objdirtypes.nodir)
        {
            if (dodge)
                SelectDodgeDir(ob);
            else
                SelectChaseDir(ob);
            if (ob.dir == objdirtypes.nodir)
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
                TryWalk(ob);
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

            if (dist < 4)
                SelectRunDir(ob);
            else if (dodge)
                SelectDodgeDir(ob);
            else
                SelectChaseDir(ob);

            if (ob.dir == objdirtypes.nodir)
                return;                                                 // object is blocked in
        }
    }

    internal static void T_GiftThrow(objstruct ob)
    {
        objstruct newobj = null!;
        int deltax, deltay;
        float angle;
        int iangle;

        deltax = player.x - ob.x;
        deltay = ob.y - player.y;
        angle = (float)Math.Atan2((float)deltay, (float)deltax);
        if (angle < 0)
            angle = (float)(M_PI * 2 + angle);
        iangle = (int)(angle / (M_PI * 2) * ANGLES);

        newobj = GetNewActor();
        newobj.state = s_rocket;
        newobj.ticcount = 1;

        newobj.tilex = ob.tilex;
        newobj.tiley = ob.tiley;
        newobj.x = ob.x;
        newobj.y = ob.y;
        newobj.obclass = classtypes.rocketobj;
        newobj.dir = objdirtypes.nodir;
        newobj.angle = (short)iangle;
        newobj.speed = (int)0x2000L;
        newobj.flags = objflags.FL_NEVERMARK;
        newobj.active = activetypes.ac_yes;

        PlaySoundLocActor((int)soundnames.MISSILEFIRESND, newobj);
    }

    //
    // fat
    //


    internal static statestruct s_fatstand = new(0, "FATFA", 0, T_Stand, null, "s_fatstand" );

    internal static statestruct s_fatchase1 = new(0, "FATFA", 10, T_Fat, null, "s_fatchase1s" );
    internal static statestruct s_fatchase1s = new(0, "FATFA", 3, null, null, "s_fatchase2" );
    internal static statestruct s_fatchase2 = new(0, "FATFB", 8, T_Fat, null, "s_fatchase3" );
    internal static statestruct s_fatchase3 = new(0, "FATFC", 10, T_Fat, null, "s_fatchase3s" );
    internal static statestruct s_fatchase3s = new(0, "FATFC", 3, null, null, "s_fatchase4" );
    internal static statestruct s_fatchase4 = new(0, "FATFD", 8, T_Fat, null, "s_fatchase1" );

    internal static statestruct s_fatdeathcam = new(0, "FATFA", 1, null, null, "s_fatdie1" );

    internal static statestruct s_fatdie1 = new (0, "FATFA", 1, null, A_DeathScream, "s_fatdie2" );
    internal static statestruct s_fatdie2 = new (0, "FATFA", 10, null, null, "s_fatdie3" );
    internal static statestruct s_fatdie3 = new (0, "FATFI", 10, null, null, "s_fatdie4" );
    internal static statestruct s_fatdie4 = new (0, "FATFJ", 10, null, null, "s_fatdie5" );
    internal static statestruct s_fatdie5 = new (0, "FATFK", 10, null, null, "s_fatdie6" );
    internal static statestruct s_fatdie6 = new (0, "FATFL", 20, null, A_StartDeathCam, "s_fatdie6" );

    internal static statestruct s_fatshoot1 = new (0, "FATFE", 30, null, null, "s_fatshoot2" );
    internal static statestruct s_fatshoot2 = new (0, "FATFF", 10, null, T_GiftThrow, "s_fatshoot3" );
    internal static statestruct s_fatshoot3 = new (0, "FATFG", 10, null, T_Shoot, "s_fatshoot4" );
    internal static statestruct s_fatshoot4 = new (0, "FATFH", 10, null, T_Shoot, "s_fatshoot5" );
    internal static statestruct s_fatshoot5 = new (0, "FATFG", 10, null, T_Shoot, "s_fatshoot6" );
    internal static statestruct s_fatshoot6 = new (0, "FATFH", 10, null, T_Shoot, "s_fatchase1" );

    internal static void T_Fat(objstruct ob)
    {
        int move;
        int dx, dy, dist;
        bool dodge;

        dodge = false;
        dx = Math.Abs(ob.tilex - player.tilex);
        dy = Math.Abs(ob.tiley - player.tiley);
        dist = dx > dy ? dx : dy;

        if (CheckLine(ob))                                              // got a shot at player?
        {
            ob.hidden = false;
            if (US_RndT() < (tics << 3))
            {
                //
                // go into attack frame
                //
                NewState(ob, s_fatshoot1);
                return;
            }
            dodge = true;
        }
        else
            ob.hidden = true;

        if (ob.dir == objdirtypes.nodir)
        {
            if (dodge)
                SelectDodgeDir(ob);
            else
                SelectChaseDir(ob);
            if (ob.dir == objdirtypes.nodir)
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
                TryWalk(ob);
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

            if (dist < 4)
                SelectRunDir(ob);
            else if (dodge)
                SelectDodgeDir(ob);
            else
                SelectChaseDir(ob);

            if (ob.dir == objdirtypes.nodir)
                return;                                                 // object is blocked in
        }
    }

    /*
    ============================================================================

                                        BJ VICTORY

    ============================================================================
    */


    //
    // BJ victory
    //
    internal static statestruct s_bjrun1 =  new statestruct(0, "BLAZA0", 12, T_BJRun, null, "s_bjrun1s" );
    internal static statestruct s_bjrun1s = new statestruct(0, "BLAZA0", 3, null, null, "s_bjrun2" );
    internal static statestruct s_bjrun2 =  new statestruct(0, "BLAZB0", 8, T_BJRun, null, "s_bjrun3" );
    internal static statestruct s_bjrun3 =  new statestruct(0, "BLAZC0", 12, T_BJRun, null, "s_bjrun3s" );
    internal static statestruct s_bjrun3s = new statestruct(0, "BLAZC0", 3, null, null, "s_bjrun4" );
    internal static statestruct s_bjrun4 =  new statestruct(0, "BLAZD0", 8, T_BJRun, null, "s_bjrun1" );
    internal static statestruct s_bjjump1 = new statestruct(0, "BLAZE0", 14, T_BJJump, null, "s_bjjump2" );
    internal static statestruct s_bjjump2 = new statestruct(0, "BLAZF0", 14, T_BJJump, T_BJYell, "s_bjjump3" );
    internal static statestruct s_bjjump3 = new statestruct(0, "BLAZG0", 14, T_BJJump, null, "s_bjjump4" );
    internal static statestruct s_bjjump4 = new statestruct(0, "BLAZH0", 300, null, T_BJDone, "s_bjjump4" );


    internal static statestruct s_deathcam = new statestruct( 0, "DCAMA0", 0, null, null, null );

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

        {"s_blinkychase1", s_blinkychase1 },
        {"s_blinkychase2", s_blinkychase2 },
        {"s_inkychase1", s_inkychase1 },
        {"s_inkychase2", s_inkychase2 },
        {"s_pinkychase1", s_pinkychase1 },
        {"s_pinkychase2", s_pinkychase2 },
        {"s_clydechase1", s_clydechase1 },
        {"s_clydechase2", s_clydechase2 },
        { "s_dogpath1", s_dogpath1},
        { "s_dogpath1s", s_dogpath1s},
        { "s_dogpath2", s_dogpath2},
        { "s_dogpath3", s_dogpath3},
        { "s_dogpath3s", s_dogpath3s},
        { "s_dogpath4", s_dogpath4},

        { "s_dogjump1", s_dogjump1},
        { "s_dogjump2", s_dogjump2},
        { "s_dogjump3", s_dogjump3},
        { "s_dogjump4", s_dogjump4},
        { "s_dogjump5", s_dogjump5},

        { "s_dogchase1", s_dogchase1},
        { "s_dogchase1s", s_dogchase1s},
        { "s_dogchase2", s_dogchase2},
        { "s_dogchase3", s_dogchase3},
        { "s_dogchase3s", s_dogchase3s},
        { "s_dogchase4", s_dogchase4},

        { "s_dogdie1", s_dogdie1},
        { "s_dogdie2", s_dogdie2},
        { "s_dogdie3", s_dogdie3},
        { "s_dogdead", s_dogdead},

        { "s_ofcstand", s_ofcstand},

        { "s_ofcpath1", s_ofcpath1},
        { "s_ofcpath1s", s_ofcpath1s},
        { "s_ofcpath2", s_ofcpath2},
        { "s_ofcpath3", s_ofcpath3},
        { "s_ofcpath3s", s_ofcpath3s},
        { "s_ofcpath4", s_ofcpath4},

        { "s_ofcpain", s_ofcpain},
        { "s_ofcpain1", s_ofcpain1},

        { "s_ofcshoot1", s_ofcshoot1},
        { "s_ofcshoot2", s_ofcshoot2},
        { "s_ofcshoot3", s_ofcshoot3},

        { "s_ofcchase1", s_ofcchase1},
        { "s_ofcchase1s", s_ofcchase1s},
        { "s_ofcchase2", s_ofcchase2},
        { "s_ofcchase3", s_ofcchase3},
        { "s_ofcchase3s", s_ofcchase3s},
        { "s_ofcchase4", s_ofcchase4},

        { "s_ofcdie1", s_ofcdie1},
        { "s_ofcdie2", s_ofcdie2},
        { "s_ofcdie3", s_ofcdie3},
        { "s_ofcdie4", s_ofcdie4},
        { "s_ofcdie5", s_ofcdie5},

        { "s_mutstand", s_mutstand},

        { "s_mutpath1", s_mutpath1},
        { "s_mutpath1s", s_mutpath1s},
        { "s_mutpath2", s_mutpath2},
        { "s_mutpath3", s_mutpath3},
        { "s_mutpath3s", s_mutpath3s},
        { "s_mutpath4", s_mutpath4},

        { "s_mutpain", s_mutpain},
        { "s_mutpain1", s_mutpain1},

        { "s_mutshoot1", s_mutshoot1},
        { "s_mutshoot2", s_mutshoot2},
        { "s_mutshoot3", s_mutshoot3},
        { "s_mutshoot4", s_mutshoot4},

        { "s_mutchase1", s_mutchase1},
        { "s_mutchase1s", s_mutchase1s},
        { "s_mutchase2", s_mutchase2},
        { "s_mutchase3", s_mutchase3},
        { "s_mutchase3s", s_mutchase3s},
        { "s_mutchase4", s_mutchase4},

        { "s_mutdie1", s_mutdie1},
        { "s_mutdie2", s_mutdie2},
        { "s_mutdie3", s_mutdie3},
        { "s_mutdie4", s_mutdie4},
        { "s_mutdie5", s_mutdie5},

        { "s_ssstand", s_ssstand},

        { "s_sspath1", s_sspath1},
        { "s_sspath1s", s_sspath1s},
        { "s_sspath2", s_sspath2},
        { "s_sspath3", s_sspath3},
        { "s_sspath3s", s_sspath3s},
        { "s_sspath4", s_sspath4},

        { "s_sspain", s_sspain},
        { "s_sspain1", s_sspain1},

        { "s_ssshoot1", s_ssshoot1},
        { "s_ssshoot2", s_ssshoot2},
        { "s_ssshoot3", s_ssshoot3},
        { "s_ssshoot4", s_ssshoot4},
        { "s_ssshoot5", s_ssshoot5},
        { "s_ssshoot6", s_ssshoot6},
        { "s_ssshoot7", s_ssshoot7},
        { "s_ssshoot8", s_ssshoot8},
        { "s_ssshoot9", s_ssshoot9},

        { "s_sschase1", s_sschase1},
        { "s_sschase1s", s_sschase1s},
        { "s_sschase2", s_sschase2},
        { "s_sschase3", s_sschase3},
        { "s_sschase3s", s_sschase3s},
        { "s_sschase4", s_sschase4},

        { "s_ssdie1", s_ssdie1},
        { "s_ssdie2", s_ssdie2},
        { "s_ssdie3", s_ssdie3},
        { "s_ssdie4", s_ssdie4},

        { "s_bossstand", s_bossstand},

        { "s_bosschase1", s_bosschase1},
        { "s_bosschase1s", s_bosschase1s},
        { "s_bosschase2", s_bosschase2},
        { "s_bosschase3", s_bosschase3},
        { "s_bosschase3s", s_bosschase3s},
        { "s_bosschase4", s_bosschase4},

        { "s_bossdie1", s_bossdie1},
        { "s_bossdie2", s_bossdie2},
        { "s_bossdie3", s_bossdie3},
        { "s_bossdie4", s_bossdie4},

        { "s_bossshoot1", s_bossshoot1},
        { "s_bossshoot2", s_bossshoot2},
        { "s_bossshoot3", s_bossshoot3},
        { "s_bossshoot4", s_bossshoot4},
        { "s_bossshoot5", s_bossshoot5},
        { "s_bossshoot6", s_bossshoot6},
        { "s_bossshoot7", s_bossshoot7},
        { "s_bossshoot8", s_bossshoot8},

        { "s_gretelstand", s_gretelstand},

        { "s_gretelchase1", s_gretelchase1},
        { "s_gretelchase1s", s_gretelchase1s},
        { "s_gretelchase2", s_gretelchase2},
        { "s_gretelchase3", s_gretelchase3},
        { "s_gretelchase3s", s_gretelchase3s},
        { "s_gretelchase4", s_gretelchase4},

        { "s_greteldie1", s_greteldie1},
        { "s_greteldie2", s_greteldie2},
        { "s_greteldie3", s_greteldie3},
        { "s_greteldie4", s_greteldie4},

        { "s_gretelshoot1", s_gretelshoot1},
        { "s_gretelshoot2", s_gretelshoot2},
        { "s_gretelshoot3", s_gretelshoot3},
        { "s_gretelshoot4", s_gretelshoot4},
        { "s_gretelshoot5", s_gretelshoot5},
        { "s_gretelshoot6", s_gretelshoot6},
        { "s_gretelshoot7", s_gretelshoot7},
        { "s_gretelshoot8", s_gretelshoot8},


        { "s_rocket", s_rocket},
        { "s_smoke1", s_smoke1},
        { "s_smoke2", s_smoke2},
        { "s_smoke3", s_smoke3},
        { "s_smoke4", s_smoke4},
        { "s_boom1", s_boom1},
        { "s_boom2", s_boom2},
        { "s_boom3", s_boom3},


        { "s_schabbstand", s_schabbstand},

        { "s_schabbchase1", s_schabbchase1},
        { "s_schabbchase1s", s_schabbchase1s},
        { "s_schabbchase2", s_schabbchase2},
        { "s_schabbchase3", s_schabbchase3},
        { "s_schabbchase3s", s_schabbchase3s},
        { "s_schabbchase4", s_schabbchase4},

        { "s_schabbdie1", s_schabbdie1},
        { "s_schabbdie2", s_schabbdie2},
        { "s_schabbdie3", s_schabbdie3},
        { "s_schabbdie4", s_schabbdie4},
        { "s_schabbdie5", s_schabbdie5},
        { "s_schabbdie6", s_schabbdie6},

        { "s_schabbshoot1", s_schabbshoot1},
        { "s_schabbshoot2", s_schabbshoot2},

        { "s_needle1", s_needle1},
        { "s_needle2", s_needle2},
        { "s_needle3", s_needle3},
        { "s_needle4", s_needle4},

        { "s_schabbdeathcam", s_schabbdeathcam},


        { "s_giftstand", s_giftstand},

        { "s_giftchase1", s_giftchase1},
        { "s_giftchase1s", s_giftchase1s},
        { "s_giftchase2", s_giftchase2},
        { "s_giftchase3", s_giftchase3},
        { "s_giftchase3s", s_giftchase3s},
        { "s_giftchase4", s_giftchase4},

        { "s_giftdie1", s_giftdie1},
        { "s_giftdie2", s_giftdie2},
        { "s_giftdie3", s_giftdie3},
        { "s_giftdie4", s_giftdie4},
        { "s_giftdie5", s_giftdie5},
        { "s_giftdie6", s_giftdie6},

        { "s_giftshoot1", s_giftshoot1},
        { "s_giftshoot2", s_giftshoot2},

        { "s_giftdeathcam", s_giftdeathcam},

        { "s_fatstand", s_fatstand},

        { "s_fatchase1", s_fatchase1},
        { "s_fatchase1s", s_fatchase1s},
        { "s_fatchase2", s_fatchase2},
        { "s_fatchase3", s_fatchase3},
        { "s_fatchase3s", s_fatchase3s},
        { "s_fatchase4", s_fatchase4},

        { "s_fatdie1", s_fatdie1},
        { "s_fatdie2", s_fatdie2},
        { "s_fatdie3", s_fatdie3},
        { "s_fatdie4", s_fatdie4},
        { "s_fatdie5", s_fatdie5},
        { "s_fatdie6", s_fatdie6},

        { "s_fatshoot1", s_fatshoot1},
        { "s_fatshoot2", s_fatshoot2},
        { "s_fatshoot3", s_fatshoot3},
        { "s_fatshoot4", s_fatshoot4},
        { "s_fatshoot5", s_fatshoot5},
        { "s_fatshoot6", s_fatshoot6},

        { "s_fatdeathcam", s_fatdeathcam},

        { "s_fakestand", s_fakestand},

        { "s_fakechase1", s_fakechase1},
        { "s_fakechase1s", s_fakechase1s},
        { "s_fakechase2", s_fakechase2},
        { "s_fakechase3", s_fakechase3},
        { "s_fakechase3s", s_fakechase3s},
        { "s_fakechase4", s_fakechase4},

        { "s_fakedie1", s_fakedie1},
        { "s_fakedie2", s_fakedie2},
        { "s_fakedie3", s_fakedie3},
        { "s_fakedie4", s_fakedie4},
        { "s_fakedie5", s_fakedie5},
        { "s_fakedie6", s_fakedie6},

        { "s_fakeshoot1", s_fakeshoot1},
        { "s_fakeshoot2", s_fakeshoot2},
        { "s_fakeshoot3", s_fakeshoot3},
        { "s_fakeshoot4", s_fakeshoot4},
        { "s_fakeshoot5", s_fakeshoot5},
        { "s_fakeshoot6", s_fakeshoot6},
        { "s_fakeshoot7", s_fakeshoot7},
        { "s_fakeshoot8", s_fakeshoot8},
        { "s_fakeshoot9", s_fakeshoot9},

        { "s_fire1", s_fire1},
        { "s_fire2", s_fire2},

        { "s_mechachase1", s_mechachase1},
        { "s_mechachase1s", s_mechachase1s},
        { "s_mechachase2", s_mechachase2},
        { "s_mechachase3", s_mechachase3},
        { "s_mechachase3s", s_mechachase3s},
        { "s_mechachase4", s_mechachase4},

        { "s_mechadie1", s_mechadie1},
        { "s_mechadie2", s_mechadie2},
        { "s_mechadie3", s_mechadie3},
        { "s_mechadie4", s_mechadie4},

        { "s_mechashoot1", s_mechashoot1},
        { "s_mechashoot2", s_mechashoot2},
        { "s_mechashoot3", s_mechashoot3},
        { "s_mechashoot4", s_mechashoot4},
        { "s_mechashoot5", s_mechashoot5},
        { "s_mechashoot6", s_mechashoot6},


        { "s_hitlerchase1", s_hitlerchase1},
        { "s_hitlerchase1s", s_hitlerchase1s},
        { "s_hitlerchase2", s_hitlerchase2},
        { "s_hitlerchase3", s_hitlerchase3},
        { "s_hitlerchase3s", s_hitlerchase3s},
        { "s_hitlerchase4", s_hitlerchase4},

        { "s_hitlerdie1", s_hitlerdie1},
        { "s_hitlerdie2", s_hitlerdie2},
        { "s_hitlerdie3", s_hitlerdie3},
        { "s_hitlerdie4", s_hitlerdie4},
        { "s_hitlerdie5", s_hitlerdie5},
        { "s_hitlerdie6", s_hitlerdie6},
        { "s_hitlerdie7", s_hitlerdie7},
        { "s_hitlerdie8", s_hitlerdie8},
        { "s_hitlerdie9", s_hitlerdie9},
        { "s_hitlerdie10", s_hitlerdie10},

        { "s_hitlershoot1", s_hitlershoot1},
        { "s_hitlershoot2", s_hitlershoot2},
        { "s_hitlershoot3", s_hitlershoot3},
        { "s_hitlershoot4", s_hitlershoot4},
        { "s_hitlershoot5", s_hitlershoot5},
        { "s_hitlershoot6", s_hitlershoot6},

        { "s_hitlerdeathcam", s_hitlerdeathcam},

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

            case enemytypes.en_officer:
                newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_ofcstand);
                newobj.speed = SPDPATROL;
                if (!loadedgame)
                    gamestate.killtotal++;
                break;

            case enemytypes.en_mutant:
                newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_mutstand);
                newobj.speed = SPDPATROL;
                if (!loadedgame)
                    gamestate.killtotal++;
                break;

            case enemytypes.en_ss:
                newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_ssstand);
                newobj.speed = SPDPATROL;
                if (!loadedgame)
                    gamestate.killtotal++;
                break;
        }


        tile = _mapManager.MAPSPOT(tilex, tiley, 0);
        if (tile == MapConstants.AMBUSHTILE)
        {
            if (MapManager.VALIDAREA(_mapManager.MAPSPOT(tilex + 1, tiley, 0)))
                tile = _mapManager.MAPSPOT(tilex + 1, tiley, 0);
            if (MapManager.VALIDAREA(_mapManager.MAPSPOT(tilex, tiley - 1, 0)))
                tile = _mapManager.MAPSPOT(tilex, tiley - 1, 0);
            if (MapManager.VALIDAREA(_mapManager.MAPSPOT(tilex, tiley + 1, 0)))
                tile = _mapManager.MAPSPOT(tilex, tiley + 1, 0);
            if (MapManager.VALIDAREA(_mapManager.MAPSPOT(tilex - 1, tiley, 0)))
                tile = _mapManager.MAPSPOT(tilex - 1, tiley, 0);

            _mapManager.SetMapSpot(tilex, tiley, 0, (ushort)tile);
            newobj.areanumber = (byte)(tile - MapConstants.AREATILE);

            newobj.flags |= objflags.FL_AMBUSH;
        }

        newobj.obclass = (classtypes.guardobj + which);
        newobj.hitpoints = starthitpoints[(int)gamestate.difficulty, which];
        newobj.dir = (objdirtypes)(dir * 2);
        newobj.flags |= objflags.FL_SHOOTABLE;
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

            case enemytypes.en_officer:
                newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_ofcpath1);
                newobj.speed = SPDPATROL;
                if (!loadedgame)
                    gamestate.killtotal++;
                break;

            case enemytypes.en_ss:
                newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_sspath1);
                newobj.speed = SPDPATROL;
                if (!loadedgame)
                    gamestate.killtotal++;
                break;

            case enemytypes.en_mutant:
                newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_mutpath1);
                newobj.speed = SPDPATROL;
                if (!loadedgame)
                    gamestate.killtotal++;
                break;

            case enemytypes.en_dog:
                newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_dogpath1);
                newobj.speed = SPDDOG;
                if (!loadedgame)
                    gamestate.killtotal++;
                break;
        }

        newobj.obclass = (classtypes.guardobj + which);
        newobj.dir = (objdirtypes)(dir * 2);
        newobj.hitpoints = starthitpoints[(int)gamestate.difficulty, which];
        newobj.distance = (int)TILEGLOBAL;
        newobj.flags |= objflags.FL_SHOOTABLE;
        newobj.active = activetypes.ac_yes;

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
        if (!(demorecord || demoplayback))
        {
            newobj.flags |= objflags.FL_NONMARK; // walk through moving enemy fix
        }

        newobj.obclass = classtypes.inertobj;
    }

    internal static void SpawnBoss(int tilex, int tiley)
    {
        objstruct newobj = null!;

        newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_bossstand);
        newobj.speed = SPDPATROL;

        newobj.obclass = classtypes.bossobj;
        newobj.hitpoints = starthitpoints[(int)gamestate.difficulty, (short)enemytypes.en_boss];
        newobj.dir = objdirtypes.nodir;
        newobj.flags |= objflags.FL_SHOOTABLE | objflags.FL_AMBUSH;
        if (!loadedgame)
            gamestate.killtotal++;
    }

    internal static void SpawnGretel(int tilex, int tiley)
    {
        objstruct newobj = null!;

        newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_gretelstand);
        newobj.speed = SPDPATROL;

        newobj.obclass = classtypes.gretelobj;
        newobj.hitpoints = starthitpoints[(int)gamestate.difficulty, (short)enemytypes.en_gretel];
        newobj.dir = objdirtypes.nodir;
        newobj.flags |= objflags.FL_SHOOTABLE | objflags.FL_AMBUSH;
        if (!loadedgame)
            gamestate.killtotal++;
    }

    internal static void SpawnFakeHitler(int tilex, int tiley)
    {
        objstruct newobj = null!;

        newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_fakestand);
        newobj.speed = SPDPATROL;

        newobj.obclass = classtypes.fakeobj;
        newobj.hitpoints = starthitpoints[(int)gamestate.difficulty, (short)enemytypes.en_fake];
        newobj.dir = objdirtypes.nodir;
        newobj.flags |= objflags.FL_SHOOTABLE | objflags.FL_AMBUSH;
        if (!loadedgame)
            gamestate.killtotal++;
    }

    internal static void SpawnHitler(int tilex, int tiley)
    {
        objstruct newobj = null!;

        if (DigiMode != SDSMode.Off)
            s_hitlerdie2.tictime = 140;
        else
            s_hitlerdie2.tictime = 5;

        newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_mechastand);
        newobj.speed = SPDPATROL;

        newobj.obclass = classtypes.mechahitlerobj;
        newobj.hitpoints = starthitpoints[(int)gamestate.difficulty, (short)enemytypes.en_hitler];
        newobj.dir = objdirtypes.nodir;
        newobj.flags |= objflags.FL_SHOOTABLE | objflags.FL_AMBUSH;
        if (!loadedgame)
            gamestate.killtotal++;
    }

    internal static void A_HitlerMorph(objstruct ob)
    {
        objstruct newobj = null!;
        short[] hitpoints = { 500, 700, 800, 900 };

        newobj = SpawnNewObj(ob.tilex, ob.tiley, s_hitlerchase1);
        newobj.speed = SPDPATROL * 5;

        newobj.x = ob.x;
        newobj.y = ob.y;

        newobj.distance = ob.distance;
        newobj.dir = ob.dir;
        newobj.flags = ob.flags | objflags.FL_SHOOTABLE;
        newobj.flags &= ~objflags.FL_NONMARK;   // hitler stuck with nodir fix

        newobj.obclass = classtypes.realhitlerobj;
        newobj.hitpoints = hitpoints[(int)gamestate.difficulty];

        if (!loadedgame)               // Count real hitler for correct kill ratios
            gamestate.killtotal++;
    }

    internal static void A_MechaSound(objstruct ob)
    {
        if (ob.areanumber >= MapConstants.NUMAREAS || areabyplayer[ob.areanumber] != 0)
            PlaySoundLocActor((int)soundnames.MECHSTEPSND, ob);
    }

    internal static void A_Slurpie(objstruct ob)
    {
        SD_PlaySound((int)soundnames.SLURPIESND);
    }

    internal static void T_FakeFire(objstruct ob)
    {
        objstruct newobj = null;
        int deltax, deltay;
        float angle;
        int iangle;

        //if (objfreelist == MAXACTORS)       // stop shooting if over MAXACTORS
        //{
        //    NewState(ob, s_fakechase1);
        //    return;
        //}

        deltax = player.x - ob.x;
        deltay = ob.y - player.y;
        angle = (float)Math.Atan2((float)deltay, (float)deltax);
        if (angle < 0)
            angle = (float)(M_PI * 2 + angle);
        iangle = (int)(angle / (M_PI * 2) * ANGLES);

        newobj = GetNewActor();
        newobj.state = s_fire1;
        newobj.ticcount = 1;

        newobj.tilex = ob.tilex;
        newobj.tiley = ob.tiley;
        newobj.x = ob.x;
        newobj.y = ob.y;
        newobj.dir = objdirtypes.nodir;
        newobj.angle = (short)iangle;
        newobj.obclass = classtypes.fireobj;
        newobj.speed = (int)0x1200L;
        newobj.flags = objflags.FL_NEVERMARK;
        newobj.active = activetypes.ac_yes;

        PlaySoundLocActor((int)soundnames.FLAMETHROWERSND, newobj);
    }

    internal static void T_Fake(objstruct ob)
    {
        int move;

        if (CheckLine(ob))                      // got a shot at player?
        {
            ob.hidden = false;
            if (US_RndT() < (tics << 1))
            {
                //
                // go into attack frame
                //
                NewState(ob, s_fakeshoot1);
                return;
            }
        }
        else
            ob.hidden = true;

        if (ob.dir == objdirtypes.nodir)
        {
            SelectDodgeDir(ob);
            if (ob.dir == objdirtypes.nodir)
                return;                                                 // object is blocked in
        }

        move = (int)(ob.speed * tics);

        while (move != 0)
        {
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

            SelectDodgeDir(ob);

            if (ob.dir == objdirtypes.nodir)
                return;                                                 // object is blocked in
        }
    }

    internal static void SpawnGift(int tilex, int tiley)
    {
        objstruct newobj = null!;

        if (DigiMode != SDSMode.Off)
            s_giftdie2.tictime = 140;
        else
            s_giftdie2.tictime = 5;

        newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_giftstand);
        newobj.speed = SPDPATROL;

        newobj.obclass = classtypes.giftobj;
        newobj.hitpoints = starthitpoints[(int)gamestate.difficulty, (short)enemytypes.en_gift];
        newobj.dir = objdirtypes.nodir;
        newobj.flags |= objflags.FL_SHOOTABLE | objflags.FL_AMBUSH;
        if (!loadedgame)
            gamestate.killtotal++;
    }

    internal static void SpawnGhosts(int which, int tilex, int tiley)
    {
        objstruct newobj = null!;

        switch ((enemytypes)which)
        {
            case enemytypes.en_blinky:
                newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_blinkychase1);
                break;
            case enemytypes.en_clyde:
                newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_clydechase1);
                break;
            case enemytypes.en_pinky:
                newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_pinkychase1);
                break;
            case enemytypes.en_inky:
                newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_inkychase1);
                break;
        }

        newobj.obclass = classtypes.ghostobj;
        newobj.speed = SPDDOG;

        newobj.dir = objdirtypes.east;
        newobj.flags |= objflags.FL_AMBUSH;
        if (!loadedgame)
        {
            gamestate.killtotal++;
            gamestate.killcount++;
        }
    }


    internal static void SpawnSchabbs(int tilex, int tiley)
    {
        objstruct newobj = null!;

        if (DigiMode != SDSMode.Off)
            s_giftdie2.tictime = 140;
        else
            s_giftdie2.tictime = 5;

        newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_schabbstand);
        newobj.speed = SPDPATROL;

        newobj.obclass = classtypes.schabbobj;
        newobj.hitpoints = starthitpoints[(int)gamestate.difficulty, (short)enemytypes.en_schabbs];
        newobj.dir = objdirtypes.nodir;
        newobj.flags |= objflags.FL_SHOOTABLE | objflags.FL_AMBUSH;
        if (!loadedgame)
            gamestate.killtotal++;
    }

    internal static void SpawnFat(int tilex, int tiley)
    {
        objstruct newobj = null!;

        if (DigiMode != SDSMode.Off)
            s_giftdie2.tictime = 140;
        else
            s_giftdie2.tictime = 5;

        newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_fatstand);
        newobj.speed = SPDPATROL;

        newobj.obclass = classtypes.fatobj;
        newobj.hitpoints = starthitpoints[(int)gamestate.difficulty, (short)enemytypes.en_fat];
        newobj.dir = objdirtypes.nodir;
        newobj.flags |= objflags.FL_SHOOTABLE | objflags.FL_AMBUSH;
        if (!loadedgame)
            gamestate.killtotal++;
    }

    internal static void T_Stand(objstruct ob)
    {
        SightPlayer(ob);
    }

    /*
    ============================================================================

    CHASE

    ============================================================================
    */

    /*
    =================
    =
    = T_Chase
    =
    =================
    */
    internal static void T_Chase(objstruct ob)
    {
        int move, target;
        int dx, dy, dist, chance;
        bool dodge;

        if (gamestate.victoryflag)
            return;

        dodge = false;
        if (CheckLine(ob))      // got a shot at player?
        {
            ob.hidden = false;
            dx = Math.Abs(ob.tilex - player.tilex);
            dy = Math.Abs(ob.tiley - player.tiley);
            dist = dx > dy ? dx : dy;

            if ((demorecord || demoplayback))
            {
                if (dist ==0 || (dist == 1 && ob.distance < 0x4000))
                    chance = 300;
                else
                    chance = (int)((tics << 4) / dist);
            }
            else
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
                switch (ob.obclass)
                {
                    case classtypes.guardobj:
                        NewState(ob, s_grdshoot1);
                        break;
                    case classtypes.officerobj:
                        NewState(ob, s_ofcshoot1);
                        break;
                    case classtypes.mutantobj:
                        NewState(ob, s_mutshoot1);
                        break;
                    case classtypes.ssobj:
                        NewState(ob, s_ssshoot1);
                        break;
                    case classtypes.bossobj:
                        NewState(ob, s_bossshoot1);
                        break;
                    case classtypes.gretelobj:
                        NewState(ob, s_gretelshoot1);
                        break;
                    case classtypes.mechahitlerobj:
                        NewState(ob, s_mechashoot1);
                        break;
                    case classtypes.realhitlerobj:
                        NewState(ob, s_hitlershoot1);
                        break;
                }
                return;
            }
            dodge = true;
        }
        else
            ob.hidden = true;

        if (ob.dir == objdirtypes.nodir)
        {
            if (dodge)
                SelectDodgeDir(ob);
            else
                SelectChaseDir(ob);
            if (ob.dir == objdirtypes.nodir)
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
                if ((!(demorecord || demoplayback)))
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

            if (ob.dir == objdirtypes.nodir)
                return;                                                 // object is blocked in
        }
    }

    /*
    =================
    =
    = T_DogChase
    =
    =================
    */

    internal static void T_DogChase(objstruct ob)
    {
        int move;
        int dx, dy;


        if (ob.dir == objdirtypes.nodir)
        {
            SelectDodgeDir(ob);
            if (ob.dir == objdirtypes.nodir)
                return;                                                 // object is blocked in
        }

        move = (int)(ob.speed * tics);

        while (move != 0)
        {
            //
            // check for byte range
            //
            dx = player.x - ob.x;
            if (dx < 0)
                dx = -dx;
            dx -= move;
            if (dx <= MINACTORDIST)
            {
                dy = player.y - ob.y;
                if (dy < 0)
                    dy = -dy;
                dy -= move;
                if (dy <= MINACTORDIST)
                {
                    NewState(ob, s_dogjump1);
                    return;
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

            SelectDodgeDir(ob);

            if (ob.dir == objdirtypes.nodir)
                return;                                                 // object is blocked in
        }
    }

    internal static void T_Path(objstruct ob)
    {
        int move;

        if (SightPlayer(ob))
            return;

        if (ob.dir == objdirtypes.nodir)
        {
            SelectPathDir(ob);
            if (ob.dir == objdirtypes.nodir)
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

            if (ob.tilex > MapManager.MAPSIZE || ob.tiley > MapManager.MAPSIZE)
                _gameEngineManager.Quit($"T_Path hit a wall at {ob.tilex},{ob.tiley}, dir {ob.dir}");

            ob.x = (int)((ob.tilex << TILESHIFT) + TILEGLOBAL / 2);
            ob.y = (int)((ob.tiley << TILESHIFT) + TILEGLOBAL / 2);
            move -= ob.distance;

            SelectPathDir(ob);

            if (ob.dir == objdirtypes.nodir)
                return;                                 // all movement is blocked
        }
    }

    /*
    =============================================================================

                                        FIGHT

    =============================================================================
    */


    /*
    ===============
    =
    = T_Shoot
    =
    = Try to damage the player, based on skill level and player's speed
    =
    ===============
    */
    internal static void T_Shoot(objstruct ob)
    {
        int dx, dy, dist;
        int hitchance, damage;

        hitchance = 128;

        if (ob.areanumber < MapConstants.NUMAREAS && areabyplayer[ob.areanumber] == 0)
            return;

        if (CheckLine(ob))                    // player is not behind a wall
        {
            dx = Math.Abs(ob.tilex - player.tilex);
            dy = Math.Abs(ob.tiley - player.tiley);
            dist = dx > dy ? dx : dy;

            if (ob.obclass == classtypes.ssobj || ob.obclass == classtypes.bossobj)
                dist = dist * 2 / 3;                                        // ss are better shots

            if (thrustspeed >= RUNSPEED)
            {
                if (ob.flags.HasFlag(objflags.FL_VISABLE))
                    hitchance = 160 - dist * 16;                // player can see to dodge
                else
                    hitchance = 160 - dist * 8;
            }
            else
            {
                if (ob.flags.HasFlag(objflags.FL_VISABLE))
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

        switch (ob.obclass)
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
        // TODO: mapinfo requires a flag like ecwolf's "secretdeathsounds"
        //if (gamestate.mapon == 9 && US_RndT() == 0)
        //{
        //    switch (ob.obclass)
        //    {
        //        case classtypes.mutantobj:
        //        case classtypes.guardobj:
        //        case classtypes.officerobj:
        //        case classtypes.ssobj:
        //        case classtypes.dogobj:
        //            PlaySoundLocActor((int)soundnames.DEATHSCREAM6SND, ob);
        //            return;
        //    }
        //}

        switch (ob.obclass)
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

        spot = (uint)(_mapManager.MAPSPOT(ob.tilex, ob.tiley, 1) - MapConstants.ICONARROWS);

        if (spot < 8)
        {
            // new direction
            ob.dir = (objdirtypes)spot;
        }

        ob.distance = (int)TILEGLOBAL;

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

        xl = (int)((ob.x - PLAYERSIZE) >> TILESHIFT);
        yl = (int)((ob.y - PLAYERSIZE) >> TILESHIFT);

        xh = (int)((ob.x + PLAYERSIZE) >> TILESHIFT);
        yh = (int)((ob.y + PLAYERSIZE) >> TILESHIFT);

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

    /*
    ===============
    =
    = A_StartDeathCam
    =
    ===============
    */

    internal static void A_StartDeathCam(objstruct ob)
    {
        int dx, dy;
        float fangle;
        int xmove, ymove;
        int dist;

        _videoManager.FinishPaletteShifts();

        GameEngineManager.WaitVBL(100);

        if (gamestate.victoryflag)
        {
            playstate = playstatetypes.ex_victorious;                              // exit castle tile
            return;
        }

        gamestate.victoryflag = true;
        uint fadeheight = (uint)(viewsize != 21 ? _videoManager.screenHeight - _videoManager.scaleFactor * STATUSLINES : _videoManager.screenHeight);
        _videoManager.BarScaledCoord(0, 0, _videoManager.screenWidth, (int)fadeheight, bordercol);
        _videoManager.FizzleFade(0, 0, (uint)_videoManager.screenWidth, fadeheight, 70, false);

        if (bordercol != VIEWCOLOR)
        {
            fontnumber = 1;
            SETFONTCOLOR(15, bordercol);
            PrintX = 68; PrintY = 45;
            US_Print(STR_SEEAGAIN);
        }
        else
        {
            Write(0, 7, STR_SEEAGAIN);
        }

        _videoManager.Update();

        _inputManager.UserInput(300);

        //
        // line angle up exactly
        //
        NewState(player, s_deathcam);

        player.x = gamestate.killx;
        player.y = gamestate.killy;

        dx = ob.x - player.x;
        dy = player.y - ob.y;

        fangle = (float)Math.Atan2((float)dy, (float)dx);          // returns -pi to pi
        if (fangle < 0)
            fangle = (float)(M_PI * 2 + fangle);

        player.angle = (short)(fangle / (M_PI * 2) * ANGLES);

        //
        // try to position as close as possible without being in a wall
        //
        dist = 0x14000;
        do
        {
            xmove = FixedMul(dist, costable[player.angle]);
            ymove = -FixedMul(dist, sintable[player.angle]);

            player.x = ob.x - xmove;
            player.y = ob.y - ymove;
            dist += 0x1000;

        } while (!CheckPosition(player));
        plux = (ushort)(player.x >> UNSIGNEDSHIFT);                      // scale to fit in unsigned
        pluy = (ushort)(player.y >> UNSIGNEDSHIFT);
        player.tilex = (byte)(player.x >> TILESHIFT);         // scale to tile values
        player.tiley = (byte)(player.y >> TILESHIFT);

        //
        // go back to the game
        //

        DrawPlayBorder();

        fizzlein = true;

        switch (ob.obclass)
        {
            case classtypes.schabbobj:
                NewState(ob, s_schabbdeathcam);
                break;
            case classtypes.realhitlerobj:
                NewState(ob, s_hitlerdeathcam);
                break;
            case classtypes.giftobj:
                NewState(ob, s_giftdeathcam);
                break;
            case classtypes.fatobj:
                NewState(ob, s_fatdeathcam);
                break;
        }
    }
}
