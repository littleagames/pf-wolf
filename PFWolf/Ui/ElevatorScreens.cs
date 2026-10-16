using PFWolf.Assets;

namespace PFWolf.Ui;

/*
=============================================================================

                                ELEVATOR

A_FloorSelect (a switch action, on Blake Stone's elevator panel wall) shows the floor-select
screen from gamepacks/{pack}/elevator.yaml (bstone's InputFloor). Its buttons are the floors
of the level's hub cluster by game-info floor-number. Arrows (or turning) move the selector,
space, enter or fire press its button, a number key presses that floor's (0 is floor 10),
and Esc leaves. A floor the player has been on is open; the next floor up past the highest
open one opens with the unlock item, used up as it does. Choosing a floor leaves the level
(ex_warped); the player comes out in the same elevator on the new floor.

=============================================================================
*/

/*
=============================================================================

                                TELEPORTER

Planet Strike's way between floors (bstone's ps_input_floor): its teleporter wall runs
A_FloorSelect as the elevator's does, and an elevator.yaml with `style: teleporter` shows
the teleporter map instead of the panel. Up and down (or left and right) move through the
cluster's floors by floor number; Enter goes, Esc leaves. A floor is open once the player
has been on it or it's been unlocked (A_UnlockFloor, when a floor's security cube is
destroyed); others show "teleport disabled". Each floor's overhead map is kept as it was
when the player left it (Gameplay/Hubs.cs floorOverheads).

The fission detonator (an item with `inventory.useslot` and `inventory.use`) is dropped by
its slot key: A_DropDetonator, beside the floor's security cube, with the next floor still
locked. It goes off a few seconds later, breaking the cube (`monster.blastedby`), whose
death unlocks the next floor.

=============================================================================
*/

/// <summary>What the elevator and teleporter screens need from the rest of the game</summary>
/// <param name="TileColor">A seen tile's color on the radar (GameHost.RadarTileColor): its tile, then the unmapped, floor, door and locked colors</param>
/// <param name="MapDisplayName">A map's name as it's shown</param>
internal sealed record ElevatorHooks(Func<int, int, string, string, string, string, string> TileColor, Func<string, string> MapDisplayName);

