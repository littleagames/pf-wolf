using PFWolf.Assets;
using PFWolf.Managers;

namespace PFWolf;

internal partial class Program
{
    /*
    =============================================================================

                                    TELEPORTER

    Planet Strike's way between floors (bstone's ps_input_floor): its teleporter wall runs
    A_FloorSelect as the elevator's does, and an elevator.yaml with `style: teleporter` shows
    the teleporter map instead of the panel. Up and down (or left and right) move through the
    cluster's floors by floor number; Enter goes, Esc leaves. A floor is open once the player
    has been on it or it's been unlocked (A_UnlockFloor, when a floor's security cube is
    destroyed); others show "teleport disabled". Each floor's overhead map is kept as it was
    when the player left it (Program.Hubs.cs floorOverheads).

    The fission detonator (an item with `inventory.useslot` and `inventory.use`) is dropped by
    its slot key: A_DropDetonator, beside the floor's security cube, with the next floor still
    locked. It goes off a few seconds later, breaking the cube (`monster.blastedby`), whose
    death unlocks the next floor.

    =============================================================================
    */

    private static void RegisterTeleporterActions()
    {
        // A_UnlockFloor: the cluster's next floor up (by floor number) opens in the teleporter
        Entities.MapTriggerRegistry.Register("A_UnlockFloor", (_, args) => UnlockNextFloor(args.ElementAtOrDefault(0)));
        Entities.Actors.ActorActionRegistry.Register("A_UnlockFloor", A_UnlockFloor);

        // A_DropDetonator("ArmedClass", "CubeClass"[, range]): see above
        Entities.MapTriggerRegistry.Register("A_DropDetonator", DropDetonatorAction);
    }

    /// <summary>The map of the level's cluster with this floor number, if any</summary>
    static string? ClusterFloorMap(int floor)
    {
        var cluster = ClusterOf(gamestate.mapon);
        return _gameEngineManager.GetGameInfo().Maps.FirstOrDefault(m => m.Value.Cluster == cluster && m.Value.FloorNumber == floor).Key;
    }

    static int CurrentFloorNumber() =>
        _gameEngineManager.GetGameInfo().Maps.TryGetValue(gamestate.mapon, out var info) ? info.FloorNumber : 0;

    static bool FloorOpen(string map) => visitedMaps.Contains(map) || unlockedMaps.Contains(map);

    static void A_UnlockFloor(Entities.Actors.Actor ob, string[] args) => UnlockNextFloor(args.ElementAtOrDefault(0));

    static bool UnlockNextFloor(string? message)
    {
        if (ClusterFloorMap(CurrentFloorNumber() + 1) is not { } next || FloorOpen(next))
            return false;
        unlockedMaps.Add(next);
        _hudMessageManager.Show(HudMessageKind.Other, string.IsNullOrEmpty(message) ? "$PS_FLOORUNLOCKED" : message);
        _audioManager.Play("bs/roll_score");
        return true;
    }

    static bool DropDetonatorAction(Entities.TriggerActivation trigger, string[] args)
    {
        var armed = args.ElementAtOrDefault(0);
        var cubeClass = args.ElementAtOrDefault(1);
        int range = int.TryParse(args.ElementAtOrDefault(2), out var r) ? r : 2;
        if (string.IsNullOrEmpty(armed) || string.IsNullOrEmpty(cubeClass))
            return false;

        void Say(string text) => _hudMessageManager.Show(HudMessageKind.Other, text);

        var next = ClusterFloorMap(CurrentFloorNumber() + 1);
        if (next == null || FloorOpen(next))
        {
            Say("$PS_FLOORNOTLOCKED");
            return false;
        }

        var cube = _mapManager.GetActors().FirstOrDefault(a => !a.IsRemoved && a.Name.Equals(cubeClass, StringComparison.OrdinalIgnoreCase)
            && a.CurrentState?.StateName != "Death");
        if (cube == null)
        {
            Say("$PS_NOCOMPUTER");
            return false;
        }
        if (cube.AreaNumber != player.AreaNumber && SpawnAreaOf(cube) != _mapManager.SpawnArea(player.TileX, player.TileY))
        {
            Say("$PS_NOTNEAR");
            return false;
        }
        if (Math.Max(Math.Abs(player.TileX - cube.TileX), Math.Abs(player.TileY - cube.TileY)) > range)
        {
            Say("$PS_GETCLOSER");
            return false;
        }

        var dropped = _mapManager.SpawnAtActor(armed, player);
        if (dropped == null)
            return false;
        _inventoryManager.Take("PlasmaDetonator", 1);
        Say("$PS_DETONATORDROPPED");
        PlaySoundLocActor("bs/robot_servo", dropped);
        return true;
    }

