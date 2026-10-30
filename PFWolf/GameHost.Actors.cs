using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Enums;
using PFWolf.Extensions;
using PFWolf.Managers;
using System.Data;

namespace PFWolf;

// GameHost's player and actor wiring: the player, weapons, players, doors and switches, enemies, projectiles
internal static partial class GameHost
{
    // ==== WL_AGENT ===============================================================================

    /*
    =============================================================================

                                    LOCAL CONSTANTS

    =============================================================================
    */

    /*
    =============================================================================

                                    GLOBAL VARIABLES

    =============================================================================
    */

    //
    // player state info: thrustspeed, plux/pluy, anglefrac, LastAttacker and the rest are the
    // acting player's (Gameplay/Players.cs)
    //

    /*
    =============================================================================

                                                     LOCAL VARIABLES

    =============================================================================
    */

    // The acting player's movement (Gameplay/PlayerMovement.cs)
    static PlayerMovement playerMovement = null!;   // made in InitServices

    static PlayerMovement MakePlayerMovement() =>
        new(MapManager, AudioManager, pushWalls, Session, new PlayerMovementHooks(
            PlayerFactor, MaxPitch, PlayPlayerSound,
            (xl, yl, xh, yh, x, y, size) => WallSpriteHitByBox(xl, yl, xh, yh, x, y, size, player: true),
            (x, y) => (WallSpriteFromX, WallSpriteFromY) = (x, y),
            ActivateTrigger, (controlY, tics) => Vehicles.Throttle(controlY, tics), () => Vehicles.Stalled()));

    static ControlDir FacingDir(int angle) => PlayerMovement.FacingDir(angle);

    // Picking items up (Gameplay/Pickups.cs)
    static Pickups pickups = null!;     // made in InitServices

    static Pickups MakePickups() =>
        new(MapManager, _videoManager, HudMessageManager, playerClassRules, playerStats, Session,
            new PickupHooks(PlayPlayerSound, HasWeaponFrom,
                () => NetRules.ItemRespawn, QueueItemRespawn, item => deathmatch.Objective?.ItemTaken(item),
                item => gameScripts.RunTryPickupHooks(Session.Player, item),
                (item, amount) => gameScripts.RunPickupHooks(Session.Player, item, amount)));

    internal static void GetBonus(Inventory builtActor) => pickups.GetBonus(builtActor);
    // The player classes' rules (Gameplay/PlayerClassRules.cs) and the acting player's health,
    // armor, lives, score and loadout (Gameplay/PlayerStats.cs)
    static PlayerClassRules playerClassRules = null!;   // made in InitServices
    static PlayerStats playerStats = null!;

    static PlayerStats MakePlayerStats() =>
        new(GameEngineManager, AssetManager, _videoManager, InventoryManager, statusBarDrawing, playerClassRules, playerWeapons, Session,
            Settings, new PlayerStatsHooks(() => CurrentSkill.DamageTaken, PlayerDies, PlayPlayerSound, HudMessageManager.ShowKeyed, ShowAttacker,
                (points, attacker, type) => gameScripts.RunDamageHooks(Session.Player, points, attacker, type),
                killer => gameScripts.RunPlayerDeathHooks(Session.Player, killer),
                (item, amount) => gameScripts.RunGiveHooks(Session.Player, item, amount),
                (powerup, started) => gameScripts.RunPowerupHooks(Session.Player, powerup, started)));

    // The attacker's attackinfo, else game-info attack-message; %k the attacker (Blake Stone's
    // REBA "attacking:", which has its picture: an info area script, Ui/TextPresenter.cs)
    static void ShowAttacker(Entities.Actors.Actor attacker)
    {
        var message = attacker.PropertyStrings("attackinfo").FirstOrDefault() is { Length: > 0 } own ? own
            : GameEngineManager.GetGameInfo().AttackMessage;
        if (!string.IsNullOrEmpty(message))
            HudMessageManager.Show(HudMessageKind.Other, message, null,
                new Dictionary<char, string> { ['k'] = hudMessageDrawing.ActorTag(attacker) });
    }

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

