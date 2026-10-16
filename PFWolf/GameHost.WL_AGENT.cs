using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Enums;
using PFWolf.Entities.Actors;
using PFWolf.Managers;

namespace PFWolf;

internal static partial class GameHost
{
    /*
    =============================================================================

                                    LOCAL CONSTANTS

    =============================================================================
    */

    internal const int MAXMOUSETURN   = 10;

    internal const long MOVESCALE = GameConstants.MOVESCALE;
    internal const long BACKMOVESCALE = GameConstants.BACKMOVESCALE;
    internal const int ANGLESCALE = GameConstants.ANGLESCALE;

    /*
    =============================================================================

                                    GLOBAL VARIABLES

    =============================================================================
    */

    //
    // player state info: thrustspeed, plux/pluy, anglefrac, LastAttacker and the rest are the
    // acting player's (GameHost.Players.cs)
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

    internal static void ClipMove(Entities.Actors.Actor ob, int xmove, int ymove) => playerMovement.ClipMove(ob, xmove, ymove);
    static controldirs FacingDir(int angle) => PlayerMovement.FacingDir(angle);

    // Picking items up (Gameplay/Pickups.cs)
    static Pickups pickups = null!;     // made in InitServices

    static Pickups MakePickups() =>
        new(_mapManager, _videoManager, _hudMessageManager, playerClassRules, playerStats, session,
            new PickupHooks(() => playstate == playstatetypes.ex_died, PlayPlayerSound, HasWeaponFrom,
                () => netrules.ItemRespawn, QueueItemRespawn));

    internal static void GetBonus(Inventory builtActor) => pickups.GetBonus(builtActor);
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
    internal static List<string> PlayerClasses() => playerClassRules.PlayerClasses();
    internal static string DefaultPlayerClass => playerClassRules.DefaultPlayerClass;
    private static double PlayerFactor(string key) => playerClassRules.PlayerFactor(key);
    internal static double DamageDealt => playerClassRules.DamageDealt;

    internal static void TakeDamage(int points, Entities.Actors.Actor attacker) => playerStats.TakeDamage(points, attacker);
    internal static void GivePoints(int points) => playerStats.GivePoints(points);
    internal static bool CouldTakeInventory(Inventory item) => playerStats.CouldTakeInventory(item);
    internal static bool TryApplyInventory(Inventory item, int amount) => playerStats.TryApplyInventory(item, amount);

