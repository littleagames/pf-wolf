namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                                    LOCAL CONSTANTS

    =============================================================================
    */

    internal const int MAXMOUSETURN   = 10;


    internal const long MOVESCALE = 150L;
    internal const long BACKMOVESCALE = 100L;
    internal const int ANGLESCALE = 20;

    /*
    =============================================================================

                                    GLOBAL VARIABLES

    =============================================================================
    */

    //
    // player state info
    //
    static int thrustspeed;

    static ushort plux, pluy;          // player coordinates scaled to unsigned

    static short anglefrac;

    static objstruct? LastAttacker;

    /*
    =============================================================================

                                                     LOCAL VARIABLES

    =============================================================================
    */

    internal static statestruct s_player = new(0/*false*/, 0, 0, T_Player, null, null);
    internal static statestruct s_attack = new(0/*false*/, 0, 0, T_Attack, null, null);

    internal static Dictionary<string, statestruct> PlayerStateDict = new()
    {
        { "s_player", s_player },
        { "s_attack", s_attack }
    };

    internal static List<statestruct> PlayerStateList => PlayerStateDict.Values.ToList();

    internal struct atkinf
    {
        public short tics, attack, frame;

        public atkinf(short tics, short attack, short frame)
        {
            this.tics = tics;
            this.attack = attack;
            this.frame = frame;
        }
    }

    internal static atkinf[,] attackinfo =
    {
        { new atkinf(6,0,1), new atkinf(6,2,2), new atkinf(6,0,3), new atkinf(6,-1,4) },
        { new atkinf(6,0,1), new atkinf(6,1,2), new atkinf(6,0,3), new atkinf(6,-1,4) },
        { new atkinf(6,0,1), new atkinf(6,1,2), new atkinf(6,3,3), new atkinf(6,-1,4) },
        { new atkinf(6,0,1), new atkinf(6,1,2), new atkinf(6,4,3), new atkinf(6,-1,4) },
    };

    internal static void CheckWeaponChange()
    {
        int i, newWeapon = -1;

        if (gamestate.ammo == 0)            // must use knife with no ammo
            return;
        if (buttonstate[(int)buttontypes.bt_nextweapon] && !buttonheld[(int)buttontypes.bt_nextweapon])
        {
            newWeapon = gamestate.weapon + 1;
            if (newWeapon > gamestate.bestweapon) newWeapon = 0;
        }
        else if (buttonstate[(int)buttontypes.bt_prevweapon] && !buttonheld[(int)buttontypes.bt_prevweapon])
        {
            newWeapon = gamestate.weapon - 1;
            if (newWeapon < 0) newWeapon = gamestate.bestweapon;
        }
        else
        {
            for (i = (int)weapontypes.wp_knife; i <= gamestate.bestweapon; i++)
            {
                if (buttonstate[(int)buttontypes.bt_readyknife + i - (int)weapontypes.wp_knife])
                {
                    newWeapon = i;
                    break;
                }
            }
        }

        if (newWeapon != -1)
        {
            gamestate.weapon = gamestate.chosenweapon = (short)newWeapon;
            DrawWeapon();
        }
    }

    internal static void ControlMovement(objstruct ob)
    {
        int angle;
        int angleunits;

        thrustspeed = 0;

        if (buttonstate[(int)buttontypes.bt_strafeleft])
        {
            angle = ob.angle + ANGLES / 4;
            if (angle >= ANGLES)
                angle -= ANGLES;
            if (buttonstate[(int)buttontypes.bt_run])
                Thrust(angle, (int)(RUNMOVE * MOVESCALE * tics));
            else
                Thrust(angle, (int)(BASEMOVE * MOVESCALE * tics));
        }

        if (buttonstate[(int)buttontypes.bt_straferight])
        {
            angle = ob.angle - ANGLES / 4;
            if (angle < 0)
                angle += ANGLES;
            if (buttonstate[(int)buttontypes.bt_run])
                Thrust(angle, (int)(RUNMOVE * MOVESCALE * tics));
            else
                Thrust(angle, (int)(BASEMOVE * MOVESCALE * tics));
        }

        //
        // side to side move
        //
        if (buttonstate[(int)buttontypes.bt_strafe])
        {
            //
            // strafing
            //
            //
            if (controlx > 0)
            {
                angle = ob.angle - ANGLES / 4;
                if (angle < 0)
                    angle += ANGLES;
                Thrust(angle, (int)(controlx * MOVESCALE));      // move to left
            }
            else if (controlx < 0)
            {
                angle = ob.angle + ANGLES / 4;
                if (angle >= ANGLES)
                    angle -= ANGLES;
                Thrust(angle, (int)(-controlx * MOVESCALE));     // move to right
            }
        }
        else
        {
            //
            // not strafing
            //
            anglefrac += (short)controlx;
            angleunits = anglefrac / ANGLESCALE;
            anglefrac -= (short)(angleunits * ANGLESCALE);
            ob.angle -= (short)angleunits;

            if (ob.angle >= ANGLES)
                ob.angle -= ANGLES;
            if (ob.angle < 0)
                ob.angle += ANGLES;
        }

        //
        // forward/backwards move
        //
        if (controly < 0)
        {
            Thrust(ob.angle, (int)(-controly * MOVESCALE)); // move forwards
        }
        else if (controly > 0)
        {
            angle = ob.angle + ANGLES / 2;
            if (angle >= ANGLES)
                angle -= ANGLES;
            Thrust(angle, (int)(controly * BACKMOVESCALE));          // move backwards
        }
    }

    internal static void Thrust(int angle, int speed)
    {
        int xmove, ymove;
        //
        // ZERO FUNNY COUNTER IF MOVED!
        //
        thrustspeed += speed;
        //
        // moving bounds speed
        //
        if (speed >= MINDIST * 2)
            speed = (int)(MINDIST * 2 - 1);

        xmove = //DEMOCHOOSE_ORIG_SDL(
                 //   FixedByFracOrig(speed, costable[angle]),
                    FixedMul(speed, costable[angle]);
        ymove =// DEMOCHOOSE_ORIG_SDL(
                 //   -FixedByFracOrig(speed, sintable[angle]),
                    -FixedMul(speed, sintable[angle]);

        ClipMove(player, xmove, ymove);

        player.tilex = (byte)(player.x >> (int)TILESHIFT);                // scale to tile values
        player.tiley = (byte)(player.y >> (int)TILESHIFT);

        player.areanumber = (byte)(MAPSPOT(player.tilex, player.tiley, 0) - AREATILE);

        if (MAPSPOT(player.tilex, player.tiley, 1) == EXITTILE)
            VictoryTile();
    }

    internal static bool TryMove(objstruct ob)
    {
        uint xl, yl, xh, yh, x, y;
        //uint checkIndex;
        Actor? check;
        int deltax, deltay;

        xl = (uint)((ob.x - PLAYERSIZE) >> (int)TILESHIFT);
        yl = (uint)((ob.y - PLAYERSIZE) >> (int)TILESHIFT);

        xh = (uint)((ob.x + PLAYERSIZE) >> (int)TILESHIFT);
        yh = (uint)((ob.y + PLAYERSIZE) >> (int)TILESHIFT);

        const long PUSHWALLMINDIST = PLAYERSIZE;

        //
        // check for solid walls
        //
        for (y = yl; y <= yh; y++)
        {
            for (x = xl; x <= xh; x++)
            {
                check = actorat[x, y];
                if (check != null && !ISPOINTER(check))
                {
                    if (tilemap[x, y] == BIT_WALL && x == pwallx && y == pwally)   // back of moving pushwall?
                    {
                        switch (pwalldir)
                        {
                            case controldirs.di_north:
                                if (ob.y - PUSHWALLMINDIST <= (pwally << (int)TILESHIFT) + ((63 - pwallpos) << 10))
                                    return false;
                                break;
                            case controldirs.di_west:
                                if (ob.x - PUSHWALLMINDIST <= (pwallx << (int)TILESHIFT) + ((63 - pwallpos) << 10))
                                    return false;
                                break;
                            case controldirs.di_east:
                                if (ob.x + PUSHWALLMINDIST >= (pwallx << (int)TILESHIFT) + (pwallpos << 10))
                                    return false;
                                break;
                            case controldirs.di_south:
                                if (ob.y + PUSHWALLMINDIST >= (pwally << (int)TILESHIFT) + (pwallpos << 10))
                                    return false;
                                break;
                        }
                    }
                    else return false;
                }
            }
        }

        //
        // check for actors
        //
        if (yl > 0)
            yl--;
        if (yh < MAPSIZE - 1)
            yh++;
        if (xl > 0)
            xl--;
        if (xh < MAPSIZE - 1)
            xh++;

        for (y = yl; y <= yh; y++)
        {
            for (x = xl; x <= xh; x++)
            {
                check = actorat[x, y];
                // TODO: !check.Equals(player) might not operate correctly obclass != playerobj
                if (check is objstruct actor && actor.obclass != classtypes.playerobj && actor.flags.HasFlag(objflags.FL_SHOOTABLE))
                {
                    deltax = ob.x - check.x;
                    if (deltax < -MINACTORDIST || deltax > MINACTORDIST)
                        continue;
                    deltay = ob.y - check.y;
                    if (deltay < -MINACTORDIST || deltay > MINACTORDIST)
                        continue;

                    return false;
                }
            }
        }

        return true;
    }

    internal static void ClipMove(objstruct ob, int xmove, int ymove)
    {
        int basex, basey;

        basex = ob.x;
        basey = ob.y;

        ob.x = basex + xmove;
        ob.y = basey + ymove;
        if (TryMove(ob))
            return;

        if (noclip != 0 && ob.x > 2 * TILEGLOBAL && ob.y > 2 * TILEGLOBAL
            && ob.x < (((int)(mapwidth - 1)) << (int)TILESHIFT)
            && ob.y < (((int)(mapheight - 1)) << (int)TILESHIFT))
            return;         // walk through walls

        if (SD_SoundPlaying() == 0)
            SD_PlaySound((int)soundnames.HITWALLSND);

        ob.x = basex + xmove;
        ob.y = basey;
        if (TryMove(ob))
            return;

        ob.x = basex;
        ob.y = basey + ymove;
        if (TryMove(ob))
            return;

        ob.x = basex;
        ob.y = basey;
    }

    internal static void VictoryTile()
    {
        SpawnBJVictory();
        gamestate.victoryflag = 1; // true
    }


    internal static void GetBonus(statobj_t check)
    {
        if (playstate == (byte)playstatetypes.ex_died)   // ADDEDFIX 31 - Chris
            return;

        switch ((wl_stat_types)check.itemnumber)
        {
            case wl_stat_types.bo_firstaid:
                if (gamestate.health == 100)
                    return;

                SD_PlaySound((int)soundnames.HEALTH2SND);
                HealSelf(25);
                break;

            case wl_stat_types.bo_key1:
            case wl_stat_types.bo_key2:
            case wl_stat_types.bo_key3:
            case wl_stat_types.bo_key4:
                GiveKey(check.itemnumber - (int)wl_stat_types.bo_key1);
                SD_PlaySound((int)soundnames.GETKEYSND);
                break;

            case wl_stat_types.bo_cross:
                SD_PlaySound((int)soundnames.BONUS1SND);
                GivePoints(100);
                gamestate.treasurecount++;
                break;
            case wl_stat_types.bo_chalice:
                SD_PlaySound((int)soundnames.BONUS2SND);
                GivePoints(500);
                gamestate.treasurecount++;
                break;
            case wl_stat_types.bo_bible:
                SD_PlaySound((int)soundnames.BONUS3SND);
                GivePoints(1000);
                gamestate.treasurecount++;
                break;
            case wl_stat_types.bo_crown:
                SD_PlaySound((int)soundnames.BONUS4SND);
                GivePoints(5000);
                gamestate.treasurecount++;
                break;

            case wl_stat_types.bo_clip:
                if (gamestate.ammo == 99)
                    return;

                SD_PlaySound((int)soundnames.GETAMMOSND);
                GiveAmmo(8);
                break;
            case wl_stat_types.bo_clip2:
                if (gamestate.ammo == 99)
                    return;

                SD_PlaySound((int)soundnames.GETAMMOSND);
                GiveAmmo(4);
                break;

            case wl_stat_types.bo_machinegun:
                SD_PlaySound((int)soundnames.GETMACHINESND);
                GiveWeapon((int)weapontypes.wp_machinegun);
                break;
            case wl_stat_types.bo_chaingun:
                SD_PlaySound((int)soundnames.GETGATLINGSND);
                facetimes = 38;
                GiveWeapon((int)weapontypes.wp_chaingun);

                if (viewsize != 21)
                    StatusDrawFace((int)graphicnums.GOTGATLINGPIC);
                facecount = 0;
                break;

            case wl_stat_types.bo_fullheal:
                SD_PlaySound((int)soundnames.BONUS1UPSND);
                HealSelf(99);
                GiveAmmo(25);
                GiveExtraMan();
                gamestate.treasurecount++;
                break;

            case wl_stat_types.bo_food:
                if (gamestate.health == 100)
                    return;

                SD_PlaySound((int)soundnames.HEALTH1SND);
                HealSelf(10);
                break;

            case wl_stat_types.bo_alpo:
                if (gamestate.health == 100)
                    return;

                SD_PlaySound((int)soundnames.HEALTH1SND);
                HealSelf(4);
                break;

            case wl_stat_types.bo_gibs:
                if (gamestate.health > 10)
                    return;

                SD_PlaySound((int)soundnames.SLURPIESND);
                HealSelf(1);
                break;
        }

        StartBonusFlash();
        check.shapenum = -1;                   // remove from list
    }


    static void StatusDrawPic (uint x, uint y, uint picnum)
    {
        VWB_DrawPicScaledCoord((int)(((screenWidth - scaleFactor * 320) / 16 + scaleFactor * x) * 8),
            (int)(screenHeight - scaleFactor * (STATUSLINES - y)), (int)picnum);
    }

    static void StatusDrawFace(uint picnum)
    {
        StatusDrawPic(17, 4, picnum);
    }

    static void LatchNumber(int x, int y, uint width, int number)
    {
        uint length, c;
        string str;

        str = number.ToString();

        length = (uint)str.Length;
        while (length < width)
        {
            StatusDrawPic((uint)x, (uint)y, (int)graphicnums.N_BLANKPIC);
            x++;
            width--;
        }

        c = length <= width ? 0 : length - width;

        while (c < length)
        {
            StatusDrawPic((uint)x, (uint)y, (uint)(str[(int)c] - '0' + (int)graphicnums.N_0PIC));
            x++;
            c++;
        }
    }

    static void DrawAmmo()
    {
        if (viewsize == 21 && ingame) return;
        LatchNumber(27, 16, 2, gamestate.ammo);
    }

