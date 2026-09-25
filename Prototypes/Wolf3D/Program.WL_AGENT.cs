using Wolf3D.Assets;
using Wolf3D.Constants;
using Wolf3D.Entities.Actors;
using Wolf3D.Managers;

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

    static Entities.Actors.Actor? LastAttacker;

    /*
    =============================================================================

                                                     LOCAL VARIABLES

    =============================================================================
    */

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

        if (GetAmmo() == 0)                 // must use knife with no ammo
            return;

        var bestWeapon = GetBestWeapon();
        if (_inputManager.IsButtonPressed(buttontypes.bt_nextweapon) && !_inputManager.IsButtonHeld(buttontypes.bt_nextweapon))
        {
            newWeapon = gamestate.weapon + 1;
            if (newWeapon > bestWeapon) newWeapon = 0;
        }
        else if (_inputManager.IsButtonPressed(buttontypes.bt_prevweapon) && !_inputManager.IsButtonHeld(buttontypes.bt_prevweapon))
        {
            newWeapon = gamestate.weapon - 1;
            if (newWeapon < 0) newWeapon = bestWeapon;
        }
        else
        {
            for (i = weapontypes.wp_knife; i <= bestWeapon; i++)
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

    internal static void ControlMovement(Entities.Actors.Actor ob)
    {
        int angle;
        int angleunits;

        thrustspeed = 0;

        if (_inputManager.IsButtonPressed(buttontypes.bt_strafeleft))
        {
            angle = ob.Angle + ANGLES / 4;
            if (angle >= ANGLES)
                angle -= ANGLES;
            if (_inputManager.IsButtonPressed(buttontypes.bt_run))
                Thrust(angle, (int)(RUNMOVE * MOVESCALE * tics));
            else
                Thrust(angle, (int)(BASEMOVE * MOVESCALE * tics));
        }

        if (_inputManager.IsButtonPressed(buttontypes.bt_straferight))
        {
            angle = ob.Angle - ANGLES / 4;
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
                angle = ob.Angle - ANGLES / 4;
                if (angle < 0)
                    angle += ANGLES;
                Thrust(angle, (int)(controlx * MOVESCALE));      // move to left
            }
            else if (controlx < 0)
            {
                angle = ob.Angle + ANGLES / 4;
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
            ob.Angle -= (short)angleunits;

            if (ob.Angle >= ANGLES)
                ob.Angle -= ANGLES;
            if (ob.Angle < 0)
                ob.Angle += ANGLES;
        }

        //
        // forward/backwards move
        //
        if (controly < 0)
        {
            Thrust(ob.Angle, (int)(-controly * MOVESCALE)); // move forwards
        }
        else if (controly > 0)
        {
            angle = ob.Angle + ANGLES / 2;
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

        var (oldtilex, oldtiley) = (player.TileX, player.TileY);
        player.TileX = (byte)(player.X >> (int)MapConstants.TILESHIFT);                // scale to tile values
        player.TileY = (byte)(player.Y >> (int)MapConstants.TILESHIFT);

        player.AreaNumber = (byte)(_mapManager.MAPSPOT(player.TileX, player.TileY, 0) - MapDataConstants.AREATILE);

        //
        // mapdefs walk-over trigger (the end-of-castle exit) on the tile just stepped onto
        //
        if ((player.TileX != oldtilex || player.TileY != oldtiley)
            && _mapManager.GetTrigger(player.TileX, player.TileY) is { IsWalkOver: true } trigger)
            ActivateTrigger(trigger, player.TileX, player.TileY, FacingDir(player.Angle));
    }

    internal static bool TryMove(Entities.Actors.Actor ob)
    {
        uint xl, yl, xh, yh, x, y;
        Actor? check;

        xl = (uint)((ob.X - PLAYERSIZE) >> (int)MapConstants.TILESHIFT);
        yl = (uint)((ob.Y - PLAYERSIZE) >> (int)MapConstants.TILESHIFT);

        xh = (uint)((ob.X + PLAYERSIZE) >> (int)MapConstants.TILESHIFT);
        yh = (uint)((ob.Y + PLAYERSIZE) >> (int)MapConstants.TILESHIFT);

        const long PUSHWALLMINDIST = PLAYERSIZE;

        //
        // check for solid walls
        //
        for (y = yl; y <= yh; y++)
        {
            for (x = xl; x <= xh; x++)
            {
                check = _mapManager.actorat[x, y];
                if (check != null)
                {
                    if (_mapManager.tilemap[x, y] == BIT_WALL && x == pwallx && y == pwally)   // back of moving pushwall?
                    {
                        switch (pwalldir)
                        {
                            case controldirs.di_north:
                                if (ob.Y - PUSHWALLMINDIST <= (pwally << (int)MapConstants.TILESHIFT) + ((63 - pwallpos) << 10))
                                    return false;
                                break;
                            case controldirs.di_west:
                                if (ob.X - PUSHWALLMINDIST <= (pwallx << (int)MapConstants.TILESHIFT) + ((63 - pwallpos) << 10))
                                    return false;
                                break;
                            case controldirs.di_east:
                                if (ob.X + PUSHWALLMINDIST >= (pwallx << (int)MapConstants.TILESHIFT) + (pwallpos << 10))
                                    return false;
                                break;
                            case controldirs.di_south:
                                if (ob.Y + PUSHWALLMINDIST >= (pwally << (int)MapConstants.TILESHIFT) + (pwallpos << 10))
                                    return false;
                                break;
                        }
                    }
                    else return false;
                }
            }
        }

        //
        // check for actors: a living actor on a tile near the player, and within
        // MINACTORDIST of it, blocks the move
        //
        if (yl > 0)
            yl--;
        if (yh < MapManager.MAPSIZE - 1)
            yh++;
        if (xl > 0)
            xl--;
        if (xh < MapManager.MAPSIZE - 1)
            xh++;

        foreach (var actor in _mapManager.GetActors())
        {
            if (actor.IsRemoved || !actor.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE))
                continue;
            if (actor.TileX < xl || actor.TileX > xh || actor.TileY < yl || actor.TileY > yh)
                continue;

            var deltax = ob.X - actor.X;
            if (deltax < -MINACTORDIST || deltax > MINACTORDIST)
                continue;
            var deltay = ob.Y - actor.Y;
            if (deltay < -MINACTORDIST || deltay > MINACTORDIST)
                continue;

            return false;
        }

        return true;
    }

    internal static void ClipMove(Entities.Actors.Actor ob, int xmove, int ymove)
    {
        int basex, basey;

        basex = ob.X;
        basey = ob.Y;

        ob.X = basex + xmove;
        ob.Y = basey + ymove;
        if (TryMove(ob))
            return;

        if (noclip != 0 && ob.X > 2 * MapConstants.TILEGLOBAL && ob.Y > 2 * MapConstants.TILEGLOBAL
            && ob.X < (((int)(_mapManager.mapwidth - 1)) << (int)MapConstants.TILESHIFT)
            && ob.Y < (((int)(_mapManager.mapheight - 1)) << (int)MapConstants.TILESHIFT))
            return;         // walk through walls

        if (!_audioManager.IsAnySoundPlaying())
             _audioManager.Play("world/hitwall");

        ob.X = basex + xmove;
        ob.Y = basey;
        if (TryMove(ob))
            return;

        ob.X = basex;
        ob.Y = basey + ymove;
        if (TryMove(ob))
            return;

        ob.X = basex;
        ob.Y = basey;
    }

    internal static void VictoryTile()
    {
        SpawnBJVictory();
        gamestate.victoryflag = true;
    }


    internal static void GetBonus(Inventory builtActor)
    {
        if (playstate == playstatetypes.ex_died) 
            return;

        //if (string.IsNullOrWhiteSpace(check.item_class))
        //    return;

        //var actors = _assetManager.GetActorMetadata();
        //if (!actors.Actors.TryGetValue(check.item_class, out var actor))
        //    return;
        //var builtActor = actors.BuildActor(check.item_class, actor); // TODO: Should this just create objects?

        if (builtActor.Properties.Count == 0)
            return;

        // An item the player can't take (health at 100, a key already held) stays on the
        // floor, unless it's flagged ALWAYSPICKUP.
        if (builtActor.Properties.TryGetValue("inventory.amount", out var amount)
            && !TryApplyInventory(builtActor, Convert.ToInt32(amount))
            && !builtActor.Flags.Contains("INVENTORY.ALWAYSPICKUP", StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (builtActor.Flags.Contains("COUNTITEM", StringComparer.OrdinalIgnoreCase))
            gamestate.treasurecount++;

        builtActor.RunState("Pickup");

        if (builtActor.Properties.TryGetValue("inventory.pickupsound", out var pickupSound))
        {
            _audioManager.Play(pickupSound?.ToString() ?? "");
        }

        _videoManager.StartBonusFlash();
        //check.shapenum = "";                   // remove from list
        _mapManager.RemoveActor(builtActor);
    }

    /// <summary>
    /// Gives the player <paramref name="amount"/> of <paramref name="item"/>. Returns false
    /// when nothing could be taken, so the pickup should be left where it is.
    /// </summary>
    internal static bool TryApplyInventory(Inventory item, int amount)
    {
        switch (item)
        {
            case Entities.Actors.Health:
                if (gamestate.health >= 100)
                    return false;
                HealSelf(amount);
                return true;
            case Entities.Actors.Ammo:
                return GiveAmmo(item.Name, amount) > 0;
            case Entities.Actors.Weapon weapon:
                return TryGiveWeapon(weapon);
            case Entities.Actors.Key:
                if (_inventoryManager.Give(item.Name, amount) == 0)
                    return false;
                DrawKeys();
                return true;
            case Entities.Actors.ScoreItem:
                GivePoints(amount);
                return true;
            default:
                return true;
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
        ActorActionRegistry.Register("A_ChangeMap", ChangeMapAction);

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

        // Spear of Destiny bosses (Program.EnemyAI.cs)
        ActorActionRegistry.Register("T_Will", T_Will);
        ActorActionRegistry.Register("T_UShoot", T_UShoot);
        ActorActionRegistry.Register("A_FireProjectile", A_FireProjectile);
        ActorActionRegistry.Register("A_StartAttack", A_StartAttack);
        ActorActionRegistry.Register("A_Relaunch", A_Relaunch);
        ActorActionRegistry.Register("A_Victory", A_Victory);
        ActorActionRegistry.Register("A_PlaySound", A_PlaySound);
        ActorActionRegistry.Register("A_Dormant", A_Dormant);

        // Projectiles and effects (Program.WL_ACT2.cs).
        ActorActionRegistry.Register("A_Projectile", A_Projectile);
        ActorActionRegistry.Register("A_SpawnThing", A_SpawnThing);
        ActorActionRegistry.Register("A_Remove", A_Remove);

        // BJ victory cutscene (Program.WL_ACT2.cs).
        ActorActionRegistry.Register("T_BJRun", T_BJRun);
        ActorActionRegistry.Register("T_BJJump", T_BJJump);
        ActorActionRegistry.Register("T_BJYell", T_BJYell);
        ActorActionRegistry.Register("T_BJDone", T_BJDone);

        // The player's own think states (PlayerPawn), ticked by MapManager.DoActor.
        ActorActionRegistry.Register("T_Player", T_Player);
        ActorActionRegistry.Register("T_Attack", T_Attack);

        // mapdefs trigger actions, run when the player uses a trigger's tile (Cmd_Use) or steps
        // onto a walk-over one (Thrust)
        Entities.MapTriggerRegistry.Register("A_PushWall", (trigger, _) => PushWall(trigger.TileX, trigger.TileY, trigger.Dir));
        Entities.MapTriggerRegistry.Register("A_VictoryTile", (_, _) => { VictoryTile(); return true; });
    }

    /// <summary>
    /// A_ChangeMap("MAP21"[, "keep-position"]): ends the level and moves to another map with no
    /// intermission. With keep-position the player keeps their spot and facing on the new map,
    /// as when picking up the Spear of Destiny.
    /// </summary>
    private static void ChangeMapAction(Entities.Actors.Actor actor, string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("A_ChangeMap: no map given.");
            return;
        }

        var map = _gameEngineManager.GetGameInfo().Maps.Keys
            .FirstOrDefault(k => k.Equals(args[0], StringComparison.OrdinalIgnoreCase));
        if (map == null)
        {
            Console.WriteLine($"A_ChangeMap: unknown map '{args[0]}'.");
            return;
        }

        var keepPosition = args.Skip(1).Any(a => a.Equals("keep-position", StringComparison.OrdinalIgnoreCase));
        pendingMapChange = new PendingMapChange(map, keepPosition, player.X, player.Y, player.Angle);
        gamestate.mapon = map;
        playstate = playstatetypes.ex_warped;
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

        TryApplyInventory(item, amount);
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

    // The one ammo type every Wolf3D weapon draws on (`weapon.ammotype1` in weapons.yaml);
    // the status bar and the legacy attack code only ever look at this stack.
    private const string AmmoType = "Clip";

    static int GetAmmo() => _inventoryManager.GetCount(AmmoType);

    static void DrawAmmo()
    {
        if (viewsize == 21 && ingame) return;
        LatchNumber(27, 16, 2, GetAmmo());
    }

/*
===============
=
= GiveAmmo
=
= Returns how much was actually taken (0 when the stack was already full)
=
===============
*/

    internal static int GiveAmmo(string ammoType, int ammo)
    {
        var knifeWasOut = GetAmmo() == 0;
        var added = _inventoryManager.Give(ammoType, ammo);
        if (added > 0 && knifeWasOut && gamestate.attackframe == 0)
        {
            gamestate.weapon = gamestate.chosenweapon;
            DrawWeapon();
        }
        DrawAmmo();
        return added;
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
            if (LastAttacker != null && LastAttacker.Name == "Needle")
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

    // LastAttacker feeds Died()'s swing-around-to-face-the-killer (Program.WL_GAME.cs) and the
    // needle-death face in DrawFace. Every attacker -- enemies (Program.EnemyAI.cs) and
    // projectiles (Program.WL_ACT2.cs) -- is an Entities.Actors.Actor now.
    internal static void TakeDamage(int points, Entities.Actors.Actor attacker)
    {
        LastAttacker = attacker;
        ApplyDamageToPlayer(points);
    }

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
        // The status bar has one fixed slot per key.
        if (_inventoryManager.Has("GoldKey"))
            StatusDrawPic("goldkey", 30, 4);
        else
            StatusDrawPic("nokey", 30, 4);

        if (_inventoryManager.Has("SilverKey"))
            StatusDrawPic("silverkey", 30, 20);
        else
            StatusDrawPic("nokey", 30, 20);
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
        _audioManager.Play("misc/1up");
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

    // The weapon item behind each legacy weapontypes slot, for the cheats and the starting
    // loadout. Pickups on the map don't use this: they carry their own `weapon.slot`.
    private static readonly string[] WeaponSlotItems = ["Knife", "Pistol", "MachineGun", "GatlingGun"];

    /// <summary>
    /// The highest weapon slot the player owns, from each held weapon's `weapon.slot`.
    /// Blue (Spear of Destiny) reskins share their base weapon's slot.
    /// </summary>
    internal static weapontypes GetBestWeapon()
    {
        var best = weapontypes.wp_none;
        foreach (var item in _inventoryManager.Items.Keys)
        {
            var slot = (weapontypes)_inventoryManager.GetIntProperty(item, "weapon.slot", -1);
            if (slot > best)
                best = slot;
        }
        return best;
    }

    /// <summary>
    /// New game / respawn loadout: knife, pistol and starting ammo, with the pistol selected.
    /// </summary>
    internal static void GiveStartingInventory()
    {
        _inventoryManager.Clear();
        _inventoryManager.Give(WeaponSlotItems[(int)weapontypes.wp_knife], 1);
        _inventoryManager.Give(WeaponSlotItems[(int)weapontypes.wp_pistol], 1);
        _inventoryManager.Give(AmmoType, STARTAMMO);
        gamestate.weapon = gamestate.chosenweapon = weapontypes.wp_pistol;
    }

    internal static void GiveWeapon(weapontypes weapon) =>
        TryGiveWeapon(WeaponSlotItems[(int)weapon], AmmoType, 6);

    private static bool TryGiveWeapon(Entities.Actors.Weapon pickup)
    {
        // A WeaponGiver (e.g. the map's gatling upgrade) names the weapon it hands out.
        var weaponName = pickup.Properties.TryGetValue("weapongiver.weapon", out var given)
            ? given.ToString() ?? pickup.Name
            : pickup.Name;
        var ammoType = pickup.Properties.TryGetValue("weapon.ammotype1", out var type)
            ? type.ToString() ?? AmmoType
            : AmmoType;
        var ammoGive = pickup.Properties.TryGetValue("weapon.ammogive1", out var ammo)
            ? Convert.ToInt32(ammo)
            : 0;

        return TryGiveWeapon(weaponName, ammoType, ammoGive);
    }

    // Weapon pickups always come with ammo (vanilla GiveWeapon gave 6), and a weapon that's
    // better than anything held becomes the selected one. Returns false only when the player
    // already owned the weapon and had no room for its ammo.
    private static bool TryGiveWeapon(string weaponName, string ammoType, int ammoGive)
    {
        var oldBest = GetBestWeapon();

        var gotAmmo = ammoGive > 0
            && !string.Equals(ammoType, "None", StringComparison.OrdinalIgnoreCase)
            && GiveAmmo(ammoType, ammoGive) > 0;
        var gotWeapon = _inventoryManager.Give(weaponName, 1) > 0;

        var newBest = GetBestWeapon();
        if (newBest > oldBest)
            gamestate.weapon = gamestate.chosenweapon = newBest;

        DrawWeapon();
        return gotWeapon || gotAmmo;
    }


    /// <summary>
    /// Runs a mapdefs trigger's action. If it goes off, a secret trigger counts as found, and the
    /// trigger's tile is cleared so it can't go off again.
    /// </summary>
    private static void ActivateTrigger(MapTriggerTranslation trigger, int tilex, int tiley, controldirs dir)
    {
        var activation = new Entities.TriggerActivation(tilex, tiley, dir, player);
        if (!Entities.MapTriggerRegistry.Invoke(trigger.Action, activation))
            return;

        if (trigger.Secret)
            gamestate.secretcount++;
        _mapManager.SetMapSpot(tilex, tiley, 1, 0);
    }

    // The cardinal direction the player faces, as Cmd_Use reckons it
    private static controldirs FacingDir(int angle) =>
        angle < ANGLES / 8 || angle > 7 * ANGLES / 8 ? controldirs.di_east
        : angle < 3 * ANGLES / 8 ? controldirs.di_north
        : angle < 5 * ANGLES / 8 ? controldirs.di_west
        : controldirs.di_south;

    internal static void Cmd_Use()
    {
        int checkx, checky, cmdtile;
        controldirs dir;
        bool elevatorok;

        //
        // find which cardinal direction the player is facing
        //
        if (player.Angle < ANGLES / 8 || player.Angle > 7 * ANGLES / 8)
        {
            checkx = player.TileX + 1;
            checky = player.TileY;
            dir = controldirs.di_east;
            elevatorok = true;
        }
        else if (player.Angle < 3 * ANGLES / 8)
        {
            checkx = player.TileX;
            checky = player.TileY - 1;
            dir = controldirs.di_north;
            elevatorok = false;
        }
        else if (player.Angle < 5 * ANGLES / 8)
        {
            checkx = player.TileX - 1;
            checky = player.TileY;
            dir = controldirs.di_west;
            elevatorok = true;
        }
        else
        {
            checkx = player.TileX;
            checky = player.TileY + 1;
            dir = controldirs.di_south;
            elevatorok = false;
        }

        cmdtile = _mapManager.tilemap[checkx, checky];
        if (_mapManager.GetTrigger(checkx, checky) is { IsWalkOver: false } trigger)
        {
            //
            // mapdefs use trigger (a pushable wall)
            //
            ActivateTrigger(trigger, checkx, checky, dir);
            return;
        }
        if (!_inputManager.IsButtonHeld(buttontypes.bt_use) && cmdtile == MapDataConstants.ELEVATORTILE && elevatorok)
        {
            //
            // use elevator
            //
            _inputManager.SetButtonHeld(buttontypes.bt_use, true);

            _mapManager.tilemap[checkx, checky]++;              // flip switch
            if (_mapManager.MAPSPOT(player.TileX, player.TileY, 0) == MapDataConstants.ALTELEVATORTILE)
                playstate = playstatetypes.ex_secretlevel;
            else
                playstate = playstatetypes.ex_completed;
            _audioManager.Play("switches/elevbutn");
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

        NewActorState(player, PlayerPawn.AttackState);

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
    internal static void T_Player(Entities.Actors.Actor ob)
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

        plux = (ushort)(player.X >> UNSIGNEDSHIFT);                     // scale to fit in unsigned
        pluy = (ushort)(player.Y >> UNSIGNEDSHIFT);
        player.TileX = (byte)(player.X >> (int)MapConstants.TILESHIFT);                // scale to tile values
        player.TileY = (byte)(player.Y >> (int)MapConstants.TILESHIFT);
    }

    internal static void T_Attack(Entities.Actors.Actor ob)
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

        plux = (ushort)(player.X >> UNSIGNEDSHIFT);                     // scale to fit in unsigned
        pluy = (ushort)(player.Y >> UNSIGNEDSHIFT);
        player.TileX = (byte)(player.X >> (int)MapConstants.TILESHIFT);                // scale to tile values
        player.TileY = (byte)(player.Y >> (int)MapConstants.TILESHIFT);

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
                    NewActorState(ob, PlayerPawn.SpawnState);
                    if (GetAmmo() == 0)
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
                    if (GetAmmo() == 0)
                        break;
                    if (_inputManager.IsButtonPressed(buttontypes.bt_attack))
                        gamestate.attackframe -= 2;
                    // case passthrough is not a thing in C#, repeating case 1 code
                    if (GetAmmo() == 0)
                    {       // can only happen with chain gun
                        gamestate.attackframe++;
                        break;
                    }
                    GunAttack(ob);
                    if (ammocheat == 0)
                        _inventoryManager.Take(AmmoType, 1);
                    DrawAmmo();
                    break;
                case 1:
                    if (GetAmmo() == 0)
                    {       // can only happen with chain gun
                        gamestate.attackframe++;
                        break;
                    }
                    GunAttack(ob);
                    if (ammocheat == 0)
                        _inventoryManager.Take(AmmoType, 1);
                    DrawAmmo();
                    break;

                case 2:
                    KnifeAttack(ob);
                    break;

                case 3:
                    if (GetAmmo() != 0 && _inputManager.IsButtonPressed(buttontypes.bt_attack))
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
    // (Program.EnemyAI.cs). The player pawn, projectiles and the BJ-victory actor share
    // _actors with the enemies but have no "Chase" state, so they never qualify.
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

    internal static void KnifeAttack(Entities.Actors.Actor ob)
    {
        _audioManager.Play("weapon/knife/attack");

        var closest = FindShootCandidates().FirstOrDefault(c => c.TransX <= 0x18000L);
        if (closest == null)
            return; // missed

        DamageActor(closest, (uint)(US_RndT() >> 4));
    }

    internal static void GunAttack(Entities.Actors.Actor ob)
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
        dx = Math.Abs(closest.TileX - player.TileX);
        dy = Math.Abs(closest.TileY - player.TileY);
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
        if (player.Angle > 270)
        {
            player.Angle -= (short)(tics * 3);
            if (player.Angle < 270)
                player.Angle = 270;
        }
        else if (player.Angle < 270)
        {
            player.Angle += (short)(tics * 3);
            if (player.Angle > 270)
                player.Angle = 270;
        }

        desty = ((player.TileY - 5) << (int)MapConstants.TILESHIFT) - 0x3000;

        if (player.Y > desty)
        {
            player.Y -= (int)(tics * 4096);
            if (player.Y < desty)
                player.Y = desty;
        }
    }

    // angle: the mapdefs player start's facing, 0=east, 90=north, 180=west, 270=south
    internal static void SpawnPlayer(int tilex, int tiley, int angle)
    {
        player.Active = activetypes.ac_yes;
        player.SetPosition(tilex, tiley);       // tile, and the tile-centred world x/y
        player.AreaNumber = (byte)(_mapManager.MAPSPOT(tilex, tiley, 0) - MapDataConstants.AREATILE);
        NewActorState(player, PlayerPawn.SpawnState);
        player.Angle = (short)((angle % ANGLES + ANGLES) % ANGLES);
        player.RuntimeFlags = objflags.FL_NEVERMARK;
        Thrust(0, 0);                           // set some variables

        InitAreas();
    }
}