    /// <summary>
    /// Registers the C# handlers for the actor-state `action:` names authored in actordefs
    /// YAML. Called once at startup (see Main). Names with no handler here (e.g. the weapon
    /// Ready/Fire actions) are simply logged and skipped by ActorActionRegistry when hit.
    /// </summary>
    internal static void RegisterActorActions()
    {
        (playerActions ??= MakePlayerActions()).Register();     // A_GiveExtraMan, A_GiveInventory, A_ChangeMap

        // Enemies' thinks and actions (Entities.Actors.Monster, BlakeMonster)
        Entities.Actors.Monster.RegisterActions();
        Entities.Actors.BlakeMonster.RegisterActions();

        // Any actor's sounds, and the end of the game (GameHost.WL_ACT2.cs)
        ActorActionRegistry.Register("A_DeathScream", A_DeathScream);
        ActorActionRegistry.Register("A_ActiveSound", A_ActiveSound);
        ActorActionRegistry.Register("A_PlaySound", A_PlaySound);
        ActorActionRegistry.Register("A_Victory", A_Victory);
        ActorActionRegistry.Register("A_StartDeathCam", A_StartDeathCam);
        ActorActionRegistry.Register("A_Quake", A_Quake);       // the view shakes (GameHost.WL_DRAW.cs)
        // A_MakeNoise: a noise, as a shot's: enemies in the areas joined to the player's come
        // (Midnight Assault's lights coming on: madenoise)
        ActorActionRegistry.Register("A_MakeNoise", (Entities.Actors.Actor _) => madenoise = true);

        // Level objectives (Gameplay/Objectives.cs): A_CompleteObjective("name"[, n]) and
        // A_AddObjective("name"[, n]), from an actor or a switch; A_JumpIfObjectiveDone("name", "State")
        static int Amount(string[] args) => args.Length > 1 && int.TryParse(args[1], out var n) ? n : 1;
        ActorActionRegistry.Register("A_CompleteObjective", (Entities.Actors.Actor _, string[] args) => { if (args.Length > 0) objectives.Complete(args[0], Amount(args)); });
        ActorActionRegistry.Register("A_AddObjective", (Entities.Actors.Actor _, string[] args) => { if (args.Length > 0) objectives.Add(args[0], Amount(args)); });
        ActorActionRegistry.Register("A_JumpIfObjectiveDone", (Entities.Actors.Actor ob, string[] args) => objectives.A_JumpIfObjectiveDone(ob, args));
        Entities.MapTriggerRegistry.Register("A_CompleteObjective", (_, args) => { if (args.Length == 0) return false; objectives.Complete(args[0], Amount(args)); return true; });
        Entities.MapTriggerRegistry.Register("A_AddObjective", (_, args) => { if (args.Length == 0) return false; objectives.Add(args[0], Amount(args)); return true; });
        ActorActionRegistry.Register("A_Mount", (Entities.Actors.Actor ob, string[] args) => mountedWeapons.A_Mount(ob, args));
        ActorActionRegistry.RegisterFor<Entities.Actors.Monster>("A_LatchBite", (m, args) => latching.A_LatchBite(m, args));
        ActorActionRegistry.Register("A_LatchHurt", (Entities.Actors.Actor ob) => latching.A_LatchHurt(ob));
        ActorActionRegistry.Register("A_ShakeOff", (Entities.Actors.Actor ob, string[] args) => latching.A_ShakeOff(ob, args));
        ActorActionRegistry.Register("A_JumpIfLatched", (Entities.Actors.Actor ob, string[] args) => latching.A_JumpIfLatched(ob, args));
        ActorActionRegistry.Register("A_LatchRelease", (Entities.Actors.Actor ob) => latching.A_LatchRelease(ob));
        ActorActionRegistry.Register("A_ShowPapers", (Entities.Actors.Actor ob) => stealth.A_ShowPapers(ob));
        ActorActionRegistry.Register("A_Disguise", (Entities.Actors.Actor ob, string[] args) => stealth.A_Disguise(ob, args));

        // Goldfire's warp sites (Managers.LevelAI)
        ActorActionRegistry.Register("A_WarpSiteGone", (Entities.Actors.Actor _) => levelAI.WarpSiteGone());
        ActorActionRegistry.Register("A_WarpSitesOff", (Entities.Actors.Actor _) => levelAI.WarpSitesOff());

        // Projectiles and effects (GameHost.WL_ACT2.cs).
        ActorActionRegistry.Register("A_Projectile", A_Projectile);
        ActorActionRegistry.Register("A_SpawnThing", A_SpawnThing);
        ActorActionRegistry.Register("A_Remove", A_Remove);

        // BJ victory cutscene (GameHost.WL_ACT2.cs): BJ walks the patrol arrows as a Monster.
        ActorActionRegistry.RegisterFor<Entities.Actors.Monster>("T_BJRun", T_BJRun);
        ActorActionRegistry.RegisterFor<Entities.Actors.Monster>("T_BJJump", T_BJJump);
        ActorActionRegistry.Register("T_BJDone", T_BJDone);

        // The player's own think states (PlayerPawn), ticked by MapManager.DoActor.
        ActorActionRegistry.Register("T_Player", T_Player);
        ActorActionRegistry.Register("T_DeathCam", T_DeathCam);
        ActorActionRegistry.Register("T_PlayerDead", T_PlayerDead);

        // The weapon in hand's states (GameHost.PlayerWeapon.cs).
        RegisterWeaponActions();

        // mapdefs trigger and switch actions, run when the player uses a trigger's tile or a
        // switch wall (Cmd_Use) or steps onto a walk-over trigger (Thrust)
        // A_PushWall("moving sound", "blocked sound"[, "direction", "message"]): either sound left
        // out is silent. With a direction (north, east, south or west) it only moves that way:
        // pushed from another side, it shows the message and plays the blocked sound instead
        // (Midnight Assault's one-way pushwalls)
        Entities.MapTriggerRegistry.Register("A_PushWall", PushWallAction);
        Entities.MapTriggerRegistry.Register("A_VictoryTile", (_, _) => { VictoryTile(); return true; });
        Entities.MapTriggerRegistry.Register("A_Exit", (_, _) => ExitAction(secret: false));
        Entities.MapTriggerRegistry.Register("A_SecretExit", (_, _) => ExitAction(secret: true));
        // Hub clusters' ways between levels, the teleporter's floors and the victory run (Gameplay/HubActions.cs)
        (hubActions ??= MakeHubActions()).Register();
        RegisterSwitchActions();    // tag-targeted door and wall actions (Gameplay/SwitchActions.cs)
        RegisterSmartSwitchActions();   // A_SmartSwitch (Gameplay/SmartSwitches.cs)
        RegisterZoneLightActions(); // light zone actions, for switches and actors (Rendering/Lighting.cs)
        RegisterActorLightActions(); // actors' own lights (Rendering/ActorLights.cs)
        RegisterProjectileActions(); // projectiles and jumps for any actor, traps (GameHost.ProjectileActions.cs)
    }

