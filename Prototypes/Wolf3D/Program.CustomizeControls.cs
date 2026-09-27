using Wolf3D.Configuration;
using Wolf3D.Extensions;
using static SDL2.SDL;

namespace Wolf3D;

internal partial class Program
{
    ////////////////////////////////////////////////////////////////////
    //
    // CUSTOMIZE CONTROLS
    //
    // A scrolling list of the controls from menudefs/customize, grouped under headers, each
    // with two key/mouse slots and a controller slot (ControlBindings' slots, left to right).
    // Choosing a slot waits for the key or button to put in it; whatever else in the same group
    // had that key or button loses it.
    //
    ////////////////////////////////////////////////////////////////////

    private enum CustomRowKind { Header, Blank, Control, Reset }

    private sealed record CustomRow(CustomRowKind Kind, string Text, ControlAction Action = default)
    {
        public bool Selectable => Kind is CustomRowKind.Control or CustomRowKind.Reset;
    }

    // The rows, from menudefs/customize; loaded with the other menus in CheckForEpisodes
    private static List<CustomRow> customizeRows = [];

    // Layout, inside the window in menudefs/customize (the title graphic comes down to it)
    private const int CUS_TITLEY = 48;          // the column titles
    private const int CUS_ROWY = 61;            // the first row
    private const int CUS_ROWH = 11;
    private const int CUS_ROWS = 9;             // rows showing at once
    private const int CUS_NAMEX = 12;
    private static readonly int[] CUS_COLX = [124, 188, 252];   // a column per ControlBindings slot
    private const int CUS_COLW = 62;
    private const int CUS_SEPY = CUS_ROWY + CUS_ROWS * CUS_ROWH + 1;
    private const int CUS_INFOY = CUS_SEPY + 2;     // what's going on: a prompt, a message, the controller
    private const int CUS_HELPY = CUS_INFOY + 10;   // which keys do what
    private const int CUS_ARROWX = 309;

    // How long a slot waits for a key or button before giving up, in tics
    private const int CUS_CAPTURETICS = 6 * 70;

    private static List<CustomRow> LoadCustomizeRows()
    {
        var menu = _assetManager.GetMenu("customize")
            ?? throw new InvalidOperationException("Menu 'customize' not found in menudefs");
        var language = _assetManager.GetText("en-us");
        var rows = new List<CustomRow>();

        foreach (var item in menu.MenuItems.Where(mi => InCurrentGamePack(mi.GamePacks)))
        {
            string text = (item.Text ?? "").ToLanguageText(language);
            switch (item)
            {
                case HeaderMenuItem:
                    rows.Add(new(CustomRowKind.Header, text));
                    break;
                case ControlMenuItem control when ControlAction.TryParse(control.Control ?? "", out var action):
                    rows.Add(new(CustomRowKind.Control, text, action));
                    break;
                case ControlMenuItem control:
                    Console.WriteLine($"Menu 'customize': unknown control '{control.Control}'");
                    break;
                case MenuSwitcher { Id: "reset" }:
                    rows.Add(new(CustomRowKind.Reset, text));
                    break;
                case BlankMenuItem:
                    rows.Add(new(CustomRowKind.Blank, ""));
                    break;
                default:
                    Console.WriteLine($"Menu 'customize': a {item.GetType().Name} has no use here, skipped");
                    break;
            }
        }

        return rows;
    }