        // Any actor's sounds, and the end of the game (Gameplay/ActorSounds.cs)
        ActorActionRegistry.Register("A_DeathScream", DeathScream);
        ActorActionRegistry.Register("A_ActiveSound", ActiveSound);
        ActorActionRegistry.Register("A_PlaySound", PlaySound);
        // A_Taunt("a", "b", ...): one of the sounds at random, as the acting player's (heard from
        // where they stand by everyone else: a Prop Hunt prop's fire, actordefs prophunt.yaml)
        ActorActionRegistry.Register("A_Taunt", (Entities.Actors.Actor _, string[] sounds) =>
        {
            if (sounds.Length > 0)
                PlayPlayerSound(sounds[Session.Random.RndT() % sounds.Length]);
        });
        ActorActionRegistry.Register("A_Victory", Victory);
        ActorActionRegistry.Register("A_StartDeathCam", StartDeathCam);
        ActorActionRegistry.Register("A_Quake", Quake);       // the view shakes (A_Quake, in GameHost.Rendering.cs)
        // A_MakeNoise: a noise, as a shot's: enemies in the areas joined to the player's come
        // (Midnight Assault's lights coming on: MadeNoise)
        ActorActionRegistry.Register("A_MakeNoise", (Entities.Actors.Actor _) => MadeNoise = true);
        // A_PlayerFire("off" | "on"): the player's weapon can't fire, or can again (Goldfire
        // morphing: bstone's noShots); a level starts with it on
        ActorActionRegistry.Register("A_PlayerFire", (Entities.Actors.Actor _, string[] args) =>
            Session.NoShots = args.ElementAtOrDefault(0)?.Trim().Equals("off", StringComparison.OrdinalIgnoreCase) == true);
        // A_BlastDoors(["Class"]): the doors on and around the actor's tile that are at most half
        // open, unlocked and not one-way are blown open for good (bstone's BlastNearDoors, Planet
        // Strike's fission detonator), with one of the class put in each (its explosion)
        ActorActionRegistry.Register("A_BlastDoors", (Entities.Actors.Actor ob, string[] args) =>
        {
            var effect = args.ElementAtOrDefault(0)?.Trim();
            for (int x = ob.TileX - 1; x <= ob.TileX + 1; x++)
                for (int y = ob.TileY - 1; y <= ob.TileY + 1; y++)
                {
                    if (!MapManager.InMap(x, y) || (MapManager.TileMap[x, y] & GameConstants.BitDoor) == 0)
                        continue;
                    int door = MapManager.TileMap[x, y] & ~GameConstants.BitDoor;
                    if (door < doors.LastDoorObj && doors.BlastDoor(door) && !string.IsNullOrEmpty(effect))
                        MapManager.SpawnThing(x, y, new Assets.MapActorTranslation { Class = effect }, countKill: false);
                }
        });

