using Wolf3D.Extensions;
using Wolf3D.Fonts;

namespace Wolf3D;

internal partial class Program
{
    ////////////////////////////////////////////////////////////////////
    //
    // LOAD / SAVE GAME SCREENS
    //
    // A scrolling list of the saves in the save folder on the left and, on the right, the
    // highlighted one's picture and progress. The save screen's first row makes a new save.
    // The list can be sorted by date or name, and shows this game pack's saves or everyone's.
    //
    ////////////////////////////////////////////////////////////////////

    // Layout, inside the window in menudefs/load-game and save-game
    private const int LS_STATUSY = 49;          // how it's sorted and filtered, across the window
    private const int LS_LISTX = 11;            // the list column
    private const int LS_LISTW = 124;
    private const int LS_ROWY = 61;             // the first row
    private const int LS_ROWH = 11;
    private const int LS_ROWS = 10;             // rows showing at once
    private const int LS_EDITW = 312 - (LS_LISTX - 3);  // the name being typed
    private const int LS_ARROWX = 140;
    private const int LS_DIVIDERX = 145;
    private const int LS_PREVX = 148;           // the preview column
    private const int LS_PREVW = 163;
    private const int LS_THUMBX = LS_PREVX + (LS_PREVW - LS_THUMBW) / 2;
    private const int LS_THUMBY = 61;
    private const int LS_THUMBW = 112;
    private const int LS_THUMBH = 56;
    private const int LS_INFOY = LS_THUMBY + LS_THUMBH + 3;
    private const int LS_INFOH = 9;
    private const int LS_SEPY = LS_ROWY + LS_ROWS * LS_ROWH + 3;
    private const int LS_HELPY = LS_SEPY + 3;

    // The save last saved or loaded, which F8/F9 save over and load
    private static SaveInfo? lastSaveGame;

    // How the list is shown; kept for the rest of the session
    private static bool saveListByName;         // else newest first
    private static bool saveListAllGames;       // else only this game pack's

    internal static int CP_LoadGame(int quick)
    {
        //
        // QUICKLOAD?
        //
        if (quick != 0 && lastSaveGame != null)
        {
            // Loaded in place: play carries straight on in the restored level.
            loadedgame = true;
            var loaded = LoadTheGame(lastSaveGame.Path, 0, 0);
            loadedgame = false;
            if (!loaded)
                return 0;

            if (viewsize != 21)
                DrawPlayScreen();
            ContinueMusic(lastgamemusicoffset);
            return 1;
        }

        return LoadSaveScreen(saving: false);
    }

    internal static int CP_SaveGame(int quick)
    {
        //
        // QUICKSAVE?
        //
        if (quick != 0 && lastSaveGame != null)
        {
            if (!SaveTheGame(lastSaveGame.Path, lastSaveGame.Name, 0, 0))
                ShowSaveFailed();
            return 1;
        }

        return LoadSaveScreen(saving: true);
    }

    /// <summary>What the load/save screen is showing, redrawn whole after every change.</summary>
    private sealed class LoadSaveState
    {
        public required bool Saving;
        // The rows; null is the save screen's "new save" row
        public List<SaveInfo?> Rows = [];
        public int Row, Top;
        // Pictures read so far, by path
        public readonly Dictionary<string, SaveThumbnail?> Thumbnails = [];
        // The game as it stands, for the new save row
        public SaveInfo? Current;
        public SaveThumbnail? CurrentThumbnail;

        public SaveInfo? Selected => Row < Rows.Count ? Rows[Row] : null;
    }

