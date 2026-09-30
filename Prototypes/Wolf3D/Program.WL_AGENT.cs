using Wolf3D.Assets;
using Wolf3D.Constants;
using Wolf3D.Enums;
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

    // The weapon slot keys, indexed by slot number (`weapon.slot` 0-9, 1-4 for Wolf3D's four).
    private static readonly buttontypes[] SlotButtons =
    [
        buttontypes.bt_slot0, buttontypes.bt_slot1, buttontypes.bt_slot2, buttontypes.bt_slot3, buttontypes.bt_slot4,
        buttontypes.bt_slot5, buttontypes.bt_slot6, buttontypes.bt_slot7, buttontypes.bt_slot8, buttontypes.bt_slot9,
    ];

    // Only weapons with ammo can be switched to. Out of ammo for the picked weapon, the player
    // keeps the fallback it forced (the knife) and the pick stands, so the weapon comes back
    // when ammo turns up -- unless they switch to another weapon that still has ammo.
    internal static void CheckWeaponChange()
    {
        var usable = OwnedWeapons().Where(CanFire).ToList();
        if (!CanFire(gamestate.chosenweapon) && gamestate.weapon != null)
            usable.Remove(gamestate.weapon);
        if (usable.Count == 0)
            return;

        string? newWeapon = null;
        var current = gamestate.weapon != null ? usable.IndexOf(gamestate.weapon) : -1;

        if (_inputManager.IsButtonPressed(buttontypes.bt_nextweapon) && !_inputManager.IsButtonHeld(buttontypes.bt_nextweapon))
        {
            newWeapon = usable[(current + 1) % usable.Count];
        }
        else if (_inputManager.IsButtonPressed(buttontypes.bt_prevweapon) && !_inputManager.IsButtonHeld(buttontypes.bt_prevweapon))
        {
            newWeapon = usable[current <= 0 ? usable.Count - 1 : current - 1];
        }
        else
        {
            for (var slot = 0; slot < SlotButtons.Length; slot++)
            {
                if (!_inputManager.IsButtonPressed(SlotButtons[slot]) || _inputManager.IsButtonHeld(SlotButtons[slot]))
                    continue;

                // The best weapon with ammo in that slot; pressed again with one of the slot's
                // weapons already in hand, the next one in the slot.
                var inSlot = usable.Where(w => WeaponSlot(w) == slot).ToList();
                if (inSlot.Count == 0)
                    break;
                var inHand = gamestate.weapon != null ? inSlot.IndexOf(gamestate.weapon) : -1;
                newWeapon = inHand >= 0
                    ? inSlot[(inHand + 1) % inSlot.Count]
                    : inSlot.OrderBy(WeaponSelectionOrder).First();
                if (newWeapon == gamestate.weapon)
                    newWeapon = null;
                break;
            }
        }

        if (newWeapon != null)
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

        //
        // looking up and down: only the view, which SetupPitch keeps within what it can show
        //
        if (controlcenterview)
            viewpitch = 0;
        else if (controlpitch != 0)
            viewpitch = Math.Clamp(viewpitch + controlpitch, -MaxPitch(), MaxPitch());

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
        // a controller stick's strafe, as far as it's pushed
        //
        if (controlstrafe != 0)
        {
            angle = ob.Angle + (controlstrafe > 0 ? -ANGLES / 4 : ANGLES / 4);
            if (angle < 0)
                angle += ANGLES;
            else if (angle >= ANGLES)
                angle -= ANGLES;
            Thrust(angle, (int)(Math.Abs(controlstrafe) * MOVESCALE));
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

        // A diagonal wall tile's open half has no area number (plane 0 holds the wall), so keep
        // the one the player walked in from.
        var areatile = _mapManager.MAPSPOT(player.TileX, player.TileY, 0);
        if (MapManager.VALIDAREA(areatile))
            player.AreaNumber = (byte)(areatile - MapDataConstants.AREATILE);

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
                    else if (_mapManager.wallshape[x, y] is var shape and not WallShape.Square)
                    {
                        //
                        // a diagonal only blocks on its solid side of the face
                        //
                        if (BoxHitsDiagonal(shape, (int)x, (int)y, ob.X, ob.Y, PLAYERSIZE))
                        {
                            diagonalblock = shape;
                            return false;
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
        diagonalblock = WallShape.Square;
        if (TryMove(ob))
            return;

        if (noclip != 0 && ob.X > 2 * MapConstants.TILEGLOBAL && ob.Y > 2 * MapConstants.TILEGLOBAL
            && ob.X < (((int)(_mapManager.mapwidth - 1)) << (int)MapConstants.TILESHIFT)
            && ob.Y < (((int)(_mapManager.mapheight - 1)) << (int)MapConstants.TILESHIFT))
            return;         // walk through walls

        if (!_audioManager.IsAnySoundPlaying())
             _audioManager.Play("world/hitwall");

        //
        // ran into a diagonal face: slide along it (the move's part in the face's direction),
        // since neither axis on its own gets past a 45 degree wall
        //
        if (diagonalblock != WallShape.Square)
        {
            bool nwToSe = diagonalblock is WallShape.SolidNE or WallShape.SolidSW;
            int along = nwToSe ? (xmove + ymove) / 2 : (xmove - ymove) / 2;
            ob.X = basex + along;
            ob.Y = basey + (nwToSe ? along : -along);
            if (TryMove(ob))
                return;
        }

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

        // Shown over the view in the item's message style (hud-messages.yaml), or game-info's for pickups
        if (builtActor.Properties.TryGetValue("inventory.pickupmessage", out var pickupMessage))
        {
            builtActor.Properties.TryGetValue("inventory.pickupmessagestyle", out var pickupMessageStyle);
            _hudMessageManager.Show(HudMessageKind.Pickup, pickupMessage?.ToString(), pickupMessageStyle?.ToString());
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
        ActorActionRegistry.Register("T_DeathCam", T_DeathCam);

        // The weapon in hand's states (Program.PlayerWeapon.cs).
        RegisterWeaponActions();

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

    /// <summary>The status bar's numbers (fonts.yaml)</summary>
    internal static readonly Fonts.TextStyle StatusNumberStyle = new("StatusNumbers", "White");

    /// <summary>
    /// A number right-aligned in <paramref name="width"/> characters of the status bar, blanking
    /// the rest; a number too long to fit shows its last digits. x is in 8 pixel columns.
    /// </summary>
    static void LatchNumber(int x, int y, int width, int number)
    {
        string str = number.ToString();
        str = str.Length <= width ? str.PadLeft(width) : str[^width..];

        _graphicManager.DrawText(x * 8, 200 - (STATUSLINES - y), str, StatusNumberStyle);
    }

    /*
    =============================================================================

                                    WEAPONS

    Everything about a weapon comes from its actordefs class (weapons.yaml): `weapon.slot`
    and `weapon.selectionorder` (lower is better) order and rank them, `weapon.ammotype1`/
    `weapon.ammouse1` say what a shot costs, `attacksound` and `inventory.icon` are its
    sound and status bar pic, and its Ready state's sprite is drawn in the view.

    =============================================================================
    */

    // The slot key (1-9, 0) that picks a weapon; -1 for one with no `weapon.slot`, which only
    // next/previous weapon reach.
    static int WeaponSlot(string weapon) => _inventoryManager.GetIntProperty(weapon, "weapon.slot", -1);

    // Slot order for cycling, as on the keyboard: 1-9, then 0, then weapons with no slot.
    static int WeaponSlotOrder(string weapon) => WeaponSlot(weapon) switch { 0 => 10, < 0 => 11, var slot => slot };

    static int WeaponSelectionOrder(string weapon) =>
        _inventoryManager.GetIntProperty(weapon, "weapon.selectionorder", int.MaxValue);

    /// <summary>The ammo a weapon shoots, or null for one that needs none (the knife).</summary>
    static string? WeaponAmmoType(string? weapon)
    {
        if (weapon == null || _inventoryManager.GetIntProperty(weapon, "weapon.ammouse1", 0) <= 0)
            return null;
        var type = _inventoryManager.GetStringProperty(weapon, "weapon.ammotype1");
        return string.IsNullOrEmpty(type) || type.Equals("None", StringComparison.OrdinalIgnoreCase) ? null : type;
    }

    /// <summary>Whether the player has the ammo for one shot of <paramref name="weapon"/>.</summary>
    static bool CanFire(string? weapon)
    {
        if (weapon == null)
            return false;
        var ammoType = WeaponAmmoType(weapon);
        return ammoType == null
            || _inventoryManager.GetCount(ammoType) >= _inventoryManager.GetIntProperty(weapon, "weapon.ammouse1", 0);
    }

    /// <summary>Takes one shot's ammo for the weapon in hand (unless the ammo cheat is on).</summary>
    static void UseAmmo()
    {
        var ammoType = WeaponAmmoType(gamestate.weapon);
        if (ammoType != null && ammocheat == 0)
            _inventoryManager.Take(ammoType, _inventoryManager.GetIntProperty(gamestate.weapon!, "weapon.ammouse1", 0));
        DrawAmmo();
    }

    /// <summary>The held weapons, in slot order and worst to best within a slot.</summary>
    static List<string> OwnedWeapons() =>
        _inventoryManager.Items.Keys
            .Where(item => _inventoryManager.FindClass(item, "Weapon") != null
                && _inventoryManager.FindClass(item, "WeaponGiver") == null)
            .OrderBy(WeaponSlotOrder)
            .ThenByDescending(WeaponSelectionOrder)
            .ToList();

    /// <summary>The best held weapon (lowest selectionorder), optionally only among those with ammo.</summary>
    static string? BestWeapon(bool canFire = false) =>
        OwnedWeapons()
            .Where(w => !canFire || CanFire(w))
            .OrderBy(WeaponSelectionOrder)
            .FirstOrDefault();

    // The status bar shows the ammo of the weapon the player picked, even while out of ammo
    // forces a fallback; with an ammo-less weapon picked, the ammo of the best one that has some.
    static string? DisplayAmmoType() =>
        WeaponAmmoType(gamestate.chosenweapon)
        ?? OwnedWeapons().OrderBy(WeaponSelectionOrder).Select(WeaponAmmoType).FirstOrDefault(t => t != null);

    static int GetAmmo() => DisplayAmmoType() is { } type ? _inventoryManager.GetCount(type) : 0;

    static void DrawAmmo()
    {
        if (viewsize == 21 && ingame) return;
        LatchNumber(27, 16, 2, GetAmmo());
    }

    /// <summary>Gives <paramref name="amount"/> of every ammo type; returns how much was taken in all.</summary>
    internal static int GiveAllAmmo(int amount) =>
        _inventoryManager.GetClassesDerivedFrom("Ammo")
            .Select(_inventoryManager.GetItemType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Sum(type => GiveAmmo(type, amount));

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
        // Out of ammo had forced a fallback (the knife): A_WeaponReady takes the picked weapon
        // back up once the fallback is idle again.
        var added = _inventoryManager.Give(ammoType, ammo);
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
        if (gamestate.weapon == null) return;

        var icon = _inventoryManager.GetStringProperty(gamestate.weapon, "inventory.icon");
        if (!string.IsNullOrEmpty(icon))
            StatusDrawPic(icon, 32, 8);
    }
/*
==================
=
= GiveWeapon
=
==================
*/

    // The player's actordefs class (actordefs/wolf3d/player.yaml), for its `player.*` properties.
    private const string PlayerClass = "Player";

    /// <summary>
    /// New game / respawn loadout: the Player class's `player.startitem` (item: amount), with
    /// the best weapon in it selected.
    /// </summary>
    internal static void GiveStartingInventory()
    {
        _inventoryManager.Clear();

        if (_inventoryManager.GetProperty(PlayerClass, "player.startitem") is IDictionary<object, object> items)
        {
            foreach (var (item, amount) in items)
                _inventoryManager.Give(item.ToString() ?? "", Convert.ToInt32(amount));
        }
        else
            Console.WriteLine($"No player.startitem on actordefs class {PlayerClass}: the player starts with nothing.");

        gamestate.weapon = gamestate.chosenweapon = BestWeapon();
    }

    /// <summary>
    /// Every weapon in actordefs that can be held, as held item types: a Blue reskin counts as
    /// its base weapon, and base classes with no Ready state (WolfWeapon) and WeaponGivers are
    /// left out.
    /// </summary>
    internal static List<string> AllWeapons() =>
        _inventoryManager.GetClassesDerivedFrom("Weapon")
            .Where(w => _inventoryManager.FindClass(w, "WeaponGiver") == null
                && _inventoryManager.CreateActor(w)?.ResolvedStates.ContainsKey(WeaponReadyState) == true)
            .Select(_inventoryManager.GetItemType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Gives a weapon by class name with a pickup's worth (6) of its ammo.</summary>
    internal static void GiveWeapon(string weapon) =>
        TryGiveWeapon(weapon, 6);

    private static bool TryGiveWeapon(Entities.Actors.Weapon pickup)
    {
        // A WeaponGiver (e.g. the map's gatling upgrade) names the weapon it hands out.
        var weaponName = pickup.Properties.TryGetValue("weapongiver.weapon", out var given)
            ? given.ToString() ?? pickup.Name
            : pickup.Name;
        var ammoGive = pickup.Properties.TryGetValue("weapon.ammogive1", out var ammo)
            ? Convert.ToInt32(ammo)
            : 0;

        return TryGiveWeapon(weaponName, ammoGive);
    }

    // Weapon pickups always come with ammo (vanilla GiveWeapon gave 6) of the weapon's own
    // type, and a weapon that's better than anything held becomes the selected one. Returns
    // false only when the player already owned the weapon and had no room for its ammo.
    private static bool TryGiveWeapon(string weaponName, int ammoGive)
    {
        var oldBest = BestWeapon();

        var ammoType = WeaponAmmoType(weaponName);
        var gotAmmo = ammoGive > 0 && ammoType != null && GiveAmmo(ammoType, ammoGive) > 0;
        var gotWeapon = _inventoryManager.Give(weaponName, 1) > 0;

        var newBest = BestWeapon();
        if (newBest != null && newBest != oldBest)
            gamestate.weapon = gamestate.chosenweapon = newBest;

        DrawWeapon();
        DrawAmmo();
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

        if (IsWeaponReady())
        {
            CheckWeaponChange();

            if (_inputManager.IsButtonPressed(buttontypes.bt_use))
                Cmd_Use();
        }
        else
        {
            // Mid-attack, a fresh press of use or fire is dropped rather than kept for later
            // (vanilla T_Attack); fire held down from before still counts for A_ReFire.
            if (_inputManager.IsButtonPressed(buttontypes.bt_use) && !_inputManager.IsButtonHeld(buttontypes.bt_use))
                _inputManager.SetButtonPressed(buttontypes.bt_use, false);

            if (_inputManager.IsButtonPressed(buttontypes.bt_attack) && !_inputManager.IsButtonHeld(buttontypes.bt_attack))
                _inputManager.SetButtonPressed(buttontypes.bt_attack, false);
        }

        ControlMovement(ob);
        if (gamestate.victoryflag)              // watching the BJ actor
            return;

        plux = (ushort)(player.X >> UNSIGNEDSHIFT);                     // scale to fit in unsigned
        pluy = (ushort)(player.Y >> UNSIGNEDSHIFT);
        player.TileX = (byte)(player.X >> (int)MapConstants.TILESHIFT);                // scale to tile values
        player.TileY = (byte)(player.Y >> (int)MapConstants.TILESHIFT);

        // The weapon's Ready/Fire states (Program.PlayerWeapon.cs): starts and plays attacks
        TickWeapon();
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

    // The weapon in hand's `attacksound`.
    static void PlayAttackSound()
    {
        if (gamestate.weapon != null && _inventoryManager.GetStringProperty(gamestate.weapon, "attacksound") is { Length: > 0 } sound)
            _audioManager.Play(sound);
    }

    internal static void KnifeAttack(Entities.Actors.Actor ob)
    {
        PlayAttackSound();

        var closest = FindShootCandidates().FirstOrDefault(c => c.TransX <= 0x18000L);
        if (closest == null)
            return; // missed

        DamageActor(closest, (uint)(US_RndT() >> 4));
    }

    internal static void GunAttack(Entities.Actors.Actor ob)
    {
        int damage;
        int dx, dy, dist;

        PlayAttackSound();

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
        viewpitch = 0;              // watch BJ straight on
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