        // Level objectives (Gameplay/Objectives.cs): A_CompleteObjective("name"[, n]) and
        // A_AddObjective("name"[, n]), from an actor or a switch; A_JumpIfObjectiveDone("name", "State")
        static int Amount(string[] args) => args.Length > 1 && int.TryParse(args[1], out var n) ? n : 1;
        ActorActionRegistry.Register("A_CompleteObjective", (Entities.Actors.Actor _, string[] args) => { if (args.Length > 0) Objectives.Complete(args[0], Amount(args)); });
        ActorActionRegistry.Register("A_AddObjective", (Entities.Actors.Actor _, string[] args) => { if (args.Length > 0) Objectives.Add(args[0], Amount(args)); });
        ActorActionRegistry.Register("A_JumpIfObjectiveDone", (Entities.Actors.Actor ob, string[] args) => Objectives.JumpIfObjectiveDone(ob, args));
        Entities.MapTriggerRegistry.Register("A_CompleteObjective", (_, args) => { if (args.Length == 0) return false; Objectives.Complete(args[0], Amount(args)); return true; });
        Entities.MapTriggerRegistry.Register("A_AddObjective", (_, args) => { if (args.Length == 0) return false; Objectives.Add(args[0], Amount(args)); return true; });
        // A_Unlock("name"[, "message"]): unlocks a name for good (Gameplay/Unlocks.cs: a starts-locked
        // episode's key, or level's name), showing the message if it wasn't already. An actor's
        // fills in its {tag} and counts (Gameplay/ActorText.cs): a numbered dog tag's "DT0{tag}"
        static void UnlockWithMessage(string[] args, Func<string, string> expand)
        {
            if (args.Length > 0 && Unlock(expand(args[0])) && args.Length > 1 && args[1].Length > 0)
                HudMessageManager.Show(HudMessageKind.Lock, expand(HudMessageManager.Localize(args[1])));
        }
        ActorActionRegistry.Register("A_Unlock", (Entities.Actors.Actor ob, string[] args) =>
            UnlockWithMessage(args, text => ActorText.Expand(text, ob, InventoryManager)));
        Entities.MapTriggerRegistry.Register("A_Unlock", (_, args) => { if (args.Length == 0) return false; UnlockWithMessage(args, text => text); return true; });
        // A_JumpIfUnlocked("name", "State"): goes to the state if the name's unlocked ({tag} and
        // counts filled in): a dog tag found before, greyed out
        ActorActionRegistry.Register("A_JumpIfUnlocked", (Entities.Actors.Actor ob, string[] args) =>
        {
            if (args.Length >= 2 && Unlocks.IsUnlocked(ActorText.Expand(args[0], ob, InventoryManager))
                && ob.ResolvedStates.TryGetValue(args[1].Trim(), out var frame))
                ob.JumpTo(frame);
        });
        // A_ShowText("name"): a text shown as an article in the middle of the level, then back to
        // it (Midnight Assault's log books and radios); {tag} in the name is the actor's tag, so
        // each one placed can show its own page ("LOG{tag}")
        ActorActionRegistry.Register("A_ShowText", (Entities.Actors.Actor ob, string[] args) =>
        {
            if (args.Length > 0 && args[0].Length > 0)
                ShowTextInLevel(ActorText.Expand(args[0], ob, InventoryManager));
        });
        ActorActionRegistry.Register("A_Mount",(Entities.Actors.Actor ob, string[] args) => MountedWeapons.Mount(ob, args));
        ActorActionRegistry.Register("A_Board", (Entities.Actors.Actor ob, string[] args) => Vehicles.Board(ob, args));     // Gameplay/Vehicles.cs
        // A_Cinematic(tics): every player watches a cutscene that long: they turn but don't walk, with no
        // weapon in hand and black bars top and bottom (Midnight Assault's inCinematic, the Commander's retreats)
        ActorActionRegistry.Register("A_Cinematic", (Entities.Actors.Actor _, string[] args) =>
        {
            if (int.TryParse(args.ElementAtOrDefault(0), out var tics) && tics > 0)
                foreach (var p in PlayerStates)
                    p.CinematicTics = Math.Max(p.CinematicTics, tics);
        });
        // A_Replace("Class"): becomes one of another class where it stands, facing the same way
        // (Midnight Assault's gates: a lock clipped off, a gate opened)
        ActorActionRegistry.Register("A_Replace", (Entities.Actors.Actor ob, string[] args) =>
        {
            if (args.Length == 0 || MapManager.ReplaceActor(ob, args[0].Trim()) == null)
                Console.WriteLine($"A_Replace ({ob.Name}): there's no actordefs class \"{args.ElementAtOrDefault(0)}\".");
        });
        // A_JumpIfInventory("Item", "State"): goes to the state if the acting player holds the item
        // (a locked gate the wire clippers open)
        ActorActionRegistry.Register("A_JumpIfInventory", (Entities.Actors.Actor ob, string[] args) =>
        {
            if (args.Length >= 2 && InventoryManager.Has(args[0].Trim()) && ob.ResolvedStates.TryGetValue(args[1].Trim(), out var frame))
                ob.JumpTo(frame);
        });
        ActorActionRegistry.RegisterFor<Entities.Actors.Monster>("A_LatchBite", (m, args) => Latching.LatchBite(m, args));
        ActorActionRegistry.Register("A_LatchHurt", (Entities.Actors.Actor ob) => Latching.LatchHurt(ob));
        ActorActionRegistry.Register("A_ShakeOff", (Entities.Actors.Actor ob, string[] args) => Latching.ShakeOff(ob, args));
        ActorActionRegistry.Register("A_JumpIfLatched", (Entities.Actors.Actor ob, string[] args) => Latching.JumpIfLatched(ob, args));
        ActorActionRegistry.Register("A_LatchRelease", (Entities.Actors.Actor ob) => Latching.LatchRelease(ob));
        ActorActionRegistry.Register("A_ShowPapers", (Entities.Actors.Actor ob) => Stealth.ShowPapers(ob));
        ActorActionRegistry.Register("A_Disguise", (Entities.Actors.Actor ob, string[] args) => Stealth.Disguise(ob, args));

        // Goldfire's warp sites (Managers.LevelAI)
        ActorActionRegistry.Register("A_WarpSiteGone", (Entities.Actors.Actor _) => LevelAI.WarpSiteGone());
        ActorActionRegistry.Register("A_WarpSitesOff", (Entities.Actors.Actor _) => LevelAI.WarpSitesOff());

        // Projectiles and effects (Gameplay/Projectiles.cs).
        ActorActionRegistry.Register("A_Projectile", Projectile);
        ActorActionRegistry.Register("A_SpawnThing", SpawnThing);
        ActorActionRegistry.Register("A_Remove", Remove);

        // BJ victory cutscene (Gameplay/BJVictory.cs): BJ walks the patrol arrows as a Monster.
        ActorActionRegistry.RegisterFor<Entities.Actors.Monster>("T_BJRun", ThinkBjRun);
        ActorActionRegistry.RegisterFor<Entities.Actors.Monster>("T_BJJump", ThinkBjJump);
        ActorActionRegistry.Register("T_BJDone", ThinkBjDone);

