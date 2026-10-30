using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>What the player's use key needs from the game</summary>
/// <param name="PlayPlayerSound">A sound the acting player makes (a switch's)</param>
/// <param name="Dismount">Steps the player off a mounted gun they're manning: true if they were on one (Gameplay/MountedWeapons.cs)</param>
/// <param name="CompleteObjective">So many more of a level objective are done (a switch with `objective`: Gameplay/Objectives.cs)</param>
/// <param name="ExitAllowed">Whether the level can be left (its blocks-exit objectives done); saying why not if not</param>
/// <param name="ModeUse">The match mode's own use, with nothing ahead to use: true if it took the press (Prop Hunt's disguises)</param>
/// <param name="Message">Shows the acting player a message (a $NAME language key or the text)</param>
/// <param name="LeaveVehicle">Gets the player off what they're riding: true if they were riding (Gameplay/Vehicles.cs)</param>
/// <param name="VehicleHint">Riding, the use hint (getting off at a dock beside them), else null</param>
/// <param name="ExitBlock">Whether the objectives keep the level shut, and their refused message (Objectives.ExitBlock)</param>
/// <param name="ShowFacing">Keeps the facing message up (text, style): renewed each tic, it lasts while it's faced</param>
/// <param name="Unlock">Unlocks a name for good (a thrown switch's `unlock`: Gameplay/Unlocks.cs)</param>
/// <param name="ScriptUse">The scripts' use of the tile ahead (game.onuse): true if one used it, and nothing else happens</param>
/// <param name="SecretFound">A secret's been found at a tile (game.onsecret)</param>
/// <param name="MaySwitch">Whether the scripts let a switch at a tile with a tag be thrown (game.onswitch)</param>
/// <param name="MayExit">Whether the scripts let an exit ("exit" or "secret") end the level (game.onexit)</param>
/// <param name="VehicleMessage">Riding, the message kept up while a dock is beside them (vehicle.dockmessage), else null</param>
internal sealed record PlayerUseHooks(
    Action<string> PlayPlayerSound,
    Func<bool> Dismount,
    Action<string, int>? CompleteObjective = null,
    Func<bool>? ExitAllowed = null,
    Func<bool>? ModeUse = null,
    Action<string>? Message = null,
    Func<bool>? LeaveVehicle = null,
    Func<string?>? VehicleHint = null,
    Func<(bool Blocked, string? Message)>? ExitBlock = null,
    Action<string, string?>? ShowFacing = null,
    Action<string>? Unlock = null,
    Func<int, int, ControlDir, bool>? ScriptUse = null,
    Action<int, int>? SecretFound = null,
    Func<int, int, ushort, bool>? MaySwitch = null,
    Func<string, bool>? MayExit = null,
    Func<string?>? VehicleMessage = null);