internal sealed class ElevatorScreens(GameEngineManager engine, AssetManager assets, InputManager input, AudioManager audio, VideoManager video,
    GraphicManager graphics, InventoryManager inventory, MapManager mapManager, MenuDrawing draw, TextPresenter presenter, Hubs hubs,
    GameSession session, ElevatorHooks hooks)
{
    ElevatorAsset? elevatorAsset;
    internal ElevatorAsset? Elevator => elevatorAsset ??= assets.FindInGamePack<ElevatorAsset>("elevator");

    /// <summary>The cluster's floors, by floor number, that have a button</summary>
    internal Dictionary<int, string> ElevatorFloors(ElevatorAsset elevator)
    {
        var cluster = hubs.ClusterOf(session.gamestate.mapon);
        var floors = new Dictionary<int, string>();
        foreach (var (map, info) in engine.GetGameInfo().Maps)
        {
            if (info.Cluster == cluster && info.FloorNumber >= 1 && info.FloorNumber <= elevator.Buttons)
                floors.TryAdd(info.FloorNumber, map);
        }
        return floors;
    }

    /// <summary>The floor-select screen: the map chosen, or null if the player left it</summary>
    internal string? FloorSelectScreen(ElevatorAsset elevator)
    {
        var floors = ElevatorFloors(elevator);
        int current = engine.GetGameInfo().Maps.TryGetValue(session.gamestate.mapon, out var mapInfo) ? mapInfo.FloorNumber : 0;
        bool Open(int floor) => floors.TryGetValue(floor, out var m) && hubs.visitedMaps.Contains(m);
        int lastOpen = Enumerable.Range(1, elevator.Buttons).Where(Open).DefaultIfEmpty(0).Max();

        video.FadeOut();

        if (elevator.Frame is { } frame)
        {
            draw.BevelBox(frame.X, frame.Y, frame.Width, frame.Height, frame.Hi, frame.Med, frame.Lo);
            draw.BevelBox(frame.X + frame.InsetX, frame.Y + frame.InsetY, frame.Width - 2 * frame.InsetX, frame.Height - 2 * frame.InsetY,
                frame.Lo, frame.Med, frame.Hi);
        }
        if (elevator.Panel is { } panel)
            graphics.DrawPic(panel.Pic, panel.X, panel.Y);
        if (!string.IsNullOrEmpty(elevator.Prompt) && presenter.PresenterScript(elevator.Prompt) is { } prompt)
            presenter.PresenterMessageBox(prompt);

        var messages = elevator.Messages;
        int cursor = current >= 1 && current <= elevator.Buttons ? current : 1;
        string? result = null;
        bool done = false;
        bool statsShown = false;

        void DrawMessage(string textName, int numberX = -1, int number = 0)
        {
            if (messages.Pic is { } pic)
                graphics.DrawPic(pic.Pic, pic.X, pic.Y);
            if (presenter.PresenterScript(textName) is { } script)
            {
                presenter.TP_Presenter(new PresenterInfo
                {
                    Flags = PresenterFlags.CacheNoGfx | PresenterFlags.UseCurrent,
                    Script = script,
                    X1 = messages.X1,
                    Y1 = messages.Y1,
                    X2 = messages.X2,
                    Y2 = messages.Y2,
                    Font = messages.Font,
                    FontColor = messages.Color,
                    CustomLineHeight = messages.LineHeight,
                });
            }

            if (numberX >= 0 && presenter.PresenterFont(messages.NumberFont) is { } font)
                graphics.DrawText(numberX, messages.Y1 + 2, number.ToString(), font, messages.NumberColor.ToString());
        }

        (int X, int Y) ButtonSpot(int floor)
        {
            int i = floor - 1;
            var layout = elevator.ButtonLayout;
            return (layout.X + layout.StepX * (i % 2), layout.Y - layout.StepY * (i / 2));
        }

        void DrawButton(int floor, bool lit)
        {
            var (x, y) = ButtonSpot(floor);
            graphics.DrawPic((lit ? elevator.ButtonLayout.LitPicPrefix : elevator.ButtonLayout.PicPrefix) + floor, x, y);
        }

        void DrawCursor()
        {
            var layout = elevator.ButtonLayout;
            for (int floor = 1; floor <= elevator.Buttons; floor++)
            {
                int color = floor == current
                    ? (cursor == current ? layout.CurrentColor : layout.CurrentAwayColor)
                    : floor == cursor ? layout.SelectedColor : layout.OtherColor;
                var (x, y) = ButtonSpot(floor);
                var c = color.ToString();
                video.HorizontalLine(x, x + layout.Size - 1, y, c);
                video.HorizontalLine(x, x + layout.Size - 1, y + layout.Size - 1, c);
                video.VerticalLine(y, y + layout.Size - 1, x, c);
                video.VerticalLine(y, y + layout.Size - 1, x + layout.Size - 1, c);
            }
        }

        void DrawSelectMessage() => DrawMessage(messages.Select, current >= 1 ? messages.CurrentX : -1, current);

        DrawSelectMessage();
        DrawCursor();
        input.ClearKeysDown();

        while (!done)
        {
            video.Update();
            if (video.screenfaded)
                video.FadeIn();
            if (!statsShown)
            {
                statsShown = true;
                ShowFloorStats(elevator);
            }

            GameEngineManager.DelayMs(5);
            input.ProcessEvents();

            int target = 0;
            while (input.TryTakePressedKey(out var key))
            {
                switch (key)
                {
                    case ScanCodes.sc_Escape:
                        done = true;
                        break;
                    case ScanCodes.sc_UpArrow:
                    case ScanCodes.sc_RightArrow:
                        cursor = cursor >= elevator.Buttons ? 1 : cursor + 1;
                        DrawCursor();
                        break;
                    case ScanCodes.sc_DownArrow:
                    case ScanCodes.sc_LeftArrow:
                        cursor = cursor <= 1 ? elevator.Buttons : cursor - 1;
                        DrawCursor();
                        break;
                    case ScanCodes.sc_Space:
                    case ScanCodes.sc_Enter:
                    case ScanCodes.sc_Control:
                        target = cursor;
                        break;
                    case >= ScanCodes.sc_1 and <= ScanCodes.sc_0:
                        target = key - ScanCodes.sc_1 + 1;
                        cursor = target;
                        DrawCursor();
                        break;
                }
            }

            if (done || target < 1 || target > elevator.Buttons || target == current)
                continue;

            if (!string.IsNullOrEmpty(elevator.ButtonSound))
                audio.Play(elevator.ButtonSound);
            DrawButton(target, lit: true);

            if (Open(target))
            {
                result = floors[target];
                done = true;
                continue;
            }

            if (target == lastOpen + 1 && floors.ContainsKey(target)
                && !string.IsNullOrEmpty(elevator.UnlockItem) && inventory.Has(elevator.UnlockItem))
            {
                inventory.Take(elevator.UnlockItem, inventory.GetCount(elevator.UnlockItem));
                DrawMessage(messages.Unlocked);
                video.Update();
                input.UserInput((uint)elevator.MessageTics);
                result = floors[target];
                done = true;
                continue;
            }

            if (target == lastOpen + 1)
                DrawMessage(messages.NeedItem);
            else
                DrawMessage(messages.Locked, messages.LockedX, target);
            video.Update();
            input.UserInput((uint)elevator.MessageTics);
            input.ClearKeysDown();

            DrawButton(target, lit: false);
            DrawSelectMessage();
            DrawCursor();
        }

        video.FadeOut();
        input.ClearKeysDown();
        return result;
    }

    /*
    =============================================================================

                                FLOOR STATS

    =============================================================================
    */

    const int StatBarWidth = 48, StatBarHeight = 5;

    /// <summary>
    /// The floor's stat bars (bstone's ShowStats): points, informants and enemies, the floor's
    /// score, then the mission's, each filling up to its percentage with a tick, unless a key
    /// is pressed
    /// </summary>
    internal void ShowFloorStats(ElevatorAsset elevator)
    {
        if (elevator.Stats is not { } stats)
            return;

        var font = presenter.PresenterFont(stats.Font);
        var floor = hubs.FloorStats();
        bool anything = floor.Any(s => s.Total > 0);
        bool quick = false;
        input.ClearLastKey();
        int by = stats.Y;

        void Row(int percent, bool none)
        {
            ShowRatio(stats.X, by, stats.X + 52, font, stats.Color, percent, none, ref quick);
        }

        foreach (var (got, total) in floor)
        {
            Row(Hubs.Ratio(got, total), total == 0);
            by += 7;
        }
        by += 5;
        Row(hubs.FloorScore() * 100 / 300, !anything);
        by += 7;
        Row(hubs.MissionRatio(), false);
    }

    void ShowRatio(int bx, int by, int nx, Fonts.Font? font, int color, int percent, bool none, ref bool quick)
    {
        void Percent(int value)
        {
            video.Bar(nx, by, 18, StatBarHeight, "0");
            if (font == null)
                return;
            int x = value < 10 ? nx + 9 : value < 100 ? nx + 4 : nx - 1;
            graphics.DrawText(x, by, $"{value}%", font, color.ToString());
        }

        if (none)
        {
            video.Bar(bx, by, StatBarWidth, StatBarHeight, "0");
            video.Bar(nx, by, 19, StatBarHeight, "0");
            if (font != null)
                graphics.DrawText(nx, by, "N/A", font, "87");
            return;
        }

        percent = Math.Clamp(percent, 0, 100);
        int bars = percent * StatBarWidth / 100;
        video.Bar(bx, by, StatBarWidth, StatBarHeight, "7");
        Percent(0);
        for (int i = 0; i < bars; i++)
        {
            video.VerticalLine(by, by + StatBarHeight - 1, bx + i, "200");
            Percent(i == bars - 1 ? percent : Math.Min(percent, 1 + (i + 1) * 2));

            if (!quick)
            {
                // A key finishes the bars at once, and still counts on the screen (a floor's number)
                input.ProcessEvents();
                if (input.GetLastKeyPressed() != ScanCodes.sc_None)
                    quick = true;
                if (i % 2 == 0)
                    audio.Play("bs/stats1");
                video.Update();
                GameEngineManager.DelayMs(14);
            }
        }

        if (bars == 0)
            Percent(percent);
        if (!quick && bars > 0)
            audio.Play("bs/stats2");
        video.Update();
    }

    /*
    =============================================================================

                                OVERHEAD MAPS

    =============================================================================
    */

    internal static string OverheadColor(TeleporterLayout? layout, string name, int fallback) =>
        (layout?.Colors.TryGetValue(name, out var c) == true ? c : fallback).ToString();

    /// <summary>The level as the player has seen it, 64x64 palette colors (north up), for the teleporter</summary>
    internal byte[] CaptureOverhead()
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
                if (x < MapManager.MAPSIZE && y < MapManager.MAPSIZE && mapManager.seen[x, y] != SeenFlags.None)
                    color = byte.Parse(hooks.TileColor(x, y, unmapped, floor, door, locked));
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
    internal string? TeleporterScreen(ElevatorAsset elevator, TeleporterLayout layout)
    {
        var cluster = hubs.ClusterOf(session.gamestate.mapon);
        var floors = engine.GetGameInfo().Maps
            .Where(m => m.Value.Cluster == cluster && m.Value.FloorNumber >= 0 && m.Value.FloorNumber < layout.Floors.Count)
            .GroupBy(m => m.Value.FloorNumber).ToDictionary(g => g.Key, g => g.First().Key);
        if (floors.Count == 0)
            return null;
        int maxFloor = floors.Keys.Max();
        int current = hubs.CurrentFloorNumber();
        int selected = floors.ContainsKey(current) ? current : floors.Keys.Min();

        // This floor's overhead as it stands
        hubs.floorOverheads[session.gamestate.mapon] = CaptureOverhead();

        var font = presenter.PresenterFont(layout.Font);
        byte? mask = layout.MaskColor is >= 0 and <= 255 ? (byte)layout.MaskColor : null;
        video.FadeOut();
        video.FillScreen("0");
        foreach (var pic in layout.Background)
            graphics.DrawPic(pic.Pic, pic.X, pic.Y);

        void FloorPic(int floor, bool on)
        {
            if (floor < 0 || floor >= layout.Floors.Count)
                return;
            var spot = layout.Floors[floor];
            graphics.DrawPic(string.Format(on ? layout.OnPic : layout.OffPic, floor + 1), spot.X, spot.Y, mask);
        }

        bool Locked(int floor) => !floors.TryGetValue(floor, out var map) || !hubs.FloorOpen(map);

        void ShowFloor(int floor)
        {
            video.Bar(layout.BarX, layout.BarY, layout.BarWidth, layout.BarHeight, layout.BarColor.ToString());
            bool locked = Locked(floor);
            var text = locked ? layout.LockedText : floors.TryGetValue(floor, out var m) ? hooks.MapDisplayName(m) : "";
            if (font != null)
            {
                int w = font.Measure(text);
                graphics.DrawText(160 - w / 2, layout.TextY, text, font, (locked ? layout.LockedColor : layout.NameColor).ToString());
            }

            // Its overhead map, as last seen
            var o = layout.Overhead;
            if (floors.TryGetValue(floor, out var map) && hubs.floorOverheads.TryGetValue(map, out var image) && !locked)
            {
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                        video.Bar(o.X + x, o.Y + y, 1, 1, image[y * 64 + x].ToString());
                if (floor == current)
                {
                    int px = session.player.TileX, py = session.player.TileY;
                    if (px < 64 && py < 64)
                        video.Bar(o.X + px, o.Y + py, 1, 1, OverheadColor(layout, "player", 240));
                }
            }
            else
            {
                video.Bar(o.X, o.Y, 64, 64, OverheadColor(layout, "background", 0x52));
                if (font != null && !string.IsNullOrEmpty(layout.UnmappedText))
                {
                    int ly = o.Y + 13;
                    foreach (var line in layout.UnmappedText.Split('\n'))
                    {
                        graphics.DrawText(o.X + 5, ly, line, font, layout.UnmappedColor.ToString());
                        ly += 7;
                    }
                }
            }
        }

        void Buttons(int lit)
        {
            foreach (var s in layout.UpSpots)
                graphics.DrawPic(lit < 0 ? layout.UpOnPic : layout.UpOffPic, s.X, s.Y, mask);
            foreach (var s in layout.DownSpots)
                graphics.DrawPic(lit > 0 ? layout.DownOnPic : layout.DownOffPic, s.X, s.Y, mask);
        }

        for (int f = 0; f < layout.Floors.Count && f <= maxFloor; f++)
            FloorPic(f, f == selected);
        Buttons(0);
        ShowFloor(selected);
        if (font != null && !string.IsNullOrEmpty(layout.Help))
        {
            graphics.DrawText(layout.HelpSpot.X + 1, layout.HelpSpot.Y + 1, layout.Help, font, "0");
            graphics.DrawText(layout.HelpSpot.X, layout.HelpSpot.Y, layout.Help, font, layout.HelpColor.ToString());
        }

        input.ClearKeysDown();
        string? result = null;
        bool done = false, statsShown = false;
        while (!done)
        {
            video.Update();
            if (video.screenfaded)
                video.FadeIn();
            if (!statsShown && layout.Stats != null)
            {
                statsShown = true;
                ShowFloorStats(elevator with { Stats = layout.Stats });
            }

            GameEngineManager.DelayMs(5);
            input.ProcessEvents();

            int move = 0;
            while (input.TryTakePressedKey(out var key))
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
                                audio.Play(layout.LockedSound);
                        }
                        else if (selected != current)
                        {
                            // The chosen floor flashes
                            for (int i = 0; i < layout.Flashes; i++)
                            {
                                FloorPic(selected, false);
                                video.Update();
                                input.UserInput((uint)layout.FlashTics);
                                FloorPic(selected, true);
                                video.Update();
                                input.UserInput((uint)layout.FlashTics);
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

        video.FadeOut();
        input.ClearKeysDown();
        return result;
    }
}