    /// <summary>A_PushWall: the trigger's wall pushed the way the player faces, unless it only goes another way (see RegisterActorActions)</summary>
    static bool PushWallAction(Entities.TriggerActivation trigger, string[] args)
    {
        var moveSound = args.ElementAtOrDefault(0);
        var blockedSound = args.ElementAtOrDefault(1);
        var only = args.ElementAtOrDefault(2)?.Trim();
        if (!string.IsNullOrEmpty(only))
        {
            if (Gameplay.SwitchActions.ParseDirection(only) is not { } onlyDir)
            {
                Console.WriteLine($"A_PushWall: unknown direction '{only}' (north, east, south or west).");
                return false;
            }
            if (trigger.Dir != onlyDir)
            {
                if (args.ElementAtOrDefault(3) is { Length: > 0 } message)
                    _hudMessageManager.Show(HudMessageKind.Lock, message);
                if (!string.IsNullOrEmpty(blockedSound))
                    _audioManager.Play(blockedSound);
                return false;
            }
        }
        return PushWall(trigger.TileX, trigger.TileY, trigger.Dir, moveSound, blockedSound);
    }

    // A_GiveExtraMan, A_GiveInventory and A_ChangeMap (Gameplay/PlayerActions.cs)
    static PlayerActions playerActions = null!;     // made in InitServices (or by RegisterActorActions without it)

    static PlayerActions MakePlayerActions() => new(_gameEngineManager, _assetManager, playerStats, session,
        new PlayerActionsHooks(change => pendingMapChange = change, s => playstate = s));

    // The weapons as inventory: picking, ammo, giving, the charge (Gameplay/PlayerWeapons.cs)
    static PlayerWeapons playerWeapons = null!;     // made in InitServices

    static PlayerWeapons MakePlayerWeapons() =>
        new(_inventoryManager, _graphicManager, statusBarDrawing, session, new PlayerWeaponsHooks(() => ammocheat != 0, UseSlotItem));

    internal static int WeaponSelectionOrder(string weapon) => playerWeapons.WeaponSelectionOrder(weapon);
    internal static string? BestWeapon(bool canFire = false) => playerWeapons.BestWeapon(canFire);
    private static bool TryGiveWeapon(Entities.Actors.Weapon pickup) => playerWeapons.TryGiveWeapon(pickup);
    private static bool TryGiveWeapon(string weaponName, int ammoGive) => playerWeapons.TryGiveWeapon(weaponName, ammoGive);

    // The use key, switches, mapdefs triggers, the exits and held items' use (Gameplay/PlayerUse.cs)
    static PlayerUse playerUse = null!;     // made in InitServices

    static PlayerUse MakePlayerUse() =>
        new(_mapManager, _audioManager, _inventoryManager, doors, levelAI, timedSwitches, session,
            new PlayerUseHooks(PlayPlayerSound, state => playstate = state, () => mountedWeapons.TryDismount(),
                (objective, amount) => objectives.Complete(objective, amount), () => objectives.ExitAllowed()));

    private static void ActivateTrigger(MapTriggerTranslation trigger, int tilex, int tiley, controldirs dir) => playerUse.ActivateTrigger(trigger, tilex, tiley, dir);
    private static void UseSwitch(MapSwitchTranslation wallSwitch, int tilex, int tiley, controldirs dir) => playerUse.UseSwitch(wallSwitch, tilex, tiley, dir);
    private static bool ExitAction(bool secret) => playerUse.ExitAction(secret);
    static void UseSlotItem(int slot) => playerUse.UseSlotItem(slot);

