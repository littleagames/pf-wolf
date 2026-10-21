namespace PFWolf.Definitions;

/// <summary>Where an action can be run from</summary>
[Flags]
public enum ActionUse
{
    /// <summary>An actordefs state's `think:` or `action:`</summary>
    Actor = 1,
    /// <summary>A mapdefs switch's or trigger's `actions:` (and a script's game.trigger)</summary>
    Switch = 2,
}

/// <summary>A think or action the engine runs, and what it does</summary>
/// <param name="Name">Its name, as the YAML calls it (A_OpenDoor)</param>
/// <param name="Use">Where it can be run from</param>
/// <param name="Signature">How it's called, its arguments in [brackets] when they can be left out</param>
/// <param name="Description">What it does</param>
/// <param name="Needs">The actor class it needs (`parent:` one of them), or null for any actor</param>
public sealed record GameAction(string Name, ActionUse Use, string Signature, string Description, string? Needs = null)
{
    /// <summary>A think (T_): run every tic the actor is on the state, rather than once as it starts</summary>
    public bool IsThink => Name.StartsWith("T_", StringComparison.Ordinal);
}

/// <summary>
/// The thinks and actions the engine registers, for the documentation and the tests that keep it in
/// step with the code (a name the engine registers that isn't here, or one here it doesn't
/// register, fails them). Add an action here as the engine registers it.
/// </summary>
public static class GameActions
{
    const ActionUse Actor = ActionUse.Actor, Switch = ActionUse.Switch, Both = ActionUse.Actor | ActionUse.Switch;
    const string Monster = "Monster", BlakeMonster = "BlakeMonster";

