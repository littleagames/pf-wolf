using PFWolf.Assets;
using PFWolf.Managers;

namespace PFWolf;

internal partial class Program
{
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

    static ElevatorAsset? elevatorAsset;
    static ElevatorAsset? Elevator => elevatorAsset ??= _assetManager.FindInGamePack<ElevatorAsset>("elevator");

    static bool FloorSelectAction(Entities.TriggerActivation trigger, string[] args)
    {
        if (Elevator is not { } elevator)
            return false;

        int panelWall = _mapManager.MAPSPOT(trigger.TileX, trigger.TileY, 0);

        // Planet Strike's teleporter map (Program.Teleporter.cs): the player comes out at the
        // new floor's start
        bool teleporter = elevator.Style.Equals("teleporter", StringComparison.OrdinalIgnoreCase) && elevator.Teleporter != null;
        var map = teleporter ? TeleporterScreen(elevator, elevator.Teleporter!) : FloorSelectScreen(elevator);
        if (map == null)
        {
            DrawPlayScreen();
            ThreeDRefresh();
            _inputManager.ClearKeysDown();
            return true;
        }

        pendingMapChange = teleporter
            ? new PendingMapChange(map, false, 0, 0, 0, WaitTics: 0)
            : new PendingMapChange(map, true, player.X, player.Y, player.Angle, WaitTics: 0, ElevatorWall: panelWall);
        gamestate.mapon = map;
        playstate = playstatetypes.ex_warped;
        return true;
    }

    /// <summary>The cluster's floors, by floor number, that have a button</summary>
    static Dictionary<int, string> ElevatorFloors(ElevatorAsset elevator)
    {
        var cluster = ClusterOf(gamestate.mapon);
        var floors = new Dictionary<int, string>();
        foreach (var (map, info) in _gameEngineManager.GetGameInfo().Maps)
        {
            if (info.Cluster == cluster && info.FloorNumber >= 1 && info.FloorNumber <= elevator.Buttons)
                floors.TryAdd(info.FloorNumber, map);
        }
        return floors;
    }

