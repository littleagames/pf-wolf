using Wolf3D.Managers;
using Wolf3D.Mappers;

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

    internal static statestruct s_player = new(0/*false*/, "", 0, T_Player, null, null);
    internal static statestruct s_attack = new(0/*false*/, "", 0, T_Attack, null, null);

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
        { new(6,0,1), new (6,2,2), new (6,0,3), new (6,-1,4) },
        { new(6,0,1), new (6,1,2), new (6,0,3), new (6,-1,4) },
        { new(6,0,1), new (6,1,2), new (6,3,3), new (6,-1,4) },
        { new(6,0,1), new (6,1,2), new (6,4,3), new (6,-1,4) },
    };

    internal static void CheckWeaponChange()
    {
        weapontypes i, newWeapon = weapontypes.wp_none;

        if (gamestate.ammo == 0)            // must use knife with no ammo
            return;
        if (_inputManager.IsButtonPressed(buttontypes.bt_nextweapon) && !_inputManager.IsButtonHeld(buttontypes.bt_nextweapon))
        {
            newWeapon = gamestate.weapon + 1;
            if (newWeapon > gamestate.bestweapon) newWeapon = 0;
        }
        else if (_inputManager.IsButtonPressed(buttontypes.bt_prevweapon) && !_inputManager.IsButtonHeld(buttontypes.bt_prevweapon))
        {
            newWeapon = gamestate.weapon - 1;
            if (newWeapon < 0) newWeapon = gamestate.bestweapon;
        }
        else
        {
            for (i = weapontypes.wp_knife; i <= gamestate.bestweapon; i++)
            {
                if (_inputManager.IsButtonPressed((buttontypes)((int)buttontypes.bt_readyknife + i - weapontypes.wp_knife)))
                {
                    newWeapon = i;
                    break;
                }
            }
        }

        if (newWeapon != weapontypes.wp_none)
        {
            gamestate.weapon = gamestate.chosenweapon = newWeapon;
            DrawWeapon();
        }
    }

    internal static void ControlMovement(objstruct ob)
    {
        int angle;
        int angleunits;

        thrustspeed = 0;

        if (_inputManager.IsButtonPressed(buttontypes.bt_strafeleft))
        {
            angle = ob.angle + ANGLES / 4;
            if (angle >= ANGLES)
                angle -= ANGLES;
            if (_inputManager.IsButtonPressed(buttontypes.bt_run))
                Thrust(angle, (int)(RUNMOVE * MOVESCALE * tics));
            else
                Thrust(angle, (int)(BASEMOVE * MOVESCALE * tics));
        }

        if (_inputManager.IsButtonPressed(buttontypes.bt_straferight))
        {
            angle = ob.angle - ANGLES / 4;
            if (angle < 0)
                angle += ANGLES;
            if (_inputManager.IsButtonPressed(buttontypes.bt_run))
                Thrust(angle, (int)(RUNMOVE * MOVESCALE * tics));
            else
                Thrust(angle, (int)(BASEMOVE * MOVESCALE * tics));
        }

        //
        // side to side move
        //
        if (_inputManager.IsButtonPressed(buttontypes.bt_strafe))
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

        player.areanumber = (byte)(_mapManager.MAPSPOT(player.tilex, player.tiley, 0) - MapConstants.AREATILE);

        if (_mapManager.MAPSPOT(player.tilex, player.tiley, 1) == MapConstants.EXITTILE)
            VictoryTile();
    }

    internal static bool TryMove(objstruct ob)
    {
        uint xl, yl, xh, yh, x, y;
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
                check = _mapManager.actorat[x, y];
                if (check != null && !MapManager.ISPOINTER(check))
                {
                    if (_mapManager.tilemap[x, y] == BIT_WALL && x == pwallx && y == pwally)   // back of moving pushwall?
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
        if (yh < MapManager.MAPSIZE - 1)
            yh++;
        if (xl > 0)
            xl--;
        if (xh < MapManager.MAPSIZE - 1)
            xh++;

        for (y = yl; y <= yh; y++)
        {
            for (x = xl; x <= xh; x++)
            {
                check = _mapManager.actorat[x, y];
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
            && ob.x < (((int)(_mapManager.mapwidth - 1)) << (int)TILESHIFT)
            && ob.y < (((int)(_mapManager.mapheight - 1)) << (int)TILESHIFT))
            return;         // walk through walls

        if (SD_SoundPlaying() == "")
            SD_PlaySound("HITWALLSND");

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
        gamestate.victoryflag = true;
    }


    internal static void GetBonus(statobj_t check)
    {
        if (playstate == playstatetypes.ex_died)   // ADDEDFIX 31 - Chris
            return;

        switch ((wl_stat_types)check.itemnumber)
        {
            case wl_stat_types.bo_firstaid:
                if (gamestate.health == 100)
                    return;

                SD_PlaySound("HEALTH2SND");
                HealSelf(25);
                break;

            case wl_stat_types.bo_key1:
            case wl_stat_types.bo_key2:
            case wl_stat_types.bo_key3:
            case wl_stat_types.bo_key4:
                GiveKey(check.itemnumber - wl_stat_types.bo_key1);
                SD_PlaySound("GETKEYSND");
                break;

            case wl_stat_types.bo_cross:
                SD_PlaySound("BONUS1SND");
                GivePoints(100);
                gamestate.treasurecount++;
                break;
            case wl_stat_types.bo_chalice:
                SD_PlaySound("BONUS2SND");
                GivePoints(500);
                gamestate.treasurecount++;
                break;
            case wl_stat_types.bo_bible:
                SD_PlaySound("BONUS3SND");
                GivePoints(1000);
                gamestate.treasurecount++;
                break;
            case wl_stat_types.bo_crown:
                SD_PlaySound("BONUS4SND");
                GivePoints(5000);
                gamestate.treasurecount++;
                break;

            case wl_stat_types.bo_clip:
                if (gamestate.ammo == 99)
                    return;

                SD_PlaySound("GETAMMOSND");
                GiveAmmo(8);
                break;
            case wl_stat_types.bo_clip2:
                if (gamestate.ammo == 99)
                    return;

                SD_PlaySound("GETAMMOSND");
                GiveAmmo(4);
                break;

            case wl_stat_types.bo_machinegun:
                SD_PlaySound("GETMACHINESND");
                GiveWeapon(weapontypes.wp_machinegun);
                break;
            case wl_stat_types.bo_chaingun:
                SD_PlaySound("GETGATLINGSND");
                facetimes = 38;
                GiveWeapon(weapontypes.wp_chaingun);

                if (viewsize != 21)
                    StatusDrawFace("gotgatling");
                facecount = 0;
                break;

            case wl_stat_types.bo_fullheal:
                SD_PlaySound("BONUS1UPSND");
                HealSelf(99);
                GiveAmmo(25);
                GiveExtraMan();
                gamestate.treasurecount++;
                break;

            case wl_stat_types.bo_food:
                if (gamestate.health == 100)
                    return;

                SD_PlaySound("HEALTH1SND");
                HealSelf(10);
                break;

            case wl_stat_types.bo_alpo:
                if (gamestate.health == 100)
                    return;

                SD_PlaySound("HEALTH1SND");
                HealSelf(4);
                break;

            case wl_stat_types.bo_gibs:
                if (gamestate.health > 10)
                    return;

                SD_PlaySound("SLURPIESND");
                HealSelf(1);
                break;
        }

        _videoManager.StartBonusFlash();
        check.shapenum = "";                   // remove from list
    }


    static void StatusDrawPic (string picName, uint x, uint y)
    {
        _graphicManager.DrawPic(
            picName,
            (int)x*8, // TODO: Allow more flexibility than 8s
            (int)(200 - (STATUSLINES - y)));
    }

    static void StatusDrawFace(string picName)
    {
        StatusDrawPic(picName, 17, 4);
    }

    static void LatchNumber(int x, int y, uint width, int number)
    {
        uint length, c;
        string str;

        str = number.ToString();

        length = (uint)str.Length;
        while (length < width)
        {
            // TODO: 032 is "space"
            StatusDrawPic("FONTN032", (uint)x, (uint)y);
            x++;
            width--;
        }

        c = length <= width ? 0 : length - width;

        while (c < length)
        {
            // TODO: "FONTN" + ascii value (if exists)
            string[] numberPic = ["FONTN048", "FONTN049", "FONTN050", "FONTN051", "FONTN052", "FONTN053", "FONTN054", "FONTN055", "FONTN056", "FONTN057"];
            var digitIndex = (int)(str[(int)c] - '0');
            StatusDrawPic(numberPic[digitIndex], (uint)x, (uint)y);
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
        if (SD_SoundPlaying() == "GETGATLINGSND")
            StatusDrawFace("gotgatling");
        else if (gamestate.health != 0)
        {
            int tier = ((100 - gamestate.health) / 16) + 1;
            char animationFrame = (char)('a' + gamestate.faceframe);
            string facePic = $"face{tier}{animationFrame}";
            StatusDrawFace(facePic);
        }
        else
        {
            if (LastAttacker != null && LastAttacker.obclass == classtypes.needleobj)
                StatusDrawFace("mutantbj");
            else
                StatusDrawFace("face8a");
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
        else if (SD_SoundPlaying() == "GETGATLINGSND")
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

        if (gamestate.victoryflag)
            return;
        if (gamestate.difficulty == difficultytypes.gd_baby)
            points >>= 2;

        if (godmode == 0)
            gamestate.health -= (short)points;

        if (gamestate.health <= 0)
        {
            gamestate.health = 0;
            playstate = playstatetypes.ex_died;
        }

        if (godmode != 2)
            _videoManager.StartDamageFlash(points);

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
            StatusDrawPic("goldkey", 30, 4);
        else
            StatusDrawPic("nokey", 30, 4);

        if ((gamestate.keys & 2) != 0)
            StatusDrawPic("silverkey", 30, 20);
        else
            StatusDrawPic("nokey", 30, 20);
    }

    static void GiveKey(int key)
    {
        gamestate.keys |= (short)(1 << key);
        DrawKeys();
    }

    static void DrawLevel()
    {
        var mapInfo = _assetManager.GetGameInfo().Maps[gamestate.mapon];
        //var mapInfo = MapInfoMappings.GameInfo.Maps[gamestate.mapon];
        if (viewsize == 21 && ingame) return;
        LatchNumber(2, 16, 2, mapInfo.FloorNumber);
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
        SD_PlaySound("BONUS1UPSND");
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

        string[] weaponPic = ["knife", "gun", "machinegun", "gatlinggun"];
        StatusDrawPic(weaponPic[(int)gamestate.weapon], 32, 8);
    }
/*
==================
=
= GiveWeapon
=
==================
*/

    internal static void GiveWeapon(weapontypes weapon)
    {
        GiveAmmo(6);

        if (gamestate.bestweapon < weapon)
            gamestate.bestweapon = gamestate.weapon
            = gamestate.chosenweapon = weapon;

        DrawWeapon();
    }


    internal static void Cmd_Use()
    {
        int checkx, checky, cmdtile;
        controldirs dir;
        bool elevatorok;

        //
        // find which cardinal direction the player is facing
        //
        if (player.angle < ANGLES / 8 || player.angle > 7 * ANGLES / 8)
        {
            checkx = player.tilex + 1;
            checky = player.tiley;
            dir = controldirs.di_east;
            elevatorok = true;
        }
        else if (player.angle < 3 * ANGLES / 8)
        {
            checkx = player.tilex;
            checky = player.tiley - 1;
            dir = controldirs.di_north;
            elevatorok = false;
        }
        else if (player.angle < 5 * ANGLES / 8)
        {
            checkx = player.tilex - 1;
            checky = player.tiley;
            dir = controldirs.di_west;
            elevatorok = true;
        }
        else
        {
            checkx = player.tilex;
            checky = player.tiley + 1;
            dir = controldirs.di_south;
            elevatorok = false;
        }

        cmdtile = _mapManager.tilemap[checkx, checky];
        if (_mapManager.MAPSPOT(checkx, checky, 1) == MapConstants.PUSHABLETILE)
        {
            //
            // pushable wall
            //

            PushWall(checkx, checky, dir);
            return;
        }
        if (!_inputManager.IsButtonHeld(buttontypes.bt_use) && cmdtile == MapConstants.ELEVATORTILE && elevatorok)
        {
            //
            // use elevator
            //
            _inputManager.SetButtonHeld(buttontypes.bt_use, true);

            _mapManager.tilemap[checkx, checky]++;              // flip switch
            if (_mapManager.MAPSPOT(player.tilex, player.tiley, 0) == MapConstants.ALTELEVATORTILE)
                playstate = playstatetypes.ex_secretlevel;
            else
                playstate = playstatetypes.ex_completed;
            SD_PlaySound("LEVELDONESND");
            SD_WaitSoundDone();
        }
        else if (!_inputManager.IsButtonHeld(buttontypes.bt_use) && (cmdtile & BIT_DOOR) != 0)
        {
            _inputManager.SetButtonHeld(buttontypes.bt_use, true);
            OperateDoor(cmdtile & ~BIT_DOOR);
        }
        else
            SD_PlaySound("DONOTHINGSND");
    }

    internal static void Cmd_Fire()
    {
        _inputManager.SetButtonHeld(buttontypes.bt_attack, true);

        gamestate.weaponframe = 0;

        player.state = s_attack;

        gamestate.attackframe = 0;
        gamestate.attackcount =
            attackinfo[(int)gamestate.weapon, gamestate.attackframe].tics;
        gamestate.weaponframe =
            attackinfo[(int)gamestate.weapon, gamestate.attackframe].frame;
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
        if (gamestate.victoryflag)              // watching the BJ actor
        {
            VictorySpin();
            return;
        }

        UpdateFace();
        CheckWeaponChange();

        if (_inputManager.IsButtonPressed(buttontypes.bt_use))
            Cmd_Use();

        if (_inputManager.IsButtonPressed(buttontypes.bt_attack) && !_inputManager.IsButtonHeld(buttontypes.bt_attack))
            Cmd_Fire();

        ControlMovement(ob);
        if (gamestate.victoryflag)              // watching the BJ actor
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

        if (gamestate.victoryflag)              // watching the BJ actor
        {
            VictorySpin();
            return;
        }


        if (_inputManager.IsButtonPressed(buttontypes.bt_use) && !_inputManager.IsButtonHeld(buttontypes.bt_use))
            _inputManager.SetButtonPressed(buttontypes.bt_use, false);

        if (_inputManager.IsButtonPressed(buttontypes.bt_attack) && !_inputManager.IsButtonHeld(buttontypes.bt_attack))
            _inputManager.SetButtonPressed(buttontypes.bt_attack, false);

        ControlMovement(ob);
        if (gamestate.victoryflag)              // watching the BJ actor
            return;

        plux = (ushort)(player.x >> UNSIGNEDSHIFT);                     // scale to fit in unsigned
        pluy = (ushort)(player.y >> UNSIGNEDSHIFT);
        player.tilex = (byte)(player.x >> TILESHIFT);                // scale to tile values
        player.tiley = (byte)(player.y >> TILESHIFT);

        //
        // change frame and fire
        //
        gamestate.attackcount -= (short)tics;
        while (gamestate.attackcount <= 0)
        {
            cur = attackinfo[(int)gamestate.weapon, gamestate.attackframe];
            switch (cur.attack)
            {
                case -1:
                    ob.state = s_player;
                    if (gamestate.ammo == 0)
                    {
                        gamestate.weapon = weapontypes.wp_knife;
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
                    if (_inputManager.IsButtonPressed(buttontypes.bt_attack))
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
                    if (gamestate.ammo != 0 && _inputManager.IsButtonPressed(buttontypes.bt_attack))
                        gamestate.attackframe -= 2;
                    break;
            }

            gamestate.attackcount += cur.tics;
            gamestate.attackframe++;
            gamestate.weaponframe =
                attackinfo[(int)gamestate.weapon, gamestate.attackframe].frame;
        }
    }

    internal static void KnifeAttack(objstruct ob)
    {
        objstruct? closest;
        int dist;

        SD_PlaySound(("ATKKNIFESND"));
        // actually fire
        dist = 0x7fffffff;
        closest = null;
        foreach (var actor in objlist2)
        {
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

        switch (gamestate.weapon)
        {
            case weapontypes.wp_pistol:
                SD_PlaySound("ATKPISTOLSND");
                break;
            case weapontypes.wp_machinegun:
                SD_PlaySound("ATKMACHINEGUNSND");
                break;
            case weapontypes.wp_chaingun:
                SD_PlaySound("ATKGATLINGSND");
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
            {
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
        player.areanumber = (byte)(_mapManager.MAPSPOT(tilex, tiley, 0) - MapConstants.AREATILE);
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
