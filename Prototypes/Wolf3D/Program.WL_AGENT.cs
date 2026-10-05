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
    // player state info: thrustspeed, plux/pluy, anglefrac, LastAttacker and the rest are the
    // acting player's (Program.Players.cs)
    //

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
        if (!CanFire(playerstate.chosenweapon) && playerstate.weapon != null)
            usable.Remove(playerstate.weapon);
        if (usable.Count == 0)
            return;

        string? newWeapon = null;
        var current = playerstate.weapon != null ? usable.IndexOf(playerstate.weapon) : -1;

        if (playerinput.IsFreshPress(buttontypes.bt_nextweapon))
        {
            newWeapon = usable[(current + 1) % usable.Count];
        }
        else if (playerinput.IsFreshPress(buttontypes.bt_prevweapon))
        {
            newWeapon = usable[current <= 0 ? usable.Count - 1 : current - 1];
        }
        else
        {
            for (var slot = 0; slot < SlotButtons.Length; slot++)
            {
                if (!playerinput.IsFreshPress(SlotButtons[slot]))
                    continue;

                // The best weapon with ammo in that slot; pressed again with one of the slot's
                // weapons already in hand, the next one in the slot.
                var inSlot = usable.Where(w => WeaponSlot(w) == slot).ToList();
                if (inSlot.Count == 0)
                {
                    UseSlotItem(slot);      // Planet Strike's fission detonator (Program.Teleporter.cs)
                    break;
                }
                var inHand = playerstate.weapon != null ? inSlot.IndexOf(playerstate.weapon) : -1;
                newWeapon = inHand >= 0
                    ? inSlot[(inHand + 1) % inSlot.Count]
                    : inSlot.OrderBy(WeaponSelectionOrder).First();
                if (newWeapon == playerstate.weapon)
                    newWeapon = null;
                break;
            }
        }

        if (newWeapon != null)
        {
            playerstate.weapon = playerstate.chosenweapon = newWeapon;
            DrawWeapon();
        }
    }

    internal static void ControlMovement(Entities.Actors.Actor ob)
    {
        int angle;
        int angleunits;

        thrustspeed = 0;

        // The player class's speeds: player.forwardmove forwards and back, player.sidemove strafing
        double forward = PlayerFactor("player.forwardmove"), side = PlayerFactor("player.sidemove");

        //
        // looking up and down: only the view, which SetupPitch keeps within what it can show
        //
        if (playerinput.CenterView)
            playerpitch = 0;
        else if (playerinput.Pitch != 0)
            playerpitch = Math.Clamp(playerpitch + playerinput.Pitch, -MaxPitch(), MaxPitch());

        if (playerinput.IsPressed(buttontypes.bt_strafeleft))
        {
            angle = ob.Angle + ANGLES / 4;
            if (angle >= ANGLES)
                angle -= ANGLES;
            if (playerinput.IsPressed(buttontypes.bt_run))
                Thrust(angle, (int)(RUNMOVE * MOVESCALE * tics * side));
            else
                Thrust(angle, (int)(BASEMOVE * MOVESCALE * tics * side));
        }

        if (playerinput.IsPressed(buttontypes.bt_straferight))
        {
            angle = ob.Angle - ANGLES / 4;
            if (angle < 0)
                angle += ANGLES;
            if (playerinput.IsPressed(buttontypes.bt_run))
                Thrust(angle, (int)(RUNMOVE * MOVESCALE * tics * side));
            else
                Thrust(angle, (int)(BASEMOVE * MOVESCALE * tics * side));
        }

        //
        // a controller stick's strafe, as far as it's pushed
        //
        if (playerinput.ControlStrafe != 0)
        {
            angle = ob.Angle + (playerinput.ControlStrafe > 0 ? -ANGLES / 4 : ANGLES / 4);
            if (angle < 0)
                angle += ANGLES;
            else if (angle >= ANGLES)
                angle -= ANGLES;
            Thrust(angle, (int)(Math.Abs(playerinput.ControlStrafe) * MOVESCALE * side));
        }

        //
        // side to side move
        //
        if (playerinput.IsPressed(buttontypes.bt_strafe))
        {
            //
            // strafing
            //
            //
            if (playerinput.ControlX > 0)
            {
                angle = ob.Angle - ANGLES / 4;
                if (angle < 0)
                    angle += ANGLES;
                Thrust(angle, (int)(playerinput.ControlX * MOVESCALE * side));      // move to left
            }
            else if (playerinput.ControlX < 0)
            {
                angle = ob.Angle + ANGLES / 4;
                if (angle >= ANGLES)
                    angle -= ANGLES;
                Thrust(angle, (int)(-playerinput.ControlX * MOVESCALE * side));     // move to right
            }
        }
        else
        {
            //
            // not strafing
            //
            anglefrac += (short)playerinput.ControlX;
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
        if (playerinput.ControlY < 0)
        {
            Thrust(ob.Angle, (int)(-playerinput.ControlY * MOVESCALE * forward)); // move forwards
        }
        else if (playerinput.ControlY > 0)
        {
            angle = ob.Angle + ANGLES / 2;
            if (angle >= ANGLES)
                angle -= ANGLES;
            Thrust(angle, (int)(playerinput.ControlY * BACKMOVESCALE * forward));          // move backwards
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

        // demos move the player exactly as v1.4 did, or they drift from what was recorded
        if (demorecord || demoplayback)
        {
            xmove = MathUtils.FixedByFracOrig(speed, costable[angle]);
            ymove = -MathUtils.FixedByFracOrig(speed, sintable[angle]);
        }
        else
        {
            xmove = MathUtils.FixedMul(speed, costable[angle]);
            ymove = -MathUtils.FixedMul(speed, sintable[angle]);
        }

        ClipMove(player, xmove, ymove);

        var (oldtilex, oldtiley) = (player.TileX, player.TileY);
        player.TileX = (byte)(player.X >> (int)MapConstants.TILESHIFT);                // scale to tile values
        player.TileY = (byte)(player.Y >> (int)MapConstants.TILESHIFT);

        // A diagonal wall tile's open half has no area number (plane 0 holds the wall), so keep
        // the one the player walked in from.
        var areatile = _mapManager.MAPSPOT(player.TileX, player.TileY, 0);
        if (_mapManager.VALIDAREA(areatile))
            player.AreaNumber = (byte)(areatile - _mapManager.Floors.AreaTile);

        //
        // mapdefs walk-over trigger (the end-of-castle exit) on the tile just stepped onto
        //
        if ((player.TileX != oldtilex || player.TileY != oldtiley)
            && _mapManager.GetTrigger(player.TileX, player.TileY) is { IsWalkOver: true } trigger)
            ActivateTrigger(trigger, player.TileX, player.TileY, FacingDir(player.Angle));

        // mapdefs floors trigger: a floor code that does something when stepped onto (Blake Stone's way out)
        if ((player.TileX != oldtilex || player.TileY != oldtiley)
            && _mapManager.Floors.Triggers.TryGetValue(areatile, out var floorAction))
        {
            Entities.MapTriggerRegistry.Invoke(floorAction, new Entities.TriggerActivation(player.TileX, player.TileY,
                FacingDir(player.Angle), player, _mapManager.GetTag(player.TileX, player.TileY)));
        }
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
        bool nearwallsprite = false;

        //
        // check for solid walls
        //
        for (y = yl; y <= yh; y++)
        {
            for (x = xl; x <= xh; x++)
            {
                check = _mapManager.actorat[x, y];
                if (check != null && check is not ActorMark)       // enemies are checked below
                {
                    if (check is WallSpriteBlocker)
                        nearwallsprite = true;          // only its panel blocks: checked below
                    else if (_mapManager.tilemap[x, y] == BIT_WALL && x == pwallx && y == pwally)   // back of moving pushwall?
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
        // a wall sprite only blocks along its panel (Program.WallSprites.cs)
        //
        if (nearwallsprite
            && WallSpriteHitByBox((int)xl, (int)yl, (int)xh, (int)yh, ob.X, ob.Y, PLAYERSIZE, player: true) is { } panel)
        {
            wallspriteblock = panel;
            return false;
        }

        //
        // check for actors: a living actor marked on a tile near the player
        // (MapManager.MarkActorTile), and within MINACTORDIST of it, blocks the move
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
                if (_mapManager.ActorMarkAt((int)x, (int)y) is not { } actor || !Managers.MapManager.IsSolidActor(actor))
                    continue;

                var deltax = ob.X - actor.X;
                if (deltax < -MINACTORDIST || deltax > MINACTORDIST)
                    continue;
                var deltay = ob.Y - actor.Y;
                if (deltay < -MINACTORDIST || deltay > MINACTORDIST)
                    continue;

                return false;
            }
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
        wallspriteblock = null;
        wallspritefromx = basex;
        wallspritefromy = basey;
        if (TryMove(ob))
            return;

        if (noclip != 0 && ob.X > 2 * MapConstants.TILEGLOBAL && ob.Y > 2 * MapConstants.TILEGLOBAL
            && ob.X < (((int)(_mapManager.mapwidth - 1)) << (int)MapConstants.TILESHIFT)
            && ob.Y < (((int)(_mapManager.mapheight - 1)) << (int)MapConstants.TILESHIFT))
            return;         // walk through walls

        if (!_audioManager.IsAnySoundPlaying())
             PlayPlayerSound("world/hitwall");

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

        //
        // ran into a wall sprite's panel: slide along it, which for a diagonal one neither
        // axis on its own would
        //
        if (wallspriteblock is { } panel)
        {
            double ux = (panel.X2 - panel.X1) / panel.Length, uy = (panel.Y2 - panel.Y1) / panel.Length;
            double along = xmove * ux + ymove * uy;
            ob.X = basex + (int)(along * ux);
            ob.Y = basey + (int)(along * uy);
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

        // An item for other player classes stays on the floor
        if (!PlayerClassCanPickUp(builtActor.Name))
            return;

        // Deathmatch: a weapon stays where it is, so it's only for a player without it yet
        if (gamemode == GameMode.Deathmatch && builtActor is Entities.Actors.Weapon weaponPickup && HasWeaponFrom(weaponPickup))
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
            PlayPlayerSound(pickupSound?.ToString() ?? "");
            GrinAtPickup(builtActor, pickupSound?.ToString());
        }

        // Shown over the view in the item's message style (hud-messages.yaml), or game-info's for pickups
        if (builtActor.Properties.TryGetValue("inventory.pickupmessage", out var pickupMessage))
        {
            builtActor.Properties.TryGetValue("inventory.pickupmessagestyle", out var pickupMessageStyle);
            if (ActingIsLocal)
                _hudMessageManager.Show(HudMessageKind.Pickup, pickupMessage?.ToString(), pickupMessageStyle?.ToString());
        }

        if (ActingIsLocal)
            _videoManager.StartBonusFlash();

        // Deathmatch (Program.Deathmatch.cs): a weapon stays for everyone else, and anything
        // else comes back a while after it's taken
        if (gamemode == GameMode.Deathmatch && builtActor is Entities.Actors.Weapon)
            return;
        if (gamemode == GameMode.Deathmatch && netrules.ItemRespawn)
            QueueItemRespawn(builtActor);

        //check.shapenum = "";                   // remove from list
        _mapManager.RemoveActor(builtActor);
    }

    /// <summary>
    /// Whether the player class may pick up an item: not when it's in the item's
    /// `inventory.forbiddento`, or the item has an `inventory.restrictedto` it isn't in. A class
    /// counts as in a list that names it or a class it descends from.
    /// </summary>
    internal static bool PlayerClassCanPickUp(string item)
    {
        // A list of classes, or one class on its own; null when the item doesn't say
        string[]? Classes(string key) => _inventoryManager.GetProperty(item, key) switch
        {
            string one => [one],
            IEnumerable<object> list => list.Select(c => c.ToString() ?? "").ToArray(),
            _ => null,
        };
        bool In(string[] classes) => _inventoryManager.FindClass(PlayerClass, classes) != null;

        if (Classes("inventory.forbiddento") is { } forbidden && In(forbidden))
            return false;
        return Classes("inventory.restrictedto") is not { } restricted || In(restricted);
    }

    /// <summary>
    /// Gives the player <paramref name="amount"/> of <paramref name="item"/>. Returns false
    /// when nothing could be taken, so the pickup should be left where it is.
    /// </summary>
    /// <summary>Whether giving the item would do anything now (a health item, unless health is full); without giving it</summary>
    internal static bool CouldTakeInventory(Inventory item)
    {
        if (item is not Entities.Actors.Health)
            return true;
        int itemMax = _inventoryManager.GetIntProperty(item.Name, "inventory.maxamount", 0);
        int cap = itemMax > 0 ? Math.Min(itemMax, MaxHealth) : MaxHealth;
        return playerstate.health < cap;
    }

    internal static bool TryApplyInventory(Inventory item, int amount)
    {
        switch (item)
        {
            case Entities.Actors.Health:
            {
                // A health item's inventory.maxamount is the most it heals to (Wolf3D's blood and
                // gibs only help when nearly dead); 0 is up to the player's max health
                int itemMax = _inventoryManager.GetIntProperty(item.Name, "inventory.maxamount", 0);
                int cap = itemMax > 0 ? Math.Min(itemMax, MaxHealth) : MaxHealth;
                if (playerstate.health >= cap)
                    return false;
                HealSelf(amount, cap);
                return true;
            }
            case Entities.Actors.Ammo:
                return GiveAmmo(item.Name, amount) > 0;
            case Entities.Actors.BasicArmor:
                return TryGiveArmor(item.Name);
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

        // Enemies' thinks and actions (Entities.Actors.Monster, BlakeMonster)
        Entities.Actors.Monster.RegisterActions();
        Entities.Actors.BlakeMonster.RegisterActions();

        // Any actor's sounds, and the end of the game (Program.WL_ACT2.cs)
        ActorActionRegistry.Register("A_DeathScream", A_DeathScream);
        ActorActionRegistry.Register("A_ActiveSound", A_ActiveSound);
        ActorActionRegistry.Register("A_PlaySound", A_PlaySound);
        ActorActionRegistry.Register("A_Victory", A_Victory);
        ActorActionRegistry.Register("A_StartDeathCam", A_StartDeathCam);

        // Goldfire's warp sites (Managers.LevelAI)
        ActorActionRegistry.Register("A_WarpSiteGone", (Entities.Actors.Actor _) => _mapManager.AI.WarpSiteGone());
        ActorActionRegistry.Register("A_WarpSitesOff", (Entities.Actors.Actor _) => _mapManager.AI.WarpSitesOff());

        // Projectiles and effects (Program.WL_ACT2.cs).
        ActorActionRegistry.Register("A_Projectile", A_Projectile);
        ActorActionRegistry.Register("A_SpawnThing", A_SpawnThing);
        ActorActionRegistry.Register("A_Remove", A_Remove);

        // BJ victory cutscene (Program.WL_ACT2.cs): BJ walks the patrol arrows as a Monster.
        ActorActionRegistry.RegisterFor<Entities.Actors.Monster>("T_BJRun", T_BJRun);
        ActorActionRegistry.RegisterFor<Entities.Actors.Monster>("T_BJJump", T_BJJump);
        ActorActionRegistry.Register("T_BJDone", T_BJDone);

        // The player's own think states (PlayerPawn), ticked by MapManager.DoActor.
        ActorActionRegistry.Register("T_Player", T_Player);
        ActorActionRegistry.Register("T_DeathCam", T_DeathCam);
        ActorActionRegistry.Register("T_PlayerDead", T_PlayerDead);

        // The weapon in hand's states (Program.PlayerWeapon.cs).
        RegisterWeaponActions();

        // mapdefs trigger and switch actions, run when the player uses a trigger's tile or a
        // switch wall (Cmd_Use) or steps onto a walk-over trigger (Thrust)
        // A_PushWall("moving sound", "blocked sound"): either left out is silent
        Entities.MapTriggerRegistry.Register("A_PushWall", (trigger, args) => PushWall(trigger.TileX, trigger.TileY, trigger.Dir,
            args.ElementAtOrDefault(0), args.ElementAtOrDefault(1)));
        Entities.MapTriggerRegistry.Register("A_VictoryTile", (_, _) => { VictoryTile(); return true; });
        Entities.MapTriggerRegistry.Register("A_Exit", (_, _) => ExitAction(secret: false));
        Entities.MapTriggerRegistry.Register("A_SecretExit", (_, _) => ExitAction(secret: true));
        // Hub clusters' ways between levels (Program.Elevator.cs, Program.Hubs.cs)
        Entities.MapTriggerRegistry.Register("A_FloorSelect", FloorSelectAction);
        Entities.MapTriggerRegistry.Register("A_Teleport", TeleportAction);
        Entities.MapTriggerRegistry.Register("A_VictoryRun", VictoryRunAction);
        ActorActionRegistry.Register("T_VictoryRun", T_VictoryRun);
        RegisterSwitchActions();    // tag-targeted door and wall actions (Program.SwitchActions.cs)
        RegisterSmartSwitchActions();   // A_SmartSwitch (Program.SmartSwitch.cs)
        RegisterTeleporterActions();    // A_UnlockFloor, A_DropDetonator (Program.Teleporter.cs)
        RegisterZoneLightActions(); // light zone actions, for switches and actors (Program.ZoneLights.cs)
        RegisterActorLightActions(); // actors' own lights (Program.ActorLights.cs)
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


    private static StatusBarAsset? statusbar;
    private static readonly StatusBarAsset NoStatusBar = new();

    /// <summary>
    /// The game pack's status bar layout (statusbar.yaml); empty if it has none. Only a layout
    /// that was found is kept, so a look before the assets are loaded doesn't stick.
    /// </summary>
    internal static StatusBarAsset StatusBar =>
        statusbar ?? (statusbar = _assetManager?.FindInGamePack<StatusBarAsset>("statusbar")) ?? NoStatusBar;

    /// <summary>Draws a picture at a status bar position (320x200 pixels from its top left corner).</summary>
    static void StatusDrawPic(string picName, int x, int y)
    {
        using var _ = _videoManager.UseUiOrigin(UiAnchor.Bottom);
        _graphicManager.DrawPic(picName, x, 200 - STATUSLINES + y);
    }

    /// <summary>Draws a picture at the named part of the status bar, if the layout has it.</summary>
    static void StatusDrawPic(string picName, string part)
    {
        if (StatusBar.Get(part) is { } element)
            StatusDrawPic(picName, element.X, element.Y);
    }

    static void StatusDrawFace(string picName) => StatusDrawPic(picName, "face");

    /// <summary>
    /// A number right-aligned in the named part's digits, blanking the rest; a number too long
    /// to fit shows its last digits. In the part's font and color, else the layout's numbers'.
    /// </summary>
    static void LatchNumber(string part, int number, bool hide = false)
    {
        if (StatusBar.Get(part) is not { } element)
            return;
        var numbers = StatusBar.Get("numbers");
        var font = element.Font ?? numbers?.Font;
        if (string.IsNullOrEmpty(font))
            return;

        // What the number is drawn over: a part under it, or a box to clear the old one
        if (element.Behind is { Length: > 0 } behind)
            DrawStatusPart(behind);
        if (element.Box.Count == 4)
        {
            using var _ = StatusBarOrigin(element);
            _videoManager.Bar(element.Box[0], StatusBarTop(element) + element.Box[1], element.Box[2], element.Box[3],
                element.BoxColor ?? "Black");
        }
        if (hide)
            return;

        // digits 0: as long as the number is, else right-aligned in that many (its last digits if longer)
        string str = number.ToString();
        if (element.Digits > 0)
            str = str.Length <= element.Digits ? str.PadLeft(element.Digits) : str[^element.Digits..];
        str = element.Prefix + str + element.Suffix;

        DrawStatusText(element, str, font, element.Color ?? numbers?.Color ?? "White");
    }

    /// <summary>Text at a status bar part, lined up as its align says</summary>
    static void DrawStatusText(Assets.StatusBarElement element, string text, string font, string color)
    {
        int x = element.Align.ToLowerInvariant() switch
        {
            "right" => element.X - TextWidth(text, font),
            "center" => element.X - TextWidth(text, font) / 2,
            _ => element.X,
        };
        using var _ = StatusBarOrigin(element);
        _graphicManager.DrawText(x, StatusBarTop(element) + element.Y, text, new Fonts.TextStyle(font, color));
    }

    /// <summary>Where a part's bar starts down the screen (320x200): the top bar's, or the bottom one's</summary>
    static int StatusBarTop(Assets.StatusBarElement element) => element.Top ? 0 : 200 - STATUSLINES;

    /// <summary>
    /// Lines the 320x200 layout up with the edge of the screen a part's bar is on, until
    /// disposed, so the bars stay at the top and bottom of a screen taller than 320x200.
    /// </summary>
    static VideoManager.UiOriginScope StatusBarOrigin(Assets.StatusBarElement element) =>
        _videoManager.UseUiOrigin(element.Top ? UiAnchor.Top : UiAnchor.Bottom);

    /// <summary>Draws a part that a number can sit on (statusbar.yaml behind)</summary>
    static void DrawStatusPart(string part)
    {
        if (part.Equals("weapon", StringComparison.OrdinalIgnoreCase))
            DrawWeaponPic();
        else if (part.Equals("weapon-corner", StringComparison.OrdinalIgnoreCase))
            DrawWeaponCorner();
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

    internal static int WeaponSelectionOrder(string weapon) =>
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
        var ammoType = WeaponAmmoType(playerstate.weapon);
        if (ammoType != null && ammocheat == 0)
            _inventoryManager.Take(ammoType, _inventoryManager.GetIntProperty(playerstate.weapon!, "weapon.ammouse1", 0));
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
    internal static string? BestWeapon(bool canFire = false) =>
        OwnedWeapons()
            .Where(w => !canFire || CanFire(w))
            .OrderBy(WeaponSelectionOrder)
            .FirstOrDefault();

    // The status bar shows the ammo of the weapon the player picked, even while out of ammo
    // forces a fallback; with an ammo-less weapon picked, the ammo of the best one that has some.
    static string? DisplayAmmoType() =>
        WeaponAmmoType(playerstate.chosenweapon)
        ?? OwnedWeapons().OrderBy(WeaponSelectionOrder).Select(WeaponAmmoType).FirstOrDefault(t => t != null);

    static int GetAmmo() => DisplayAmmoType() is { } type ? _inventoryManager.GetCount(type) : 0;

    static void DrawAmmo()
    {
        if (StatusBarHidden) return;
        // only-with-ammo: nothing while the weapon in hand needs none (Blake Stone's auto charge pistol)
        bool hide = StatusBar.Get("ammo")?.OnlyWithAmmo == true && WeaponAmmoType(playerstate.weapon) == null;
        // item: what to count while the weapon picked uses none (Planet Strike's charge units)
        int ammo = WeaponAmmoType(playerstate.chosenweapon) == null && StatusBar.Get("ammo")?.Item is { Length: > 0 } item
            ? _inventoryManager.GetCount(item)
            : GetAmmo();
        LatchNumber("ammo", ammo, hide);
        DrawAmmoGauge();
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

    // The face grins while grinsound, the pickup sound of the WEAPON.ALWAYSGRIN item that made
    // the player grin, plays
    static bool Grinning => grinsound != null && _audioManager.IsPlaying(grinsound);

    /// <summary>
    /// Starts the grin if the item, or the weapon it gives, is flagged WEAPON.ALWAYSGRIN: the
    /// status bar's face grin shows while the item's pickup sound plays.
    /// </summary>
    static void GrinAtPickup(Inventory item, string? pickupSound)
    {
        const string AlwaysGrin = "WEAPON.ALWAYSGRIN";
        var given = item.Properties.TryGetValue("weapongiver.weapon", out var weapon) ? weapon?.ToString() : null;
        if (string.IsNullOrEmpty(pickupSound)
            || !(item.Flags.Contains(AlwaysGrin, StringComparer.OrdinalIgnoreCase)
                || given != null && _inventoryManager.CreateActor(given)?.Flags.Contains(AlwaysGrin, StringComparer.OrdinalIgnoreCase) == true))
            return;

        grinsound = pickupSound;
        facetimes = 38;     // how long demos hold it, as they can't depend on the sound playing
        facecount = 0;
        DrawFace();
    }

    // The face (statusbar.yaml face): the grin, else the one for the player's health, looking
    // around through its pics, else once dead the one for what killed them
    static void DrawFace()
    {
        if (StatusBarHidden) return;
        if (StatusBar.Get("face") is not { } face) return;

        string? pic;
        if (Grinning && !string.IsNullOrEmpty(face.Grin))
            pic = face.Grin;
        else if (playerstate.health != 0)
        {
            var faces = face.Faces.OrderByDescending(f => f.Health).ToList();
            var pics = (faces.FirstOrDefault(f => playerstate.health >= f.Health) ?? faces.LastOrDefault())?.Pics;
            pic = pics is { Count: > 0 } ? pics[playerstate.faceframe % pics.Count] : null;
        }
        else
            pic = LastAttacker != null && face.KilledBy.TryGetValue(LastAttacker.Name, out var killedBy) ? killedBy : face.Dead;

        if (!string.IsNullOrEmpty(pic))
            StatusDrawFace(pic);
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

    internal static void UpdateFace()
    {
        // don't make demo depend on sound playback, nor a game every player's machine plays alike
        if (demoplayback || demorecord || gamemode != GameMode.Single)
        {
            if (facetimes > 0)
            {
                facetimes--;
                return;
            }
        }
        else if (Grinning)
            return;

        facecount += (int)tics;
        if (facecount > US_RndT())
        {
            playerstate.faceframe = (short)(US_RndT() >> 6);
            if (playerstate.faceframe == 3)
                playerstate.faceframe = 1;

            facecount = 0;
            DrawFace();
        }
    }

    static void DrawHealth()
    {
        if (StatusBarHidden) return;
        LatchNumber("health", playerstate.health);
    }

    /*
    ===============
    =
    = TakeDamage
    =
    ===============
    */

    // LastAttacker feeds Died()'s swing-around-to-face-the-killer (Program.WL_GAME.cs) and the
    // needle-death face in DrawFace. Every attacker -- enemies (Entities.Actors.Monster) and
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
        // With others playing, a dead player lies there until they come back (T_PlayerDead)
        if (gamemode != GameMode.Single && playerstate.health <= 0)
            return;
        // The skill's damage-taken (the easiest skill's 0.25 is vanilla's points >> 2), then the class's
        points = (int)(points * Math.Max(CurrentSkill.DamageTaken, 0) * DamageTakenFactor);

        if (godmode == 0)
        {
            // Armor takes its share of the hit off its points, as far as they go
            int saved = Math.Min(points * playerstate.armorpercent / 100, playerstate.armor);
            playerstate.armor -= (short)saved;
            if (playerstate.armor == 0)
                playerstate.armorpercent = 0;
            points -= saved;

            playerstate.health -= (short)points;
        }

        // How others see it (their body's Pain or Pain1, as an enemy's by its health left)
        if (points > 0 && playerstate.health > 0)
        {
            playerstate.PainTics = 10;
            playerstate.PainAlt = (playerstate.health & 1) == 0;
        }

        if (playerstate.health <= 0)
        {
            playerstate.health = 0;
            if (gamemode == GameMode.Single)
                playstate = playstatetypes.ex_died;     // Died(): the level starts again
            else
                PlayerDies();                           // the rest play on (Program.Players.cs)
        }

        if (godmode != 2 && ActingIsLocal)
            _videoManager.StartDamageFlash(points);

        DrawHealth();
        DrawArmor();
        DrawFace();

        // A heavy hit makes BJ wince (Spear of Destiny); demos rely on the face's count restarting.
        // Vanilla only restarted it with the status bar showing; here it always does, so how a
        // demo plays doesn't depend on the view size.
        if (StatusBar.Get("face") is { Ouch.Length: > 0 } face && points > face.OuchDamage
            && playerstate.health != 0 && godmode == 0 && _assetManager.Exists<Assets.GraphicAsset>(face.Ouch))
        {
            if (viewsize != 21 && ActingIsLocal)
                StatusDrawFace(face.Ouch);
            facecount = 0;
        }
    }

    /// <summary>The most health the player can have: the Player class's `player.maxhealth` (Wolf3D's 100 when left out)</summary>
    internal static short MaxHealth => (short)Math.Clamp(_inventoryManager.GetIntProperty(PlayerClass, "player.maxhealth", 100), 1, short.MaxValue);

    /// <summary>The most lives the player can have: the Player class's `player.maxlives` (Wolf3D's 9 when left out)</summary>
    internal static short MaxLives => (short)Math.Clamp(_inventoryManager.GetIntProperty(PlayerClass, "player.maxlives", 9), 0, short.MaxValue);

    /// <summary>The most armor points the player can have: the Player class's `player.maxarmor` (no cap but the armor's own when left out)</summary>
    internal static short MaxArmor => (short)Math.Clamp(_inventoryManager.GetIntProperty(PlayerClass, "player.maxarmor", short.MaxValue), 0, short.MaxValue);

    /// <summary>
    /// Puts on a BasicArmor class (native.yaml): an armor replaces the player's when it has more
    /// points; a bonus (armor.maxsaveamount) adds to it. False when it would change nothing.
    /// </summary>
    internal static bool TryGiveArmor(string armorClass)
    {
        int amount = _inventoryManager.GetIntProperty(armorClass, "armor.saveamount", 0);
        short percent = (short)Math.Clamp(_inventoryManager.GetIntProperty(armorClass, "armor.savepercent", 0), 0, 100);

        if (_inventoryManager.GetProperty(armorClass, "armor.maxsaveamount") != null)
        {
            int cap = Math.Min(_inventoryManager.GetIntProperty(armorClass, "armor.maxsaveamount", 0), MaxArmor);
            if (playerstate.armor >= cap || amount <= 0)
                return false;
            if (playerstate.armor == 0)
                playerstate.armorpercent = percent;
            playerstate.armor = (short)Math.Min(playerstate.armor + amount, cap);
        }
        else
        {
            amount = Math.Min(amount, MaxArmor);
            if (playerstate.armor >= amount)
                return false;
            playerstate.armor = (short)amount;
            playerstate.armorpercent = percent;
        }

        DrawArmor();
        return true;
    }

    // The status bar's optional armor number (statusbar.yaml `armor`)
    static void DrawArmor()
    {
        if (StatusBarHidden) return;
        LatchNumber("armor", playerstate.armor);
    }

    /// <summary>Heals the player by <paramref name="points"/>, up to <paramref name="upTo"/> (their max health when null)</summary>
    internal static void HealSelf(int points, int? upTo = null)
    {
        int cap = Math.Min(upTo ?? MaxHealth, MaxHealth);
        playerstate.health = (short)Math.Min(playerstate.health + points, Math.Max(cap, playerstate.health));

        DrawHealth();
        DrawFace();
    }

    static void DrawKeys()
    {
        if (StatusBarHidden) return;
        if (StatusBar.Get("keys") is not { } layout) return;
        // Each key with a "key.statusbarslot" (0 at the top) has its own spot on the status bar:
        // its "key.statusbarpic" while carried, else "key.statusbaremptypic". Carried keys are
        // drawn last, so one wins a slot it shares with a key that isn't.
        var keys = _inventoryManager.GetClassesDerivedFrom("Key")
            .Select(key => (Key: key, Slot: _inventoryManager.GetIntProperty(key, "key.statusbarslot", -1), Has: _inventoryManager.Has(key)))
            .Where(key => key.Slot >= 0)
            .OrderBy(key => key.Has);
        using var _ = StatusBarOrigin(layout);
        foreach (var (key, slot, has) in keys)
        {
            int x = layout.X + layout.SpacingX * slot, y = layout.Y + layout.Spacing * slot;

            // size: a colored square for each key ("key.statusbarcolor" / "key.statusbaremptycolor")
            if (layout.Size.Count == 2)
            {
                var color = _inventoryManager.GetStringProperty(key, has ? "key.statusbarcolor" : "key.statusbaremptycolor");
                if (!string.IsNullOrEmpty(color))
                    _videoManager.Bar(x, StatusBarTop(layout) + y, layout.Size[0], layout.Size[1], color);
                continue;
            }

            var pic = _inventoryManager.GetStringProperty(key, has ? "key.statusbarpic" : "key.statusbaremptypic");
            if (!string.IsNullOrEmpty(pic))
                _graphicManager.DrawPic(pic, x, StatusBarTop(layout) + y);
        }
    }

    static void DrawLevel()
    {
        var gameInfo = _gameEngineManager.GetGameInfo();
        var mapInfo = gameInfo.Maps[gamestate.mapon];
        //var mapInfo = MapInfoMappings.GameInfo.Maps[gamestate.mapon];
        if (StatusBarHidden) return;
        LatchNumber("level", mapInfo.FloorNumber);
    }

    static void DrawLives()
    {
        if (StatusBarHidden) return;
        LatchNumber("lives", playerstate.lives);
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
        if (playerstate.lives < MaxLives)
            playerstate.lives++;
        DrawLives();
        if (StatusBar.Get("lives")?.Sound is { Length: > 0 } sound)
            PlayPlayerSound(sound);
    }

    static void DrawScore()
    {
        if (StatusBarHidden) return;
        // a deathmatch keeps score in frags
        LatchNumber("score", gamemode == GameMode.Deathmatch ? playerstate.Frags : playerstate.score);
    }

    /*
    ===============
    =
    = GivePoints
    =
    ===============
    */

    /// <summary>The score between extra lives (game-info extra-life-score); 0 for none</summary>
    internal static int ExtraLifeScore => Math.Max(_gameEngineManager.GetGameInfo().ExtraLifeScore, 0);

    internal static void GivePoints(int points)
    {
        playerstate.score += points;
        while (ExtraLifeScore > 0 && playerstate.score >= playerstate.nextextra)
        {
            playerstate.nextextra += ExtraLifeScore;
            GiveExtraMan();
        }
        DrawScore();
    }

    // The weapon in hand's picture, and what goes with it: its ammo (drawn over it, for a layout
    // that puts it there) and its charge
    static void DrawWeapon()
    {
        if (StatusBarHidden) return;
        DrawWeaponPic();
        if (StatusBar.Get("ammo")?.Behind?.Equals("weapon", StringComparison.OrdinalIgnoreCase) == true
            || StatusBar.Get("ammo")?.OnlyWithAmmo == true || StatusBar.Get("ammo-gauge") != null)
            DrawAmmo();
        DrawCharge();
    }

    static void DrawWeaponPic()
    {
        if (StatusBarHidden) return;
        if (playerstate.weapon == null) return;

        var icon = _inventoryManager.GetStringProperty(playerstate.weapon, "inventory.icon");
        if (!string.IsNullOrEmpty(icon) && StatusBar.Get("weapon") is { } element)
        {
            using var _ = StatusBarOrigin(element);
            _graphicManager.DrawPic(icon, element.X, StatusBarTop(element) + element.Y);
        }
        DrawWeaponCorner();
    }

    // Planet Strike's corner under the weapon's picture, with the ammo on it (statusbar.yaml
    // weapon-corner): its picture for the weapon in hand
    static void DrawWeaponCorner()
    {
        if (StatusBarHidden) return;
        if (playerstate.weapon == null || StatusBar.Get("weapon-corner") is not { } element) return;
        var pic = element.WeaponPics.GetValueOrDefault(playerstate.weapon);
        using var _ = StatusBarOrigin(element);
        if (!string.IsNullOrEmpty(pic))
            _graphicManager.DrawPic(pic, element.X, StatusBarTop(element) + element.Y);
    }
/*
==================
=
= GiveWeapon
=
==================
*/

    // The base player class (actordefs/wolf3d/player.yaml); every player class is it or descends from it
    private const string BasePlayerClass = "Player";

    // The class being played as, for its `player.*` properties (inherited from Player where it
    // leaves them out)
    private static string PlayerClass => playerstate.playerclass;

    /// <summary>The classes a new game can be played as: game-info's player-classes, else just Player</summary>
    internal static List<string> PlayerClasses()
    {
        var classes = _gameEngineManager.GetGameInfo().PlayerClasses.Keys
            .Select(name => (Name: name, Class: FindPlayerClass(name)))
            .Where(c =>
            {
                if (c.Class == null)
                    Console.WriteLine($"game-info player-classes: '{c.Name}' isn't Player or an actordefs class with Player as a parent");
                return c.Class != null;
            })
            .Select(c => c.Class!)
            .ToList();
        return classes.Count > 0 ? classes : [BasePlayerClass];
    }

    /// <summary>The class a new game is played as when none is picked: the first in game-info's player-classes</summary>
    internal static string DefaultPlayerClass => PlayerClasses()[0];

    /// <summary>A player class's exact actordefs spelling, or null if the name isn't Player or a class descended from it</summary>
    internal static string? FindPlayerClass(string name) =>
        string.Equals(name, BasePlayerClass, StringComparison.OrdinalIgnoreCase)
            ? BasePlayerClass
            : _inventoryManager.FindClass(name, BasePlayerClass);

    /// <summary>A factor from the player class (e.g. `player.damagedealt`), 1 when left out; never below 0</summary>
    private static double PlayerFactor(string key) =>
        _inventoryManager.GetProperty(PlayerClass, key) is { } value
        && double.TryParse(value.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var factor)
            ? Math.Max(factor, 0)
            : 1;

    /// <summary>How much of the damage their weapons would do the player deals: the class's `player.damagedealt`</summary>
    internal static double DamageDealt => PlayerFactor("player.damagedealt");

    /// <summary>How much of the damage they're dealt the player takes, before the skill's: the class's `player.damagetaken`</summary>
    internal static double DamageTakenFactor => PlayerFactor("player.damagetaken");

    /// <summary>A new game's and a respawn's health: the Player class's `player.starthealth` (Wolf3D's 100 when left out)</summary>
    internal static short StartingHealth => (short)Math.Clamp(_inventoryManager.GetIntProperty(PlayerClass, "player.starthealth", 100), 1, short.MaxValue);

    /// <summary>A new game's lives: the Player class's `player.startlives` (Wolf3D's 3 when left out)</summary>
    internal static short StartingLives => (short)Math.Clamp(_inventoryManager.GetIntProperty(PlayerClass, "player.startlives", 3), 0, short.MaxValue);

    /// <summary>
    /// New game / respawn loadout: the Player class's `player.startitem` (item: amount), with
    /// the best weapon in it selected.
    /// </summary>
    internal static void GiveStartingInventory()
    {
        _inventoryManager.Clear();
        playerstate.armor = playerstate.armorpercent = 0;

        if (_inventoryManager.GetProperty(PlayerClass, "player.startitem") is IDictionary<object, object> items)
        {
            foreach (var (item, amount) in items)
            {
                // Armor isn't held: it's put on (the amount doesn't matter)
                if (_inventoryManager.FindClass(item.ToString() ?? "", "BasicArmor") is { } armor)
                    TryGiveArmor(armor);
                else
                    _inventoryManager.Give(item.ToString() ?? "", Convert.ToInt32(amount));
            }
        }
        else
            Console.WriteLine($"No player.startitem on actordefs class {PlayerClass}: the player starts with nothing.");

        playerstate.weapon = playerstate.chosenweapon = BestWeapon();
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
            playerstate.weapon = playerstate.chosenweapon = newBest;

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
        var activation = new Entities.TriggerActivation(tilex, tiley, dir, player, _mapManager.GetTag(tilex, tiley));
        if (!Entities.MapTriggerRegistry.Invoke(trigger.Action, activation))
            return;

        if (trigger.Secret)
            gamestate.secretcount++;
        _mapManager.SetMapSpot(tilex, tiley, 1, 0);
    }

    /// <summary>
    /// Throws a switch wall: refused without its lock item, else it turns into its `to` wall,
    /// plays its sound and runs its actions on whatever shares its tile's tag.
    /// </summary>
    private static void UseSwitch(MapSwitchTranslation wallSwitch, int tilex, int tiley, controldirs dir)
    {
        if (!string.IsNullOrEmpty(wallSwitch.Lock) && !_inventoryManager.Has(wallSwitch.Lock))
        {
            RefuseLocked(wallSwitch.Lock, wallSwitch.LockedSound, wallSwitch.LockMessage, wallSwitch.LockMessageStyle, isSwitch: true);
            return;
        }

        if (wallSwitch.To is > 0 and < BIT_WALL && _mapManager.GetMapData().Walls.ContainsKey(wallSwitch.To))
        {
            // flip the switch, keeping the door-side mark on a wall beside a door
            var tile = _mapManager.tilemap[tilex, tiley];
            _mapManager.tilemap[tilex, tiley] = (byte)(wallSwitch.To | (tile & BIT_WALL));
        }

        if (!string.IsNullOrEmpty(wallSwitch.Sound))
            PlayPlayerSound(wallSwitch.Sound);

        var activation = new Entities.TriggerActivation(tilex, tiley, dir, player, _mapManager.GetTag(tilex, tiley));
        foreach (var action in wallSwitch.Actions)
            Entities.MapTriggerRegistry.Invoke(action, activation);
    }

    /// <summary>
    /// A_Exit ends the level, going to the secret level when the player stands on the floors'
    /// secret-exit code (as the elevator always did); A_SecretExit always goes to the secret
    /// level. Either waits for a sound that's playing (the switch's) to finish first.
    /// </summary>
    private static bool ExitAction(bool secret)
    {
        if (secret || _mapManager.MAPSPOT(player.TileX, player.TileY, 0) == _mapManager.Floors.SecretExitTile)
            playstate = playstatetypes.ex_secretlevel;
        else
            playstate = playstatetypes.ex_completed;

        _audioManager.WaitSoundDone();
        return true;
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

        //
        // find which cardinal direction the player is facing
        //
        if (player.Angle < ANGLES / 8 || player.Angle > 7 * ANGLES / 8)
        {
            checkx = player.TileX + 1;
            checky = player.TileY;
            dir = controldirs.di_east;
        }
        else if (player.Angle < 3 * ANGLES / 8)
        {
            checkx = player.TileX;
            checky = player.TileY - 1;
            dir = controldirs.di_north;
        }
        else if (player.Angle < 5 * ANGLES / 8)
        {
            checkx = player.TileX - 1;
            checky = player.TileY;
            dir = controldirs.di_west;
        }
        else
        {
            checkx = player.TileX;
            checky = player.TileY + 1;
            dir = controldirs.di_south;
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
        // A wall beside a door keeps its id under BIT_WALL; a moving pushwall's tiles are bare BIT_WALL
        if (!playerinput.IsHeld(buttontypes.bt_use) && (cmdtile & BIT_DOOR) == 0
            && _mapManager.GetMapData().Walls.TryGetValue(cmdtile & ~BIT_WALL, out var switchWall)
            && switchWall.Switch is { } wallSwitch && wallSwitch.UsableFrom(dir))
        {
            //
            // use a switch (a mapdefs wall with a switch, such as the elevator's)
            //
            playerinput.SetHeld(buttontypes.bt_use, true);
            UseSwitch(wallSwitch, checkx, checky, dir);
        }
        else if (!playerinput.IsHeld(buttontypes.bt_use) && (cmdtile & BIT_DOOR) != 0)
        {
            playerinput.SetHeld(buttontypes.bt_use, true);
            OperateDoor(cmdtile & ~BIT_DOOR);
        }
        else
        {
            // Nothing ahead to use: talk to whoever's there (BlakeMonster.TalkTo)
            _mapManager.AI.TryTalk();
        }
    }

    //===========================================================================

    /// <summary>Whether the weapon in hand is a silent one (weapon.silent): its shots don't alert anyone</summary>
    internal static bool PlayerWeaponIsSilent() =>
        playerstate.weapon != null && _inventoryManager.GetProperty(playerstate.weapon, "weapon.silent") is { } silent
        && (silent is true || bool.TryParse(silent.ToString(), out var b) && b);


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
            if (!victoryRunning)                // A_VictoryRun's runner goes the way the player faces
                VictorySpin();
            return;
        }

        UpdateFace();
        UpdateHeartMonitor();
        UpdateRadar();

        if (IsWeaponReady())
        {
            CheckWeaponChange();

            if (playerinput.IsPressed(buttontypes.bt_use))
                Cmd_Use();
            else
                _mapManager.AI.ResetTalkDelay();
        }
        else
        {
            // Mid-attack, a fresh press of use or fire is dropped rather than kept for later
            // (vanilla T_Attack); fire held down from before still counts for A_ReFire.
            if (playerinput.IsFreshPress(buttontypes.bt_use))
                playerinput.SetPressed(buttontypes.bt_use, false);

            if (playerinput.IsFreshPress(buttontypes.bt_attack))
                playerinput.SetPressed(buttontypes.bt_attack, false);
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

    /// <summary>Something a player's shot could hit, and how far in front of them it is (TransX)</summary>
    private readonly record struct ShotTarget(Entities.Actors.Actor Actor, int Depth);

    // What the acting player's shot could hit, nearest first. Alone, as vanilla: the enemies
    // (Entities.Actors.Monster) the renderer last drew near the middle of the view -- the player
    // pawn, projectiles and the BJ-victory actor share _actors with the enemies but have no
    // "Chase" state, so they never qualify. With others playing, ShootCandidatesFrom.
    private static List<ShotTarget> FindShootCandidates()
    {
        if (gamemode != GameMode.Single)
            return ShootCandidatesFrom(player);

        var candidates = new List<Entities.Actors.Monster>();

        foreach (var actor in _mapManager.GetActors().OfType<Entities.Actors.Monster>())
        {
            if (actor == null || !actor.ResolvedStates.ContainsKey("Chase")) continue;
            if (actor.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE) && actor.RuntimeFlags.HasFlag(objflags.FL_VISABLE)
                && Math.Abs(actor.ViewX - centerx) < shootdelta)
            {
                candidates.Add(actor);
            }
        }

        candidates.Sort((a, b) => a.TransX.CompareTo(b.TransX));
        return candidates.Select(c => new ShotTarget(c, c.TransX)).ToList();
    }

    /// <summary>
    /// What a player's shot could hit, worked out from where they stand rather than from what
    /// any screen shows (another player's machine draws another view, and a game played on
    /// several machines has to come out the same on each): the living enemies -- and, in
    /// deathmatch, the other living players -- within the shot's angle either side of straight
    /// ahead (the renderer's shootdelta, which comes to the same angle however wide the view),
    /// with a clear line to them. Nearest first.
    /// </summary>
    private static List<ShotTarget> ShootCandidatesFrom(Entities.Actors.PlayerPawn shooter)
    {
        var (fromx, fromy, fromsin, fromcos) = PlayerViewOrigin();
        var candidates = new List<ShotTarget>();

        foreach (var actor in _mapManager.GetActors())
        {
            bool enemy = actor is Entities.Actors.Monster && !actor.IsRemoved && actor.ResolvedStates.ContainsKey("Chase")
                && actor.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE);
            bool rival = gamemode == GameMode.Deathmatch && actor is Entities.Actors.PlayerPawn pawn
                && pawn != shooter && pawn.State.health > 0;
            if (!enemy && !rival)
                continue;

            // As TransformActorFrom: how far in front (nx) and to the side (ny)
            int gx = actor.X - fromx, gy = actor.Y - fromy;
            int nx = MathUtils.FixedMul(gx, fromcos) - MathUtils.FixedMul(gy, fromsin) - ACTORSIZE;
            int ny = MathUtils.FixedMul(gy, fromcos) + MathUtils.FixedMul(gx, fromsin);
            if (nx < MINDIST)
                continue;

            // |ny / nx| * scale < shootdelta, with the view's width taken out of both
            if (Math.Abs((long)ny) * 5 * (FOCALLENGTH + MINDIST) >= (long)nx * (VIEWGLOBAL / 2))
                continue;

            if (!CheckLine(actor, shooter))
                continue;

            candidates.Add(new ShotTarget(actor, nx));
        }

        return candidates.OrderBy(c => c.Depth).ToList();
    }

    /// <summary>A player's shot or blow lands on an enemy, or (deathmatch) another player</summary>
    static void HitShotTarget(Entities.Actors.Actor target, uint damage)
    {
        if (target is Entities.Actors.PlayerPawn pawn)
            TakeDamage(pawn, (int)damage, player);
        else if (target is Entities.Actors.Monster monster)
            monster.Damage(damage, player);
    }

    // The weapon in hand's `attacksound`.
    static void PlayAttackSound()
    {
        if (playerstate.weapon != null && _inventoryManager.GetStringProperty(playerstate.weapon, "attacksound") is { Length: > 0 } sound)
            PlayPlayerSound(sound);
    }

    internal static void KnifeAttack(Entities.Actors.Actor ob)
    {
        PlayAttackSound();

        var closest = FindShootCandidates().FirstOrDefault(c => c.Depth <= 0x18000L).Actor;
        if (closest == null)
            return; // missed

        HitShotTarget(closest, PlayerDamage(US_RndT() >> 4));
    }

    // A player attack's damage, scaled by the class's player.damagedealt
    static uint PlayerDamage(int damage) => (uint)Math.Round(damage * DamageDealt);

    // near, mid, far: what a random 0-255 is divided by for a hit within 2 tiles, within 4, and
    // beyond (where it can also miss); Wolf3D's are 4, 6, 6
    internal static void GunAttack(Entities.Actors.Actor ob, int near = 4, int mid = 6, int far = 6)
    {
        int damage;
        int dx, dy, dist;

        PlayAttackSound();

        // A silent weapon (weapon.silent, Blake Stone's auto-charge pistol) alerts no one
        if (!PlayerWeaponIsSilent())
            madenoise = true;

        //
        // find the closest potential target, and confirm a clear line to it
        // (the legacy version of this re-scanned in a loop, but since nothing marks a
        // failed candidate as excluded, re-scanning always finds the same actor again --
        // it only ever gives the single closest actor one shot at passing CheckLine)
        //
        var closest = FindShootCandidates().FirstOrDefault().Actor;
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
            damage = US_RndT() / near;
        else if (dist < 4)
            damage = US_RndT() / mid;
        else
        {
            if ((US_RndT() / 12) < dist)           // missed
                return;
            damage = US_RndT() / far;
        }

        HitShotTarget(closest, PlayerDamage(damage));
    }

    internal static void VictorySpin()
    {
        int desty;
        playerpitch = 0;            // watch BJ straight on
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
        player.AreaNumber = _mapManager.SpawnArea(tilex, tiley);
        player.SetState(PlayerPawn.SpawnState);
        player.Angle = (short)((angle % ANGLES + ANGLES) % ANGLES);
        player.RuntimeFlags = objflags.FL_NEVERMARK;
        Thrust(0, 0);                           // set some variables

        InitAreas();
    }
}
