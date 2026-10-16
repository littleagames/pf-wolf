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

        // Goldfire's warp sites (Managers.LevelAI)
        ActorActionRegistry.Register("A_WarpSiteGone", (Entities.Actors.Actor _) => _mapManager.AI.WarpSiteGone());
        ActorActionRegistry.Register("A_WarpSitesOff", (Entities.Actors.Actor _) => _mapManager.AI.WarpSitesOff());

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
        // A_PushWall("moving sound", "blocked sound"): either left out is silent
        Entities.MapTriggerRegistry.Register("A_PushWall", (trigger, args) => PushWall(trigger.TileX, trigger.TileY, trigger.Dir,
            args.ElementAtOrDefault(0), args.ElementAtOrDefault(1)));
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
        new(_mapManager, _audioManager, _inventoryManager, doors, session,
            new PlayerUseHooks(PlayPlayerSound, state => playstate = state));

    private static void ActivateTrigger(MapTriggerTranslation trigger, int tilex, int tiley, controldirs dir) => playerUse.ActivateTrigger(trigger, tilex, tiley, dir);
    private static void UseSwitch(MapSwitchTranslation wallSwitch, int tilex, int tiley, controldirs dir) => playerUse.UseSwitch(wallSwitch, tilex, tiley, dir);
    private static bool ExitAction(bool secret) => playerUse.ExitAction(secret);
    static void UseSlotItem(int slot) => playerUse.UseSlotItem(slot);

    // The player's think each tic, and putting them at a start (Gameplay/PlayerThink.cs)
    static PlayerThink playerThink = null!;     // made in InitServices

    static PlayerThink MakePlayerThink() =>
        new(_mapManager, statusBarDrawing, playerStats, playerWeapons, weaponInHand, playerUse, playerMovement, weaponBob, session,
            new PlayerThinkHooks(UpdateRadar, () => victoryRunning, InitAreas));

    internal static void T_Player(Entities.Actors.Actor ob) => playerThink.T_Player(ob);
}