/// <summary>
/// The acting player's use key: what's ahead of them -- a mapdefs use trigger, a patch's switch,
/// a switch wall, a door -- else an actor there to be used or talked to, else an item held to be
/// used that way. Also the switches themselves, mapdefs triggers, the exits and the items an
/// empty weapon slot's key uses.
/// </summary>
internal sealed class PlayerUse(
    MapManager map,
    AudioManager audio,
    InventoryManager inventory,
    Doors doors,
    LevelAI levelAI,
    TimedSwitches timedSwitches,
    GameSession session,
    PlayerUseHooks hooks)
{
    const int BitWall = GameConstants.BitWall;
    const int BitDoor = GameConstants.BitDoor;

    // How long use has been held with nothing to use or talk to
    int useHeldTics;

    /// <summary>
    /// Runs a mapdefs trigger's action. If it goes off, a secret trigger counts as found, and the
    /// trigger's tile is cleared so it can't go off again (unless it's a `repeat` one).
    /// </summary>
    internal void ActivateTrigger(MapTriggerTranslation trigger, int tilex, int tiley, ControlDir dir)
    {
        var activation = new Entities.TriggerActivation(tilex, tiley, dir, session.Player, map.GetTag(tilex, tiley));
        if (!Entities.MapTriggerRegistry.Invoke(trigger.Action, activation))
            return;

        if (trigger.Secret)
        {
            session.GameState.SecretCount++;
            hooks.SecretFound?.Invoke(tilex, tiley);
        }
        if (!trigger.Repeat)
            map.SetMapSpot(tilex, tiley, 1, 0);
    }

    /// <summary>
    /// Throws a switch wall: refused without its lock item, else it turns into its `to` wall,
    /// plays its sound and runs its actions on whatever shares its tile's tag. The other switches
    /// with its tag that are either way of the same switch flip with it (as Blake Stone's
    /// barrier switches do), without running their actions.
    /// </summary>
    internal void UseSwitch(MapSwitchTranslation wallSwitch, int tilex, int tiley, ControlDir dir)
    {
        if (RefuseSwitchLock(wallSwitch))
            return;

        var gamestate = session.GameState;
        var tag = map.GetTag(tilex, tiley);
        if (hooks.MaySwitch?.Invoke(tilex, tiley, tag) == false)
            return;                 // a script leaves it be (game.onswitch)
        if (wallSwitch.To is > 0 and < BitWall && map.GetMapData().Walls.ContainsKey(wallSwitch.To))
        {
            // flip the switch, keeping the door-side mark on a wall beside a door
            var tile = map.TileMap[tilex, tiley];
            int from = tile & ~BitWall;
            map.TileMap[tilex, tiley] = (ushort)(wallSwitch.To | (tile & BitWall));
            map.RestartAnimations(tilex, tiley, gamestate.TimeCount);   // a `to` wall's play-once animation starts now
            int thrown = from != wallSwitch.To ? 1 : 0;

            // and the linked ones: set to the new way, not toggled, so any out of step come back
            foreach (var (x, y) in map.TaggedTiles(tag))
            {
                var other = map.TileMap[x, y];
                int id = other & ~BitWall;
                if ((other & BitDoor) == 0 && (id == from || id == wallSwitch.To))
                {
                    if (id == from && id != wallSwitch.To && (x, y) != (tilex, tiley))
                        thrown++;
                    map.TileMap[x, y] = (ushort)(wallSwitch.To | (other & BitWall));
                    map.RestartAnimations(x, y, gamestate.TimeCount);
                }
            }
            timedSwitches.Thrown(wallSwitch, tilex, tiley, patch: false, from, wallSwitch.To, tag, dir);     // reset-tics: it turns back
            if (wallSwitch.Objective is { Length: > 0 } objective && thrown > 0)
                hooks.CompleteObjective?.Invoke(objective, thrown);     // a sabotage target (Gameplay/Objectives.cs)
            if (thrown > 0)
                UnlockFor(wallSwitch, tag);
        }

        if (!string.IsNullOrEmpty(wallSwitch.Sound))
            hooks.PlayPlayerSound(wallSwitch.Sound);

        var activation = new Entities.TriggerActivation(tilex, tiley, dir, session.Player, tag);
        foreach (var action in wallSwitch.Actions)
            Entities.MapTriggerRegistry.Invoke(action, activation);
    }

    // Refuses a switch the player lacks the lock item for: true if it did
    internal bool RefuseSwitchLock(MapSwitchTranslation sw)
    {
        if (string.IsNullOrEmpty(sw.Lock) || inventory.Has(sw.Lock))
            return false;
        doors.RefuseLocked(sw.Lock, sw.LockedSound, sw.LockMessage, sw.LockMessageStyle, isSwitch: true);
        return true;
    }

    /// <summary>
    /// The switch of the patch on the wall tile (TileX, TileY) the player faces going dir, if
    /// it's on the face they press (facing east presses the west face) and usable from there
    /// </summary>
    internal MapSwitchTranslation? PatchSwitchAt(int tilex, int tiley, ControlDir dir)
    {
        int tile = map.TileMap[tilex, tiley];
        if ((tile & BitDoor) != 0 || (tile & ~BitWall) == 0)
            return null;                // open floor, a door or a moving pushwall
        int id = map.GetPatch(tilex, tiley);
        if (id == 0 || !map.GetMapData().Patches.TryGetValue(id, out var patch) || patch.Switch is not { } patchSwitch)
            return null;

        var face = dir switch
        {
            ControlDir.East => "west",
            ControlDir.West => "east",
            ControlDir.North => "south",
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
    internal void UsePatchSwitch(MapSwitchTranslation patchSwitch, int tilex, int tiley, ControlDir dir)
    {
        if (RefuseSwitchLock(patchSwitch))
            return;

        var tag = map.GetTag(tilex, tiley);
        if (hooks.MaySwitch?.Invoke(tilex, tiley, tag) == false)
            return;                 // a script leaves it be (game.onswitch)

        if (!string.IsNullOrEmpty(patchSwitch.Sound))
            hooks.PlayPlayerSound(patchSwitch.Sound);

        var activation = new Entities.TriggerActivation(tilex, tiley, dir, session.Player, tag);
        bool wentOff = patchSwitch.Actions.Count == 0;
        foreach (var action in patchSwitch.Actions)
            wentOff |= Entities.MapTriggerRegistry.Invoke(action, activation);
        if (!wentOff)
            return;

        int from = map.GetPatch(tilex, tiley);
        int to = patchSwitch.Remove ? 0
            : patchSwitch.To is > 0 and <= 255 && map.GetMapData().Patches.ContainsKey(patchSwitch.To) ? patchSwitch.To
            : from;
        if (to == from)
            return;

        void Flip(int x, int y)
        {
            map.SetPatch(x, y, (byte)to);
            map.RestartAnimations(x, y, session.GameState.TimeCount);    // a `to` patch's play-once animation starts now
        }

        Flip(tilex, tiley);
        int thrown = 1;
        foreach (var (x, y) in map.TaggedTiles(tag))
        {
            int other = map.GetPatch(x, y);
            if (other != 0 && (other == from || other == to))
            {
                if (other == from && (x, y) != (tilex, tiley))
                    thrown++;
                Flip(x, y);
            }
        }
        if (to != 0)
            timedSwitches.Thrown(patchSwitch, tilex, tiley, patch: true, from, to, tag, dir);     // reset-tics: it turns back
        if (patchSwitch.Objective is { Length: > 0 } objective)
            hooks.CompleteObjective?.Invoke(objective, thrown);         // a sabotage target (Gameplay/Objectives.cs)
        UnlockFor(patchSwitch, tag);
    }

    // A thrown switch's `unlock` name, for good (a Kilroy drawn)
    void UnlockFor(MapSwitchTranslation sw, ushort tag)
    {
        if (sw.Unlock.Length > 0 && !session.DemoPlayback)
            hooks.Unlock?.Invoke(SwitchUnlockName(sw.Unlock, session.GameState.MapOn, tag));
    }

    /// <summary>A switch's `unlock` name with {map} and {tag} filled in</summary>
    internal static string SwitchUnlockName(string pattern, string? mapName, ushort tag) =>
        pattern.Replace("{map}", mapName ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{tag}", tag.ToString(), StringComparison.OrdinalIgnoreCase).Trim();

    /// <summary>
    /// As the level starts afresh: each switch (a wall's or a patch's) with `unlock` and
    /// `unlocked-to` whose name is unlocked already is shown as its unlocked-to wall or patch;
    /// returns the objectives they're each one done of (Objectives.CountLevel's doneBefore).
    /// Midnight Assault's Kilroys drawn in an earlier game, "Kilroy was here".
    /// </summary>
    internal List<string> ShowUnlockedSwitches(Func<string, bool> unlocked)
    {
        var done = new List<string>();
        var data = map.GetMapData();
        var mapName = session.GameState.MapOn;
        for (int y = 0; y < map.MapHeight; y++)
            for (int x = 0; x < map.MapWidth; x++)
            {
                int tile = map.TileMap[x, y];
                if (tile != 0 && (tile & BitDoor) == 0 && data.Walls.TryGetValue(tile & ~BitWall, out var wall)
                    && wall.Switch is { Unlock.Length: > 0, UnlockedTo: > 0 and < BitWall } wallSwitch
                    && data.Walls.ContainsKey(wallSwitch.UnlockedTo) && unlocked(SwitchUnlockName(wallSwitch.Unlock, mapName, map.GetTag(x, y))))
                {
                    map.TileMap[x, y] = (ushort)(wallSwitch.UnlockedTo | (tile & BitWall));
                    if (wallSwitch.Objective.Length > 0)
                        done.Add(wallSwitch.Objective);
                }
                if (map.GetPatch(x, y) is var id and > 0 && data.Patches.TryGetValue(id, out var patch)
                    && patch.Switch is { Unlock.Length: > 0, UnlockedTo: > 0 and <= 255 } patchSwitch
                    && data.Patches.ContainsKey(patchSwitch.UnlockedTo) && unlocked(SwitchUnlockName(patchSwitch.Unlock, mapName, map.GetTag(x, y))))
                {
                    map.SetPatch(x, y, (byte)patchSwitch.UnlockedTo);
                    if (patchSwitch.Objective.Length > 0)
                        done.Add(patchSwitch.Objective);
                }
            }
        return done;
    }

    /// <summary>
    /// A_Exit ends the level, going to the secret level when the player stands on the floors'
    /// secret-exit code (as the elevator always did); A_SecretExit always goes to the secret
    /// level. Either waits for a sound that's playing (the switch's) to finish first.
    /// </summary>
    internal bool ExitAction(bool secret)
    {
        // Not while a level objective the exits wait for has any left (Gameplay/Objectives.cs)
        if (hooks.ExitAllowed?.Invoke() == false)
            return false;

        var player = session.Player;
        bool toSecret = secret || map.MapSpot(player.TileX, player.TileY, 0) == map.Floors.SecretExitTile;
        if (hooks.MayExit?.Invoke(toSecret ? "secret" : "exit") == false)
            return false;           // a script keeps the level going (game.onexit)
        session.PlayState = toSecret ? PlayState.SecretLevel : PlayState.Completed;

        audio.WaitSoundDone();
        return true;
    }

    /// <summary>
    /// Each tic: the facing message of the use trigger on the tile the player faces (its
    /// `facing-message`, Midnight Assault's "Press Use to Exit Level"), kept up while they face
    /// it. An exit (A_Exit, A_SecretExit) the objectives keep shut says why instead, with its
    /// refused message ("2 items left to sabotage to exit level."), as the original's
    /// CheckFacingTile. Else the `usemessage` of the actor the use key would use (in its
    /// `usemessage.style`; not on a state its usehint.except names): the beaten Commander's
    /// "Press Use to apprehend the Commander". Only for the local player, alive and not riding.
    /// </summary>
    internal void ShowFacingMessage()
    {
        if (!session.ActingIsLocal || session.Player is not { } player || session.PlayerState.Health <= 0
            || hooks.ShowFacing == null)
            return;
        if (session.PlayerState.Vehicle != null)
        {
            // riding, only getting off: the vehicle's dock message while a dock's beside them
            if (hooks.VehicleMessage?.Invoke() is { Length: > 0 } dock)
                hooks.ShowFacing(dock, null);
            return;
        }
        if (FacingMessage(player) is ({ Length: > 0 } text, var style))
            hooks.ShowFacing(text, style is { Length: > 0 } ? style : null);
    }

    /// <summary>The facing message there is now, and its style (see ShowFacingMessage); null text for none</summary>
    internal (string? Text, string? Style) FacingMessage(Entities.Actors.Actor player)
    {
        var (x, y, _) = FacingTile(player);
        if (map.InMap(x, y) && map.GetTrigger(x, y) is { IsWalkOver: false, FacingMessage.Length: > 0 } trigger)
            return (FacingText(trigger, hooks.ExitBlock?.Invoke() ?? (false, null)), trigger.FacingStyle);
        if (UsableActor() is { } actor && !OnExceptedState(actor)
            && actor.Properties.TryGetValue("usemessage", out var message) && message?.ToString() is { Length: > 0 } text)
            return (text, actor.Properties.TryGetValue("usemessage.style", out var style) ? style?.ToString() : null);
        return (null, null);
    }

    /// <summary>
    /// What a use trigger's facing message says: its own, or for an exit the objectives keep shut
    /// their refused message (nothing if they have none)
    /// </summary>
    internal static string? FacingText(MapTriggerTranslation trigger, (bool Blocked, string? Message) exitBlock)
    {
        var action = trigger.Action.TrimStart();
        bool exit = action.StartsWith("A_Exit", StringComparison.OrdinalIgnoreCase)
            || action.StartsWith("A_SecretExit", StringComparison.OrdinalIgnoreCase);
        return exit && exitBlock.Blocked ? exitBlock.Message : trigger.FacingMessage;
    }

    /// <summary>The tile the player faces, by the nearest of the four directions, and that direction</summary>
    internal static (int X, int Y, ControlDir Dir) FacingTile(Entities.Actors.Actor player)
    {
        const int ANGLES = GameConstants.Angles;
        if (player.Angle < ANGLES / 8 || player.Angle > 7 * ANGLES / 8)
            return (player.TileX + 1, player.TileY, ControlDir.East);
        if (player.Angle < 3 * ANGLES / 8)
            return (player.TileX, player.TileY - 1, ControlDir.North);
        if (player.Angle < 5 * ANGLES / 8)
            return (player.TileX - 1, player.TileY, ControlDir.West);
        return (player.TileX, player.TileY + 1, ControlDir.South);
    }

    internal void PressUse()
    {
        var playerinput = session.PlayerInput;
        var player = session.Player;
        int cmdtile;

        // Nothing's used with something latched on (Gameplay/Latching.cs)
        if (session.PlayerState.Latch != null)
            return;

        // find which cardinal direction the player is facing
        var (checkx, checky, dir) = FacingTile(player);

        // Manning a mounted gun, a fresh press (not while firing) steps them off it (Gameplay/MountedWeapons.cs)
        if (!playerinput.IsHeld(ButtonType.Use) && !playerinput.IsPressed(ButtonType.Attack) && hooks.Dismount())
        {
            playerinput.SetHeld(ButtonType.Use, true);
            return;
        }

        // Riding, use works a door ahead, else gets them off beside them, and does nothing else
        // (Gameplay/Vehicles.cs)
        if (session.PlayerState.Vehicle != null)
        {
            if (!playerinput.IsHeld(ButtonType.Use))
            {
                playerinput.SetHeld(ButtonType.Use, true);
                if (map.InMap(checkx, checky) && map.TileMap[checkx, checky] is var door && (door & BitDoor) != 0)
                    doors.OperateDoor(door & ~BitDoor);
                else
                    hooks.LeaveVehicle?.Invoke();
            }
            return;
        }

        // A fresh press: a script may use the tile ahead itself (game.onuse), and then nothing else is
        if (!playerinput.IsHeld(ButtonType.Use) && hooks.ScriptUse?.Invoke(checkx, checky, dir) == true)
        {
            playerinput.SetHeld(ButtonType.Use, true);
            ResetUseHeld();
            return;
        }

        cmdtile = map.TileMap[checkx, checky];
        if (map.GetTrigger(checkx, checky) is { IsWalkOver: false } trigger)
        {
            //
            // mapdefs use trigger (a pushable wall): once a press, not every frame use is held,
            // for one that stays (`repeat`: a ladder) or refuses (a one-way pushwall's message)
            //
            ResetUseHeld();
            if (playerinput.IsHeld(ButtonType.Use))
                return;
            playerinput.SetHeld(ButtonType.Use, true);
            ActivateTrigger(trigger, checkx, checky, dir);
            return;
        }
        if (!playerinput.IsHeld(ButtonType.Use) && PatchSwitchAt(checkx, checky, dir) is { } patchSwitch)
        {
            //
            // use a patch that's a switch (patches.yaml), over the wall's own
            //
            playerinput.SetHeld(ButtonType.Use, true);
            UsePatchSwitch(patchSwitch, checkx, checky, dir);
        }
        // A wall beside a door keeps its id under BitWall; a moving pushwall's tiles are bare BitWall
        else if (!playerinput.IsHeld(ButtonType.Use) && (cmdtile & BitDoor) == 0
            && map.GetMapData().Walls.TryGetValue(cmdtile & ~BitWall, out var switchWall)
            && switchWall.Switch is { } wallSwitch && wallSwitch.UsableFrom(dir))
        {
            //
            // use a switch (a mapdefs wall with a switch, such as the elevator's)
            //
            playerinput.SetHeld(ButtonType.Use, true);
            UseSwitch(wallSwitch, checkx, checky, dir);
        }
        else if (!playerinput.IsHeld(ButtonType.Use) && (cmdtile & BitDoor) != 0)
        {
            playerinput.SetHeld(ButtonType.Use, true);
            doors.OperateDoor(cmdtile & ~BitDoor);
        }
        else
        {
            // Nothing ahead to use: the match mode's own use (a Prop Hunt disguise), else use an
            // actor that's there to be used (its Use state), else talk to whoever's there
            // (BlakeMonster.TalkTo), else held long enough, use an item that's used that way
            // (Planet Strike's fission detonator)
            if (hooks.ModeUse?.Invoke() == true || TryUseActor() || levelAI.TryTalk())
                ResetUseHeld();
            else
                UseHeldItem();
            return;
        }
        ResetUseHeld();
    }

    /// <summary>
    /// The use key pressed facing an actor with a Use state (actordefs), within a tile and a half
    /// and half a right angle either side of straight ahead, or on the player's own tile: it goes
    /// to that state (a prisoner's could be A_SetFaction("allies", "follow"); a log book opens).
    /// An enemy only while it lives and isn't going after someone; scenery any time it has one.
    /// </summary>
    internal bool TryUseActor()
    {
        var playerinput = session.PlayerInput;
        if (playerinput.IsHeld(ButtonType.Use) || UsableActor() is not { } chosen)
            return false;
        playerinput.SetHeld(ButtonType.Use, true);
        chosen.SetState("Use");
        return true;
    }

    /// <summary>The actor the use key would use now (see TryUseActor), or null</summary>
    internal Entities.Actors.Actor? UsableActor()
    {
        const int ANGLES = GameConstants.Angles;
        var player = session.Player;
        const long Reach = MapConstants.TileGlobal * 3 / 2;
        Entities.Actors.Actor? chosen = null;
        long best = Reach;
        foreach (var actor in map.GetActors())
        {
            if (actor.IsRemoved || actor is Entities.Actors.PlayerPawn || !actor.ResolvedStates.ContainsKey("Use"))
                continue;
            // The gun the player's manning stands on their tile; use steps them off it instead
            if (actor == session.PlayerState.Mount)
                continue;
            if (actor is Entities.Actors.Monster && (!actor.RuntimeFlags.Has(ObjFlags.Shootable)
                || actor.RuntimeFlags.Has(ObjFlags.AttackMode)))
                continue;

            long dx = actor.X - player.X, dy = player.Y - actor.Y;     // y grows southward
            long dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (dist >= best)
                continue;

            bool underfoot = actor.TileX == player.TileX && actor.TileY == player.TileY;
            int toward = (int)((Math.Atan2(dy, dx) + Math.PI * 2) % (Math.PI * 2) / (Math.PI * 2) * ANGLES);
            int off = Math.Abs(player.Angle - toward) % ANGLES;
            if (!underfoot && Math.Min(off, ANGLES - off) > ANGLES / 8)
                continue;

            (chosen, best) = (actor, dist);
        }
        return chosen;
    }

    /*
    =============================================================================

    USE HINTS

    What the use key would do, shown as a hint (statusbar.yaml use-hint draws it by name:
    Midnight Assault's indicators, CheckFacingTile). Looked for as the use key looks: a use
    trigger on the tile the player faces (its `hint`), a patch on it, the wall itself, then an
    actor there to be used (its `usehint`, but not on a state its `usehint.except` names: an
    emptied uniform rack). A patch or wall that's a switch only gives its hint from a side it
    can be used from. Last, the weapon in hand's `weapon.backhint` while an enemy
    in its reach is unaware with its back turned (Gameplay/PlayerAttacks.cs BackstabTarget), or
    its `weapon.facehint` while one faces the player (Midnight Assault's stealth kill and papers).

    =============================================================================
    */

    // On a state its `usehint.except` names (one, or a list): a radio already listened to, an
    // empty uniform rack, which can still be used but give no hint
    static bool OnExceptedState(Entities.Actors.Actor actor)
    {
        if (actor.CurrentState?.StateName is not { } state || !actor.Properties.TryGetValue("usehint.except", out var except) || except == null)
            return false;
        var names = except is IEnumerable<object> list ? list.Select(o => o?.ToString() ?? "")
            : except.ToString()!.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return names.Any(name => name.Equals(state, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The hint the use key's target gives now (see USE HINTS), or null for none</summary>
    internal string? UseHint(PlayerAttacks? attacks)
    {
        var player = session.Player;
        if (player == null || session.PlayerState.Health <= 0 || session.PlayerState.Latch != null)
            return null;
        if (session.PlayerState.Vehicle != null)
            return hooks.VehicleHint?.Invoke();       // riding, only getting off (Gameplay/Vehicles.cs)

        string? hint = null;
        var (x, y, dir) = FacingTile(player);
        if (map.InMap(x, y))
        {
            int tile = map.TileMap[x, y];
            var data = map.GetMapData();
            if (map.GetTrigger(x, y) is { IsWalkOver: false, Hint.Length: > 0 } trigger)
                hint = trigger.Hint;
            else if ((tile & BitDoor) == 0 && (tile & ~BitWall) != 0 && map.GetPatch(x, y) is var id and > 0
                && data.Patches.TryGetValue(id, out var patch) && patch.Hint.Length > 0
                && (patch.Switch == null || PatchSwitchAt(x, y, dir) != null))
                hint = patch.Hint;
            else if ((tile & BitDoor) == 0 && data.Walls.TryGetValue(tile & ~BitWall, out var wall) && wall.Hint.Length > 0
                && (wall.Switch == null || wall.Switch.UsableFrom(dir)))
                hint = wall.Hint;
        }
        if (hint == null && UsableActor() is { } actor && actor.Properties.TryGetValue("usehint", out var usehint)
            && usehint?.ToString() is { Length: > 0 } actorHint && !OnExceptedState(actor))
            hint = actorHint;

        if (attacks != null && session.PlayerState.Weapon is { } weapon)
        {
            if (inventory.GetStringProperty(weapon, "weapon.backhint") is { Length: > 0 } back && attacks.BackstabTarget() != null)
                hint = back;
            else if (inventory.GetStringProperty(weapon, "weapon.facehint") is { Length: > 0 } face && attacks.FacingTarget() != null)
                hint = face;
        }
        return hint;
    }

    /// <summary>
    /// An empty weapon slot's key uses an item held for that slot (`inventory.useslot`,
    /// `inventory.use`). With none held, the slot's item class says so if it has an
    /// `inventory.usenone` message (Midnight Assault's "You do not have any medkits.")
    /// </summary>
    internal void UseSlotItem(int slot)
    {
        if (HeldUsableItem(item => inventory.GetIntProperty(item, "inventory.useslot", -1) == slot) is { } action)
            UseItemAction(action);
        else if (NoneHeldMessage(slot) is { } message)
            hooks.Message?.Invoke(message);
    }

    /// <summary>
    /// The `inventory.usenone` message of the item class a slot's key uses, if it has one; else,
    /// when the player has no weapon in that slot at all, a `weapon.notheld` message of one of the
    /// slot's weapons (End of Destiny's "You don't have this weapon"). A weapon held but out of
    /// ammo says nothing.
    /// </summary>
    internal string? NoneHeldMessage(int slot) =>
        inventory.GetClassesDerivedFrom("Inventory")
            .Where(item => inventory.GetIntProperty(item, "inventory.useslot", -1) == slot)
            .Select(item => inventory.GetStringProperty(item, "inventory.usenone"))
            .FirstOrDefault(message => !string.IsNullOrEmpty(message))
        ?? NotHeldMessage(slot);

    string? NotHeldMessage(int slot)
    {
        var inSlot = inventory.GetClassesDerivedFrom("Weapon")
            .Where(weapon => inventory.GetIntProperty(weapon, "weapon.slot", -1) == slot).ToList();
        if (inSlot.Any(inventory.Has))
            return null;
        return inSlot.Select(weapon => inventory.GetStringProperty(weapon, "weapon.notheld"))
            .FirstOrDefault(message => !string.IsNullOrEmpty(message));
    }

    /// <summary>
    /// Use held with nothing ahead to use or talk to: every `inventory.useholdtics` tics of it,
    /// an item held with that property is used (bstone drops the fission detonator this way,
    /// every 60 tics)
    /// </summary>
    internal void UseHeldItem()
    {
        int hold = 0;
        if (HeldUsableItem(item => (hold = inventory.GetIntProperty(item, "inventory.useholdtics", 0)) > 0) is not { } action)
        {
            useHeldTics = 0;
            return;
        }
        useHeldTics += (int)session.Tics;
        if (useHeldTics < hold)
            return;
        useHeldTics = 0;
        UseItemAction(action);
    }

    internal void ResetUseHeld() => useHeldTics = 0;

    /// <summary>The `inventory.use` action of the first item held that matches</summary>
    internal string? HeldUsableItem(Func<string, bool> match)
    {
        foreach (var (item, count) in inventory.Items.ToList())
            if (count > 0 && match(item) && inventory.GetStringProperty(item, "inventory.use") is { Length: > 0 } action)
                return action;
        return null;
    }

    void UseItemAction(string action)
    {
        var player = session.Player;
        Entities.MapTriggerRegistry.Invoke(action,
            new Entities.TriggerActivation(player.TileX, player.TileY, PlayerMovement.FacingDir(player.Angle), player, 0));
    }
}