/*
===============
=
= GiveAmmo
=
===============
*/

    internal static void GiveAmmo(int ammo)
    {
        if (gamestate.ammo != 0)                            // knife was out
        {
            if (gamestate.attackframe != 0)
            {
                gamestate.weapon = gamestate.chosenweapon;
                DrawWeapon();
            }
        }
        gamestate.ammo += (short)ammo;
        if (gamestate.ammo > 99)
            gamestate.ammo = 99;
        DrawAmmo();
    }

    static void DrawFace()
    {
        if (viewsize == 21 && ingame) return;
        if (SD_SoundPlaying() == (int)soundnames.GETGATLINGSND)
            StatusDrawFace((uint)graphicnums.GOTGATLINGPIC);
        else if (gamestate.health != 0)
        {
            StatusDrawFace((uint)((uint)graphicnums.FACE1APIC + 3 * ((100 - gamestate.health) / 16) + gamestate.faceframe));
        }
        else
        {
            if (LastAttacker != null && LastAttacker.obclass == classtypes.needleobj)
                StatusDrawFace((int)graphicnums.MUTANTBJPIC);
            else
                StatusDrawFace((uint)graphicnums.FACE8APIC);
        }
    }

    /*
    ===============
    =
    = UpdateFace
    =
    = Calls draw face if time to change
    =
    ===============
    */

    static int facecount = 0;
    static int facetimes = 0;

    internal static void UpdateFace()
    {
        // don't make demo depend on sound playback
        if (demoplayback || demorecord)
        {
            if (facetimes > 0)
            {
                facetimes--;
                return;
            }
        }
        else if (SD_SoundPlaying() == (int)soundnames.GETGATLINGSND)
            return;

        facecount += (int)tics;
        if (facecount > US_RndT())
        {
            gamestate.faceframe = (short)(US_RndT() >> 6);
            if (gamestate.faceframe == 3)
                gamestate.faceframe = 1;

            facecount = 0;
            DrawFace();
        }
    }

    static void DrawHealth()
    {
        if (viewsize == 21 && ingame) return;
        LatchNumber(21, 16, 3, gamestate.health);
    }

    /*
    ===============
    =
    = TakeDamage
    =
    ===============
    */

    internal static void TakeDamage(int points, objstruct attacker)
    {
        LastAttacker = attacker;

        if (gamestate.victoryflag != 0)
            return;
        if (gamestate.difficulty == (short)difficultytypes.gd_baby)
            points >>= 2;

        if (godmode == 0)
            gamestate.health -= (short)points;

        if (gamestate.health <= 0)
        {
            gamestate.health = 0;
            playstate = (byte)playstatetypes.ex_died;
        }

        if (godmode != 2)
            StartDamageFlash(points);

        DrawHealth();
        DrawFace();
    }

    internal static void HealSelf(int points)
    {
        gamestate.health += (short)points;
        if (gamestate.health > 100)
            gamestate.health = 100;

        DrawHealth();
        DrawFace();
    }

    static void DrawKeys()
    {
        if (viewsize == 21 && ingame) return;
        if ((gamestate.keys & 1) != 0)
            StatusDrawPic(30, 4, (int)graphicnums.GOLDKEYPIC);
        else
            StatusDrawPic(30, 4, (int)graphicnums.NOKEYPIC);

        if ((gamestate.keys & 2) != 0)
            StatusDrawPic(30, 20, (int)graphicnums.SILVERKEYPIC);
        else
            StatusDrawPic(30, 20, (int)graphicnums.NOKEYPIC);
    }

    static void GiveKey(int key)
    {
        gamestate.keys |= (short)(1 << key);
        DrawKeys();
    }

    static void DrawLevel()
    {
        if (viewsize == 21 && ingame) return;
        LatchNumber(2, 16, 2, gamestate.mapon + 1);
    }

    static void DrawLives()
    {
        if (viewsize == 21 && ingame) return;
        LatchNumber(14, 16, 1, gamestate.lives);
    }

    /*
    ===============
    =
    = GiveExtraMan
    =
    ===============
    */

    static void GiveExtraMan()
    {
        if (gamestate.lives < 9)
            gamestate.lives++;
        DrawLives();
        SD_PlaySound((int)soundnames.BONUS1UPSND);
    }

    static void DrawScore()
    {
        if (viewsize == 21 && ingame) return;
        LatchNumber(6, 16, 6, gamestate.score);
    }

    /*
    ===============
    =
    = GivePoints
    =
    ===============
    */

    static void GivePoints(int points)
    {
        gamestate.score += points;
        while (gamestate.score >= gamestate.nextextra)
        {
            gamestate.nextextra += EXTRAPOINTS;
            GiveExtraMan();
        }
        DrawScore();
    }

    static void DrawWeapon()
    {
        if (viewsize == 21 && ingame) return;
        StatusDrawPic(32, 8, (uint)(graphicnums.KNIFEPIC + gamestate.weapon));
    }