    internal static int CustomControls(int _)
    {
        var rows = customizeRows;
        int row = NextCustomRow(rows, -1, 1), col = 0, top = 0;
        string info = "";

        if (row < 0)
            return 0;

        DrawCustomizeScreen(rows, row, col, ref top, info);
        MenuFadeIn();
        WaitKeyUp();

        while (true)
        {
            CheckPause();
            GameEngineManager.DelayMs(5);
            ReadAnyControl(out var ci);

            //
            // MOVE AROUND
            //
            int moved = row, movedCol = col;
            if (_inputManager.IsKeyDown(ScanCodes.sc_PgUp) || _inputManager.IsKeyDown(ScanCodes.sc_PgDn))
            {
                int step = _inputManager.IsKeyDown(ScanCodes.sc_PgUp) ? -1 : 1;
                for (int i = 0; i < CUS_ROWS - 1; i++)
                    moved = NextCustomRow(rows, moved, step, wrap: false);
            }
            else
            {
                switch (ci.dir)
                {
                    case Direction.North: moved = NextCustomRow(rows, row, -1); break;
                    case Direction.South: moved = NextCustomRow(rows, row, 1); break;
                    case Direction.West: movedCol = Math.Max(col - 1, 0); break;
                    case Direction.East: movedCol = Math.Min(col + 1, ControlBindings.SlotCount - 1); break;
                }
            }

            if (moved != row || movedCol != col)
            {
                row = moved;
                col = movedCol;
                info = "";
                _audioManager.Play("menu/move1");
                DrawCustomizeScreen(rows, row, col, ref top, info);
                TicDelay(15);
                continue;
            }

            var current = rows[row];

            //
            // CHANGE A SLOT, OR RESET THEM ALL
            //
            if (ci.button0 || _inputManager.IsKeyDown(ScanCodes.sc_Space) || _inputManager.IsKeyDown(ScanCodes.sc_Enter))
            {
                WaitKeyUp();

                if (current.Kind == CustomRowKind.Reset)
                {
                    ShootSnd();
                    if (Confirm("$STR_CTL_RESETASK") != 0)
                        controls.SetDefaults();
                    info = "";
                }
                else
                    info = ChangeControl(rows, row, col, ref top);

                DrawCustomizeScreen(rows, row, col, ref top, info);
                continue;
            }

            //
            // CLEAR A SLOT
            //
            if (current.Kind == CustomRowKind.Control
                && (ci.button3 || _inputManager.IsKeyDown(ScanCodes.sc_Delete) || _inputManager.IsKeyDown(ScanCodes.sc_BackSpace)))
            {
                if (!controls[current.Action, col].IsNone)
                {
                    controls[current.Action, col] = InputCode.None;
                    _audioManager.Play("menu/escape");
                }

                _inputManager.ClearKeysDown();
                WaitKeyUp();
                info = "";
                DrawCustomizeScreen(rows, row, col, ref top, info);
                continue;
            }

            //
            // DONE
            //
            if (ci.button1 && !_inputManager.IsKeyDown(ScanCodes.sc_Alt) || _inputManager.IsKeyDown(ScanCodes.sc_Escape))
                break;
        }

        _audioManager.Play("menu/escape");
        _inputManager.ClearKeysDown();
        WaitKeyUp();
        MenuFadeOut();
        fontnumber = "LargeFont";

        return 0;
    }

    /// <summary>The next row that can be picked, going <paramref name="step"/> from <paramref name="from"/>; -1 with none.</summary>
    private static int NextCustomRow(List<CustomRow> rows, int from, int step, bool wrap = true)
    {
        for (int i = 1; i <= rows.Count; i++)
        {
            int next = from + step * i;
            if (!wrap && (next < 0 || next >= rows.Count))
                return from;

            next = ((next % rows.Count) + rows.Count) % rows.Count;
            if (rows[next].Selectable)
                return next;
        }
        return rows.Any(r => r.Selectable) ? from : -1;
    }

    /// <summary>
    /// Waits for the key or button to go in a slot, and puts it there. Returns the line to show
    /// afterwards: what it was taken off, or why nothing changed.
    /// </summary>
    private static string ChangeControl(List<CustomRow> rows, int row, int col, ref int top)
    {
        var language = _assetManager.GetText("en-us");
        var action = rows[row].Action;
        bool controller = col == ControlBindings.ControllerSlot;

        if (controller && !_inputManager.JoyPresent())
        {
            _audioManager.Play("menu/escape");
            return "$STR_CTL_NOPAD".ToLanguageText(language);
        }

        ShootSnd();
        DrawCustomizeScreen(rows, row, col, ref top, "", capturing: true);

        var code = CaptureInput(controller, CUS_COLX[col], CustomRowY(row, top));
        if (code.IsNone)
        {
            _audioManager.Play("menu/escape");
            return "";
        }

        var takenFrom = controls.Set(action, col, code);
        ShootSnd();

        // Let it go before carrying on, or a key or button the menu also uses (Enter, A, the
        // d-pad) is taken as a menu press too
        while (_inputManager.IsInputDown(code))
        {
            _inputManager.ProcessEvents();
            GameEngineManager.DelayMs(5);
        }
        _inputManager.ClearKeysDown();
        WaitKeyUp();

        if (takenFrom.Count == 0)
            return "";

        var labels = takenFrom.Select(taken => rows.FirstOrDefault(r => r.Kind == CustomRowKind.Control && r.Action == taken)?.Text ?? taken.Name);
        return $"{"$STR_CTL_TAKEN".ToLanguageText(language)} {string.Join(", ", labels)}";
    }