    /// <summary>The floor-select screen: the map chosen, or null if the player left it</summary>
    static string? FloorSelectScreen(ElevatorAsset elevator)
    {
        var floors = ElevatorFloors(elevator);
        int current = _gameEngineManager.GetGameInfo().Maps.TryGetValue(gamestate.mapon, out var mapInfo) ? mapInfo.FloorNumber : 0;
        bool Open(int floor) => floors.TryGetValue(floor, out var m) && visitedMaps.Contains(m);
        int lastOpen = Enumerable.Range(1, elevator.Buttons).Where(Open).DefaultIfEmpty(0).Max();

        _videoManager.FadeOut();

        if (elevator.Frame is { } frame)
        {
            BevelBox(frame.X, frame.Y, frame.Width, frame.Height, frame.Hi, frame.Med, frame.Lo);
            BevelBox(frame.X + frame.InsetX, frame.Y + frame.InsetY, frame.Width - 2 * frame.InsetX, frame.Height - 2 * frame.InsetY,
                frame.Lo, frame.Med, frame.Hi);
        }
        if (elevator.Panel is { } panel)
            _graphicManager.DrawPic(panel.Pic, panel.X, panel.Y);
        if (!string.IsNullOrEmpty(elevator.Prompt) && PresenterScript(elevator.Prompt) is { } prompt)
            PresenterMessageBox(prompt);

        var messages = elevator.Messages;
        int cursor = current >= 1 && current <= elevator.Buttons ? current : 1;
        string? result = null;
        bool done = false;
        bool statsShown = false;

        void DrawMessage(string textName, int numberX = -1, int number = 0)
        {
            if (messages.Pic is { } pic)
                _graphicManager.DrawPic(pic.Pic, pic.X, pic.Y);
            if (PresenterScript(textName) is { } script)
            {
                TP_Presenter(new PresenterInfo
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

            if (numberX >= 0 && PresenterFont(messages.NumberFont) is { } font)
                _graphicManager.DrawText(numberX, messages.Y1 + 2, number.ToString(), font, messages.NumberColor.ToString());
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
            _graphicManager.DrawPic((lit ? elevator.ButtonLayout.LitPicPrefix : elevator.ButtonLayout.PicPrefix) + floor, x, y);
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
                _videoManager.HorizontalLine(x, x + layout.Size - 1, y, c);
                _videoManager.HorizontalLine(x, x + layout.Size - 1, y + layout.Size - 1, c);
                _videoManager.VerticalLine(y, y + layout.Size - 1, x, c);
                _videoManager.VerticalLine(y, y + layout.Size - 1, x + layout.Size - 1, c);
            }
        }

        void DrawSelectMessage() => DrawMessage(messages.Select, current >= 1 ? messages.CurrentX : -1, current);

        DrawSelectMessage();
        DrawCursor();
        _inputManager.ClearKeysDown();

        while (!done)
        {
            _videoManager.Update();
            if (_videoManager.screenfaded)
                _videoManager.FadeIn();
            if (!statsShown)
            {
                statsShown = true;
                ShowFloorStats(elevator);
            }

            GameEngineManager.DelayMs(5);
            _inputManager.ProcessEvents();

            int target = 0;
            while (_inputManager.TryTakePressedKey(out var key))
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
                _audioManager.Play(elevator.ButtonSound);
            DrawButton(target, lit: true);

            if (Open(target))
            {
                result = floors[target];
                done = true;
                continue;
            }

            if (target == lastOpen + 1 && floors.ContainsKey(target)
                && !string.IsNullOrEmpty(elevator.UnlockItem) && _inventoryManager.Has(elevator.UnlockItem))
            {
                _inventoryManager.Take(elevator.UnlockItem, _inventoryManager.GetCount(elevator.UnlockItem));
                DrawMessage(messages.Unlocked);
                _videoManager.Update();
                _inputManager.UserInput((uint)elevator.MessageTics);
                result = floors[target];
                done = true;
                continue;
            }

            if (target == lastOpen + 1)
                DrawMessage(messages.NeedItem);
            else
                DrawMessage(messages.Locked, messages.LockedX, target);
            _videoManager.Update();
            _inputManager.UserInput((uint)elevator.MessageTics);
            _inputManager.ClearKeysDown();

            DrawButton(target, lit: false);
            DrawSelectMessage();
            DrawCursor();
        }

        _videoManager.FadeOut();
        _inputManager.ClearKeysDown();
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
    static void ShowFloorStats(ElevatorAsset elevator)
    {
        if (elevator.Stats is not { } stats)
            return;

        var font = PresenterFont(stats.Font);
        var floor = FloorStats();
        bool anything = floor.Any(s => s.Total > 0);
        bool quick = false;
        _inputManager.ClearLastKey();
        int by = stats.Y;

        void Row(int percent, bool none)
        {
            ShowRatio(stats.X, by, stats.X + 52, font, stats.Color, percent, none, ref quick);
        }

        foreach (var (got, total) in floor)
        {
            Row(Ratio(got, total), total == 0);
            by += 7;
        }
        by += 5;
        Row(FloorScore() * 100 / 300, !anything);
        by += 7;
        Row(MissionRatio(), false);
    }

    static void ShowRatio(int bx, int by, int nx, Fonts.Font? font, int color, int percent, bool none, ref bool quick)
    {
        void Percent(int value)
        {
            _videoManager.Bar(nx, by, 18, StatBarHeight, "0");
            if (font == null)
                return;
            int x = value < 10 ? nx + 9 : value < 100 ? nx + 4 : nx - 1;
            _graphicManager.DrawText(x, by, $"{value}%", font, color.ToString());
        }

        if (none)
        {
            _videoManager.Bar(bx, by, StatBarWidth, StatBarHeight, "0");
            _videoManager.Bar(nx, by, 19, StatBarHeight, "0");
            if (font != null)
                _graphicManager.DrawText(nx, by, "N/A", font, "87");
            return;
        }

        percent = Math.Clamp(percent, 0, 100);
        int bars = percent * StatBarWidth / 100;
        _videoManager.Bar(bx, by, StatBarWidth, StatBarHeight, "7");
        Percent(0);
        for (int i = 0; i < bars; i++)
        {
            _videoManager.VerticalLine(by, by + StatBarHeight - 1, bx + i, "200");
            Percent(i == bars - 1 ? percent : Math.Min(percent, 1 + (i + 1) * 2));

            if (!quick)
            {
                // A key finishes the bars at once, and still counts on the screen (a floor's number)
                _inputManager.ProcessEvents();
                if (_inputManager.GetLastKeyPressed() != ScanCodes.sc_None)
                    quick = true;
                if (i % 2 == 0)
                    _audioManager.Play("bs/stats1");
                _videoManager.Update();
                GameEngineManager.DelayMs(14);
            }
        }

        if (bars == 0)
            Percent(percent);
        if (!quick && bars > 0)
            _audioManager.Play("bs/stats2");
        _videoManager.Update();
    }
}