        // The player's own think states (PlayerPawn), ticked by MapManager.DoActor.
        ActorActionRegistry.Register("T_Player", ThinkPlayer);
        ActorActionRegistry.Register("T_DeathCam", ThinkDeathCam);
        ActorActionRegistry.Register("T_PlayerDead", ThinkPlayerDead);

        // The weapon in hand's states (Gameplay/WeaponInHand.cs).
        RegisterWeaponActions();

        // mapdefs trigger and switch actions, run when the player uses a trigger's tile or a
        // switch wall (PressUse) or steps onto a walk-over trigger (Thrust)
        // A_PushWall("moving sound", "blocked sound"[, "direction", "message"]): either sound left
        // out is silent. With a direction (north, east, south or west) it only moves that way:
        // pushed from another side, it shows the message and plays the blocked sound instead
        // (Midnight Assault's one-way MovingWalls)
        Entities.MapTriggerRegistry.Register("A_PushWall", PushWallAction);
        // A_VictoryTile("BJVictory"): that class runs for the exit, then the level ends (Gameplay/BJVictory.cs)
        Entities.MapTriggerRegistry.Register("A_VictoryTile", (_, args) => { VictoryTile(args.ElementAtOrDefault(0)); return true; });
        Entities.MapTriggerRegistry.Register("A_Exit", (_, _) => ExitAction(secret: false));
        Entities.MapTriggerRegistry.Register("A_SecretExit", (_, _) => ExitAction(secret: true));
        // Hub clusters' ways between levels, the teleporter's floors and the victory run (Gameplay/HubActions.cs)
        (hubActions ??= MakeHubActions()).Register();
        RegisterSwitchActions();    // tag-targeted door and wall actions (Gameplay/SwitchActions.cs)
        RegisterSmartSwitchActions();   // A_SmartSwitch (Gameplay/SmartSwitches.cs)
        RegisterDestructibleWallActions();  // A_RemoveWall, for switches and actors (Gameplay/DestructibleWalls.cs)
        RegisterZoneLightActions(); // light zone actions, for switches and actors (Rendering/Lighting.cs)
        Scrollers.RegisterActions(() => MapManager.Scrollers); // scrolling walls and flats, for switches and actors (Rendering/Scrollers.cs)
        RegisterActorLightActions(); // actors' own lights (Rendering/ActorLights.cs)
        RegisterProjectileActions(); // projectiles and jumps for any actor, traps (Gameplay/ProjectileActions.cs)
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
                    HudMessageManager.Show(HudMessageKind.Lock, message);
                if (!string.IsNullOrEmpty(blockedSound))
                    AudioManager.Play(blockedSound);
                return false;
            }
        }
        return PushWall(trigger.TileX, trigger.TileY, trigger.Dir, moveSound, blockedSound);
    }

    // A_GiveExtraMan, A_GiveInventory and A_ChangeMap (Gameplay/PlayerActions.cs)
    static PlayerActions playerActions = null!;     // made in InitServices (or by RegisterActorActions without it)

    static PlayerActions MakePlayerActions() => new(GameEngineManager, AssetManager, playerStats, Session,
        new PlayerActionsHooks(change => PendingMapChange = change, () => AudioManager?.WaitSoundDone()));

    // The weapons as inventory: picking, ammo, giving, the charge (Gameplay/PlayerWeapons.cs)
    static PlayerWeapons playerWeapons = null!;     // made in InitServices

    static PlayerWeapons MakePlayerWeapons() =>
        new(InventoryManager, _graphicManager, statusBarDrawing, Session, new PlayerWeaponsHooks(UseSlotItem));

    // The use key, switches, mapdefs triggers, the exits and held items' use (Gameplay/PlayerUse.cs)
    static PlayerUse playerUse = null!;     // made in InitServices

    static PlayerUse MakePlayerUse() =>
        new(MapManager, AudioManager, InventoryManager, doors, LevelAI, TimedSwitches, Session,
            new PlayerUseHooks(PlayPlayerSound, () => MountedWeapons.TryDismount(),
                (objective, amount) => Objectives.Complete(objective, amount), () => Objectives.ExitAllowed(),
                () => deathmatch.Objective?.PlayerUse() == true,
                message => HudMessageManager.Show(HudMessageKind.Lock, message), () => Vehicles.TryLeave(), () => Vehicles.Hint(),
                () => Objectives.ExitBlock(),
                (text, style) => HudMessageManager.ShowKeyed("facing", text, style ?? HudMessageManager.KindStyle(HudMessageKind.Lock)),
                name => Unlock(name),
                (x, y, dir) => gameScripts.RunUseHooks(Session.Player, x, y, dir),
                (x, y) => gameScripts.RunSecretHooks(Session.Player, x, y),
                (x, y, tag) => gameScripts.RunSwitchHooks(Session.Player, x, y, tag),
                kind => gameScripts.RunExitHooks(Session.Player, kind),
                () => Vehicles.DockMessage()));

    private static void ActivateTrigger(MapTriggerTranslation trigger, int tilex, int tiley, ControlDir dir) => playerUse.ActivateTrigger(trigger, tilex, tiley, dir);
    private static void UseSwitch(MapSwitchTranslation wallSwitch, int tilex, int tiley, ControlDir dir) => playerUse.UseSwitch(wallSwitch, tilex, tiley, dir);
    private static bool ExitAction(bool secret) => playerUse.ExitAction(secret);
    static void UseSlotItem(int slot) => playerUse.UseSlotItem(slot);

    // The player's think each tic, and putting them at a start (Gameplay/PlayerThink.cs)
    static PlayerThink playerThink = null!;     // made in InitServices

    static PlayerThink MakePlayerThink() =>
        new(MapManager, statusBarDrawing, playerStats, playerWeapons, weaponInHand, playerUse, playerMovement, WeaponBob, LevelAI, Session,
            new PlayerThinkHooks(UpdateRadar, () => VictoryRunning, InitAreas, reloading =>
            {
                WeaponZoom.Tick(reloading);
                MountedWeapons.Tick();          // let off a gun they're no longer manning (dying, a load, a new level)
                Vehicles.Tick();                // and off a vehicle (dying, a new level)
                Latching.Tick();                // and out of a struggle that's over
                Stealth.Tick();                 // the suspicion meter
                if (Session.PlayerState.CinematicTics > 0)     // a cutscene running out (A_Cinematic)
                    Session.PlayerState.CinematicTics = Math.Max(Session.PlayerState.CinematicTics - (int)Session.Tics, 0);
                playerUse.ShowFacingMessage();  // a use trigger's facing-message (the exits')
            },
            () => deathmatch.Objective?.IsWaiting(Session.PlayerState) == true,
            (x, y) => gameScripts.RunEnterHooks(Session.Player, x, y),
            (from, to) => gameScripts.RunWeaponSwitchHooks(Session.Player, from, to)));

    internal static void ThinkPlayer(Entities.Actors.Actor ob) => playerThink.ThinkPlayer(ob);

    // A weapon's scope (Gameplay/WeaponZoom.cs): the zoom key, the view's magnification and the scope's picture
    static WeaponZoom? _weaponZoom;
    static WeaponZoom WeaponZoom => _weaponZoom ??= new WeaponZoom(InventoryManager, AudioManager, Session);

    // Mounted guns the player mans (Gameplay/MountedWeapons.cs): A_Mount, and getting off with use
    static MountedWeapons? _mountedWeapons;
    static MountedWeapons MountedWeapons => _mountedWeapons ??= new MountedWeapons(MapManager, InventoryManager, playerWeapons, Session,
        new MountedWeaponsHooks(PlayPlayerSound, message => HudMessageManager.Show(HudMessageKind.Lock, message), () => playerMovement.Thrust(0, 0)));

    // Vehicles the player rides (Gameplay/Vehicles.cs): A_Board, the throttle, docks, its weapon, crushing
    static Vehicles? _vehicles;
    static Vehicles Vehicles => _vehicles ??= new Vehicles(MapManager, InventoryManager, Session,
        new VehiclesHooks(PlayPlayerSound, message => HudMessageManager.Show(HudMessageKind.Lock, message), () => playerMovement.Thrust(0, 0),
            (x, y, vehicle) => MapManager.SpawnThing(x, y, vehicle), (monster, damage) => monster.Damage((uint)damage, Session.Player)),
        playerWeapons);

    // Things that latch onto the player (Gameplay/Latching.cs): A_LatchBite, and striking them off
    static Latching? _latching;
    static Latching Latching => _latching ??= new Latching(MapManager, InventoryManager, playerWeapons, Session,
        new LatchingHooks(playersManager.TakeDamage, playerAttacks.PlayAttackSound, weaponInHand.IsWeaponReady,
            (monster, damage, by) => monster.Damage((uint)damage, by)));

    // Switches that turn back by themselves (Gameplay/TimedSwitches.cs), ticked by the level loop
    static TimedSwitches? _timedSwitches;
    static TimedSwitches TimedSwitches => _timedSwitches ??= new TimedSwitches(MapManager, Session);

    // Floors that hurt (Gameplay/FloorDamage.cs), ticked by the level loop
    static FloorDamage? _floorDamage;
    static FloorDamage FloorDamage => _floorDamage ??= new FloorDamage(MapManager, Session, new FloorDamageHooks(
        (pawn, points) =>
        {
            using var _ = Session.ActAs(pawn.State);
            playerStats.ApplyDamageToPlayer(points);
        },
        (monster, points) => monster.Damage((uint)points),
        () => MapManager.GetMapData().Flats,
        (x, y) => MapManager.FlatTextureName(x, y, ceiling: false)));

    // Kills in quick succession (Gameplay/KillChains.cs): their messages and prizes
    static KillChains? _killChains;
    static KillChains KillChains => _killChains ??= new KillChains(Session, new KillChainHooks(
        () => GameEngineManager.GetGameInfo().KillChain,
        message => HudMessageManager.Show(HudMessageKind.Lock, message),
        item => InventoryManager.Has(item),
        (x, y, item) => MapManager.SpawnThing(x, y, item),
        points => playerStats.HealSelf(points)));

    // Disguise and going unseen (Gameplay/Stealth.cs): enemies' suspicion, papers, the uniform
    static Stealth? _stealth;
    static Stealth Stealth => _stealth ??= new Stealth(MapManager, InventoryManager, playerWeapons, Session, currentMapInfo,
        new StealthHooks(
            () => GameEngineManager.GetGameInfo().Stealth,
            message => HudMessageManager.Show(HudMessageKind.Lock, message),
            PlayPlayerSound,
            playerAttacks.PlayAttackSound,
            actor => (actor.AreaNumber >= MapManager.Floors.NumAreas || Session.Areas.AreaSeenByPlayer[actor.AreaNumber] != 0)
                && lineOfSight.CheckLine(actor),
            actor => playerStats.Kill(actor)));     // caught on a stealth-only map: the mission's failed, as a death at their hands

    // ==== PlayerWeapon ===========================================================================
    // The weapon in hand's states (Gameplay/WeaponInHand.cs) and the player's knife and gun
    // (Gameplay/PlayerAttacks.cs)

    static PlayerAttacks playerAttacks = null!;     // made in InitServices
    static WeaponInHand weaponInHand = null!;

    static PlayerAttacks MakePlayerAttacks() =>
        new(MapManager, InventoryManager, FactionManager, lineOfSight, Session,
            new PlayerAttackHooks(PlayerViewOrigin, () => CenterX, () => ShootDelta, TakeDamage, PlayPlayerSound,
                () => DamageDealt, actor => deathmatch.Objective?.IsDecoy(actor) == true,
                (x, y, type, damage) => destructibleWalls.Hit(x, y, type, Session.Player, damage)));

    static WeaponInHand MakeWeaponInHand() =>
        new(InventoryManager, MapManager, Session, playerWeapons, playerAttacks, RegisterMissileActions)
        {
            MayFire = weapon => gameScripts?.RunFireHooks(Session.Player, weapon) != false,
        };

    static void RegisterWeaponActions() => (weaponInHand ??= MakeWeaponInHand()).Register();

    static string? WeaponShapeName() => weaponInHand.WeaponShapeName();

    // ==== Players ================================================================================

    /*
    ====================
    =
    = Players
    =
    = Everyone in the game, each with their own PlayerState, and who's acting now: the
    = GameSession (Gameplay/GameSession.cs), which these forward to.
    =
    ====================
    */

    internal static List<Entities.PlayerState> PlayerStates => Session.Players;
    internal static Entities.PlayerState PlayerState => Session.PlayerState;
    internal static GameSession.ActingScope ActAs(Entities.PlayerState p) => Session.ActAs(p);
    static void SetActing(Entities.PlayerState p) => Session.SetActing(p);

    // Everyone in the game coming and going (Gameplay/Players.cs)
    static Players playersManager = null!;      // made in InitServices

    static Players MakePlayers() =>
        new(MapManager, AudioManager, _videoManager, InventoryManager, HudMessageManager, statusBarDrawing, playerClassRules, playerStats,
            playerWeapons, playerMovement, playerThink, Session,
            new PlayersHooks(PlaySoundLocActor, DeathmatchSpot, GiveDeathmatchKeys, CountFrag, MayRespawn, ShowObituary, ConnectAreas,
                () => DeathTurnSpeed, () => DeathDropHeight, () => DeathDropSpeed, PlayerColor,
                (pawn, reason) => gameScripts.RunSpawnHooks(pawn, reason)));

    internal static void TakeDamage(Entities.Actors.PlayerPawn victim, int points, Entities.Actors.Actor attacker) => playersManager.TakeDamage(victim, points, attacker);
    internal static void PlayPlayerSound(string name) => playersManager.PlayPlayerSound(name);
    static bool IsFreeFloor(int x, int y) => playersManager.IsFreeFloor(x, y);
    internal static void PlayerDies() => playersManager.PlayerDies();
    internal static void ThinkPlayerDead(Entities.Actors.Actor ob) => playersManager.ThinkPlayerDead(ob);
    static void AddNetPlayer(Networking.NetJoin join) => playersManager.AddNetPlayer(join);

    // addplayer, GameMode and controlplayer (Commands/PlayerCommands.cs)
    static PlayerCommands playerCommands = null!;       // made in InitServices

    //
    // The acting player's state, under the names the game has always used for it
    //
    static Entities.Actors.Actor? LastAttacker { get => PlayerState.LastAttacker; set => PlayerState.LastAttacker = value; }

    // The player's own look up/down (degrees, up positive) and eye height (texels above the
    // floor): reset each level, not saved
    internal static int PlayerEyeZ { get => PlayerState.EyeZ; set => PlayerState.EyeZ = value; }

    // ==== WL_ACT1 ================================================================================

    /*
    ===============
    =
    = PlaceItemType
    =
    = Called during game play to drop actors' items.  It finds the proper
    = item number based on the item type (bo_???).  If there are no free item
    = spots, nothing is done.
    =
    ===============
    */
    internal static void PlaceItemType(string item_class, int tilex, int tiley)
    {
        MapManager.SpawnThing(tilex, tiley, item_class);
    }

    /*
    =============================================================================

                                    DOORS

    The doors and the areas they join are Gameplay/Doors.cs (made in InitServices) and
    Gameplay/AreaConnections.cs (on the session); these forward to them.

    =============================================================================
    */

    static Doors doors = null!;

    internal static void ConnectAreas() => doors.ConnectAreas();
    internal static void InitAreas() => Session.Areas.InitAreas(MapManager.Players.Select(pawn => (int)pawn.AreaNumber), MapManager.Floors.NumAreas);

    /*
    =============================================================================

                                    PUSHABLE WALLS

    Gameplay/PushWalls.cs (made in InitServices); these forward to it, the renderer's PWall
    accessors included.

    =============================================================================
    */

    static PushWalls pushWalls = null!;

    internal static bool PushWall(int checkx, int checky, ControlDir dir, string? moveSound = null, string? blockedSound = null)
        => pushWalls.PushWall(checkx, checky, dir, moveSound, blockedSound);

    /*
    =============================================================================

                                    SWITCHES

    The switch and trigger actions and Blake Stone's smart switches are Gameplay/SwitchActions.cs
    and Gameplay/SmartSwitches.cs (made in InitServices); these forward to them.

    =============================================================================
    */

    static SwitchActions switchActions = null!;
    static SmartSwitches smartSwitches = null!;

    // Made in InitServices. Registering only binds the handlers, so a test that lists the action
    // names (ScriptApiTests) can register them before any services exist: then they're made here.
    static SwitchActions MakeSwitchActions() => new(AudioManager, MapManager, InventoryManager, HudMessageManager, doors, pushWalls, Session,
        new SwitchHooks(CouldTakeInventory, TryApplyInventory, () => _videoManager.StartBonusFlash(), currentMapInfo));
    static SmartSwitches MakeSmartSwitches() => new(MapManager, doors, Session, (wallSwitch, x, y) => UseSwitch(wallSwitch, x, y, FacingDir(Player.Angle)));

    private static void RegisterSwitchActions() => (switchActions ??= MakeSwitchActions()).Register();
    private static void RegisterSmartSwitchActions() => (smartSwitches ??= MakeSmartSwitches()).Register();

    // Walls taken away as the level's played (Gameplay/DestructibleWalls.cs)
    static DestructibleWalls destructibleWalls = null!;
    static DestructibleWalls MakeDestructibleWalls() => new(MapManager, doors, Session,
        new DestructibleWallHooks(PlaySoundLocTile, name => Objectives.Complete(name),
            name => AssetManager.FindAnimation<Assets.TextureAsset>(name) is { Loops: false } animation ? animation.TotalTics : null,
            (x, y, wall, type, by) => gameScripts?.RunWallBreakHooks(x, y, wall, type, by) != false));
    private static void RegisterDestructibleWallActions() => (destructibleWalls ??= MakeDestructibleWalls()).Register();

    // ==== WL_ACT2 ================================================================================

    /*
    =============================================================================

                                   LOCAL CONSTANTS

    =============================================================================
    */

    // Enemy state tables, spawn functions (SpawnStand/SpawnPatrol/SpawnBoss/etc.), and AI
    // think/action functions (T_Stand/T_Chase/T_Shoot/etc., along with starthitpoints and
    // the enemy_states/EnemyStateList lookup) were removed from this file -- enemies now
    // run on the new Entities.Actors.Actor type, spawned by MapManager.LoadMap from
    // mapdefs/wolf3d/enemies.yaml and driven by the AI on Entities.Actors.Monster.
    //
    // Projectiles (Rocket/Smoke/Needle/Fire, actordefs/wolf3d/projectiles.yaml) and the
    // BJ-victory end-of-episode cutscene (actordefs/wolf3d/victory.yaml) run on the same type,
    // spawned into MapManager._actors by MapManager.SpawnAtActor. CheckPosition's only caller
    // is A_StartDeathCam (Gameplay/PlayerDeath.cs), placing the camera.

    // Enemies' projectiles and their effects (Gameplay/Projectiles.cs), actors' sounds
    // (Gameplay/ActorSounds.cs) and BJ's victory run (Gameplay/BJVictory.cs)
    static Projectiles projectiles = null!;     // made in InitServices
    static ActorSounds actorSounds = null!;
    static BJVictory bjVictory = null!;

    static Projectiles MakeProjectiles() =>
        new(MapManager, FactionManager, Session, new ProjectileHooks(
            (xl, yl, xh, yh, x, y, size) => WallSpriteHitByBox(xl, yl, xh, yh, x, y, size, player: false),
            PlaySoundLocActor, TakeDamage, (ob, damage) => destructibleWalls.HitByMissile(ob, damage)));

    internal static void SpawnThing(Entities.Actors.Actor ob, string[] args) => projectiles.SpawnThing(ob, args);
    internal static void Remove(Entities.Actors.Actor ob) => projectiles.Remove(ob);
    internal static bool ProjectileTryMove(Entities.Actors.Actor ob) => projectiles.ProjectileTryMove(ob);
    internal static void Projectile(Entities.Actors.Actor ob, string[] args) => projectiles.Projectile(ob, args);

    internal static void DeathScream(Entities.Actors.Actor ob) => actorSounds.DeathScream(ob);
    internal static void PlayActorSound(Entities.Actors.Actor ob, string key) => actorSounds.PlayActorSound(ob, key);
    internal static void ActiveSound(Entities.Actors.Actor ob) => actorSounds.ActiveSound(ob);
    internal static void PlaySound(Entities.Actors.Actor ob, string[] args) => actorSounds.PlaySound(ob, args);

    internal static void VictoryTile(string? runnerClass) => bjVictory.VictoryTile(runnerClass);
    internal static void Victory(Entities.Actors.Actor ob) => bjVictory.Victory(ob);
    internal static void ThinkBjRun(Entities.Actors.Monster ob) => bjVictory.ThinkBjRun(ob);
    internal static void ThinkBjJump(Entities.Actors.Monster ob) => bjVictory.ThinkBjJump(ob);
    internal static void ThinkBjDone(Entities.Actors.Actor ob) => bjVictory.ThinkBjDone(ob);

    // ==== WL_STATE ===============================================================================

    // Line of sight (Gameplay/LineOfSight.cs, made in InitServices); these forward to it
    static LineOfSight lineOfSight = null!;

    // What the level does on its own: outlets, warp sites, floor drops, talking (Managers/LevelAI.cs)
    internal static LevelAI LevelAI = null!;      // made in InitServices

    // ==== Missiles ===============================================================================

    // Missiles, blasts and barriers (Gameplay/Missiles.cs)
    static Missiles missiles = null!;   // made in InitServices

    static Missiles MakeMissiles() =>
        new(MapManager, FactionManager, doors, playerWeapons, playerAttacks, playerStats, Session,
            new MissileHooks(ProjectileTryMove, PlaySoundLocActor, TakeDamage,
                (ob, damage) => destructibleWalls.HitByMissile(ob, damage),
                (ob, x, y, damage) => destructibleWalls.HitByBlast(ob, x, y, damage)));

    static void RegisterMissileActions() => (missiles ??= MakeMissiles()).Register();

    // ==== ProjectileActions ======================================================================

    // Projectiles and jumps for any actor (Gameplay/ProjectileActions.cs, made in InitServices).
    // Registering only binds the handlers, so it can happen before the services exist (ScriptApiTests).
    static ProjectileActions projectileActions = null!;
    static ProjectileActions MakeProjectileActions() => new(MapManager, Session, lineOfSight, PlayActorSound);

    static void RegisterProjectileActions() => (projectileActions ??= MakeProjectileActions()).Register();
}
