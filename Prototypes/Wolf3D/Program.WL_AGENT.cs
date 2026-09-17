using Wolf3D.Assets;
using Wolf3D.Constants;
using Wolf3D.Entities.Actors;
using Wolf3D.Managers;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
                    MathUtils.FixedMul(speed, costable[angle]);
        ymove =// DEMOCHOOSE_ORIG_SDL(
                 //   -FixedByFracOrig(speed, sintable[angle]),
                    -MathUtils.FixedMul(speed, sintable[angle]);

        ClipMove(player, xmove, ymove);

        player.tilex = (byte)(player.x >> (int)MapConstants.TILESHIFT);                // scale to tile values
        player.tiley = (byte)(player.y >> (int)MapConstants.TILESHIFT);

        player.areanumber = (byte)(_mapManager.MAPSPOT(player.tilex, player.tiley, 0) - MapDataConstants.AREATILE);

        if (_mapManager.MAPSPOT(player.tilex, player.tiley, 1) == MapDataConstants.EXITTILE)
            VictoryTile();
    }

    internal static bool TryMove(objstruct ob)
    {
        uint xl, yl, xh, yh, x, y;
        Actor? check;
        int deltax, deltay;

        xl = (uint)((ob.x - PLAYERSIZE) >> (int)MapConstants.TILESHIFT);
        yl = (uint)((ob.y - PLAYERSIZE) >> (int)MapConstants.TILESHIFT);

        xh = (uint)((ob.x + PLAYERSIZE) >> (int)MapConstants.TILESHIFT);
        yh = (uint)((ob.y + PLAYERSIZE) >> (int)MapConstants.TILESHIFT);

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
                                if (ob.y - PUSHWALLMINDIST <= (pwally << (int)MapConstants.TILESHIFT) + ((63 - pwallpos) << 10))
                                    return false;
                                break;
                            case controldirs.di_west:
                                if (ob.x - PUSHWALLMINDIST <= (pwallx << (int)MapConstants.TILESHIFT) + ((63 - pwallpos) << 10))
                                    return false;
                                break;
                            case controldirs.di_east:
                                if (ob.x + PUSHWALLMINDIST >= (pwallx << (int)MapConstants.TILESHIFT) + (pwallpos << 10))
                                    return false;
                                break;
                            case controldirs.di_south:
                                if (ob.y + PUSHWALLMINDIST >= (pwally << (int)MapConstants.TILESHIFT) + (pwallpos << 10))
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

        if (noclip != 0 && ob.x > 2 * MapConstants.TILEGLOBAL && ob.y > 2 * MapConstants.TILEGLOBAL
            && ob.x < (((int)(_mapManager.mapwidth - 1)) << (int)MapConstants.TILESHIFT)
            && ob.y < (((int)(_mapManager.mapheight - 1)) << (int)MapConstants.TILESHIFT))
            return;         // walk through walls

        if (!_audioManager.IsAnySoundPlaying())
             _audioManager.Play("HITWALL");

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


    internal static void GetBonus(Inventory builtActor)
    {
        if (playstate == playstatetypes.ex_died)   // ADDEDFIX 31 - Chris
            return;

        //if (string.IsNullOrWhiteSpace(check.item_class))
        //    return;

        //var actors = _assetManager.GetActorMetadata();
        //if (!actors.Actors.TryGetValue(check.item_class, out var actor))
        //    return;
        //var builtActor = actors.BuildActor(check.item_class, actor); // TODO: Should this just create objects?

        if (builtActor.Properties.Count == 0)
            return;

        // TODO: This will be passed to an InventoryManager(Actor player, [inventory.* properties]
        if (builtActor.Properties.TryGetValue("inventory.amount", out var amount))
        {
            ApplyInventoryAmount(builtActor, Convert.ToInt32(amount));
        }

        builtActor.RunState("Pickup");

        if (builtActor is Entities.Actors.Weapon && WeaponPickupTypes.TryGetValue(builtActor.Name, out var pickedUpWeapon))
        {
            GiveWeapon(pickedUpWeapon);
        }

        if (builtActor.Properties.TryGetValue("inventory.pickupsound", out var pickupSound))
        {
            _audioManager.Play(pickupSound?.ToString() ?? "");
        }

        _videoManager.StartBonusFlash();
        //check.shapenum = "";                   // remove from list
        _mapManager.RemoveActor(builtActor);
    }

    internal static void ApplyInventoryAmount(Inventory item, int amount)
    {
        switch (item.GetType().Name)
        {
            case nameof(Entities.Actors.Health):
                HealSelf(amount);
                break;
            case nameof(Entities.Actors.Ammo):
                GiveAmmo(amount);
                break;
            case nameof(Entities.Actors.Key):
                GiveKey(amount);
                break;
            case nameof(Entities.Actors.ScoreItem):
                gamestate.treasurecount++;
                GivePoints(amount);
                break;
        }
    }

    /// <summary>
    /// Registers the C# handlers for the actor-state `action:` names authored in actordefs
    /// YAML. Called once at startup (see Main). Names with no handler here (e.g. the weapon
    /// Ready/Fire actions) are simply logged and skipped by ActorActionRegistry when hit.
    /// </summary>
    internal static void RegisterActorActions()
    {
        ActorActionRegistry.Register("A_GiveExtraMan", (Entities.Actors.Actor _) => GiveExtraMan());
        ActorActionRegistry.Register("A_GiveInventory", GiveInventoryAction);

        // Enemy AI (Program.EnemyAI.cs), ported from Program.WL_STATE.cs / Program.WL_ACT2.cs.
        ActorActionRegistry.Register("T_Stand", T_Stand);
        ActorActionRegistry.Register("T_Path", T_Path);
        ActorActionRegistry.Register("T_Chase", T_Chase);
        ActorActionRegistry.Register("T_DogChase", T_DogChase);
        ActorActionRegistry.Register("T_Bite", T_Bite);
        ActorActionRegistry.Register("T_Ghosts", T_Ghosts);
        ActorActionRegistry.Register("T_Schabb", T_Schabb);
        ActorActionRegistry.Register("T_SchabbThrow", T_SchabbThrow);
        ActorActionRegistry.Register("T_Gift", T_Gift);
        ActorActionRegistry.Register("T_GiftThrow", T_GiftThrow);
        ActorActionRegistry.Register("T_Fat", T_Fat);
        ActorActionRegistry.Register("T_Fake", T_Fake);
        ActorActionRegistry.Register("T_FakeFire", T_FakeFire);
        ActorActionRegistry.Register("T_Shoot", T_Shoot);
        ActorActionRegistry.Register("A_DeathScream", A_DeathScream);
        ActorActionRegistry.Register("A_MechaSound", A_MechaSound);
        ActorActionRegistry.Register("A_Slurpie", A_Slurpie);
        ActorActionRegistry.Register("A_HitlerMorph", A_HitlerMorph);
        ActorActionRegistry.Register("A_StartDeathCam", A_StartDeathCam);
    }

    private static void GiveInventoryAction(Entities.Actors.Actor actor, string[] args)
    {
        if (args.Length == 0)
            return;

        var itemName = args[0];
        var actorMetadata = _assetManager.GetActorMetadata();
        if (!actorMetadata.Actors.TryGetValue(itemName, out var itemData))
        {
            Console.WriteLine($"A_GiveInventory: unknown actor '{itemName}'.");
            return;
        }

        if (actorMetadata.CreateActor(itemName, itemData) is not Inventory item)
            return;

        int amount;
        if (args.Length > 1 && int.TryParse(args[1], out var explicitAmount))
            amount = explicitAmount;
        else if (item.Properties.TryGetValue("inventory.amount", out var defaultAmount))
            amount = Convert.ToInt32(defaultAmount);
        else
            return;

        ApplyInventoryAmount(item, amount);
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
        if (_audioManager.IsPlaying("GETGATLING"))
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
        else if (_audioManager.IsPlaying("GETGATLING"))
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
        ApplyDamageToPlayer(points);
    }

    // Enemy-side overload for the new Entities.Actors.Actor type (Program.EnemyAI.cs).
    // LastAttacker stays objstruct-typed (Program.WL_GAME.cs's damage-flash direction and
    // Program.WL_AGENT.cs's needleobj check both key off it) -- neither applies to attacks
    // from the new actor system yet, so it's simply left unset here rather than widened.
    internal static void TakeDamage(int points, Entities.Actors.Actor attacker) => ApplyDamageToPlayer(points);

    private static void ApplyDamageToPlayer(int points)
    {
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
        var gameInfo = _gameEngineManager.GetGameInfo();
        var mapInfo = gameInfo.Maps[gamestate.mapon];
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
        _audioManager.Play("BONUS1UP");
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

    // Maps a Weapon actor's yaml class name (Actor.Name, set in ActorMetadata.CreateActor)
    // to the weapontypes slot it grants on pickup. "Blue" variants are reskins of the same
    // slot (Spear of Destiny), not separate weapons.
    private static readonly Dictionary<string, weapontypes> WeaponPickupTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pistol"] = weapontypes.wp_pistol,
        ["BluePistol"] = weapontypes.wp_pistol,
        ["MachineGun"] = weapontypes.wp_machinegun,
        ["BlueAK47"] = weapontypes.wp_machinegun,
        ["GatlingGun"] = weapontypes.wp_chaingun,
        ["BlueGatlingGun"] = weapontypes.wp_chaingun,
    };


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
        if (_mapManager.MAPSPOT(checkx, checky, 1) == MapDataConstants.PUSHABLETILE)
        {
            //
            // pushable wall
            //

            PushWall(checkx, checky, dir);
            return;
        }
        if (!_inputManager.IsButtonHeld(buttontypes.bt_use) && cmdtile == MapDataConstants.ELEVATORTILE && elevatorok)
        {
            //
            // use elevator
            //
            _inputManager.SetButtonHeld(buttontypes.bt_use, true);

            _mapManager.tilemap[checkx, checky]++;              // flip switch
            if (_mapManager.MAPSPOT(player.tilex, player.tiley, 0) == MapDataConstants.ALTELEVATORTILE)
                playstate = playstatetypes.ex_secretlevel;
            else
                playstate = playstatetypes.ex_completed;
            _audioManager.Play("LEVELDONE");
            _audioManager.WaitSoundDone();
        }
        else if (!_inputManager.IsButtonHeld(buttontypes.bt_use) && (cmdtile & BIT_DOOR) != 0)
        {
            _inputManager.SetButtonHeld(buttontypes.bt_use, true);
            OperateDoor(cmdtile & ~BIT_DOOR);
        }
        else
        { }// _audioManager.Play("DONOTHING");
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
        player.tilex = (byte)(player.x >> (int)MapConstants.TILESHIFT);                // scale to tile values
        player.tiley = (byte)(player.y >> (int)MapConstants.TILESHIFT);
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
        player.tilex = (byte)(player.x >> (int)MapConstants.TILESHIFT);                // scale to tile values
        player.tiley = (byte)(player.y >> (int)MapConstants.TILESHIFT);

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

    // The player's targets are now exclusively the new Entities.Actors.Actor enemies
    // (Program.EnemyAI.cs) -- nothing left in objlist2 (player, projectiles, the
    // BJ-victory actor) is ever FL_SHOOTABLE now that enemies are gone from it, so
    // GunAttack/KnifeAttack no longer need to scan it at all.
    private static List<Entities.Actors.Actor> FindShootCandidates()
    {
        var candidates = new List<Entities.Actors.Actor>();

        foreach (var actor in _mapManager.GetActors())
        {
            if (actor == null || !actor.ResolvedStates.ContainsKey("Chase")) continue;
            if (actor.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE) && actor.RuntimeFlags.HasFlag(objflags.FL_VISABLE)
                && Math.Abs(actor.ViewX - centerx) < shootdelta)
            {
                candidates.Add(actor);
            }
        }

        candidates.Sort((a, b) => a.TransX.CompareTo(b.TransX));
        return candidates;
    }

    internal static void KnifeAttack(objstruct ob)
    {
        _audioManager.Play("weapon/knife/attack");

        var closest = FindShootCandidates().FirstOrDefault(c => c.TransX <= 0x18000L);
        if (closest == null)
            return; // missed

        DamageActor(closest, (uint)(US_RndT() >> 4));
    }

    internal static void GunAttack(objstruct ob)
    {
        int damage;
        int dx, dy, dist;

        switch (gamestate.weapon)
        {
            case weapontypes.wp_pistol:
                _audioManager.Play("weapon/pistol/attack");
                break;
            case weapontypes.wp_machinegun:
                _audioManager.Play("weapon/machine/attack");
                break;
            case weapontypes.wp_chaingun:
                _audioManager.Play("weapon/gatling/attack");
                break;
        }

        madenoise = true;

        //
        // find the closest potential target, and confirm a clear line to it
        // (the legacy version of this re-scanned in a loop, but since nothing marks a
        // failed candidate as excluded, re-scanning always finds the same actor again --
        // it only ever gives the single closest actor one shot at passing CheckLine)
        //
        var closest = FindShootCandidates().FirstOrDefault();
        if (closest == null)
            return; // no targets, missed
        if (!CheckLine(closest))
            return;

        //
        // hit something
        //
        dx = Math.Abs(closest.TileX - player.tilex);
        dy = Math.Abs(closest.TileY - player.tiley);
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

        desty = ((player.tiley - 5) << (int)MapConstants.TILESHIFT) - 0x3000;

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
        player.areanumber = (byte)(_mapManager.MAPSPOT(tilex, tiley, 0) - MapDataConstants.AREATILE);
        player.x = (tilex << (int)MapConstants.TILESHIFT) + (int)MapConstants.TILEGLOBAL / 2;
        player.y = (tiley << (int)MapConstants.TILESHIFT) + (int)MapConstants.TILEGLOBAL / 2;
        player.state = s_player;
        player.angle = (short)((1 - dir) * 90);
        if (player.angle < 0)
            player.angle += ANGLES;
        player.flags = objflags.FL_NEVERMARK;
        Thrust(0, 0);                           // set some variables

        InitAreas();
    }
}