    static byte SpawnAreaOf(Entities.Actors.Actor a) => _mapManager.SpawnArea(a.TileX, a.TileY);

    /// <summary>An empty weapon slot's key uses an item held for that slot (`inventory.useslot`, `inventory.use`)</summary>
    static void UseSlotItem(int slot)
    {
        if (HeldUsableItem(item => _inventoryManager.GetIntProperty(item, "inventory.useslot", -1) == slot) is { } action)
            UseItemAction(action);
    }

    // How long use has been held with nothing to use or talk to
    static int useHeldTics;

    /// <summary>
    /// Use held with nothing ahead to use or talk to: every `inventory.useholdtics` tics of it,
    /// an item held with that property is used (bstone drops the fission detonator this way,
    /// every 60 tics)
    /// </summary>
    static void UseHeldItem()
    {
        int hold = 0;
        if (HeldUsableItem(item => (hold = _inventoryManager.GetIntProperty(item, "inventory.useholdtics", 0)) > 0) is not { } action)
        {
            useHeldTics = 0;
            return;
        }
        useHeldTics += (int)tics;
        if (useHeldTics < hold)
            return;
        useHeldTics = 0;
        UseItemAction(action);
    }

    static void ResetUseHeld() => useHeldTics = 0;

    /// <summary>The `inventory.use` action of the first item held that matches</summary>
    static string? HeldUsableItem(Func<string, bool> match)
    {
        foreach (var (item, count) in _inventoryManager.Items.ToList())
            if (count > 0 && match(item) && _inventoryManager.GetStringProperty(item, "inventory.use") is { Length: > 0 } action)
                return action;
        return null;
    }

    static void UseItemAction(string action) =>
        Entities.MapTriggerRegistry.Invoke(action,
            new Entities.TriggerActivation(player.TileX, player.TileY, FacingDir(player.Angle), player, 0));

    /*
    =============================================================================

                                OVERHEAD MAPS

    =============================================================================
    */

    static string OverheadColor(TeleporterLayout? layout, string name, int fallback) =>
        (layout?.Colors.TryGetValue(name, out var c) == true ? c : fallback).ToString();

