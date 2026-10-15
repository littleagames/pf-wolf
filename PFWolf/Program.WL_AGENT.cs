using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Enums;
using PFWolf.Entities.Actors;
using PFWolf.Managers;

namespace PFWolf;

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

    // The diagonal shape the player last ran into in TryMove, for ClipMove to slide along
    static WallShape diagonalblock;

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
                    else if (_mapManager.tilemap[x, y] == BIT_WALL
                        && pushwalls.Find(p => p.X == x && p.Y == y) is { } pw)    // back of a moving pushwall?
                    {
                        switch (pw.Dir)
                        {
                            case controldirs.di_north:
                                if (ob.Y - PUSHWALLMINDIST <= (pw.Y << (int)MapConstants.TILESHIFT) + ((63 - pw.Pos) << 10))
                                    return false;
                                break;
                            case controldirs.di_west:
                                if (ob.X - PUSHWALLMINDIST <= (pw.X << (int)MapConstants.TILESHIFT) + ((63 - pw.Pos) << 10))
                                    return false;
                                break;
                            case controldirs.di_east:
                                if (ob.X + PUSHWALLMINDIST >= (pw.X << (int)MapConstants.TILESHIFT) + (pw.Pos << 10))
                                    return false;
                                break;
                            case controldirs.di_south:
                                if (ob.Y + PUSHWALLMINDIST >= (pw.Y << (int)MapConstants.TILESHIFT) + (pw.Pos << 10))
                                    return false;
                                break;
                        }
                    }
                    else if (_mapManager.wallshape[x, y] is var shape and not WallShape.Square)
                    {
                        //
                        // a diagonal only blocks on its solid side of the face
                        //
                        if (Diagonals.BoxHitsDiagonal(shape, (int)x, (int)y, ob.X, ob.Y, PLAYERSIZE))
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

                // An ally following the player gets out of their way
                if (ob is Entities.Actors.PlayerPawn pawn)
                    actor.ShovedBy(pawn);
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
    /// `inventory.forbiddento`, or the item has an `inventory.restrictedto` it isn't in, or it's a
    /// weapon or ammo outside the class's `player.weapons`. A class counts as in a list that names
    /// it or a class it descends from.
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
        if (!PlayerClassUsesItem(item))
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
        RegisterProjectileActions(); // projectiles and jumps for any actor, traps (Program.ProjectileActions.cs)
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

    // The weapons as inventory: picking, ammo, giving, the charge (Gameplay/PlayerWeapons.cs)
    static PlayerWeapons playerWeapons = null!;     // made in InitServices

    static PlayerWeapons MakePlayerWeapons() =>
        new(_inventoryManager, _graphicManager, statusBarDrawing, session, new PlayerWeaponsHooks(() => ammocheat != 0, UseSlotItem));

    internal static void CheckWeaponChange() => playerWeapons.CheckWeaponChange();
    internal static int WeaponSelectionOrder(string weapon) => playerWeapons.WeaponSelectionOrder(weapon);
    static bool CanFire(string? weapon) => playerWeapons.CanFire(weapon);
    static void UseAmmo() => playerWeapons.UseAmmo();
    internal static string? BestWeapon(bool canFire = false) => playerWeapons.BestWeapon(canFire);
    static void DrawAmmo() => playerWeapons.DrawAmmo();
    internal static int GiveAllAmmo(int amount) => playerWeapons.GiveAllAmmo(amount);
    internal static int GiveAmmo(string ammoType, int ammo) => playerWeapons.GiveAmmo(ammoType, ammo);
    static void DrawWeapon() => playerWeapons.DrawWeapon();
    internal static List<string> AllWeapons() => playerWeapons.AllWeapons();
    internal static void GiveWeapon(string weapon) => playerWeapons.GiveWeapon(weapon);
    private static bool TryGiveWeapon(Entities.Actors.Weapon pickup) => playerWeapons.TryGiveWeapon(pickup);
    private static bool TryGiveWeapon(string weaponName, int ammoGive) => playerWeapons.TryGiveWeapon(weaponName, ammoGive);
    static bool TakeCharge() => playerWeapons.TakeCharge();
    static void TickWeaponCharge() => playerWeapons.TickWeaponCharge();
    static void DrawCharge() => playerWeapons.DrawCharge();

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

    /// <summary>Heals the player by <paramref name="points"/>, up to <paramref name="upTo"/> (their max health when null)</summary>
    internal static void HealSelf(int points, int? upTo = null)
    {
        int cap = Math.Min(upTo ?? MaxHealth, MaxHealth);
        playerstate.health = (short)Math.Min(playerstate.health + points, Math.Max(cap, playerstate.health));

        DrawHealth();
        DrawFace();
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

    /// <summary>How high a player's eyes are standing, in texels: their class's `player.eyeheight` (EYEDEFAULT when left out)</summary>
    internal static int StandingEyeZ(Entities.PlayerState p) =>
        Math.Clamp(_inventoryManager.GetIntProperty(p.playerclass, "player.eyeheight", EYEDEFAULT), MINEYE, MAXEYE);

    /// <summary>
    /// Whether the class may pick up a weapon or ammo: with a `player.weapons` list, only the
    /// weapons in it (or descended from them) and the ammo they use. Anything else, and every
    /// item for a class without the list, may be.
    /// </summary>
    static bool PlayerClassUsesItem(string item)
    {
        if (_inventoryManager.GetProperty(PlayerClass, "player.weapons") is not { } property)
            return true;
        string[] weapons = property switch
        {
            IEnumerable<object> list => list.Select(w => w.ToString() ?? "").ToArray(),
            _ => [property.ToString() ?? ""],
        };

        if (_inventoryManager.FindClass(item, "Weapon") != null)
            return _inventoryManager.FindClass(item, weapons) != null;
        if (_inventoryManager.FindClass(item, "Ammo") != null)
            return weapons.Any(w => _inventoryManager.GetIntProperty(w, "weapon.ammouse1", 0) > 0     // the knife's Clip isn't used
                && _inventoryManager.GetStringProperty(w, "weapon.ammotype1") is { Length: > 0 } ammo
                && (_inventoryManager.FindClass(item, ammo) != null || _inventoryManager.GetItemType(ammo) == _inventoryManager.GetItemType(item)));
        return true;
    }

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
    /// plays its sound and runs its actions on whatever shares its tile's tag. The other switches
    /// with its tag that are either way of the same switch flip with it (as Blake Stone's
    /// barrier switches do), without running their actions.
    /// </summary>
    private static void UseSwitch(MapSwitchTranslation wallSwitch, int tilex, int tiley, controldirs dir)
    {
        if (RefuseSwitchLock(wallSwitch))
            return;

        var tag = _mapManager.GetTag(tilex, tiley);
        if (wallSwitch.To is > 0 and < BIT_WALL && _mapManager.GetMapData().Walls.ContainsKey(wallSwitch.To))
        {
            // flip the switch, keeping the door-side mark on a wall beside a door
            var tile = _mapManager.tilemap[tilex, tiley];
            int from = tile & ~BIT_WALL;
            _mapManager.tilemap[tilex, tiley] = (byte)(wallSwitch.To | (tile & BIT_WALL));
            _mapManager.RestartAnimations(tilex, tiley, gamestate.TimeCount);   // a `to` wall's play-once animation starts now

            // and the linked ones: set to the new way, not toggled, so any out of step come back
            foreach (var (x, y) in _mapManager.TaggedTiles(tag))
            {
                var other = _mapManager.tilemap[x, y];
                int id = other & ~BIT_WALL;
                if ((other & BIT_DOOR) == 0 && (id == from || id == wallSwitch.To))
                {
                    _mapManager.tilemap[x, y] = (byte)(wallSwitch.To | (other & BIT_WALL));
                    _mapManager.RestartAnimations(x, y, gamestate.TimeCount);
                }
            }
        }

        if (!string.IsNullOrEmpty(wallSwitch.Sound))
            PlayPlayerSound(wallSwitch.Sound);

        var activation = new Entities.TriggerActivation(tilex, tiley, dir, player, tag);
        foreach (var action in wallSwitch.Actions)
            Entities.MapTriggerRegistry.Invoke(action, activation);
    }

    // Refuses a switch the player lacks the lock item for: true if it did
    private static bool RefuseSwitchLock(MapSwitchTranslation sw)
    {
        if (string.IsNullOrEmpty(sw.Lock) || _inventoryManager.Has(sw.Lock))
            return false;
        RefuseLocked(sw.Lock, sw.LockedSound, sw.LockMessage, sw.LockMessageStyle, isSwitch: true);
        return true;
    }

    /// <summary>
    /// The switch of the patch on the wall tile (tilex, tiley) the player faces going dir, if
    /// it's on the face they press (facing east presses the west face) and usable from there
    /// </summary>
    private static MapSwitchTranslation? PatchSwitchAt(int tilex, int tiley, controldirs dir)
    {
        int tile = _mapManager.tilemap[tilex, tiley];
        if ((tile & BIT_DOOR) != 0 || (tile & ~BIT_WALL) == 0)
            return null;                // open floor, a door or a moving pushwall
        int id = _mapManager.GetPatch(tilex, tiley);
        if (id == 0 || !_mapManager.GetMapData().Patches.TryGetValue(id, out var patch) || patch.Switch is not { } patchSwitch)
            return null;

        var face = dir switch
        {
            controldirs.di_east => "west",
            controldirs.di_west => "east",
            controldirs.di_north => "south",
            _ => "north",
        };
        return patch.IsOn(face) && patchSwitch.UsableFrom(dir) ? patchSwitch : null;
    }

    /// <summary>
    /// Throws a patch's switch (patches.yaml): refused without its lock item, else it plays its
    /// sound and runs its actions on whatever shares the tile's tag, as a wall's switch does.
    /// Then, unless all its actions came to nothing (an item the player has no room for, say),
    /// the patch turns into its `to` patch, or comes off with `remove`; and so do the other
    /// patches with the tag that are either way of the same switch, so linked ones stay in step.
    /// </summary>
    private static void UsePatchSwitch(MapSwitchTranslation patchSwitch, int tilex, int tiley, controldirs dir)
    {
        if (RefuseSwitchLock(patchSwitch))
            return;

        if (!string.IsNullOrEmpty(patchSwitch.Sound))
            PlayPlayerSound(patchSwitch.Sound);

        var tag = _mapManager.GetTag(tilex, tiley);
        var activation = new Entities.TriggerActivation(tilex, tiley, dir, player, tag);
        bool wentOff = patchSwitch.Actions.Count == 0;
        foreach (var action in patchSwitch.Actions)
            wentOff |= Entities.MapTriggerRegistry.Invoke(action, activation);
        if (!wentOff)
            return;

        int from = _mapManager.GetPatch(tilex, tiley);
        int to = patchSwitch.Remove ? 0
            : patchSwitch.To is > 0 and <= 255 && _mapManager.GetMapData().Patches.ContainsKey(patchSwitch.To) ? patchSwitch.To
            : from;
        if (to == from)
            return;

        void Flip(int x, int y)
        {
            _mapManager.SetPatch(x, y, (byte)to);
            _mapManager.RestartAnimations(x, y, gamestate.TimeCount);    // a `to` patch's play-once animation starts now
        }

        Flip(tilex, tiley);
        foreach (var (x, y) in _mapManager.TaggedTiles(tag))
        {
            int other = _mapManager.GetPatch(x, y);
            if (other != 0 && (other == from || other == to))
                Flip(x, y);
        }
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
            ResetUseHeld();
            ActivateTrigger(trigger, checkx, checky, dir);
            return;
        }
        if (!playerinput.IsHeld(buttontypes.bt_use) && PatchSwitchAt(checkx, checky, dir) is { } patchSwitch)
        {
            //
            // use a patch that's a switch (patches.yaml), over the wall's own
            //
            playerinput.SetHeld(buttontypes.bt_use, true);
            UsePatchSwitch(patchSwitch, checkx, checky, dir);
        }
        // A wall beside a door keeps its id under BIT_WALL; a moving pushwall's tiles are bare BIT_WALL
        else if (!playerinput.IsHeld(buttontypes.bt_use) && (cmdtile & BIT_DOOR) == 0
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
            // Nothing ahead to use: use an actor that's there to be used (its Use state), else
            // talk to whoever's there (BlakeMonster.TalkTo), else held long enough, use an item
            // that's used that way (Planet Strike's fission detonator)
            if (TryUseActor() || _mapManager.AI.TryTalk())
                ResetUseHeld();
            else
                UseHeldItem();
            return;
        }
        ResetUseHeld();
    }

    /// <summary>
    /// The use key pressed facing a living actor with a Use state (actordefs), within a tile and
    /// a half and half a right angle either side of straight ahead: it goes to that state (a
    /// prisoner's could be A_SetFaction("allies", "follow")). Not one going after someone.
    /// </summary>
    static bool TryUseActor()
    {
        if (playerinput.IsHeld(buttontypes.bt_use))
            return false;

        const long Reach = MapConstants.TILEGLOBAL * 3 / 2;
        Entities.Actors.Monster? chosen = null;
        long best = Reach;
        foreach (var actor in _mapManager.GetActors().OfType<Entities.Actors.Monster>())
        {
            if (actor.IsRemoved || !actor.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE)
                || actor.RuntimeFlags.HasFlag(objflags.FL_ATTACKMODE) || !actor.ResolvedStates.ContainsKey("Use"))
                continue;

            long dx = actor.X - player.X, dy = player.Y - actor.Y;     // y grows southward
            long dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (dist >= best)
                continue;

            int toward = (int)((Math.Atan2(dy, dx) + Math.PI * 2) % (Math.PI * 2) / (Math.PI * 2) * ANGLES);
            int off = Math.Abs(player.Angle - toward) % ANGLES;
            if (Math.Min(off, ANGLES - off) > ANGLES / 8)
                continue;

            (chosen, best) = (actor, dist);
        }

        if (chosen == null)
            return false;
        playerinput.SetHeld(buttontypes.bt_use, true);
        chosen.SetState("Use");
        return true;
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
            {
                _mapManager.AI.ResetTalkDelay();
                ResetUseHeld();
            }
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
        if (ActingIsLocal)                      // only the local player's view bobs
            weaponBob.Update(playerinput.ControlY, tics);
        if (gamestate.victoryflag)              // watching the BJ actor
            return;

        plux = (ushort)(player.X >> UNSIGNEDSHIFT);                     // scale to fit in unsigned
        pluy = (ushort)(player.Y >> UNSIGNEDSHIFT);
        player.TileX = (byte)(player.X >> (int)MapConstants.TILESHIFT);                // scale to tile values
        player.TileY = (byte)(player.Y >> (int)MapConstants.TILESHIFT);

        // The weapon's Ready/Fire states (Program.PlayerWeapon.cs): starts and plays attacks
        TickWeapon();
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
