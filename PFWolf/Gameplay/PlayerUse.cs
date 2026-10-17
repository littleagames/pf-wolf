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
internal sealed record PlayerUseHooks(
    Action<string> PlayPlayerSound,
    Func<bool> Dismount,
    Action<string, int>? CompleteObjective = null,
    Func<bool>? ExitAllowed = null);

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
    const int BIT_WALL = GameConstants.BIT_WALL;
    const int BIT_DOOR = GameConstants.BIT_DOOR;

    // How long use has been held with nothing to use or talk to
    int useHeldTics;

    /// <summary>
    /// Runs a mapdefs trigger's action. If it goes off, a secret trigger counts as found, and the
    /// trigger's tile is cleared so it can't go off again (unless it's a `repeat` one).
    /// </summary>
    internal void ActivateTrigger(MapTriggerTranslation trigger, int tilex, int tiley, ControlDir dir)
    {
        var activation = new Entities.TriggerActivation(tilex, tiley, dir, session.player, map.GetTag(tilex, tiley));
        if (!Entities.MapTriggerRegistry.Invoke(trigger.Action, activation))
            return;

        if (trigger.Secret)
            session.gamestate.secretcount++;
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

        var gamestate = session.gamestate;
        var tag = map.GetTag(tilex, tiley);
        if (wallSwitch.To is > 0 and < BIT_WALL && map.GetMapData().Walls.ContainsKey(wallSwitch.To))
        {
            // flip the switch, keeping the door-side mark on a wall beside a door
            var tile = map.tilemap[tilex, tiley];
            int from = tile & ~BIT_WALL;
            map.tilemap[tilex, tiley] = (ushort)(wallSwitch.To | (tile & BIT_WALL));
            map.RestartAnimations(tilex, tiley, gamestate.TimeCount);   // a `to` wall's play-once animation starts now
            int thrown = from != wallSwitch.To ? 1 : 0;

            // and the linked ones: set to the new way, not toggled, so any out of step come back
            foreach (var (x, y) in map.TaggedTiles(tag))
            {
                var other = map.tilemap[x, y];
                int id = other & ~BIT_WALL;
                if ((other & BIT_DOOR) == 0 && (id == from || id == wallSwitch.To))
                {
                    if (id == from && id != wallSwitch.To && (x, y) != (tilex, tiley))
                        thrown++;
                    map.tilemap[x, y] = (ushort)(wallSwitch.To | (other & BIT_WALL));
                    map.RestartAnimations(x, y, gamestate.TimeCount);
                }
            }
            timedSwitches.Thrown(wallSwitch, tilex, tiley, patch: false, from, wallSwitch.To, tag, dir);     // reset-tics: it turns back
            if (wallSwitch.Objective is { Length: > 0 } objective && thrown > 0)
                hooks.CompleteObjective?.Invoke(objective, thrown);     // a sabotage target (Gameplay/Objectives.cs)
        }

        if (!string.IsNullOrEmpty(wallSwitch.Sound))
            hooks.PlayPlayerSound(wallSwitch.Sound);

        var activation = new Entities.TriggerActivation(tilex, tiley, dir, session.player, tag);
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
    /// The switch of the patch on the wall tile (tilex, tiley) the player faces going dir, if
    /// it's on the face they press (facing east presses the west face) and usable from there
    /// </summary>
    internal MapSwitchTranslation? PatchSwitchAt(int tilex, int tiley, ControlDir dir)
    {
        int tile = map.tilemap[tilex, tiley];
        if ((tile & BIT_DOOR) != 0 || (tile & ~BIT_WALL) == 0)
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

        if (!string.IsNullOrEmpty(patchSwitch.Sound))
            hooks.PlayPlayerSound(patchSwitch.Sound);

        var tag = map.GetTag(tilex, tiley);
        var activation = new Entities.TriggerActivation(tilex, tiley, dir, session.player, tag);
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
            map.RestartAnimations(x, y, session.gamestate.TimeCount);    // a `to` patch's play-once animation starts now
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

        var player = session.player;
        if (secret || map.MAPSPOT(player.TileX, player.TileY, 0) == map.Floors.SecretExitTile)
            session.playstate = PlayState.SecretLevel;
        else
            session.playstate = PlayState.Completed;

        audio.WaitSoundDone();
        return true;
    }

    internal void PressUse()
    {
        const int ANGLES = GameConstants.ANGLES;
        var playerinput = session.playerinput;
        var player = session.player;
        int checkx, checky, cmdtile;
        ControlDir dir;

        // Nothing's used with something latched on (Gameplay/Latching.cs)
        if (session.playerstate.Latch != null)
            return;

        //
        // find which cardinal direction the player is facing
        //
        if (player.Angle < ANGLES / 8 || player.Angle > 7 * ANGLES / 8)
        {
            checkx = player.TileX + 1;
            checky = player.TileY;
            dir = ControlDir.East;
        }
        else if (player.Angle < 3 * ANGLES / 8)
        {
            checkx = player.TileX;
            checky = player.TileY - 1;
            dir = ControlDir.North;
        }
        else if (player.Angle < 5 * ANGLES / 8)
        {
            checkx = player.TileX - 1;
            checky = player.TileY;
            dir = ControlDir.West;
        }
        else
        {
            checkx = player.TileX;
            checky = player.TileY + 1;
            dir = ControlDir.South;
        }

        // Manning a mounted gun, a fresh press (not while firing) steps them off it (Gameplay/MountedWeapons.cs)
        if (!playerinput.IsHeld(ButtonType.Use) && !playerinput.IsPressed(ButtonType.Attack) && hooks.Dismount())
        {
            playerinput.SetHeld(ButtonType.Use, true);
            return;
        }

        cmdtile = map.tilemap[checkx, checky];
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
        // A wall beside a door keeps its id under BIT_WALL; a moving pushwall's tiles are bare BIT_WALL
        else if (!playerinput.IsHeld(ButtonType.Use) && (cmdtile & BIT_DOOR) == 0
            && map.GetMapData().Walls.TryGetValue(cmdtile & ~BIT_WALL, out var switchWall)
            && switchWall.Switch is { } wallSwitch && wallSwitch.UsableFrom(dir))
        {
            //
            // use a switch (a mapdefs wall with a switch, such as the elevator's)
            //
            playerinput.SetHeld(ButtonType.Use, true);
            UseSwitch(wallSwitch, checkx, checky, dir);
        }
        else if (!playerinput.IsHeld(ButtonType.Use) && (cmdtile & BIT_DOOR) != 0)
        {
            playerinput.SetHeld(ButtonType.Use, true);
            doors.OperateDoor(cmdtile & ~BIT_DOOR);
        }
        else
        {
            // Nothing ahead to use: use an actor that's there to be used (its Use state), else
            // talk to whoever's there (BlakeMonster.TalkTo), else held long enough, use an item
            // that's used that way (Planet Strike's fission detonator)
            if (TryUseActor() || levelAI.TryTalk())
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
        const int ANGLES = GameConstants.ANGLES;
        var playerinput = session.playerinput;
        var player = session.player;
        if (playerinput.IsHeld(ButtonType.Use))
            return false;

        const long Reach = MapConstants.TILEGLOBAL * 3 / 2;
        Entities.Actors.Actor? chosen = null;
        long best = Reach;
        foreach (var actor in map.GetActors())
        {
            if (actor.IsRemoved || actor is Entities.Actors.PlayerPawn || !actor.ResolvedStates.ContainsKey("Use"))
                continue;
            if (actor is Entities.Actors.Monster && (!actor.RuntimeFlags.HasFlag(ObjFlags.Shootable)
                || actor.RuntimeFlags.HasFlag(ObjFlags.AttackMode)))
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

        if (chosen == null)
            return false;
        playerinput.SetHeld(ButtonType.Use, true);
        chosen.SetState("Use");
        return true;
    }

    /// <summary>An empty weapon slot's key uses an item held for that slot (`inventory.useslot`, `inventory.use`)</summary>
    internal void UseSlotItem(int slot)
    {
        if (HeldUsableItem(item => inventory.GetIntProperty(item, "inventory.useslot", -1) == slot) is { } action)
            UseItemAction(action);
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
        useHeldTics += (int)session.tics;
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
        var player = session.player;
        Entities.MapTriggerRegistry.Invoke(action,
            new Entities.TriggerActivation(player.TileX, player.TileY, PlayerMovement.FacingDir(player.Angle), player, 0));
    }
}
