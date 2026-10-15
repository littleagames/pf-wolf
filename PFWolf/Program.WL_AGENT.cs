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

    // The acting player's movement (Gameplay/PlayerMovement.cs)
    static PlayerMovement playerMovement = null!;   // made in InitServices

    static PlayerMovement MakePlayerMovement() =>
        new(_mapManager, _audioManager, pushWalls, session, new PlayerMovementHooks(
            PlayerFactor, MaxPitch, () => demorecord || demoplayback, () => noclip != 0, PlayPlayerSound,
            (xl, yl, xh, yh, x, y, size) => WallSpriteHitByBox(xl, yl, xh, yh, x, y, size, player: true),
            (x, y) => (wallspritefromx, wallspritefromy) = (x, y),
            ActivateTrigger));

    internal static void ControlMovement(Entities.Actors.Actor ob) => playerMovement.ControlMovement(ob);
    internal static void Thrust(int angle, int speed) => playerMovement.Thrust(angle, speed);
    internal static bool TryMove(Entities.Actors.Actor ob) => playerMovement.TryMove(ob);
    internal static void ClipMove(Entities.Actors.Actor ob, int xmove, int ymove) => playerMovement.ClipMove(ob, xmove, ymove);
    static controldirs FacingDir(int angle) => PlayerMovement.FacingDir(angle);

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

    // The player classes' rules (Gameplay/PlayerClassRules.cs) and the acting player's health,
    // armor, lives, score and loadout (Gameplay/PlayerStats.cs)
    static PlayerClassRules playerClassRules = null!;   // made in InitServices
    static PlayerStats playerStats = null!;

    static PlayerStats MakePlayerStats() =>
        new(_gameEngineManager, _assetManager, _videoManager, _inventoryManager, statusBarDrawing, playerClassRules, playerWeapons, session,
            new PlayerStatsHooks(() => godmode, () => demoplayback || demorecord, () => viewsize == 21,
                () => CurrentSkill.DamageTaken, () => playstate = playstatetypes.ex_died, PlayerDies, PlayPlayerSound));

    private const string BasePlayerClass = PlayerClassRules.BasePlayerClass;
    private static string PlayerClass => playerstate.playerclass;
    internal static bool PlayerClassCanPickUp(string item) => playerClassRules.PlayerClassCanPickUp(item);
    internal static List<string> PlayerClasses() => playerClassRules.PlayerClasses();
    internal static string DefaultPlayerClass => playerClassRules.DefaultPlayerClass;
    internal static string? FindPlayerClass(string name) => playerClassRules.FindPlayerClass(name);
    private static double PlayerFactor(string key) => playerClassRules.PlayerFactor(key);
    internal static double DamageDealt => playerClassRules.DamageDealt;
    internal static short StartingHealth => playerClassRules.StartingHealth;
    internal static short StartingLives => playerClassRules.StartingLives;
    internal static short MaxHealth => playerClassRules.MaxHealth;
    internal static int StandingEyeZ(Entities.PlayerState p) => playerClassRules.StandingEyeZ(p);

    static void GrinAtPickup(Inventory item, string? pickupSound) => playerStats.GrinAtPickup(item, pickupSound);
    internal static void UpdateFace() => playerStats.UpdateFace();
    internal static void TakeDamage(int points, Entities.Actors.Actor attacker) => playerStats.TakeDamage(points, attacker);
    internal static bool TryGiveArmor(string armorClass) => playerStats.TryGiveArmor(armorClass);
    internal static void HealSelf(int points, int? upTo = null) => playerStats.HealSelf(points, upTo);
    static void GiveExtraMan() => playerStats.GiveExtraMan();
    internal static int ExtraLifeScore => playerStats.ExtraLifeScore;
    internal static void GivePoints(int points) => playerStats.GivePoints(points);
    internal static void GiveStartingInventory() => playerStats.GiveStartingInventory();
    internal static bool CouldTakeInventory(Inventory item) => playerStats.CouldTakeInventory(item);
    internal static bool TryApplyInventory(Inventory item, int amount) => playerStats.TryApplyInventory(item, amount);
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

    // The use key, switches, mapdefs triggers, the exits and held items' use (Gameplay/PlayerUse.cs)
    static PlayerUse playerUse = null!;     // made in InitServices

    static PlayerUse MakePlayerUse() =>
        new(_mapManager, _audioManager, _inventoryManager, doors, session,
            new PlayerUseHooks(PlayPlayerSound, state => playstate = state));

    private static void ActivateTrigger(MapTriggerTranslation trigger, int tilex, int tiley, controldirs dir) => playerUse.ActivateTrigger(trigger, tilex, tiley, dir);
    private static void UseSwitch(MapSwitchTranslation wallSwitch, int tilex, int tiley, controldirs dir) => playerUse.UseSwitch(wallSwitch, tilex, tiley, dir);
    private static bool ExitAction(bool secret) => playerUse.ExitAction(secret);
    internal static void Cmd_Use() => playerUse.Cmd_Use();
    static void UseSlotItem(int slot) => playerUse.UseSlotItem(slot);
    static void ResetUseHeld() => playerUse.ResetUseHeld();
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
