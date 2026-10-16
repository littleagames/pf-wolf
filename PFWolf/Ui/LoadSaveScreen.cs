using PFWolf.Extensions;
using PFWolf.Fonts;

namespace PFWolf.Ui;

////////////////////////////////////////////////////////////////////
//
// LOAD / SAVE GAME SCREENS
//
// A scrolling list of the saves in the save folder on the left and, on the right, the
// highlighted one's picture and progress. The save screen's first row makes a new save.
// The list can be sorted by date or name, and shows this game pack's saves or everyone's.
//
////////////////////////////////////////////////////////////////////

/// <summary>What the load/save screen needs from the game</summary>
/// <param name="CurrentSave">The header a save of the game as it stands would have (for the new save row)</param>
/// <param name="CurrentThumbnail">A picture of the game as it stands</param>
/// <param name="ListSaves">Every readable save in the save folder, newest first</param>
/// <param name="NewSavePath">A path for a new save</param>
/// <param name="NewSaveName">What a new save is first named: the level's name</param>
/// <param name="HasOtherMods">Whether a save was made with other mods than are loaded now</param>
/// <param name="Load">Loads a save, the disk animating at (x, y); false (having said why) if it couldn't</param>
/// <param name="Save">Saves the game to a path with a name, the disk animating at (x, y); false if it couldn't</param>
/// <param name="Loaded">After a game is loaded from the screen: the menu starts it</param>
/// <param name="SaveFailed">Says a save couldn't be written</param>
internal sealed record LoadSaveHooks(Func<SaveInfo> CurrentSave, Func<SaveThumbnail?> CurrentThumbnail, Func<List<SaveInfo>> ListSaves,
    Func<string> NewSavePath, Func<string> NewSaveName, Func<SaveInfo, bool> HasOtherMods,
    Func<string, int, int, bool> Load, Func<string, string, int, int, bool> Save, Action Loaded, Action SaveFailed);

