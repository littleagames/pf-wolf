using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Gameplay;

/// <summary>What the hub clusters' trigger actions need from the game</summary>
/// <param name="SetPendingMapChange">The map to switch to as the level ends (Gameplay/MainGameLoop.cs)</param>
/// <param name="PlaySoundAt">Plays a sound from an actor</param>
internal sealed record HubActionsHooks(
    Action<PendingMapChange> SetPendingMapChange,
    Action<string, Entities.Actors.Actor> PlaySoundAt);

/// <summary>
/// The hub clusters' ways between floors and out, as mapdefs trigger actions: Blake Stone's
/// elevator panel and Planet Strike's teleporter (A_FloorSelect, on the screens in
/// Ui/ElevatorScreens.cs), teleporter walls (A_Teleport), floors unlocked (A_UnlockFloor) and
/// the fission detonator dropped by a floor's security cube (A_DropDetonator), and the win
/// floor's run to the shuttle (A_VictoryRun, T_VictoryRun).
/// </summary>
/// <remarks>
/// A_FloorSelect (a switch action, on Blake Stone's elevator panel wall) shows the floor-select
/// screen from gamepacks/{pack}/elevator.yaml (bstone's InputFloor). Its buttons are the floors
/// of the level's hub cluster by game-info floor-number. Arrows (or turning) move the selector,
/// space, enter or fire press its button, a number key presses that floor's (0 is floor 10),
/// and Esc leaves. A floor the player has been on is open; the next floor up past the highest
/// open one opens with the unlock item, used up as it does. Choosing a floor leaves the level
/// (Warped); the player comes out in the same elevator on the new floor.
/// <para/>
/// Planet Strike's way between floors (bstone's ps_input_floor): its teleporter wall runs
/// A_FloorSelect as the elevator's does, and an elevator.yaml with `style: teleporter` shows
/// the teleporter map instead of the panel. Up and down (or left and right) move through the
/// cluster's floors by floor number; Enter goes, Esc leaves. A floor is open once the player
/// has been on it or it's been unlocked (A_UnlockFloor, when a floor's security cube is
/// destroyed); others show "teleport disabled". Each floor's overhead map is kept as it was
/// when the player left it (Gameplay/Hubs.cs FloorOverheads).
/// <para/>
/// The fission detonator (an item with `inventory.useslot` and `inventory.use`) is dropped by
/// its slot key: A_DropDetonator, beside the floor's security cube, with the next floor still
/// locked. It goes off a few seconds later, breaking the cube (`monster.blastedby`), whose
/// death unlocks the next floor.
/// </remarks>
internal sealed class HubActions(
    GameEngineManager engine,
    MapManager mapManager,
    AudioManager audio,
    InputManager input,
    InventoryManager inventory,
    HudMessageManager hudMessages,
    Hubs hubs,
    ElevatorScreens elevatorScreens,
    PlayScreen playScreen,
    FrameRefresh frame,
    PlayerMovement movement,
    GameSession session,
    HubActionsHooks hooks)
{
    internal void Register()
    {
        // Hub clusters' ways between levels
        Entities.MapTriggerRegistry.Register("A_FloorSelect", FloorSelectAction);
        Entities.MapTriggerRegistry.Register("A_Teleport", TeleportAction);
        Entities.MapTriggerRegistry.Register("A_TeleportTag", TeleportTagAction);
        Entities.MapTriggerRegistry.Register("A_VictoryRun", VictoryRunAction);
        Entities.Actors.ActorActionRegistry.Register("T_VictoryRun", ThinkVictoryRun);

        // A_UnlockFloor: the cluster's next floor up (by floor number) opens in the teleporter
        Entities.MapTriggerRegistry.Register("A_UnlockFloor", (_, args) => UnlockNextFloor(args.ElementAtOrDefault(0)));
        Entities.Actors.ActorActionRegistry.Register("A_UnlockFloor", UnlockFloor);

        // A_DropDetonator("ArmedClass", "CubeClass", "ItemClass"[, range]): the fission detonator, dropped by its slot
        // key beside the floor's security cube with the next floor still locked, which takes one ItemClass from the
        // inventory; it goes off a few seconds later, breaking the cube (`monster.blastedby`), whose death unlocks the
        // next floor
        Entities.MapTriggerRegistry.Register("A_DropDetonator", DropDetonatorAction);
    }
    bool FloorSelectAction(Entities.TriggerActivation trigger, string[] args)
    {
        if (elevatorScreens.Elevator is not { } elevator)
            return false;

        int panelWall = mapManager.MapSpot(trigger.TileX, trigger.TileY, 0);

        // Planet Strike's teleporter map (Ui/ElevatorScreens.cs): the player comes out at the
        // new floor's start
        bool teleporter = elevator.Style.Equals("teleporter", StringComparison.OrdinalIgnoreCase) && elevator.Teleporter != null;
        var map = teleporter ? elevatorScreens.TeleporterScreen(elevator, elevator.Teleporter!) : elevatorScreens.FloorSelectScreen(elevator);
        if (map == null)
        {
            playScreen.DrawPlayScreen();
            frame.ThreeDRefresh();
            input.ClearKeysDown();
            return true;
        }

        hooks.SetPendingMapChange(teleporter
            ? new PendingMapChange(map, false, 0, 0, 0, WaitTics: 0)
            : new PendingMapChange(map, true, session.Player.X, session.Player.Y, session.Player.Angle, WaitTics: 0, ElevatorWall: panelWall));
        session.GameState.MapOn = map;
        session.GameState.Entrance = "";       // by no entrance in particular: the level's own start
        session.PlayState = PlayState.Warped;
        return true;
    }

    /*
    =============================================================================

                                TELEPORTERS

    =============================================================================
    */

    /// <summary>
    /// A_Teleport: Blake Stone's teleporter wall (32), by the object-plane value on it. 0xF4nn
    /// takes the player to floor nn of the cluster, where they come out at the same spot (the
    /// floors' teleporters line up); 0xF5nn moves them to the tile on this floor that the
    /// object-plane value after it gives, its x in the high byte and y in the low byte.
    /// </summary>
    bool TeleportAction(Entities.TriggerActivation trigger, string[] args)
    {
        int value = mapManager.MapSpot(trigger.TileX, trigger.TileY, 1);
        if (value == 0)
            return false;

        audio.Play("bs/warpin");
        if ((value & 0xff00) == 0xf400)
        {
            int floor = value & 0xff;
            var gameInfo = engine.GetGameInfo();
            var cluster = hubs.ClusterOf(session.GameState.MapOn);
            var map = gameInfo.Maps.FirstOrDefault(m => m.Value.Cluster == cluster && m.Value.FloorNumber == floor && !string.IsNullOrEmpty(m.Value.Music)).Key;
            if (map == null || map.Equals(session.GameState.MapOn, StringComparison.OrdinalIgnoreCase))
                return false;

            hooks.SetPendingMapChange(new PendingMapChange(map, true, session.Player.X, session.Player.Y, session.Player.Angle, WaitTics: 0));
            session.GameState.MapOn = map;
            session.GameState.Entrance = "";       // by no entrance in particular: the level's own start
            session.PlayState = PlayState.Warped;
            return true;
        }

        // 0xF5nn (mapinfo "value") keeps the tile in the object-plane tile after it
        if ((value & 0xff00) == 0xf500)
            value = mapManager.MapSpot(trigger.TileX + 1, trigger.TileY, 1);

        int x = value >> 8, y = value & 0xff;
        if (!mapManager.InMap(x, y))
            return false;
        session.Player.SetPosition(x, y);
        movement.Thrust(0, 0);
        return true;
    }

    /// <summary>
    /// A_TeleportTag(["beside"][, "sound"]): a pair of tiles that lead to each other by their
    /// tag (Midnight Assault's teleports, stairs and ladders, source/ma_teleport.c). The player goes
    /// to the other tile with this trigger's tag whose trigger runs A_TeleportTag too: onto it, or with "beside"
    /// (a ladder's other end, which is solid) onto the first open tile next to it, tried from the
    /// side they come out facing away from. The sound, if given, plays as they go. Give the
    /// trigger `repeat: true` to use it again; arriving on a walk-over one doesn't set it off.
    /// </summary>
    bool TeleportTagAction(Entities.TriggerActivation trigger, string[] args)
    {
        if (trigger.Tag == 0)
        {
            Console.WriteLine($"A_TeleportTag at ({trigger.TileX},{trigger.TileY}): the tile has no tag to find its other end by.");
            return false;
        }

        // Its other end runs A_TeleportTag too (a tag can also be a switch's, whose walls have triggers of their own)
        var partner = mapManager.TaggedTiles(trigger.Tag)
            .Where(t => (t.X, t.Y) != (trigger.TileX, trigger.TileY)
                && mapManager.GetTrigger(t.X, t.Y) is { } other
                && Entities.Actors.ActorActionRegistry.Parse(other.Action).Name == "A_TeleportTag")
            .Select(t => ((int X, int Y)?)t)
            .FirstOrDefault();
        if (partner is not { } to)
        {
            Console.WriteLine($"A_TeleportTag at ({trigger.TileX},{trigger.TileY}): no other trigger has tag {trigger.Tag}.");
            return false;
        }

        bool beside = args.Any(a => a.Trim().Equals("beside", StringComparison.OrdinalIgnoreCase));
        var sound = args.Select(a => a.Trim()).FirstOrDefault(a => a.Length > 0 && !a.Equals("beside", StringComparison.OrdinalIgnoreCase));

        if (beside)
        {
            // The original's CHECKEXITSPOT order: the side behind the player as they face, then
            // either hand, then ahead
            (int dx, int dy)[] order = trigger.Dir switch
            {
                ControlDir.East => [(-1, 0), (0, -1), (0, 1), (1, 0)],
                ControlDir.North => [(0, 1), (1, 0), (-1, 0), (0, -1)],
                ControlDir.West => [(1, 0), (0, -1), (0, 1), (-1, 0)],
                _ => [(0, -1), (1, 0), (-1, 0), (0, 1)],
            };
            var spot = order.Select(d => (X: to.X + d.dx, Y: to.Y + d.dy))
                .Select(t => ((int X, int Y)?)t)
                .FirstOrDefault(t => IsOpenForPlayer(t!.Value.X, t.Value.Y));
            if (spot is not { } open)
                return false;
            to = open;
        }

        if (!string.IsNullOrEmpty(sound))
            audio.Play(sound);
        session.Player.SetPosition(to.X, to.Y);
        movement.Thrust(0, 0);
        return true;
    }

    /// <summary>Whether the player can stand on a tile: on the map, no wall or door, nothing solid or alive on it</summary>
    bool IsOpenForPlayer(int x, int y) =>
        x > 0 && y > 0 && x < mapManager.MapWidth - 1 && y < mapManager.MapHeight - 1
        && mapManager.TileMap[x, y] == 0
        && mapManager.ActorAt[x, y] == null
        && !mapManager.IsShootableActorAt(x, y);

    /*
    =============================================================================

                                    VICTORY

    =============================================================================
    */

    /// <summary>
    /// A_VictoryRun("Class", wall): the player has reached the way out (Blake Stone's win
    /// floor). The view turns toward the map's first tile of the given wall (the shuttle), and
    /// the class (Blake) runs past the player that way; six tiles on, the cluster is won.
    /// </summary>
    bool VictoryRunAction(Entities.TriggerActivation trigger, string[] args)
    {
        if (session.GameState.VictoryFlag || args.Length == 0)
            return false;

        int wall = args.Length > 1 && int.TryParse(args[1], out var w) ? w : 0;
        for (int y = 0; y < mapManager.MapHeight && wall > 0; y++)
        {
            for (int x = 0; x < mapManager.MapWidth; x++)
            {
                if (mapManager.MapSpot(x, y, 0) != wall)
                    continue;
                double dx = (x << MapConstants.TileShift) + MapConstants.TileGlobal / 2 - session.Player.X;
                double dy = session.Player.Y - ((y << MapConstants.TileShift) + MapConstants.TileGlobal / 2);
                var angle = Math.Atan2(dy, dx);
                if (angle < 0)
                    angle += Math.PI * 2;
                RotateView((int)(angle / (Math.PI * 2) * GameConstants.Angles), 2);
                y = mapManager.MapHeight;
                break;
            }
        }

        session.GameState.VictoryFlag = true;
        VictoryRunning = true;
        var runner = mapManager.SpawnAtActor(args[0], session.Player);
        if (runner == null)
        {
            session.PlayState = PlayState.Victorious;
            return true;
        }

        // From two tiles behind the player
        double a = session.Player.Angle * Math.PI / 180;
        runner.X = session.Player.X - (int)(Math.Cos(a) * 2 * MapConstants.TileGlobal);
        runner.Y = session.Player.Y + (int)(Math.Sin(a) * 2 * MapConstants.TileGlobal);
        runner.TileX = (byte)(runner.X >> MapConstants.TileShift);
        runner.TileY = (byte)(runner.Y >> MapConstants.TileShift);
        runner.SyncPosition();
        runner.Temp1 = runner.TileX;       // where it set off from
        runner.Temp3 = runner.TileY;
        runner.Temp2 = session.Player.Angle;
        return true;
    }

    /// <summary>A_VictoryRun's runner is on its way: the player watches it rather than turning round (T_Player)</summary>
    internal bool VictoryRunning;

    /// <summary>T_VictoryRun: runs the way the player faced (Temp2); six tiles on from where it set off (Temp1, Temp3), or at a wall, the cluster is won</summary>
    internal void ThinkVictoryRun(Entities.Actors.Actor ob)
    {
        const int speed = 3000;
        double a = ob.Temp2 * Math.PI / 180;
        int nx = ob.X + (int)(Math.Cos(a) * speed * session.Tics);
        int ny = ob.Y - (int)(Math.Sin(a) * speed * session.Tics);
        int tx = nx >> MapConstants.TileShift, ty = ny >> MapConstants.TileShift;

        // Starting behind the player can be inside a wall: it runs on out of it, and only a wall
        // it runs into stops it
        bool IsWall(int x, int y) => mapManager.TileMap[x, y] != 0 && (mapManager.TileMap[x, y] & GameConstants.BitDoor) == 0;
        bool blocked = !mapManager.InMap(tx, ty)
            || (IsWall(tx, ty) && !IsWall(ob.TileX, ob.TileY));
        if (!blocked)
        {
            ob.X = nx;
            ob.Y = ny;
            ob.TileX = (byte)tx;
            ob.TileY = (byte)ty;
            ob.SyncPosition();
        }

        if (blocked || Math.Abs(ob.TileX - ob.Temp1) >= 6 || Math.Abs(ob.TileY - ob.Temp3) >= 6)
            session.PlayState = PlayState.Victorious;
    }

    /// <summary>Turns the view to <paramref name="destAngle"/> at <paramref name="speed"/> degrees a tic, drawing as it goes</summary>
    void RotateView(int destAngle, int speed)
    {
        destAngle = ((destAngle % GameConstants.Angles) + GameConstants.Angles) % GameConstants.Angles;
        int diff = destAngle - session.Player.Angle;
        if (diff > GameConstants.Angles / 2) diff -= GameConstants.Angles;
        if (diff < -GameConstants.Angles / 2) diff += GameConstants.Angles;

        while (diff != 0)
        {
            int step = Math.Min(Math.Abs(diff), Math.Max(1, (int)(speed * session.Tics))) * Math.Sign(diff);
            diff -= step;
            session.Player.Angle = (short)(((session.Player.Angle + step) % GameConstants.Angles + GameConstants.Angles) % GameConstants.Angles);
            frame.ThreeDRefresh();
            frame.CalcTics();
        }
    }

    void UnlockFloor(Entities.Actors.Actor ob, string[] args) => UnlockNextFloor(args.ElementAtOrDefault(0));

    bool UnlockNextFloor(string? message)
    {
        if (hubs.ClusterFloorMap(hubs.CurrentFloorNumber() + 1) is not { } next || hubs.FloorOpen(next))
            return false;
        hubs.UnlockedMaps.Add(next);
        hudMessages.Show(HudMessageKind.Other, string.IsNullOrEmpty(message) ? "$PS_FLOORUNLOCKED" : message);
        audio.Play("bs/roll_score");
        return true;
    }

    bool DropDetonatorAction(Entities.TriggerActivation trigger, string[] args)
    {
        var armed = args.ElementAtOrDefault(0);
        var cubeClass = args.ElementAtOrDefault(1);
        var itemClass = args.ElementAtOrDefault(2);
        int range = int.TryParse(args.ElementAtOrDefault(3), out var r) ? r : 2;
        if (string.IsNullOrEmpty(armed) || string.IsNullOrEmpty(cubeClass) || string.IsNullOrEmpty(itemClass))
        {
            Console.WriteLine("A_DropDetonator: give the armed, cube and item classes, as A_DropDetonator(\"PlasmaDetonatorArmed\", \"SecurityCube\", \"PlasmaDetonator\").");
            return false;
        }

        void Say(string text) => hudMessages.Show(HudMessageKind.Other, text);

        var next = hubs.ClusterFloorMap(hubs.CurrentFloorNumber() + 1);
        if (next == null || hubs.FloorOpen(next))
        {
            Say("$PS_FLOORNOTLOCKED");
            return false;
        }

        var cube = mapManager.GetActors().FirstOrDefault(a => !a.IsRemoved && a.Name.Equals(cubeClass, StringComparison.OrdinalIgnoreCase)
            && a.CurrentState?.StateName != "Death");
        if (cube == null)
        {
            Say("$PS_NOCOMPUTER");
            return false;
        }
        if (cube.AreaNumber != session.Player.AreaNumber && SpawnAreaOf(cube) != mapManager.SpawnArea(session.Player.TileX, session.Player.TileY))
        {
            Say("$PS_NOTNEAR");
            return false;
        }
        if (Math.Max(Math.Abs(session.Player.TileX - cube.TileX), Math.Abs(session.Player.TileY - cube.TileY)) > range)
        {
            Say("$PS_GETCLOSER");
            return false;
        }

        var dropped = mapManager.SpawnAtActor(armed, session.Player);
        if (dropped == null)
            return false;
        inventory.Take(itemClass, 1);
        Say("$PS_DETONATORDROPPED");
        hooks.PlaySoundAt("bs/robot_servo", dropped);
        return true;
    }

    byte SpawnAreaOf(Entities.Actors.Actor a) => mapManager.SpawnArea(a.TileX, a.TileY);
}