    /// <summary>
    /// Waits for a fresh press: a key or mouse button, or for the controller slot a controller
    /// button, trigger or stick. The slot's cursor flashes a "?" meanwhile. Escape, or waiting
    /// too long, gives up and returns None. Grave is left for the console.
    /// </summary>
    private static InputCode CaptureInput(bool controller, int x, int y)
    {
        IEnumerable<InputCode> candidates;
        if (!controller)
            candidates = Enumerable.Range(1, InputCode.MouseButtonCount).Select(InputCode.FromMouseButton);
        else if (_inputManager.HasGameController)
            candidates = Enum.GetValues<SDL_GameControllerButton>().Select(InputCode.FromPadButton)
                .Concat(Enum.GetValues<SDL_GameControllerAxis>().SelectMany(axis =>
                    new[] { InputCode.FromPadAxis(axis, false), InputCode.FromPadAxis(axis, true) }));
        else
            candidates = Enumerable.Range(0, _inputManager.JoyNumButtons).Select(InputCode.FromJoyButton);

        var watched = candidates.Where(code => !code.IsNone).Distinct().ToList();

        // Only a press that starts from here counts: not what's already held, nor a stick left pushed
        var held = watched.Where(_inputManager.IsInputDown).ToHashSet();

        _inputManager.ClearKeysDown();
        int start = (int)GameEngineManager.GetTimeCount();
        int lastFlash = start;
        bool showMark = true;

        while ((int)GameEngineManager.GetTimeCount() - start < CUS_CAPTURETICS)
        {
            _inputManager.ProcessEvents();

            while (_inputManager.TryTakePressedKey(out var key))
            {
                if (key == ScanCodes.sc_Escape)
                    return InputCode.None;
                if (!controller && key != ScanCodes.sc_Grave)
                    return InputCode.FromKey(key);
            }

            foreach (var code in watched)
            {
                if (!_inputManager.IsInputDown(code))
                    held.Remove(code);
                else if (!held.Contains(code))
                    return code;
            }

            if (GameEngineManager.GetTimeCount() - lastFlash > 10)
            {
                showMark = !showMark;
                DrawCustomCell(x, y, showMark ? "?" : "", selected: true, unbound: false);
                _videoManager.Update();
                lastFlash = (int)GameEngineManager.GetTimeCount();
            }

            GameEngineManager.DelayMs(5);
        }

        return InputCode.None;
    }

    ////////////////////////
    //
    // DRAW THE SCREEN
    //

    private static int CustomRowY(int row, int top) => CUS_ROWY + (row - top) * CUS_ROWH;

    private static void DrawCustomizeScreen(List<CustomRow> rows, int row, int col, ref int top, string info, bool capturing = false)
    {
        var language = _assetManager.GetText("en-us");

        DrawMenuComponents("customize");
        fontnumber = "SmallFont";
        WindowX = 0;
        WindowW = 320;

        //
        // COLUMN TITLES
        //
        SETFONTCOLOR("READHCOLOR", "BORDCOLOR");
        PrintY = CUS_TITLEY;
        PrintX = CUS_NAMEX;
        US_Print("$STR_CTL_COLCONTROL".ToLanguageText(language));
        string[] titles = ["$STR_CTL_COLKEY1", "$STR_CTL_COLKEY2", "$STR_CTL_COLPAD"];
        for (int c = 0; c < titles.Length; c++)
        {
            PrintX = (ushort)CUS_COLX[c];
            PrintY = CUS_TITLEY;
            US_Print(titles[c].ToLanguageText(language));
        }
        _videoManager.HorizontalLine(6, 314, CUS_ROWY - 2, "DEACTIVE");

        //
        // SCROLL SO THE CURSOR SHOWS, WITH ITS SECTION'S HEADER WHEN IT'S THE FIRST ROW UNDER ONE
        //
        if (row < top)
            top = row;
        if (row >= top + CUS_ROWS)
            top = row - CUS_ROWS + 1;
        if (row > 0 && row - 1 < top && rows[row - 1].Kind == CustomRowKind.Header)
            top = row - 1;
        top = Math.Clamp(top, 0, Math.Max(0, rows.Count - CUS_ROWS));

        for (int i = 0; i < CUS_ROWS && top + i < rows.Count; i++)
        {
            int r = top + i;
            DrawCustomRow(rows[r], CustomRowY(r, top), r == row, col, capturing && r == row);
        }

        if (top > 0)
            DrawCustomArrow(CUS_ARROWX, CUS_ROWY, up: true);
        if (top + CUS_ROWS < rows.Count)
            DrawCustomArrow(CUS_ARROWX, CUS_SEPY - 6, up: false);

        //
        // WHAT'S GOING ON, AND WHICH KEYS DO WHAT
        //
        _videoManager.HorizontalLine(6, 314, CUS_SEPY, "DEACTIVE");

        var current = rows[row];
        string help, infoColor = "HIGHLIGHT";
        if (capturing)
        {
            string prompt = col == ControlBindings.ControllerSlot ? "$STR_CTL_PRESSPAD" : "$STR_CTL_PRESSKEY";
            info = $"{prompt.ToLanguageText(language)} {current.Text}";
            help = "$STR_CTL_CANCEL".ToLanguageText(language);
        }
        else
        {
            if (info.Length == 0 && current.Kind == CustomRowKind.Control && col == ControlBindings.ControllerSlot)
            {
                info = _inputManager.ControllerName is string name
                    ? $"{"$STR_CTL_PAD".ToLanguageText(language)} {name}"
                    : "$STR_CTL_NOPAD".ToLanguageText(language);
                infoColor = "READCOLOR";
            }

            help = (current.Kind == CustomRowKind.Reset ? "$STR_CTL_HELPRESET" : "$STR_CTL_HELP").ToLanguageText(language);
        }

        SETFONTCOLOR(infoColor, "BKGDCOLOR");
        PrintY = CUS_INFOY;
        US_CPrint(FitText(info, 300));

        SETFONTCOLOR("TEXTCOLOR", "BKGDCOLOR");
        PrintY = CUS_HELPY;
        US_CPrint(FitText(help, 300));

        SETFONTCOLOR("TEXTCOLOR", "BKGDCOLOR");
        _videoManager.Update();
    }