internal sealed class LoadSaveScreen(GameEngineManager engine, AssetManager assets, InputManager input, AudioManager audio, VideoManager video,
    GraphicManager graphics, MenuDrawing draw, MenuLoop loop, MenuInput menuInput, MenuDefs defs, UiText ui, LineInput lineInput, LoadSaveHooks hooks)
{
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

    // The save last saved or loaded on these screens, which the load screen starts on
    internal SaveInfo? lastSaveGame;

    // How the list is shown; kept for the rest of the session
    private bool saveListByName;         // else newest first
    private bool saveListAllGames;       // else only this game pack's

    /// <summary>What the load/save screen is showing, redrawn whole after every change.</summary>
    internal sealed class LoadSaveState
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
    internal int Run(bool saving)
    {
        var language = assets.GetText("en-us");
        var state = new LoadSaveState { Saving = saving };
        if (saving)
        {
            state.Current = hooks.CurrentSave();
            state.CurrentThumbnail = hooks.CurrentThumbnail();
        }

        FillSaveRows(state, saving ? null : lastSaveGame?.Path);

        DrawLoadSaveScreen(state);
        draw.MenuFadeIn();
        menuInput.WaitKeyUp();

        int exit = 0;
        while (true)
        {
            menuInput.CheckPause();
            GameEngineManager.DelayMs(5);
            menuInput.ReadAnyControl(out var ci);

            //
            // MOVE AROUND
            //
            int moved = state.Row, last = state.Rows.Count - 1;
            if (input.IsKeyDown(ScanCodes.sc_PgUp))
                moved = Math.Max(state.Row - (LS_ROWS - 1), 0);
            else if (input.IsKeyDown(ScanCodes.sc_PgDn))
                moved = Math.Min(state.Row + (LS_ROWS - 1), last);
            else if (input.IsKeyDown(ScanCodes.sc_Home))
                moved = 0;
            else if (input.IsKeyDown(ScanCodes.sc_End))
                moved = last;
            else if (ci.dir == Direction.North)
                moved = state.Row > 0 ? state.Row - 1 : last;
            else if (ci.dir == Direction.South)
                moved = state.Row < last ? state.Row + 1 : 0;

            if (last >= 0 && moved != state.Row)
            {
                state.Row = moved;
                audio.Play("menu/move1");
                DrawLoadSaveScreen(state);
                menuInput.TicDelay(15);
                continue;
            }

            //
            // THIS GAME'S SAVES OR EVERYONE'S
            //
            if (ci.dir is Direction.West or Direction.East)
            {
                saveListAllGames = !saveListAllGames;
                audio.Play("menu/move1");
                FillSaveRows(state, state.Selected?.Path);
                DrawLoadSaveScreen(state);
                while (ci.dir != Direction.None)
                {
                    GameEngineManager.DelayMs(5);
                    menuInput.ReadAnyControl(out ci);
                }
                continue;
            }

            //
            // NEWEST FIRST OR BY NAME
            //
            if (ci.button2 || input.IsKeyDown(ScanCodes.sc_Tab))
            {
                saveListByName = !saveListByName;
                audio.Play("menu/move1");
                FillSaveRows(state, state.Selected?.Path);
                DrawLoadSaveScreen(state);
                while (input.IsKeyDown(ScanCodes.sc_Tab))
                    input.ProcessEvents();
                menuInput.WaitKeyUp();
                continue;
            }

            //
            // DELETE A SAVE
            //
            if (state.Selected is { } doomed
                && (ci.button3 || input.IsKeyDown(ScanCodes.sc_Delete) || input.IsKeyDown(ScanCodes.sc_BackSpace)))
            {
                if (loop.Confirm(string.Format("$STR_LS_DELETE".ToLanguageText(language), NameForConfirm(doomed.Name))) != 0)
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
                        draw.Message("The saved game couldn't\nbe deleted.");
                        input.ClearKeysDown();
                        input.Ack();
                    }
                    FillSaveRows(state, null);
                }

                input.ClearKeysDown();
                DrawLoadSaveScreen(state);
                continue;
            }

            //
            // LOAD OR SAVE
            //
            if (ci.button0 || input.IsKeyDown(ScanCodes.sc_Space) || input.IsKeyDown(ScanCodes.sc_Enter))
            {
                menuInput.WaitKeyUp();
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
            if (ci.button1 && !input.IsKeyDown(ScanCodes.sc_Alt) || input.IsKeyDown(ScanCodes.sc_Escape))
            {
                audio.Play("menu/escape");
                break;
            }
        }

        input.ClearKeysDown();
        menuInput.WaitKeyUp();
        draw.MenuFadeOut();
        return exit;
    }

    /// <summary>The screen's text is in the small font</summary>
    private static TextStyle LoadSaveStyle(string color, string background = "BKGDCOLOR") => new(UiFonts.SMALL_FONT, color, background);

    /// <summary>Lists the saves as the screen is set to show them, keeping the cursor on <paramref name="select"/> if it's there.</summary>
    private void FillSaveRows(LoadSaveState state, string? select)
    {
        var rows = SaveRows(hooks.ListSaves(), state.Saving, saveListAllGames, saveListByName, engine.GamePackId);

        int found = select == null ? -1 : rows.FindIndex(s => s?.Path == select);
        state.Rows = rows;
        state.Row = found >= 0 ? found : Math.Clamp(state.Row, 0, Math.Max(rows.Count - 1, 0));
    }

    /// <summary>
    /// The rows for <paramref name="saves"/> (newest first): only <paramref name="gamePack"/>'s
    /// unless <paramref name="allGames"/>, by name if <paramref name="byName"/>, after a null
    /// row for a new save when <paramref name="saving"/>
    /// </summary>
    internal static List<SaveInfo?> SaveRows(IEnumerable<SaveInfo> saves, bool saving, bool allGames, bool byName, string gamePack)
    {
        if (!allGames)
            saves = saves.Where(s => string.Equals(s.GamePack, gamePack, StringComparison.OrdinalIgnoreCase));
        if (byName)
            saves = saves.OrderBy(s => s.Name, NaturalNameComparer.Instance);    // stable, so newest first within a name

        var rows = new List<SaveInfo?>();
        if (saving)
            rows.Add(null);
        rows.AddRange(saves);
        return rows;
    }

    /// <summary>A save's name cut to fit a Confirm box, which is in the large font and can't be wider than the screen.</summary>
    private string NameForConfirm(string name) => ui.FitText(name, 280, UiFonts.LARGE_FONT);

    /// <summary>Names in the order a person would put them: "test 2" before "test 10".</summary>
    internal sealed class NaturalNameComparer : IComparer<string>
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

    private bool LoadFromScreen(LoadSaveState state)
    {
        if (state.Selected is not { } save)
            return false;

        loop.ShootSnd();
        DrawLSAction(0);

        if (!hooks.Load(save.Path, LSA_X + 8, LSA_Y + 5))
            return false;

        lastSaveGame = save;
        hooks.Loaded();
        loop.ShootSnd();
        return true;
    }

    private bool SaveFromScreen(LoadSaveState state)
    {
        var language = assets.GetText("en-us");
        var existing = state.Selected;

        //
        // OVERWRITE EXISTING SAVEGAME?
        //
        if (existing != null && loop.Confirm(string.Format("$STR_LS_OVERWRITE".ToLanguageText(language), NameForConfirm(existing.Name))) == 0)
            return false;

        loop.ShootSnd();

        // A new save starts out named for the level
        var name = existing?.Name ?? hooks.NewSaveName();
        if (name.Length > GameConstants.MaxGameName - 1)
            name = name[..(GameConstants.MaxGameName - 1)];

        DrawLoadSaveScreen(state, editing: true);
        int y = LoadSaveRowY(state.Row, state.Top);
        if (!lineInput.Run(LS_LISTX, y, ref name, name, true, GameConstants.MaxGameName - 1, LS_EDITW - 8, LoadSaveStyle("HIGHLIGHT")))
        {
            audio.Play("menu/escape");
            return false;
        }

        var path = existing?.Path ?? hooks.NewSavePath();
        DrawLSAction(1);
        if (!hooks.Save(path, name, LSA_X + 8, LSA_Y + 5))
        {
            hooks.SaveFailed();
            return false;
        }

        lastSaveGame = SaveFile.ReadInfo(path);
        loop.ShootSnd();
        return true;
    }

    internal const int LSA_X = 96;
    internal const int LSA_Y = 80;
    internal const int LSA_W = 130;
    internal const int LSA_H = 42;

    /// <summary>The "Loading..." or "Saving..." box, with the disk the save code animates.</summary>
    internal void DrawLSAction(int which)
    {
        var language = assets.GetText("en-us");
        draw.DrawWindow(LSA_X, LSA_Y, LSA_W, LSA_H, "TEXTCOLOR");
        draw.DrawOutline(LSA_X, LSA_Y, LSA_W, LSA_H, "Black", "HIGHLIGHT");
        graphics.DrawPic("c_diskloading1", LSA_X + 8, LSA_Y + 5);

        var text = which == 0 ? "$STR_LOADING" : "$STR_SAVING";
        ui.TextAt(LSA_X + 46, LSA_Y + 13, new TextStyle(UiFonts.LARGE_FONT, "Black", "TEXTCOLOR")).Print(text.ToLanguageText(language) + "...");

        video.Update();
    }

    ////////////////////////
    //
    // DRAW THE SCREEN
    //

    internal static int LoadSaveRowY(int row, int top) => LS_ROWY + (row - top) * LS_ROWH;

    private void DrawLoadSaveScreen(LoadSaveState state, bool editing = false)
    {
        var language = assets.GetText("en-us");

        defs.DrawMenuComponents(state.Saving ? "save-game" : "load-game");

        //
        // HOW THE LIST IS SORTED, AND WHOSE SAVES IT SHOWS
        //
        var status = LoadSaveStyle("READHCOLOR");
        ui.TextAt(LS_LISTX, LS_STATUSY, status).Print((saveListByName ? "$STR_LS_SORTNAME" : "$STR_LS_SORTDATE").ToLanguageText(language));
        var filter = $"< {(saveListAllGames ? "$STR_LS_FILTERALL" : "$STR_LS_FILTERGAME").ToLanguageText(language)} >";
        ui.TextAt(LS_PREVX + LS_PREVW - 2 - ui.TextWidth(filter, status.Font), LS_STATUSY, status).Print(filter);
        video.HorizontalLine(8, 312, LS_ROWY - 3, "DEACTIVE");

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
            var empty = ui.CenteredText(8, 304, LS_ROWY + 4 * LS_ROWH, LoadSaveStyle("TEXTCOLOR"));
            empty.CPrint(ui.FitText("$STR_LS_NOSAVES".ToLanguageText(language), empty.Width, UiFonts.SMALL_FONT));
            if (!saveListAllGames)
                empty.CPrint(ui.FitText("$STR_LS_TRYALL".ToLanguageText(language), empty.Width, UiFonts.SMALL_FONT), LoadSaveStyle("READCOLOR"));
        }

        if (state.Top > 0)
            draw.DrawCustomArrow(LS_ARROWX, LS_ROWY, up: true);
        if (state.Top + LS_ROWS < state.Rows.Count)
            draw.DrawCustomArrow(LS_ARROWX, LS_SEPY - 7, up: false);

        if (state.Rows.Count > 0)
            video.Bar(LS_DIVIDERX, LS_ROWY - 2, 1, LS_SEPY - LS_ROWY + 1, "DEACTIVE");

        //
        // THE HIGHLIGHTED SAVE: ITS PICTURE AND PROGRESS
        //
        var info = state.Selected ?? state.Current;
        SaveThumbnail? thumbnail = null;
        if (state.Selected is { } save)
        {
            if (!state.Thumbnails.TryGetValue(save.Path, out thumbnail))
                state.Thumbnails[save.Path] = thumbnail = SaveFile.ReadThumbnail(save);
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
        video.HorizontalLine(8, 312, LS_SEPY, "DEACTIVE");
        ui.CenteredText(0, 320, LS_HELPY, LoadSaveStyle("TEXTCOLOR"))
            .CPrint(ui.FitText((state.Saving ? "$STR_LS_HELPSAVE" : "$STR_LS_HELPLOAD").ToLanguageText(language), 300, UiFonts.SMALL_FONT));

        video.Update();
    }

    /// <summary>A row: the save's name, or the new save row; the cursor's row is a lit box, and an edited row an empty field.</summary>
    private void DrawLoadSaveRow(SaveInfo? save, int y, bool selected, bool editing)
    {
        var language = assets.GetText("en-us");

        if (editing)
        {
            // Across the window, over the preview, so the name has room for all its letters
            video.Bar(LS_LISTX - 3, y - 1, LS_EDITW, LS_ROWH, "BKGDCOLOR");
            draw.DrawOutline(LS_LISTX - 3, y - 1, LS_EDITW, LS_ROWH, "HIGHLIGHT", "HIGHLIGHT");
            return;     // US_LineInput draws the name
        }

        TextStyle style;
        if (selected)
        {
            video.Bar(LS_LISTX - 3, y - 1, LS_LISTW + 4, LS_ROWH, "TEXTCOLOR");
            draw.DrawOutline(LS_LISTX - 3, y - 1, LS_LISTW + 4, LS_ROWH, "Black", "HIGHLIGHT");
            style = LoadSaveStyle("Black", "TEXTCOLOR");
        }
        else
            style = LoadSaveStyle(save == null ? "READCOLOR" : "TEXTCOLOR");

        ui.TextAt(LS_LISTX, y, style).Print(ui.FitText(save?.Name ?? "$STR_LS_NEWSAVE".ToLanguageText(language), LS_LISTW - 2, style.Font));
    }

    /// <summary>The picture, fitted inside its box keeping its shape.</summary>
    private void DrawSaveThumbnail(SaveThumbnail? thumbnail)
    {
        video.Bar(LS_THUMBX, LS_THUMBY, LS_THUMBW, LS_THUMBH, "Black");
        draw.DrawOutline(LS_THUMBX - 1, LS_THUMBY - 1, LS_THUMBW + 2, LS_THUMBH + 2, "DEACTIVE", "DEACTIVE");

        if (thumbnail == null)
        {
            ui.CenteredText(LS_THUMBX, LS_THUMBW, LS_THUMBY + LS_THUMBH / 2 - 5, LoadSaveStyle("DEACTIVE", "Black"))
                .CPrint(ui.FitText("$STR_LS_NOPICTURE".ToLanguageText(assets.GetText("en-us")), LS_THUMBW, UiFonts.SMALL_FONT));
            return;
        }

        int w = LS_THUMBW, h = w * thumbnail.Height / thumbnail.Width;
        if (h > LS_THUMBH)
        {
            h = LS_THUMBH;
            w = h * thumbnail.Width / thumbnail.Height;
        }
        video.DrawThumbnail(thumbnail, LS_THUMBX + (LS_THUMBW - w) / 2, LS_THUMBY + (LS_THUMBH - h) / 2, w, h);
    }

    /// <summary>When it was saved, where, on what skill, and how far along.</summary>
    private void DrawSaveDetails(SaveInfo info, bool isNew)
    {
        var language = assets.GetText("en-us");
        string L(string key) => key.ToLanguageText(language);

        string when = isNew ? L("$STR_LS_NEWSAVE")
            : info.Kind == SaveKind.Quick ? $"{L("$STR_LS_QUICK")}  {info.SavedAt.ToLocalTime():MM-dd HH:mm}"
            : info.Kind == SaveKind.Auto ? $"{L("$STR_LS_AUTO")}  {info.SavedAt.ToLocalTime():MM-dd HH:mm}"
            : $"{info.SavedAt.ToLocalTime():yyyy-MM-dd  HH:mm}";

        // As the intermission shows them; a level with none of something shows a dash
        static string Ratio(int count, int total) => total > 0 ? $"{count * 100 / total}%" : "-";

        // The skill with a note after it, cut short so the note always shows
        string WithNote(string text, string note)
        {
            note = $" ({note})";
            return ui.FitText(text, LS_PREVW - 4 - ui.TextWidth(note, UiFonts.SMALL_FONT), UiFonts.SMALL_FONT) + note;
        }

        // A save from another game pack says whose it is (it won't load here); one made with
        // other mods says so (it loads, with a warning)
        string skill = !string.Equals(info.GamePack, engine.GamePackId, StringComparison.OrdinalIgnoreCase)
            ? WithNote(info.SkillName, info.GamePack)
            : !isNew && hooks.HasOtherMods(info)
                ? WithNote(info.SkillName, L("$STR_LS_OTHERMODSTAG"))
                : info.SkillName;

        (string Text, string Color)[] lines =
        [
            (when, "READHCOLOR"),
            (info.MapName, "HIGHLIGHT"),
            (skill, "TEXTCOLOR"),
            (string.Format(L("$STR_LS_TIME"), SaveFile.FormatPlayTime(info.LevelTime), SaveFile.FormatPlayTime(info.PlayTime)), "TEXTCOLOR"),
            (string.Format(L("$STR_LS_KILLS"), Ratio(info.Kills, info.KillTotal), Ratio(info.Secrets, info.SecretTotal)), "TEXTCOLOR"),
            (string.Format(L("$STR_LS_TREASURE"), Ratio(info.Treasure, info.TreasureTotal), info.Score), "TEXTCOLOR"),
        ];

        for (int i = 0; i < lines.Length; i++)
            ui.CenteredText(LS_PREVX, LS_PREVW, LS_INFOY + i * LS_INFOH, LoadSaveStyle(lines[i].Color))
                .CPrint(ui.FitText(lines[i].Text, LS_PREVW - 4, UiFonts.SMALL_FONT));
    }
}