    /// <summary>Runs the load or save screen; 1 once a game has been loaded or saved.</summary>
    private static int LoadSaveScreen(bool saving)
    {
        var language = _assetManager.GetText("en-us");
        var state = new LoadSaveState { Saving = saving };
        if (saving)
        {
            state.Current = CurrentSaveInfo("", "");
            state.CurrentThumbnail = _videoManager.MakeThumbnail(ThumbnailWidth);
        }

        FillSaveRows(state, saving ? null : lastSaveGame?.Path);

        DrawLoadSaveScreen(state);
        MenuFadeIn();
        WaitKeyUp();

        int exit = 0;
        while (true)
        {
            CheckPause();
            GameEngineManager.DelayMs(5);
            ReadAnyControl(out var ci);

            //
            // MOVE AROUND
            //
            int moved = state.Row, last = state.Rows.Count - 1;
            if (_inputManager.IsKeyDown(ScanCodes.sc_PgUp))
                moved = Math.Max(state.Row - (LS_ROWS - 1), 0);
            else if (_inputManager.IsKeyDown(ScanCodes.sc_PgDn))
                moved = Math.Min(state.Row + (LS_ROWS - 1), last);
            else if (_inputManager.IsKeyDown(ScanCodes.sc_Home))
                moved = 0;
            else if (_inputManager.IsKeyDown(ScanCodes.sc_End))
                moved = last;
            else if (ci.dir == Direction.North)
                moved = state.Row > 0 ? state.Row - 1 : last;
            else if (ci.dir == Direction.South)
                moved = state.Row < last ? state.Row + 1 : 0;

            if (last >= 0 && moved != state.Row)
            {
                state.Row = moved;
                _audioManager.Play("menu/move1");
                DrawLoadSaveScreen(state);
                TicDelay(15);
                continue;
            }

            //
            // THIS GAME'S SAVES OR EVERYONE'S
            //
            if (ci.dir is Direction.West or Direction.East)
            {
                saveListAllGames = !saveListAllGames;
                _audioManager.Play("menu/move1");
                FillSaveRows(state, state.Selected?.Path);
                DrawLoadSaveScreen(state);
                while (ci.dir != Direction.None)
                {
                    GameEngineManager.DelayMs(5);
                    ReadAnyControl(out ci);
                }
                continue;
            }

            //
            // NEWEST FIRST OR BY NAME
            //
            if (ci.button2 || _inputManager.IsKeyDown(ScanCodes.sc_Tab))
            {
                saveListByName = !saveListByName;
                _audioManager.Play("menu/move1");
                FillSaveRows(state, state.Selected?.Path);
                DrawLoadSaveScreen(state);
                while (_inputManager.IsKeyDown(ScanCodes.sc_Tab))
                    _inputManager.ProcessEvents();
                WaitKeyUp();
                continue;
            }

            //
            // DELETE A SAVE
            //
            if (state.Selected is { } doomed
                && (ci.button3 || _inputManager.IsKeyDown(ScanCodes.sc_Delete) || _inputManager.IsKeyDown(ScanCodes.sc_BackSpace)))
            {
                if (Confirm(string.Format("$STR_LS_DELETE".ToLanguageText(language), NameForConfirm(doomed.Name))) != 0)
                {
                    try
                    {
                        File.Delete(doomed.Path);
                        if (lastSaveGame?.Path == doomed.Path)
                            lastSaveGame = null;
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        Console.WriteLine($"Couldn't delete {doomed.Path}: {e.Message}");
                        Message("The saved game couldn't\nbe deleted.");
                        _inputManager.ClearKeysDown();
                        _inputManager.Ack();
                    }
                    FillSaveRows(state, null);
                }

                _inputManager.ClearKeysDown();
                DrawLoadSaveScreen(state);
                continue;
            }

            //
            // LOAD OR SAVE
            //
            if (ci.button0 || _inputManager.IsKeyDown(ScanCodes.sc_Space) || _inputManager.IsKeyDown(ScanCodes.sc_Enter))
            {
                WaitKeyUp();
                bool done = saving ? SaveFromScreen(state) : LoadFromScreen(state);
                if (done)
                {
                    exit = 1;
                    break;
                }

                DrawLoadSaveScreen(state);
                continue;
            }

            //
            // DONE
            //
            if (ci.button1 && !_inputManager.IsKeyDown(ScanCodes.sc_Alt) || _inputManager.IsKeyDown(ScanCodes.sc_Escape))
            {
                _audioManager.Play("menu/escape");
                break;
            }
        }

        _inputManager.ClearKeysDown();
        WaitKeyUp();
        MenuFadeOut();
        return exit;
    }

    /// <summary>The screen's text is in the small font</summary>
    private static TextStyle LoadSaveStyle(string color, string background = "BKGDCOLOR") => new(SMALL_FONT, color, background);

    /// <summary>Lists the saves as the screen is set to show them, keeping the cursor on <paramref name="select"/> if it's there.</summary>
    private static void FillSaveRows(LoadSaveState state, string? select)
    {
        IEnumerable<SaveInfo> saves = ListSaveGames();     // newest first
        if (!saveListAllGames)
            saves = saves.Where(s => string.Equals(s.GamePack, _gameEngineManager.GamePackId, StringComparison.OrdinalIgnoreCase));
        if (saveListByName)
            saves = saves.OrderBy(s => s.Name, NaturalNameComparer.Instance);    // stable, so newest first within a name

        var rows = new List<SaveInfo?>();
        if (state.Saving)
            rows.Add(null);
        rows.AddRange(saves);

        int found = select == null ? -1 : rows.FindIndex(s => s?.Path == select);
        state.Rows = rows;
        state.Row = found >= 0 ? found : Math.Clamp(state.Row, 0, Math.Max(rows.Count - 1, 0));
    }