    private static void DrawCustomRow(CustomRow row, int y, bool selected, int col, bool capturing)
    {
        switch (row.Kind)
        {
            case CustomRowKind.Header:
                SETFONTCOLOR("READCOLOR", "BKGDCOLOR");
                PrintX = CUS_NAMEX - 4;
                PrintY = (ushort)y;
                US_Print(row.Text);
                USL_MeasureString(row.Text, out ushort w, out _);
                _videoManager.HorizontalLine(CUS_NAMEX + w + 2, 302, y + 5, "DEACTIVE");
                break;

            case CustomRowKind.Reset:
                SETFONTCOLOR(selected ? "HIGHLIGHT" : "TEXTCOLOR", "BKGDCOLOR");
                PrintX = CUS_NAMEX;
                PrintY = (ushort)y;
                US_Print(row.Text);
                break;

            case CustomRowKind.Control:
                SETFONTCOLOR(selected ? "HIGHLIGHT" : "TEXTCOLOR", "BKGDCOLOR");
                PrintX = CUS_NAMEX;
                PrintY = (ushort)y;
                US_Print(FitText(row.Text, CUS_COLX[0] - CUS_NAMEX - 4));

                for (int c = 0; c < ControlBindings.SlotCount; c++)
                {
                    var code = controls[row.Action, c];
                    bool cursor = selected && c == col;
                    string label = cursor && capturing ? "?"
                        : code.IsNone ? "$STR_CTL_UNBOUND".ToLanguageText(_assetManager.GetText("en-us"))
                        : BindingLabel(code);
                    DrawCustomCell(CUS_COLX[c], y, label, cursor, unbound: code.IsNone);
                }
                break;
        }
    }

    /// <summary>One slot's key or button; the cursor's slot is a lit box, like the old screen's.</summary>
    private static void DrawCustomCell(int x, int y, string label, bool selected, bool unbound)
    {
        if (selected)
        {
            _videoManager.Bar(x - 3, y - 1, CUS_COLW - 2, CUS_ROWH, "TEXTCOLOR");
            DrawOutline(x - 3, y - 1, CUS_COLW - 2, CUS_ROWH, "Black", "HIGHLIGHT");
            SETFONTCOLOR("Black", "TEXTCOLOR");
        }
        else
            SETFONTCOLOR(unbound ? "DEACTIVE" : "TEXTCOLOR", "BKGDCOLOR");

        PrintX = (ushort)x;
        PrintY = (ushort)y;
        US_Print(FitText(label, CUS_COLW - 6));
    }

    private static void DrawCustomArrow(int x, int y, bool up)
    {
        for (int i = 0; i < 4; i++)
            _videoManager.HorizontalLine(x - i, x + i, up ? y + i : y + 3 - i, "READCOLOR");
    }

    /// <summary>
    /// A key or button as the screen shows it: keys by their short names ("Ctrl", "KP 8"), and
    /// controller buttons without the "Pad " the controller column makes plain.
    /// </summary>
    private static string BindingLabel(InputCode code) => code.Device switch
    {
        InputDevice.Key => _inputManager.GetScanName(code.Key) is var name && name != "?" ? name : code.ToString(),
        InputDevice.PadButton or InputDevice.PadAxis => code.ToString()["Pad ".Length..],
        _ => code.ToString(),
    };

    /// <summary>Cuts text short to fit a width in the current font.</summary>
    private static string FitText(string text, int width)
    {
        USL_MeasureString(text, out ushort w, out _);
        while (text.Length > 1 && w > width)
        {
            text = text[..^1];
            USL_MeasureString(text, out w, out _);
        }
        return text;
    }
}
