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
(PlayState.Warped); the player comes out in the same elevator on the new floor.

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
when the player left it (Gameplay/Hubs.cs FloorOverheads).

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
    internal ElevatorAsset? Elevator => elevatorAsset ??= assets.FindInGamePackIfAny<ElevatorAsset>("elevator");     // a hub without one (a mod's) is fine

    /// <summary>The cluster's floors, by floor number, that have a button</summary>
    internal Dictionary<int, string> ElevatorFloors(ElevatorAsset elevator)
    {
        var cluster = hubs.ClusterOf(session.GameState.MapOn);
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
        int current = CurrentMapInfo.For(session, engine.GetGameInfo())?.FloorNumber ?? 0;
        bool Open(int floor) => floors.TryGetValue(floor, out var m) && hubs.VisitedMaps.Contains(m);
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
        if (elevator.Overhead is { } overhead)
            video.MemToScreen(CaptureOverhead(overhead.Colors, markThings: true), OverheadSize, OverheadSize, overhead.X, overhead.Y);
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
                presenter.RunPresenter(new PresenterInfo
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
            video.CycleColors();        // the overhead map's player dot flashes (palette-cycles)
            video.Update();
            if (video.ScreenFaded)
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
                    case ScanCodes.Escape:
                        done = true;
                        break;
                    case ScanCodes.UpArrow:
                    case ScanCodes.RightArrow:
                        cursor = cursor >= elevator.Buttons ? 1 : cursor + 1;
                        DrawCursor();
                        break;
                    case ScanCodes.DownArrow:
                    case ScanCodes.LeftArrow:
                        cursor = cursor <= 1 ? elevator.Buttons : cursor - 1;
                        DrawCursor();
                        break;
                    case ScanCodes.Space:
                    case ScanCodes.Enter:
                    case ScanCodes.Control:
                        target = cursor;
                        break;
                    case >= ScanCodes.D1 and <= ScanCodes.D0:
                        target = key - ScanCodes.D1 + 1;
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
        if (elevator.Stats is { } stats)
            ShowFloorStats(stats, quick: false);
    }

    /// <summary>The floor's stat bars at once (quick, bstone's ss_quick) or filling up</summary>
    void ShowFloorStats(ElevatorStats stats, bool quick)
    {
        var font = presenter.PresenterFont(stats.Font);
        var floor = hubs.FloorStats();
        bool anything = floor.Any(s => s.Total > 0);
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
        by += stats.ScoreGap - 7;
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
                if (input.GetLastKeyPressed() != ScanCodes.None)
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

    // The teleporter's overhead map is this many pixels each way: a pixel a tile on a 64x64 level,
    // a larger one's tiles sampled down to fit
    const int OverheadSize = MapConstants.ClassicMapSize;

    // The level's tile under an overhead pixel, each way
    int OverheadTile(int pixel, int levelSize) => levelSize <= OverheadSize ? pixel : pixel * levelSize / OverheadSize;

    /// <summary>The level as the player has seen it, 64x64 palette colors (north up), for the teleporter</summary>
    internal byte[] CaptureOverhead() => CaptureOverhead(Elevator?.Teleporter?.Colors, markThings: false);

    /// <summary>
    /// The level as the player has seen it, 64x64 palette colors (north up), in the colors given
    /// by name (background, unmapped, floor, door, locked); with <paramref name="markThings"/>,
    /// keys lying about (key) and the player (player) too. <paramref name="aroundPlayer"/>: the
    /// 64 tiles about the player instead, turned so the way they face is up (bstone's
    /// ShowOverhead without OV_WHOLE_MAP).
    /// </summary>
    byte[] CaptureOverhead(IReadOnlyDictionary<string, int>? colors, bool markThings, bool aroundPlayer = false)
    {
        string Color(string name, int fallback) => (colors?.TryGetValue(name, out var c) == true ? c : fallback).ToString();
        byte background = byte.Parse(Color("background", 0x52));
        string unmapped = Color("unmapped", 82), floor = Color("floor", 85), door = Color("door", 88), locked = Color("locked", 24);
        byte key = byte.Parse(Color("key", 0xf3)), player = byte.Parse(Color("player", 0xf0));
        string? hidden = colors?.ContainsKey("hidden") == true ? Color("hidden", 0) : null;

        var keys = !markThings ? [] : mapManager.GetActors()
            .Where(a => !a.IsRemoved && inventory.FindClass(a.Name, "Key") != null)
            .Select(a => (a.TileX, a.TileY)).ToHashSet();

        // About the player, bstone's walk: from the corner a radius out, a column at a time
        const int radius = OverheadSize / 2;
        double angle = session.Player.Angle * Math.PI / 180, psin = Math.Sin(angle), pcos = Math.Cos(angle);
        double baseX = session.Player.X / 65536.0 + radius * (pcos - psin);
        double baseY = session.Player.Y / 65536.0 - radius * (psin + pcos);
        (int X, int Y) Tile(int px, int py) => aroundPlayer
            ? ((int)Math.Floor(baseX + px * psin - py * pcos), (int)Math.Floor(baseY + px * pcos + py * psin))
            : (OverheadTile(px, mapManager.MapWidth), OverheadTile(py, mapManager.MapHeight));

        var image = new byte[OverheadSize * OverheadSize];
        bool playerDrawn = false;
        for (int px = 0; px < OverheadSize; px++)
        {
            for (int py = 0; py < OverheadSize; py++)
            {
                var (x, y) = Tile(px, py);
                byte color = background;
                if (mapManager.InMap(x, y) && mapManager.Seen[x, y] != SeenFlags.None)
                {
                    // Open floor in a hidden area (Blake Stone's) shows in its own color
                    var tile = hooks.TileColor(x, y, unmapped, floor, door, locked);
                    if (hidden != null && tile == floor && mapManager.IsHiddenArea(x, y))
                        tile = hidden;
                    color = keys.Contains(((byte)x, (byte)y)) ? key : byte.Parse(tile);
                }
                if (markThings && !playerDrawn && x == session.Player.TileX && y == session.Player.TileY)
                {
                    color = player;
                    playerDrawn = true;     // one pixel, as bstone's
                }
                image[py * OverheadSize + px] = color;
            }
        }
        return image;
    }

    /*
    =============================================================================

                                THE POPUP MAP

    The automap key in a game with a game-info stats-popup (Blake Stone's TAB,
    bstone's PopupAutoMap): its picture over the view with the floor's whole map
    (Shift: the tiles about the player, turned) and stat bars, until a key is
    pressed, the palette cycles going round meanwhile.

    =============================================================================
    */

    internal void PopupMap(StatsPopupInfo popup, bool aroundPlayer)
    {
        if (!string.IsNullOrEmpty(popup.Pic))
            graphics.DrawPic(popup.Pic, popup.X, popup.Y);
        if (popup.Overhead is { } overhead)
            video.MemToScreen(CaptureOverhead(overhead.Colors, markThings: true, aroundPlayer), OverheadSize, OverheadSize, overhead.X, overhead.Y);
        if (popup.Stats is { } stats)
            ShowFloorStats(stats, quick: true);
        video.Update();

        input.StartAck();
        do
        {
            if (video.CycleColors())
                video.Update();
            GameEngineManager.DelayMs(5);
            input.ProcessEvents();
        } while (!input.CheckAck());
        input.ClearKeysDown();
    }

    /*
    =============================================================================

                                THE TELEPORTER MAP

    =============================================================================
    */

    /// <summary>The teleporter map: the map chosen, or null if the player left it</summary>
    internal string? TeleporterScreen(ElevatorAsset elevator, TeleporterLayout layout)
    {
        var cluster = hubs.ClusterOf(session.GameState.MapOn);
        var floors = engine.GetGameInfo().Maps
            .Where(m => m.Value.Cluster == cluster && m.Value.FloorNumber >= 0 && m.Value.FloorNumber < layout.Floors.Count)
            .GroupBy(m => m.Value.FloorNumber).ToDictionary(g => g.Key, g => g.First().Key);
        if (floors.Count == 0)
            return null;
        int maxFloor = floors.Keys.Max();
        int current = hubs.CurrentFloorNumber();
        int selected = floors.ContainsKey(current) ? current : floors.Keys.Min();

        // This floor's overhead as it stands
        hubs.FloorOverheads[session.GameState.MapOn] = CaptureOverhead();

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
            if (floors.TryGetValue(floor, out var map) && hubs.FloorOverheads.TryGetValue(map, out var image) && !locked)
            {
                for (int y = 0; y < OverheadSize; y++)
                    for (int x = 0; x < OverheadSize; x++)
                        video.Bar(o.X + x, o.Y + y, 1, 1, image[y * OverheadSize + x].ToString());
                if (floor == current)
                {
                    int px = session.Player.TileX, py = session.Player.TileY;
                    if (mapManager.MapWidth > OverheadSize)
                        px = px * OverheadSize / mapManager.MapWidth;
                    if (mapManager.MapHeight > OverheadSize)
                        py = py * OverheadSize / mapManager.MapHeight;
                    if (px < OverheadSize && py < OverheadSize)
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
            video.CycleColors();
            video.Update();
            if (video.ScreenFaded)
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
                    case ScanCodes.Escape:
                        done = true;
                        break;
                    case ScanCodes.UpArrow:
                    case ScanCodes.LeftArrow:
                        move = -1;
                        break;
                    case ScanCodes.DownArrow:
                    case ScanCodes.RightArrow:
                        move = 1;
                        break;
                    case ScanCodes.Enter:
                    case ScanCodes.Space:
                    case ScanCodes.Control:
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