    // The player's think each tic, and putting them at a start (Gameplay/PlayerThink.cs)
    static PlayerThink playerThink = null!;     // made in InitServices

    static PlayerThink MakePlayerThink() =>
        new(_mapManager, statusBarDrawing, playerStats, playerWeapons, weaponInHand, playerUse, playerMovement, weaponBob, levelAI, session,
            new PlayerThinkHooks(UpdateRadar, () => victoryRunning, InitAreas, reloading =>
            {
                weaponZoom.Tick(reloading);
                mountedWeapons.Tick();          // let off a gun they're no longer manning (dying, a load, a new level)
                latching.Tick();                // and out of a struggle that's over
                stealth.Tick();                 // the suspicion meter
            }));

    internal static void T_Player(Entities.Actors.Actor ob) => playerThink.T_Player(ob);

    // A weapon's scope (Gameplay/WeaponZoom.cs): the zoom key, the view's magnification and the scope's picture
    static WeaponZoom? _weaponZoom;
    static WeaponZoom weaponZoom => _weaponZoom ??= new WeaponZoom(_inventoryManager, _audioManager, session);

    // Mounted guns the player mans (Gameplay/MountedWeapons.cs): A_Mount, and getting off with use
    static MountedWeapons? _mountedWeapons;
    static MountedWeapons mountedWeapons => _mountedWeapons ??= new MountedWeapons(_mapManager, _inventoryManager, playerWeapons, session,
        new MountedWeaponsHooks(PlayPlayerSound, message => _hudMessageManager.Show(HudMessageKind.Lock, message), () => playerMovement.Thrust(0, 0)));

    // Things that latch onto the player (Gameplay/Latching.cs): A_LatchBite, and striking them off
    static Latching? _latching;
    static Latching latching => _latching ??= new Latching(_mapManager, _inventoryManager, playerWeapons, session,
        new LatchingHooks(playersManager.TakeDamage, playerAttacks.PlayAttackSound, weaponInHand.IsWeaponReady,
            (monster, damage, by) => monster.Damage((uint)damage, by)));

    // Switches that turn back by themselves (Gameplay/TimedSwitches.cs), ticked by the level loop
    static TimedSwitches? _timedSwitches;
    static TimedSwitches timedSwitches => _timedSwitches ??= new TimedSwitches(_mapManager, session);

    // Floors that hurt (Gameplay/FloorDamage.cs), ticked by the level loop
    static FloorDamage? _floorDamage;
    static FloorDamage floorDamage => _floorDamage ??= new FloorDamage(_mapManager, session, new FloorDamageHooks(
        (pawn, points) =>
        {
            using var _ = session.ActAs(pawn.State);
            playerStats.ApplyDamageToPlayer(points);
        },
        (monster, points) => monster.Damage((uint)points),
        () => _mapManager.GetMapData().Flats,
        (x, y) => _mapManager.FlatTextureName(x, y, ceiling: false)));

    // Disguise and going unseen (Gameplay/Stealth.cs): enemies' suspicion, papers, the uniform
    static Stealth? _stealth;
    static Stealth stealth => _stealth ??= new Stealth(_mapManager, _inventoryManager, playerWeapons, session,
        new StealthHooks(
            () => _gameEngineManager.GetGameInfo().Stealth,
            () => session.gamestate.mapon is { } map && _gameEngineManager.GetGameInfo().Maps.TryGetValue(map, out var info) && info.StealthOnly,
            message => _hudMessageManager.Show(HudMessageKind.Lock, message),
            PlayPlayerSound,
            playerAttacks.PlayAttackSound,
            actor => (actor.AreaNumber >= _mapManager.Floors.NumAreas || session.areas.areabyplayer[actor.AreaNumber] != 0)
                && lineOfSight.CheckLine(actor),
            actor =>
            {
                // Caught on a stealth-only map: the mission's failed, as a death at their hands
                session.playerstate.LastAttacker = actor;
                session.playerstate.health = 0;
                statusBarDrawing.DrawHealth();
                if (session.gamemode == GameMode.Single)
                    playstate = playstatetypes.ex_died;
                else
                    PlayerDies();
            }));
}