    /// <summary>Every think and action, by name</summary>
    public static IReadOnlyList<GameAction> All { get; } =
    [
        // Enemies' thinks
        new("T_Stand", Actor, "T_Stand", "Stands guard, waiting to see or hear the player (Wolf3D's)", Monster),
        new("T_Path", Actor, "T_Path", "Patrols, turning at the patrol points, until it sees or hears the player (Wolf3D's)", Monster),
        new("T_Chase", Actor, "T_Chase", "Goes after the player, attacking now and then while it has a clear shot (Wolf3D's)", Monster),
        new("T_DogChase", Actor, "T_DogChase", "Runs at the player to bite, going to its Melee state when beside them (Wolf3D's dogs)", Monster),
        new("T_Bite", Actor, "T_Bite", "A bite at the player beside it (Wolf3D's dogs)", Monster),
        new("T_Ghosts", Actor, "T_Ghosts", "Drifts straight at the player, through doors (Wolf3D's Pac-Man ghosts)", Monster),
        new("T_DodgeChase", Actor, "T_DodgeChase", "Attacks at random while it has a clear shot, otherwise dodges toward the player, and backs off once within four tiles (vanilla's T_Schabb, T_Gift, T_Fat, T_Will)", Monster),
        new("T_Strafe", Actor, "T_Strafe", "Always dodges about the player, never closing in directly, and attacks at random (less often than T_DodgeChase) while it has a clear shot (vanilla's T_Fake)", Monster),
        new("T_Shoot", Actor, "T_Shoot", "A gun volley at the player; `monster.pointblankdamage` adds that much when right beside them (the Ubermutant's, vanilla's T_UShoot)", Monster),
        new("T_Scan", Actor, "T_Scan(\"AttackState\")", "A turret's watch: it goes to the state when the player is in its sights", Monster),
        new("T_Retreat", Actor, "T_Retreat", "Runs to the marker A_RetreatTo picked, by the shortest open way (opening doors, waiting behind other enemies), and goes to its Chase state once there (Midnight Assault's Commander between phases)", Monster),
        new("T_ReviveStand", Actor, "T_ReviveStand", "T_Stand that first looks for a corpse of its side to bring back (its Revive state)", Monster),
        new("T_ReviveChase", Actor, "T_ReviveChase", "T_Chase that first looks for a corpse of its side to bring back (its Revive state)", Monster),
        new("T_BlowBack", Actor, "T_BlowBack", "Knocked back by a hit, sliding away from the player (Blake Stone's)", BlakeMonster),
        new("T_Wounded", Actor, "T_Wounded", "On a wounded enemy's last, held frame: when its time's up and the player isn't on top of it, it gets up (its Recover state)", BlakeMonster),
        new("T_WaitToWake", Actor, "T_WaitToWake", "A sleeper's think (its Spawn state): once the player can see it, it counts down its wake delay and wakes (its Wake state). One with no delay only wakes when shot.", BlakeMonster),
        new("T_Bounce", Actor, "T_Bounce", "A missile's flight that bounces off walls at half speed, with its bouncesound (Planet Strike's grenade)", BlakeMonster),
        new("T_LiquidMove", Actor, "T_LiquidMove", "The liquid alien moving as a puddle (its Chase state): near the player, but not right by them, it rises (its Rise state)", BlakeMonster),
        new("T_LiquidStand", Actor, "T_LiquidStand", "The liquid alien standing up: it shoots (its Shoot state) now and then, up to five times, else sinks back (its Fall state) when the player can't see it, sometimes anyway, and after its fifth shot", BlakeMonster),
        new("T_Seek", Actor, "T_Seek", "A turret's think: facing the player (within its eighth of the circle, 15 tiles, in a clear line), it fires (its Attack state), more likely the nearer they are; else a turning one turns a step every half second (a STATIONARY one keeps facing its way)", BlakeMonster),
        new("T_SecurityLight", Actor, "T_SecurityLight", "A security light's think: once there's been a noise where the player can hear it, it starts flashing (its Alert state)", BlakeMonster),
        new("T_SteamVent", Actor, "T_SteamVent", "A steam vent's think: while the player can see it, it counts down and lets off steam (its Release state), then waits up to 34 seconds for the next", BlakeMonster),

        // Enemies' actions
        new("A_FireProjectile", Actor, "A_FireProjectile(\"HRocket\"[, offset[, \"shoot\"]])", "Launches a projectile at the player, turned by the offset in Angles units; with \"shoot\", also fires a gun volley (T_Shoot), as the Death Knight does with each rocket", Monster),
        new("A_StartAttack", Actor, "A_StartAttack", "Starts counting a volley of several shots (the Angel's sparks); see A_Relaunch", Monster),
        new("A_Relaunch", Actor, "A_Relaunch(count, \"TiredState\"[, \"BreakOffState\"])", "After each shot of a volley: once count are fired it goes to TiredState, else half the time it breaks off to BreakOffState (Chase when left out)", Monster),
        new("A_Morph", Actor, "A_Morph(\"Class\"[, \"keep\"])", "Becomes another enemy where it stands (a boss's next phase); the new one is shootable at its own health and speed and already chasing. This one leaves the level, or with \"keep\" stays as it is (a corpse, as Mecha Hitler's armor).", Monster),
        new("A_SetFaction", Actor, "A_SetFaction(\"allies\"[, \"follow\"])", "Puts it on a side (factions.yaml); with \"follow\", it goes about with the players from now on", Monster),
        new("A_Follow", Actor, "A_Follow([\"off\"])", "Starts it following the players about, or with \"off\" stops it", Monster),
        new("A_RetreatTo", Actor, "A_RetreatTo(\"Class\"[, tag[, speed]])", "Sends it off to the nearest actor of the class (with the tag, if given: a marker the map places), at the speed if given; it can't be shot on the way. T_Retreat runs it there.", Monster),
        new("A_Revive", Actor, "A_Revive([count][, radius])", "Brings back up to count corpses of its side (a number, or \"min-max\" for a random number in that range) within radius tiles that it can see, nearest first; its `monster.revive.count` and `monster.revive.radius` for those left out", Monster),
        new("A_Resume", Actor, "A_Resume", "After a heal or a raise: on with the chase it's in; else after whoever it was brought back to fight, or anyone it sees; else back to standing. Every class that dies gets a Raise state ending in it."),
        new("A_Dormant", Actor, "A_Dormant([\"heal\"])", "A dead Spectre comes back once nothing is in the way, with the health it died with; with \"heal\", its full health", Monster),
        new("A_LatchBite", Actor, "A_LatchBite(\"Struggle\"[, latch[, hit]])", "A bite that may latch onto the player, who has to shake it off with the Struggle weapon", Monster),
        new("A_SpawnEnemy", Actor, "A_SpawnEnemy(\"ScanAlien\"[, \"chase\" | \"alert\"][, \"count\"])", "An enemy appears on this actor's tile, as if placed by the map but not added to the kill total; with \"chase\", it's already after the player; with \"alert\", after them at its chase speed and attacking; with \"count\", it's added to the kill total too", BlakeMonster),
        new("A_Melee", Actor, "A_Melee(chance, min, max)", "A blow at what it's after within reach (two tiles), landing chance times in 256 for min..max, with its `meleesound`; it alerts the area", BlakeMonster),
        new("A_SetShootable", Actor, "A_SetShootable(\"on\" | \"off\")", "Makes it shootable (and in the way), or not, as the liquid alien rising out of its puddle and sinking back", BlakeMonster),
        new("A_HurtPlayerHere", Actor, "A_HurtPlayerHere(damage[, chance])", "The player standing on its tile is hurt, chance times in 256 (ooze)", BlakeMonster),
        new("A_HoldNearPlayer", Actor, "A_HoldNearPlayer(damage)", "A think for a barrier rising: while the player is within 1.5 tiles its frame doesn't run down, so it can't shut on them, and within half a tile they're hurt for damage each tic", BlakeMonster),
        new("A_JumpIfUntagged", Actor, "A_JumpIfUntagged(\"State\")", "Goes to the state if nothing is tagged to work it (no tag, nor a map-info tag-link): Planet Strike's barriers with no switch cycle by themselves", BlakeMonster),
        new("A_SelfDestruct", Actor, "A_SelfDestruct", "Dies, with its points (the floating bomb, reaching the player, blows itself up)", BlakeMonster),
        new("A_CountRemaining", Actor, "A_CountRemaining(\"message\")", "Shows the message, with %n the number of its class still standing, as each projection generator goes", BlakeMonster),
        new("A_VictoryIfLast", Actor, "A_VictoryIfLast", "The level is won once none of its class still stands (the last projection generator)", BlakeMonster),
        new("A_FireSpread", Actor, "A_FireSpread(\"SpectorShot\", chance, health, lowchance, offset, ...)", "A projectile at the player and, chance times in 256 (lowchance below that health), one either side at each offset in Angles units too (Goldfire, morphed)", BlakeMonster),

        // Any actor
        new("A_DeathScream", Actor, "A_DeathScream", "Plays its `deathsound` from where it is (one picked at random from a list)"),
        new("A_ActiveSound", Actor, "A_ActiveSound", "Plays its `activesound` (Mecha Hitler's stomp), only where the player can hear it: in an area joined to theirs"),
        new("A_PlaySound", Actor, "A_PlaySound(\"sound\"[, \"positional\"])", "Plays a sound, not placed in the world; with \"positional\", from where it is"),
        new("A_Taunt", Actor, "A_Taunt(\"a\", \"b\", ...)", "One of the sounds at random, as the acting player's, heard by everyone else from where they stand (a Prop Hunt prop's fire)"),
        new("A_Quake", Actor, "A_Quake(min[, max][, \"near\"])", "Shakes the view, at a power (0-200) rolled from min to max; with \"near\", only while its area is joined to the player's"),
        new("A_MakeNoise", Actor, "A_MakeNoise", "A noise, as a shot's: enemies in the areas joined to the player's come"),
        new("A_Victory", Actor, "A_Victory", "Ends the game in victory (the Angel of Death's last death frame)"),
        new("A_StartDeathCam", Actor, "A_StartDeathCam", "Starts the death cam on this boss's death, replaying it once the player has seen the message"),
        new("A_GiveExtraMan", Actor, "A_GiveExtraMan", "Gives the player an extra life"),
        new("A_ChangeMap", Actor, "A_ChangeMap(\"MAP21\"[, \"keep-position\"])", "Ends the level and moves to another map with no intermission; with \"keep-position\" the player keeps their spot and facing on the new map, as when picking up the Spear of Destiny"),
        new("A_JumpIfObjectiveDone", Actor, "A_JumpIfObjectiveDone(\"name\", \"State\")", "Goes to the state once all of the level objective is done"),
        new("A_JumpIfUnlocked", Actor, "A_JumpIfUnlocked(\"name\", \"State\")", "Goes to the state if the name's unlocked ({tag} and counts filled in): a dog tag found before, greyed out"),
        new("A_JumpIfInventory", Actor, "A_JumpIfInventory(\"Item\", \"State\")", "Goes to the state if the acting player holds the item (a locked gate the wire clippers open)"),
        new("A_ShowText", Actor, "A_ShowText(\"name\")", "Shows a text as an article in the middle of the level, then back to it (Midnight Assault's log books and radios); {tag} in the name is the actor's tag, so each one placed can show its own page (\"LOG{tag}\")"),
        new("A_Mount", Actor, "A_Mount(\"Gun\"[, \"sound\"])", "The player who used it mans it, with that gun"),
        new("A_Board", Actor, "A_Board([\"sound\"][, \"message\"])", "The player who used it rides it"),
        new("A_Cinematic", Actor, "A_Cinematic(tics)", "Every player watches a cutscene that long: they turn but don't walk, with no weapon in hand and black bars top and bottom"),
        new("A_Replace", Actor, "A_Replace(\"Class\")", "Becomes one of another class where it stands, facing the same way (Midnight Assault's gates: a lock clipped off, a gate opened)"),
        new("A_LatchHurt", Actor, "A_LatchHurt", "What it's latched onto is hurt, 0-15; latched onto no one, it lets go and goes after them again"),
        new("A_ShakeOff", Actor, "A_ShakeOff([tries[, chance[, damage]]])", "The struggle weapon strikes at what's latched on (its attacksound); chance times in 256 (128), or on the tries'th strike (5), that takes the damage (1)"),
        new("A_JumpIfLatched", Actor, "A_JumpIfLatched(\"State\")", "The weapon goes to the state while what's latched on holds on"),
        new("A_LatchRelease", Actor, "A_LatchRelease", "The struggle's over, and the weapon the player had is back in hand"),
        new("A_ShowPapers", Actor, "A_ShowPapers", "The papers in hand are shown (their attacksound) to every enemy in sight that suspects the player: each is satisfied, unless its `monster.papersfail` says it isn't fooled"),
        new("A_Disguise", Actor, "A_Disguise(\"Disguise\", \"Papers\"[, \"State\"])", "The player puts on the disguise and takes the papers, with the disguise's pickup message and sound, and the thing used goes to the state"),
        new("A_WarpSiteGone", Actor, "A_WarpSiteGone", "A warp site's actor has warped out: the wait for the next starts"),
        new("A_WarpSitesOff", Actor, "A_WarpSitesOff", "No more come: the level's warp sites go (Goldfire, morphed)"),

        // Projectiles, effects and jumps
        new("A_Projectile", Actor, "A_Projectile(min[, max])", "A projectile's think: flies along its angle; hitting the player deals min..max (just min without a max), and with the HURTMONSTERS flag so does hitting an enemy"),
        new("A_SpawnProjectile", Actor, "A_SpawnProjectile(\"Fireball\"[, angle[, \"relative\" | \"absolute\" | \"aim\"]])", "Fires a projectile (an actor whose think is A_Projectile), at the angle in degrees, positive turning left: relative (the default) to the way it faces, absolute from east, or aim: at the nearest player, turned by the angle. The projectile's `speed`, `speed.random` and `spread` apply, and its `attacksound` plays."),
        new("A_SpawnThing", Actor, "A_SpawnThing(\"Smoke\"[, chance])", "Spawns the class where it is (a rocket's smoke trail); with a chance, only that many times in 256"),
        new("A_Remove", Actor, "A_Remove", "Takes it off the level"),
        new("A_Jump", Actor, "A_Jump(chance, \"State\"[, \"State2\", ...])", "Chance times in 256, goes to one of the states (picked at random when there are several)"),
        new("A_JumpIfPlayerWithin", Actor, "A_JumpIfPlayerWithin(tiles, \"State\")", "Goes to the state when a player is within that many tiles of it, each way (it may be a fraction: 1.5)"),
        new("A_JumpIfActorWithin", Actor, "A_JumpIfActorWithin(tiles, \"State\")", "As A_JumpIfPlayerWithin, but anything shootable counts too, other than another of its own class (Midnight Assault's landmine)"),
        new("A_JumpIfInSight", Actor, "A_JumpIfInSight(\"State\"[, tiles])", "Goes to the state when there's a clear line to a player (within that many tiles each way, if given); one in a wall only sees out of its face"),
        new("A_FireMissile", Actor, "A_FireMissile(\"Grenade\"[, spread[, \"power\"]])", "The weapon in hand throws a missile the way the player faces, turned up to spread degrees either way at random, if there's ammo (and charge) for it; with \"power\" it's thrown as hard as the throw was cooked (A_HoldCharge), a quarter at least"),
        new("A_Missile", Actor, "A_Missile(min, max[, \"bounce\"])", "A player's missile's think: it flies along its angle (and up and down with a `gravity`); meeting a wall, a solid thing or something shootable, it hurts that min..max and goes to its Death state. With \"bounce\", a wall turns it back at half the speed, with its `bouncesound`."),
        new("A_Explode", Actor, "A_Explode(min, max[, radius[, \"hurtplayer\"][, \"falloff\"][, \"playermax:N\"]])", "A blast spreading from its tile through open floor up to radius tiles (2): each shootable thing in it takes min..max, rolled once. With \"hurtplayer\" the players in it are hurt too. A thing whose `monster.blastedby` names its class breaks (goes to its Death state)."),
        new("A_BlockTile", Actor, "A_BlockTile", "Its tile blocks movement, as a solid thing's does"),
        new("A_UnblockTile", Actor, "A_UnblockTile", "Its tile no longer blocks (if a solid thing was all that blocked it)"),
        new("A_ShockNearby", Actor, "A_ShockNearby(damage, distance, chance[, \"sound\"])", "A think for something that zaps the player within distance tiles of it: each tic, a chance in 256 of a shock for damage, with the sound from where it is"),

        // The weapon in hand
        new("A_WeaponReady", Actor, "A_WeaponReady", "The Ready state's every-tic action: falls back to the best weapon that can fire when out of ammo, reloads an empty (or, on reload, not full) magazine, and starts the Fire state on a fresh press of fire"),
        new("A_GunAttack", Actor, "A_GunAttack([near, mid, far])", "One hitscan shot from the weapon in hand, if there's ammo (and charge) for it: a hit deals a random 0-255 divided by near within 2 tiles, mid within 4 and far beyond (4, 6, 6)"),
        new("A_FireBullet", Actor, "A_FireBullet(min[, max[, \"silent\"]])", "One shot at the closest target in the weapon's sights, for min..max whatever the distance; with \"silent\", it alerts no one. Ammo and charge as A_GunAttack."),
        new("A_HoldCharge", Actor, "A_HoldCharge(\"State\"[, max])", "While fire is held, the throw cooks: the weapon's power goes up a tic's worth, to max (100), and the weapon goes back to the state; let go, it carries on"),
        new("A_CustomPunch", Actor, "A_CustomPunch", "A knife or punch at what's right in front of the player, for 0-15 (its arguments aren't read yet)"),
        new("A_ReFire", Actor, "A_ReFire", "Fire still held and a round ready: back to the Hold state (or Fire, for a weapon without one)"),
        new("A_Reload", Actor, "A_Reload([rounds[, \"state\"]])", "Loads the weapon in hand's magazine from its ammo, all that fits or that many rounds; given a state, it goes there while the magazine still has room and there's ammo left (a shotgun's shell-by-shell loop)"),

        // Cutscenes and the player
        new("T_BJRun", Actor, "T_BJRun", "The victory cutscene: BJ walks the patrol arrows toward the exit", Monster),
        new("T_BJJump", Actor, "T_BJJump", "The victory cutscene: BJ's jump", Monster),
        new("T_BJDone", Actor, "T_BJDone", "The victory cutscene is over: the level ends"),
        new("T_VictoryRun", Actor, "T_VictoryRun", "A_VictoryRun's runner runs the way the player faced; six tiles on, or at a wall, the cluster is won"),
        new("T_Player", Actor, "T_Player", "The player's own think: moving, using, and the weapon in hand"),
        new("T_DeathCam", Actor, "T_DeathCam", "The player's think on its DeathCam state: animates the banner"),
        new("T_PlayerDead", Actor, "T_PlayerDead", "A dead player's think: turning to face their killer, sinking, and waiting to come back"),

        // Switches and triggers
        new("A_PushWall", Switch, "A_PushWall([\"moving sound\"][, \"blocked sound\"][, \"direction\", \"message\"])", "Pushes the switch's own wall away from the player; either sound left out is silent. With a direction (north, east, south or west) it only moves that way: pushed from another side, it shows the message and plays the blocked sound instead."),
        new("A_Exit", Switch, "A_Exit", "Ends the level, going to the secret level when the player stands on the floors' secret-exit code; it waits for the switch's sound to finish, and refuses while a blocks-exit level objective has any left"),
        new("A_SecretExit", Switch, "A_SecretExit", "Ends the level, going to the secret level; as A_Exit otherwise"),
        new("A_VictoryTile", Switch, "A_VictoryTile(\"BJVictory\")", "That class runs for the exit (Wolf3D's BJ), then the level ends"),
        new("A_OpenDoor", Switch, "A_OpenDoor([\"hold\"])", "Opens the tagged doors, whatever their lock; with \"hold\" they stay open until something closes them"),
        new("A_CloseDoor", Switch, "A_CloseDoor", "Closes the tagged doors (not on anything standing in the doorway)"),
        new("A_ToggleDoor", Switch, "A_ToggleDoor([\"hold\"])", "Opens the tagged doors that are shut or shutting, closes the rest"),
        new("A_MoveWall", Switch, "A_MoveWall([direction][, \"moving sound\"][, \"blocked sound\"])", "Pushes every tagged wall north, east, south or west, or (left out or \"\") the way the player faces; the blocked sound plays when none of them could move. Their tag goes with them, so they can be moved again."),
        new("A_SetWall", Switch, "A_SetWall(id)", "Turns the tagged walls into wall id (walls.yaml), e.g. lights on a panel coming on"),
        new("A_ChangeTexture", Switch, "A_ChangeTexture(\"FROM\", \"TO\")", "The switch's own tile and the tagged ones show texture TO wherever they showed FROM: wall and door faces, floors and ceilings. TO can be an animation (animdefs); one that plays once starts as it's changed."),
        new("A_Activate", Switch, "A_Activate", "The tagged actors go to their Active state (a light coming on); actors without one are left alone"),
        new("A_Deactivate", Switch, "A_Deactivate", "The tagged actors go to their Inactive state; actors without one are left alone"),
        new("A_ToggleActive", Switch, "A_ToggleActive", "Each tagged actor on its Inactive state (or one named Inactive..., such as an InactiveIdle it starts on) goes Active, the rest Inactive"),
        new("A_SpawnItem", Switch, "A_SpawnItem(\"Class\")", "Puts a thing (an item, or any actordefs class) on the floor in front of the switch, on the side it was used from (from a script, where the player is)"),
        new("A_SmartSwitch", Switch, "A_SmartSwitch([\"off\" | \"on\" | \"toggle\"][, \"remove\"])", "Works the tile the trigger's object-plane value names (x high byte, y low byte; toggle by default): a door there is unlocked and opened, a wall switch thrown, an enemy woken (or killed or removed, by its `switched`). With \"remove\", the trigger goes once it has found something to work that isn't a door or a wall."),
        new("A_Vend", Switch, "A_Vend(\"FoodToken\", \"FoodUnitMeal\", \"no-money message\", \"full message\"[, \"out-of-order message\", uses code])", "A vending machine: one currency is taken and the item given, with its pickup sound and message; else the full message (the item would do nothing), the no-money message, or with a uses code (the high byte of the switch tile's object-plane value, whose low byte is the uses left) the out-of-order message, each with the use-fail sound"),
        new("A_FloorSelect", Switch, "A_FloorSelect", "Shows the floor-select screen (gamepacks elevator.yaml: Blake Stone's elevator panel, or Planet Strike's teleporter map): the floors of the level's hub cluster, by floor number. Choosing one leaves the level for it."),
        new("A_Teleport", Switch, "A_Teleport", "Blake Stone's teleporter wall, by the object-plane value on it: 0xF4nn takes the player to floor nn of the cluster, at the same spot; 0xF5nn moves them to the tile the next object-plane value gives (x high byte, y low byte)"),
        new("A_TeleportTag", Switch, "A_TeleportTag([\"beside\"][, \"sound\"])", "A pair of tiles that lead to each other by their tag: the player goes to the other tile with this trigger's tag whose trigger runs A_TeleportTag too, onto it, or with \"beside\" (a ladder's other end) onto the first open tile next to it. The sound plays as they go."),
        new("A_Warp", Switch, "A_Warp([facing][, \"sound\"][, \"telefrag\"][, \"swapcolors\"][, \"fizzle\"][, \"monsters\"])", "A one-way teleporter pad: any number of pads lead to the tile with the pad's tag that has no A_Warp of its own, the player coming out facing east, north, west or south (left out: as they were). \"telefrag\" kills what's alive at the exit, \"swapcolors\" swaps the ceiling and floor colors with game-info's alt ones, \"fizzle\" fizzles the view in again, and with \"monsters\" enemies walking onto the pad go too."),
        new("A_VictoryRun", Switch, "A_VictoryRun(\"Class\", wall)", "The player has reached the way out (Blake Stone's win floor): the view turns toward the map's first tile of the wall (the shuttle), and the class runs past the player that way (T_VictoryRun)"),
        new("A_DropDetonator", Switch, "A_DropDetonator(\"ArmedClass\", \"CubeClass\", \"ItemClass\"[, range])", "The fission detonator, dropped by its slot key beside the floor's security cube with the next floor still locked, which takes one ItemClass from the inventory; it goes off a few seconds later, breaking the cube, whose death unlocks the next floor"),

        // Both
        new("A_Message", Both, "A_Message(\"text\"[, \"style\"])", "Shows a message (a $NAME language key, or the text), in the style for locked doors unless one is named; an actor's fills in its {tag} and counts"),
        new("A_GiveInventory", Both, "A_GiveInventory(\"Item\"[, amount])", "Gives the player an inventory item, as picking it up would, with its pickup sound and message; amount left out is the item's own. From a switch, refused (with the use-fail sound) when it would do nothing."),
        new("A_SetPatch", Both, "A_SetPatch(to[, from, ...])", "The tagged walls' patches become patch `to` (0 takes them off); with froms, only those showing one of them. An actor's works on its own map tag."),
        new("A_SetLight", Both, "A_SetLight([intensity][, radius][, color])", "Gives the actor (from a switch, the tagged actors) this light: 0-255, radius in tiles, #RRGGBB or \"none\"; radius or color left out keep theirs. With no arguments, its class's light back."),
        new("A_LightOff", Both, "A_LightOff", "Puts the actor's light (from a switch, the tagged actors') out, whatever its states say, until A_SetLight"),
        new("A_SetZoneLight", Both, "A_SetZoneLight(zone, light[, tics][, color])", "Fades the light zone (plane 5) to light (0-255) over tics (left out or 0: at once); color #RRGGBB tints it, \"none\" takes the tint off, left out keeps it"),
        new("A_ToggleZoneLight", Both, "A_ToggleZoneLight(zone, light1, light2[, tics])", "Fades the zone to light2 if it's at (or going to) light1, else to light1"),
        new("A_SetZoneEffect", Both, "A_SetZoneEffect(zone, effect)", "The zone's effect: none, flicker, pulse or strobe, timed as the zone's game-info low, tics and bright-tics"),
        new("A_SetShading", Both, "A_SetShading(off | on | toggle)", "Off takes the level's shading (its fade and light) away, its zones' lights staying; on puts back the map's own. Not kept in a saved game."),
        new("A_StartScroll", Both, "A_StartScroll([tag])", "The tag's scroller runs (left out or \"\": the switch's own tag, or the actor's)"),
        new("A_StopScroll", Both, "A_StopScroll([tag])", "The tag's scroller stops where it is"),
        new("A_ToggleScroll", Both, "A_ToggleScroll([tag])", "Stops the tag's scroller if it's running, else starts it"),
        new("A_SetScroll", Both, "A_SetScroll(tag, x, y)", "The tag's scroller's new speeds, in texels a tic, and running; a tag the map has no scroller for gets one, on its walls, floors and ceilings"),
        new("A_SetMonsterWander", Both, "A_SetMonsterWander(on | off)", "The level's enemies wander rather than chase"),
        new("A_ClearMonsterBlocks", Both, "A_ClearMonsterBlocks([monsters | bosses])", "The blocktile markers of that kind (monsters when left out) stop keeping enemies off (Greed's gong)"),
        new("A_UnlockFloor", Both, "A_UnlockFloor", "The cluster's next floor up (by floor number) opens in the teleporter"),
        new("A_CompleteObjective", Both, "A_CompleteObjective(\"name\"[, n])", "One (or n) more of a level objective done, with its progress message (and its done message for the last)"),
        new("A_AddObjective", Both, "A_AddObjective(\"name\"[, n])", "One (or n) more of a level objective to do"),
        new("A_Unlock", Both, "A_Unlock(\"name\"[, \"message\"])", "Unlocks a name for good (a starts-locked episode's key, or a level's name), showing the message if it wasn't already; an actor's fills in its {tag} and counts (a numbered dog tag's \"DT0{tag}\")"),
    ];

    /// <summary>A think or action by its name (case matters, as it does to the engine)</summary>
    public static GameAction? Find(string name) => All.FirstOrDefault(a => a.Name == name);

    /// <summary>The thinks and actions as a record's fields, for the documentation (each its own field, by name)</summary>
    public static FieldSchema Schema() => FieldSchema.Record("actions",
        "The thinks (T_, run every tic) and actions (A_, run once) an actordefs state can run (`think: T_Chase`, `action: A_PlaySound(\"x\")`), "
        + "and the actions a mapdefs switch or trigger can run (`actions:`). Arguments in [brackets] can be left out.",
        [.. All.Select(a => FieldSchema.Value(a.Name, FieldType.Text, Describe(a)))]);

    static string Describe(GameAction action)
    {
        var from = action.Use switch
        {
            Both => "actor states and switches",
            Switch => "switches and triggers",
            _ => action.Needs != null ? $"actor states of a {action.Needs} (`parent: {action.Needs}`)" : "actor states",
        };
        return $"`{action.Signature}`: {action.Description.TrimEnd('.')}. Runs from {from}.";
    }
}