    /// <summary>The level as the player has seen it, 64x64 palette colors (north up), for the teleporter</summary>
    internal static byte[] CaptureOverhead()
    {
        var layout = Elevator?.Teleporter;
        byte background = (byte)int.Parse(OverheadColor(layout, "background", 0x52));
        string unmapped = OverheadColor(layout, "unmapped", 82), floor = OverheadColor(layout, "floor", 85),
            door = OverheadColor(layout, "door", 88), locked = OverheadColor(layout, "locked", 24);

        var image = new byte[64 * 64];
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                byte color = background;
                if (x < MapManager.MAPSIZE && y < MapManager.MAPSIZE && _mapManager.seen[x, y] != SeenFlags.None)
                    color = byte.Parse(RadarTileColor(x, y, unmapped, floor, door, locked));
                image[y * 64 + x] = color;
            }
        }
        return image;
    }

    /*
    =============================================================================

                                THE TELEPORTER MAP

    =============================================================================
    */

    /// <summary>The teleporter map: the map chosen, or null if the player left it</summary>
    static string? TeleporterScreen(ElevatorAsset elevator, TeleporterLayout layout)
    {
        var cluster = ClusterOf(gamestate.mapon);
        var floors = _gameEngineManager.GetGameInfo().Maps
            .Where(m => m.Value.Cluster == cluster && m.Value.FloorNumber >= 0 && m.Value.FloorNumber < layout.Floors.Count)
            .GroupBy(m => m.Value.FloorNumber).ToDictionary(g => g.Key, g => g.First().Key);
        if (floors.Count == 0)
            return null;
        int maxFloor = floors.Keys.Max();
        int current = CurrentFloorNumber();
        int selected = floors.ContainsKey(current) ? current : floors.Keys.Min();

        // This floor's overhead as it stands
        floorOverheads[gamestate.mapon] = CaptureOverhead();

        var font = PresenterFont(layout.Font);
        byte? mask = layout.MaskColor is >= 0 and <= 255 ? (byte)layout.MaskColor : null;
        _videoManager.FadeOut();
        _videoManager.FillScreen("0");
        foreach (var pic in layout.Background)
            _graphicManager.DrawPic(pic.Pic, pic.X, pic.Y);

        void FloorPic(int floor, bool on)
        {
            if (floor < 0 || floor >= layout.Floors.Count)
                return;
            var spot = layout.Floors[floor];
            _graphicManager.DrawPic(string.Format(on ? layout.OnPic : layout.OffPic, floor + 1), spot.X, spot.Y, mask);
        }

        bool Locked(int floor) => !floors.TryGetValue(floor, out var map) || !FloorOpen(map);

        void ShowFloor(int floor)
        {
            _videoManager.Bar(layout.BarX, layout.BarY, layout.BarWidth, layout.BarHeight, layout.BarColor.ToString());
            bool locked = Locked(floor);
            var text = locked ? layout.LockedText : floors.TryGetValue(floor, out var m) ? GetMapDisplayName(m) : "";
            if (font != null)
            {
                int w = font.Measure(text);
                _graphicManager.DrawText(160 - w / 2, layout.TextY, text, font, (locked ? layout.LockedColor : layout.NameColor).ToString());
            }

            // Its overhead map, as last seen
            var o = layout.Overhead;
            if (floors.TryGetValue(floor, out var map) && floorOverheads.TryGetValue(map, out var image) && !locked)
            {
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                        _videoManager.Bar(o.X + x, o.Y + y, 1, 1, image[y * 64 + x].ToString());
                if (floor == current)
                {
                    int px = player.TileX, py = player.TileY;
                    if (px < 64 && py < 64)
                        _videoManager.Bar(o.X + px, o.Y + py, 1, 1, OverheadColor(layout, "player", 240));
                }
            }
            else
            {
                _videoManager.Bar(o.X, o.Y, 64, 64, OverheadColor(layout, "background", 0x52));
                if (font != null && !string.IsNullOrEmpty(layout.UnmappedText))
                {
                    int ly = o.Y + 13;
                    foreach (var line in layout.UnmappedText.Split('\n'))
                    {
                        _graphicManager.DrawText(o.X + 5, ly, line, font, layout.UnmappedColor.ToString());
                        ly += 7;
                    }
                }
            }
        }

        void Buttons(int lit)
        {
            foreach (var s in layout.UpSpots)
                _graphicManager.DrawPic(lit < 0 ? layout.UpOnPic : layout.UpOffPic, s.X, s.Y, mask);
            foreach (var s in layout.DownSpots)
                _graphicManager.DrawPic(lit > 0 ? layout.DownOnPic : layout.DownOffPic, s.X, s.Y, mask);
        }

        for (int f = 0; f < layout.Floors.Count && f <= maxFloor; f++)
            FloorPic(f, f == selected);
        Buttons(0);
        ShowFloor(selected);
        if (font != null && !string.IsNullOrEmpty(layout.Help))
        {
            _graphicManager.DrawText(layout.HelpSpot.X + 1, layout.HelpSpot.Y + 1, layout.Help, font, "0");
            _graphicManager.DrawText(layout.HelpSpot.X, layout.HelpSpot.Y, layout.Help, font, layout.HelpColor.ToString());
        }

        _inputManager.ClearKeysDown();
        string? result = null;
        bool done = false, statsShown = false;
        while (!done)
        {
            _videoManager.Update();
            if (_videoManager.screenfaded)
                _videoManager.FadeIn();
            if (!statsShown && layout.Stats != null)
            {
                statsShown = true;
                ShowFloorStats(elevator with { Stats = layout.Stats });
            }

            GameEngineManager.DelayMs(5);
            _inputManager.ProcessEvents();

            int move = 0;
            while (_inputManager.TryTakePressedKey(out var key))
            {
                switch (key)
                {
                    case ScanCodes.sc_Escape:
                        done = true;
                        break;
                    case ScanCodes.sc_UpArrow:
                    case ScanCodes.sc_LeftArrow:
                        move = -1;
                        break;
                    case ScanCodes.sc_DownArrow:
                    case ScanCodes.sc_RightArrow:
                        move = 1;
                        break;
                    case ScanCodes.sc_Enter:
                    case ScanCodes.sc_Space:
                    case ScanCodes.sc_Control:
                        if (Locked(selected))
                        {
                            if (!string.IsNullOrEmpty(layout.LockedSound))
                                _audioManager.Play(layout.LockedSound);
                        }
                        else if (selected != current)
                        {
                            // The chosen floor flashes
                            for (int i = 0; i < layout.Flashes; i++)
                            {
                                FloorPic(selected, false);
                                _videoManager.Update();
                                _inputManager.UserInput((uint)layout.FlashTics);
                                FloorPic(selected, true);
                                _videoManager.Update();
                                _inputManager.UserInput((uint)layout.FlashTics);
                            }
                            result = floors[selected];
                            done = true;
                        }
                        else
                            done = true;
                        break;
                }
            }

            if (done || move == 0)
                continue;

            int target = selected + move;
            if (target < 0 || target > maxFloor)
                continue;
            Buttons(move);
            FloorPic(selected, false);
            selected = target;
            FloorPic(selected, true);
            ShowFloor(selected);
        }

        _videoManager.FadeOut();
        _inputManager.ClearKeysDown();
        return result;
    }
}