/*
==================
=
= GiveWeapon
=
==================
*/

    internal static void GiveWeapon(int weapon)
    {
        GiveAmmo(6);

        if (gamestate.bestweapon < weapon)
            gamestate.bestweapon = gamestate.weapon
            = gamestate.chosenweapon = (short)weapon;

        DrawWeapon();
    }


    internal static void Cmd_Use()
    {
        int checkx, checky, cmdtile, dir;
        bool elevatorok;

        //
        // find which cardinal direction the player is facing
        //
        if (player.angle < ANGLES / 8 || player.angle > 7 * ANGLES / 8)
        {
            checkx = player.tilex + 1;
            checky = player.tiley;
            dir = (int)controldirs.di_east;
            elevatorok = true;
        }
        else if (player.angle < 3 * ANGLES / 8)
        {
            checkx = player.tilex;
            checky = player.tiley - 1;
            dir = (int)controldirs.di_north;
            elevatorok = false;
        }
        else if (player.angle < 5 * ANGLES / 8)
        {
            checkx = player.tilex - 1;
            checky = player.tiley;
            dir = (int)controldirs.di_west;
            elevatorok = true;
        }
        else
        {
            checkx = player.tilex;
            checky = player.tiley + 1;
            dir = (int)controldirs.di_south;
            elevatorok = false;
        }

        cmdtile = tilemap[checkx, checky];
        if (MAPSPOT(checkx, checky, 1) == PUSHABLETILE)
        {
            //
            // pushable wall
            //

            PushWall(checkx, checky, dir);
            return;
        }
        if (!buttonheld[(int)buttontypes.bt_use] && cmdtile == ELEVATORTILE && elevatorok)
        {
            //
            // use elevator
            //
            buttonheld[(int)buttontypes.bt_use] = true;

            tilemap[checkx, checky]++;              // flip switch
            if (MAPSPOT(player.tilex, player.tiley, 0) == ALTELEVATORTILE)
                playstate = (byte)playstatetypes.ex_secretlevel;
            else
                playstate = (byte)playstatetypes.ex_completed;
            SD_PlaySound((int)soundnames.LEVELDONESND);
            SD_WaitSoundDone();
        }
        else if (!buttonheld[(int)buttontypes.bt_use] && (cmdtile & BIT_DOOR) != 0)
        {
            buttonheld[(int)buttontypes.bt_use] = true;
            OperateDoor(cmdtile & ~BIT_DOOR);
        }
        else
            SD_PlaySound((int)soundnames.DONOTHINGSND);
    }

    internal static void Cmd_Fire()
    {
        buttonheld[(int)buttontypes.bt_attack] = true;

        gamestate.weaponframe = 0;

        player.state = s_attack;

        gamestate.attackframe = 0;
        gamestate.attackcount =
            attackinfo[gamestate.weapon, gamestate.attackframe].tics;
        gamestate.weaponframe =
            attackinfo[gamestate.weapon, gamestate.attackframe].frame;
    }

    //===========================================================================

    /*
    ===============
    =
    = T_Player
    =
    ===============
    */
    internal static void T_Player(objstruct ob)
    {
        if (gamestate.victoryflag != 0)              // watching the BJ actor
        {
            VictorySpin();
            return;
        }

        UpdateFace();
        CheckWeaponChange();

        if (buttonstate[(int)buttontypes.bt_use])
            Cmd_Use();

        if (buttonstate[(int)buttontypes.bt_attack] && !buttonheld[(int)buttontypes.bt_attack])
            Cmd_Fire();

        ControlMovement(ob);
        if (gamestate.victoryflag != 0)              // watching the BJ actor
            return;

        plux = (ushort)(player.x >> UNSIGNEDSHIFT);                     // scale to fit in unsigned
        pluy = (ushort)(player.y >> UNSIGNEDSHIFT);
        player.tilex = (byte)(player.x >> (int)TILESHIFT);                // scale to tile values
        player.tiley = (byte)(player.y >> (int)TILESHIFT);
    }

    internal static void T_Attack(objstruct ob)
    {
        atkinf cur;
        UpdateFace();

        if (gamestate.victoryflag != 0)              // watching the BJ actor
        {
            VictorySpin();
            return;
        }


        if (buttonstate[(int)buttontypes.bt_use] && !buttonheld[(int)buttontypes.bt_use])
            buttonstate[(int)buttontypes.bt_use] = false;

        if (buttonstate[(int)buttontypes.bt_attack] && !buttonheld[(int)buttontypes.bt_attack])
            buttonstate[(int)buttontypes.bt_attack] = false;

        ControlMovement(ob);
        if (gamestate.victoryflag != 0)              // watching the BJ actor
            return;

        plux = (ushort)(player.x >> UNSIGNEDSHIFT);                     // scale to fit in unsigned
        pluy = (ushort)(player.y >> UNSIGNEDSHIFT);
        player.tilex = (byte)(player.x >> (int)TILESHIFT);                // scale to tile values
        player.tiley = (byte)(player.y >> (int)TILESHIFT);

        //
        // change frame and fire
        //
        gamestate.attackcount -= (short)tics;
        while (gamestate.attackcount <= 0)
        {
            cur = attackinfo[gamestate.weapon,gamestate.attackframe];
            switch (cur.attack)
            {
                case -1:
                    ob.state = s_player;
                    if (gamestate.ammo == 0)
                    {
                        gamestate.weapon = (short)weapontypes.wp_knife;
                        DrawWeapon();
                    }
                    else
                    {
                        if (gamestate.weapon != gamestate.chosenweapon)
                        {
                            gamestate.weapon = gamestate.chosenweapon;
                            DrawWeapon();
                        }
                    }
                    gamestate.attackframe = gamestate.weaponframe = 0;
                    return;

                case 4:
                    if (gamestate.ammo == 0)
                        break;
                    if (buttonstate[(int)buttontypes.bt_attack])
                        gamestate.attackframe -= 2;
                    // case passthrough is not a thing in C#, repeating case 1 code
                    if (gamestate.ammo == 0)
                    {       // can only happen with chain gun
                        gamestate.attackframe++;
                        break;
                    }
                    GunAttack(ob);
                    if (ammocheat == 0)
                        gamestate.ammo--;
                    DrawAmmo();
                    break;
                case 1:
                    if (gamestate.ammo == 0)
                    {       // can only happen with chain gun
                        gamestate.attackframe++;
                        break;
                    }
                    GunAttack(ob);
                    if (ammocheat == 0)
                        gamestate.ammo--;
                    DrawAmmo();
                    break;

                case 2:
                    KnifeAttack(ob);
                    break;

                case 3:
                    if (gamestate.ammo != 0 && buttonstate[(int)buttontypes.bt_attack])
                        gamestate.attackframe -= 2;
                    break;
            }

            gamestate.attackcount += cur.tics;
            gamestate.attackframe++;
            gamestate.weaponframe =
                attackinfo[gamestate.weapon, gamestate.attackframe].frame;
        }
    }

    internal static void KnifeAttack(objstruct ob)
    {
        objstruct? closest;
        int dist;

        SD_PlaySound(((int)soundnames.ATKKNIFESND));
        // actually fire
        dist = 0x7fffffff;
        closest = null;
        foreach (var actor in objlist2)
        //for (int? i = ob.next; i != null; i = check?.next)
        {
            //check = objlist[i.Value];
            if (actor == null) continue;

            if (actor.flags.HasFlag(objflags.FL_SHOOTABLE) && actor.flags.HasFlag(objflags.FL_VISABLE)
                && Math.Abs(actor.viewx - centerx) < shootdelta)
            {
                if (actor.transx < dist)
                {
                    dist = actor.transx;
                    closest = actor;
                }
            }
        }

        if (closest == null || dist > 0x18000L)
        {
            // missed
            return;
        }

        // hit something
        DamageActor(closest, (uint)(US_RndT() >> 4));
    }

    internal static void GunAttack(objstruct ob)
    {
        objstruct? closest,oldclosest;
        int damage;
        int dx, dy, dist;
        long viewdist;

        switch ((weapontypes)gamestate.weapon)
        {
            case weapontypes.wp_pistol:
                SD_PlaySound((int)soundnames.ATKPISTOLSND);
                break;
            case weapontypes.wp_machinegun:
                SD_PlaySound((int)soundnames.ATKMACHINEGUNSND);
                break;
            case weapontypes.wp_chaingun:
                SD_PlaySound((int)soundnames.ATKGATLINGSND);
                break;
        }

        madenoise = true;

        //
        // find potential targets
        //
        viewdist = 0x7fffffffL;
        closest = null;

        while (true)
        {
            oldclosest = closest;

            foreach (var check in objlist2)
            //for (int? i = ob.next; i != null; i = check?.next)
            //for (check = ob->next; check; check = check->next)
            {
                //check = objlist[i.Value];
                if (check == null) continue;
                if (check.flags.HasFlag(objflags.FL_SHOOTABLE) && check.flags.HasFlag(objflags.FL_VISABLE)
                    && Math.Abs(check.viewx - centerx) < shootdelta)
                {
                    if (check.transx < viewdist)
                    {
                        viewdist = check.transx;
                        closest = check;
                    }
                }
            }

            if (closest == oldclosest)
                return;                                         // no more targets, all missed

            //
            // trace a line from player to enemey
            //
            if (CheckLine(closest))
                break;
        }

        //
        // hit something
        //
        dx = Math.Abs(closest.tilex - player.tilex);
        dy = Math.Abs(closest.tiley - player.tiley);
        dist = dx > dy ? dx : dy;
        if (dist < 2)
            damage = US_RndT() / 4;
        else if (dist < 4)
            damage = US_RndT() / 6;
        else
        {
            if ((US_RndT() / 12) < dist)           // missed
                return;
            damage = US_RndT() / 6;
        }

        DamageActor(closest, (uint)damage);
    }

    internal static void VictorySpin()
    {
        int desty;
        if (player.angle > 270)
        {
            player.angle -= (short)(tics * 3);
            if (player.angle < 270)
                player.angle = 270;
        }
        else if (player.angle < 270)
        {
            player.angle += (short)(tics * 3);
            if (player.angle > 270)
                player.angle = 270;
        }

        desty = ((player.tiley - 5) << (int)TILESHIFT) - 0x3000;

        if (player.y > desty)
        {
            player.y -= (int)(tics * 4096);
            if (player.y < desty)
                player.y = desty;
        }
    }

    internal static void SpawnPlayer(int tilex, int tiley, int dir)
    {
        player.obclass = classtypes.playerobj;
        player.active = activetypes.ac_yes;
        player.tilex = (byte)tilex;
        player.tiley = (byte)tiley;
        player.areanumber = (byte)(MAPSPOT(tilex, tiley, 0) - AREATILE);
        player.x = (tilex << (int)TILESHIFT) + (int)TILEGLOBAL / 2;
        player.y = (tiley << (int)TILESHIFT) + (int)TILEGLOBAL / 2;
        player.state = s_player;
        player.angle = (short)((1 - dir) * 90);
        if (player.angle < 0)
            player.angle += ANGLES;
        player.flags = objflags.FL_NEVERMARK;
        Thrust(0, 0);                           // set some variables

        InitAreas();
    }
}