    /// <summary>A save's name cut to fit a Confirm box, which is in the large font and can't be wider than the screen.</summary>
    private static string NameForConfirm(string name) => FitText(name, 280, LARGE_FONT);

    /// <summary>Names in the order a person would put them: "test 2" before "test 10".</summary>
    private sealed class NaturalNameComparer : IComparer<string>
    {
        public static readonly NaturalNameComparer Instance = new();

        public int Compare(string? a, string? b)
        {
            a ??= "";
            b ??= "";
            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                if (char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]))
                {
                    int si = i, sj = j;
                    while (i < a.Length && char.IsAsciiDigit(a[i])) i++;
                    while (j < b.Length && char.IsAsciiDigit(b[j])) j++;
                    var da = a.AsSpan(si, i - si).TrimStart('0');
                    var db = b.AsSpan(sj, j - sj).TrimStart('0');
                    int byNumber = da.Length != db.Length ? da.Length.CompareTo(db.Length) : da.SequenceCompareTo(db);
                    if (byNumber != 0)
                        return byNumber;
                    continue;
                }

                int byChar = string.Compare(a, i, b, j, 1, StringComparison.CurrentCultureIgnoreCase);
                if (byChar != 0)
                    return byChar;
                i++;
                j++;
            }
            return (a.Length - i).CompareTo(b.Length - j);
        }
    }

    private static bool LoadFromScreen(LoadSaveState state)
    {
        if (state.Selected is not { } save)
            return false;

        ShootSnd();
        DrawLSAction(0);
        loadedgame = true;

        if (!LoadTheGame(save.Path, LSA_X + 8, LSA_Y + 5))
        {
            loadedgame = false;
            return false;
        }

        lastSaveGame = save;
        StartGame = 1;
        ShootSnd();
        //
        // CHANGE "READ THIS!" TO NORMAL COLOR
        //
        FindMenuItem(MainMenu, "readthis")?.active = 1;
        return true;
    }

    private static bool SaveFromScreen(LoadSaveState state)
    {
        var language = _assetManager.GetText("en-us");
        var existing = state.Selected;

        //
        // OVERWRITE EXISTING SAVEGAME?
        //
        if (existing != null && Confirm(string.Format("$STR_LS_OVERWRITE".ToLanguageText(language), NameForConfirm(existing.Name))) == 0)
            return false;

        ShootSnd();

        // A new save starts out named for the level
        var input = existing?.Name ?? GetMapDisplayName(gamestate.mapon);
        if (input.Length > MaxGameName - 1)
            input = input[..(MaxGameName - 1)];

        DrawLoadSaveScreen(state, editing: true);
        int y = LoadSaveRowY(state.Row, state.Top);
        if (!US_LineInput(LS_LISTX, y, ref input, input, true, MaxGameName - 1, LS_EDITW - 8, LoadSaveStyle("HIGHLIGHT")))
        {
            _audioManager.Play("menu/escape");
            return false;
        }

        var path = existing?.Path ?? NewSaveGamePath();
        DrawLSAction(1);
        if (!SaveTheGame(path, input, LSA_X + 8, LSA_Y + 5))
        {
            ShowSaveFailed();
            return false;
        }

        lastSaveGame = ReadSaveInfo(path);
        ShootSnd();
        return true;
    }

    internal const int LSA_X = 96;
    internal const int LSA_Y = 80;
    internal const int LSA_W = 130;
    internal const int LSA_H = 42;

    /// <summary>The "Loading..." or "Saving..." box, with the disk the save code animates.</summary>
    internal static void DrawLSAction(int which)
    {
        var language = _assetManager.GetText("en-us");
        DrawWindow(LSA_X, LSA_Y, LSA_W, LSA_H, "TEXTCOLOR");
        DrawOutline(LSA_X, LSA_Y, LSA_W, LSA_H, "Black", "HIGHLIGHT");
        _graphicManager.DrawPic("c_diskloading1", LSA_X + 8, LSA_Y + 5);

        var text = which == 0 ? "$STR_LOADING" : "$STR_SAVING";
        TextAt(LSA_X + 46, LSA_Y + 13, new TextStyle(LARGE_FONT, "Black", "TEXTCOLOR")).Print(text.ToLanguageText(language) + "...");

        _videoManager.Update();
    }

    ////////////////////////
    //
    // DRAW THE SCREEN
    //

    private static int LoadSaveRowY(int row, int top) => LS_ROWY + (row - top) * LS_ROWH;

    private static void DrawLoadSaveScreen(LoadSaveState state, bool editing = false)
    {
        var language = _assetManager.GetText("en-us");

        DrawMenuComponents(state.Saving ? "save-game" : "load-game");

        //
        // HOW THE LIST IS SORTED, AND WHOSE SAVES IT SHOWS
        //
        var status = LoadSaveStyle("READHCOLOR");
        TextAt(LS_LISTX, LS_STATUSY, status).Print((saveListByName ? "$STR_LS_SORTNAME" : "$STR_LS_SORTDATE").ToLanguageText(language));
        var filter = $"< {(saveListAllGames ? "$STR_LS_FILTERALL" : "$STR_LS_FILTERGAME").ToLanguageText(language)} >";
        TextAt(LS_PREVX + LS_PREVW - 2 - TextWidth(filter, status.Font), LS_STATUSY, status).Print(filter);
        _videoManager.HorizontalLine(8, 312, LS_ROWY - 3, "DEACTIVE");

        //
        // THE LIST, SCROLLED SO THE CURSOR SHOWS
        //
        if (state.Row < state.Top)
            state.Top = state.Row;
        if (state.Row >= state.Top + LS_ROWS)
            state.Top = state.Row - LS_ROWS + 1;
        state.Top = Math.Clamp(state.Top, 0, Math.Max(0, state.Rows.Count - LS_ROWS));

        for (int i = 0; i < LS_ROWS && state.Top + i < state.Rows.Count; i++)
        {
            int r = state.Top + i;
            DrawLoadSaveRow(state.Rows[r], LoadSaveRowY(r, state.Top), r == state.Row, editing: false);
        }

        // With nothing to list there's nothing to preview either, so it says so across the window
        if (state.Rows.Count == 0)
        {
            var empty = CenteredText(8, 304, LS_ROWY + 4 * LS_ROWH, LoadSaveStyle("TEXTCOLOR"));
            empty.CPrint(FitText("$STR_LS_NOSAVES".ToLanguageText(language), empty.Width, SMALL_FONT));
            if (!saveListAllGames)
                empty.CPrint(FitText("$STR_LS_TRYALL".ToLanguageText(language), empty.Width, SMALL_FONT), LoadSaveStyle("READCOLOR"));
        }

        if (state.Top > 0)
            DrawCustomArrow(LS_ARROWX, LS_ROWY, up: true);
        if (state.Top + LS_ROWS < state.Rows.Count)
            DrawCustomArrow(LS_ARROWX, LS_SEPY - 7, up: false);

        if (state.Rows.Count > 0)
            _videoManager.Bar(LS_DIVIDERX, LS_ROWY - 2, 1, LS_SEPY - LS_ROWY + 1, "DEACTIVE");

        //
        // THE HIGHLIGHTED SAVE: ITS PICTURE AND PROGRESS
        //
        var info = state.Selected ?? state.Current;
        SaveThumbnail? thumbnail = null;
        if (state.Selected is { } save)
        {
            if (!state.Thumbnails.TryGetValue(save.Path, out thumbnail))
                state.Thumbnails[save.Path] = thumbnail = ReadSaveThumbnail(save);
        }
        else
            thumbnail = state.CurrentThumbnail;

        if (info != null)
        {
            DrawSaveThumbnail(thumbnail);
            DrawSaveDetails(info, isNew: state.Selected == null);
        }

        // The name being typed goes over the preview, so it's drawn after it
        if (editing)
            DrawLoadSaveRow(state.Selected, LoadSaveRowY(state.Row, state.Top), selected: true, editing: true);

        //
        // WHICH KEYS DO WHAT
        //
        _videoManager.HorizontalLine(8, 312, LS_SEPY, "DEACTIVE");
        CenteredText(0, 320, LS_HELPY, LoadSaveStyle("TEXTCOLOR"))
            .CPrint(FitText((state.Saving ? "$STR_LS_HELPSAVE" : "$STR_LS_HELPLOAD").ToLanguageText(language), 300, SMALL_FONT));

        _videoManager.Update();
    }

    /// <summary>A row: the save's name, or the new save row; the cursor's row is a lit box, and an edited row an empty field.</summary>
    private static void DrawLoadSaveRow(SaveInfo? save, int y, bool selected, bool editing)
    {
        var language = _assetManager.GetText("en-us");

        if (editing)
        {
            // Across the window, over the preview, so the name has room for all its letters
            _videoManager.Bar(LS_LISTX - 3, y - 1, LS_EDITW, LS_ROWH, "BKGDCOLOR");
            DrawOutline(LS_LISTX - 3, y - 1, LS_EDITW, LS_ROWH, "HIGHLIGHT", "HIGHLIGHT");
            return;     // US_LineInput draws the name
        }

        TextStyle style;
        if (selected)
        {
            _videoManager.Bar(LS_LISTX - 3, y - 1, LS_LISTW + 4, LS_ROWH, "TEXTCOLOR");
            DrawOutline(LS_LISTX - 3, y - 1, LS_LISTW + 4, LS_ROWH, "Black", "HIGHLIGHT");
            style = LoadSaveStyle("Black", "TEXTCOLOR");
        }
        else
            style = LoadSaveStyle(save == null ? "READCOLOR" : "TEXTCOLOR");

        TextAt(LS_LISTX, y, style).Print(FitText(save?.Name ?? "$STR_LS_NEWSAVE".ToLanguageText(language), LS_LISTW - 2, style.Font));
    }

    /// <summary>The picture, fitted inside its box keeping its shape.</summary>
    private static void DrawSaveThumbnail(SaveThumbnail? thumbnail)
    {
        _videoManager.Bar(LS_THUMBX, LS_THUMBY, LS_THUMBW, LS_THUMBH, "Black");
        DrawOutline(LS_THUMBX - 1, LS_THUMBY - 1, LS_THUMBW + 2, LS_THUMBH + 2, "DEACTIVE", "DEACTIVE");

        if (thumbnail == null)
        {
            CenteredText(LS_THUMBX, LS_THUMBW, LS_THUMBY + LS_THUMBH / 2 - 5, LoadSaveStyle("DEACTIVE", "Black"))
                .CPrint(FitText("$STR_LS_NOPICTURE".ToLanguageText(_assetManager.GetText("en-us")), LS_THUMBW, SMALL_FONT));
            return;
        }

        int w = LS_THUMBW, h = w * thumbnail.Height / thumbnail.Width;
        if (h > LS_THUMBH)
        {
            h = LS_THUMBH;
            w = h * thumbnail.Width / thumbnail.Height;
        }
        _videoManager.DrawThumbnail(thumbnail, LS_THUMBX + (LS_THUMBW - w) / 2, LS_THUMBY + (LS_THUMBH - h) / 2, w, h);
    }

    /// <summary>When it was saved, where, on what skill, and how far along.</summary>
    private static void DrawSaveDetails(SaveInfo info, bool isNew)
    {
        var language = _assetManager.GetText("en-us");
        string L(string key) => key.ToLanguageText(language);

        string when = isNew ? L("$STR_LS_NEWSAVE")
            : info.Kind == SaveKind.Quick ? $"{L("$STR_LS_QUICK")}  {info.SavedAt.ToLocalTime():MM-dd HH:mm}"
            : info.Kind == SaveKind.Auto ? $"{L("$STR_LS_AUTO")}  {info.SavedAt.ToLocalTime():MM-dd HH:mm}"
            : $"{info.SavedAt.ToLocalTime():yyyy-MM-dd  HH:mm}";

        // As the intermission shows them; a level with none of something shows a dash
        static string Ratio(int count, int total) => total > 0 ? $"{count * 100 / total}%" : "-";

        // A save from another game pack says whose it is (it won't load here)
        string skill = string.Equals(info.GamePack, _gameEngineManager.GamePackId, StringComparison.OrdinalIgnoreCase)
            ? info.SkillName
            : $"{info.SkillName} ({info.GamePack})";

        (string Text, string Color)[] lines =
        [
            (when, "READHCOLOR"),
            (info.MapName, "HIGHLIGHT"),
            (skill, "TEXTCOLOR"),
            (string.Format(L("$STR_LS_TIME"), FormatPlayTime(info.LevelTime), FormatPlayTime(info.PlayTime)), "TEXTCOLOR"),
            (string.Format(L("$STR_LS_KILLS"), Ratio(info.Kills, info.KillTotal), Ratio(info.Secrets, info.SecretTotal)), "TEXTCOLOR"),
            (string.Format(L("$STR_LS_TREASURE"), Ratio(info.Treasure, info.TreasureTotal), info.Score), "TEXTCOLOR"),
        ];

        for (int i = 0; i < lines.Length; i++)
            CenteredText(LS_PREVX, LS_PREVW, LS_INFOY + i * LS_INFOH, LoadSaveStyle(lines[i].Color))
                .CPrint(FitText(lines[i].Text, LS_PREVW - 4, SMALL_FONT));
    }
}
