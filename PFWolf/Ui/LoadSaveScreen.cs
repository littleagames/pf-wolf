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
    private const int LsStatusY = 49;          // how it's sorted and filtered, across the window
    private const int LsListX = 11;            // the list column
    private const int LsListW = 124;
    private const int LsRowY = 61;             // the first row
    private const int LsRowH = 11;
    private const int LsRows = 10;             // rows showing at once
    private const int LsEditW = 312 - (LsListX - 3);  // the name being typed
    private const int LsArrowX = 140;
    private const int LsDividerX = 145;
    private const int LsPrevX = 148;           // the preview column
    private const int LsPrevW = 163;
    private const int LsThumbX = LsPrevX + (LsPrevW - LsThumbW) / 2;
    private const int LsThumbY = 61;
    private const int LsThumbW = 112;
    private const int LsThumbH = 56;
    private const int LsInfoY = LsThumbY + LsThumbH + 3;
    private const int LsInfoH = 9;
    private const int LsSepY = LsRowY + LsRows * LsRowH + 3;
    private const int LsHelpY = LsSepY + 3;

    // The save last saved or loaded on these screens, which the load screen starts on
    internal SaveInfo? LastSaveGame;

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

        FillSaveRows(state, saving ? null : LastSaveGame?.Path);

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
            if (input.IsKeyDown(ScanCodes.PgUp))
                moved = Math.Max(state.Row - (LsRows - 1), 0);
            else if (input.IsKeyDown(ScanCodes.PgDn))
                moved = Math.Min(state.Row + (LsRows - 1), last);
            else if (input.IsKeyDown(ScanCodes.Home))
                moved = 0;
            else if (input.IsKeyDown(ScanCodes.End))
                moved = last;
            else if (ci.Dir == Direction.North)
                moved = state.Row > 0 ? state.Row - 1 : last;
            else if (ci.Dir == Direction.South)
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
            if (ci.Dir is Direction.West or Direction.East)
            {
                saveListAllGames = !saveListAllGames;
                audio.Play("menu/move1");
                FillSaveRows(state, state.Selected?.Path);
                DrawLoadSaveScreen(state);
                while (ci.Dir != Direction.None)
                {
                    GameEngineManager.DelayMs(5);
                    menuInput.ReadAnyControl(out ci);
                }
                continue;
            }

            //
            // NEWEST FIRST OR BY NAME
            //
            if (ci.Button2 || input.IsKeyDown(ScanCodes.Tab))
            {
                saveListByName = !saveListByName;
                audio.Play("menu/move1");
                FillSaveRows(state, state.Selected?.Path);
                DrawLoadSaveScreen(state);
                while (input.IsKeyDown(ScanCodes.Tab))
                    input.ProcessEvents();
                menuInput.WaitKeyUp();
                continue;
            }

            //
            // DELETE A SAVE
            //
            if (state.Selected is { } doomed
                && (ci.Button3 || input.IsKeyDown(ScanCodes.Delete) || input.IsKeyDown(ScanCodes.BackSpace)))
            {
                if (loop.Confirm(string.Format("$STR_LS_DELETE".ToLanguageText(language), NameForConfirm(doomed.Name))) != 0)
                {
                    try
                    {
                        File.Delete(doomed.Path);
                        if (LastSaveGame?.Path == doomed.Path)
                            LastSaveGame = null;
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
            if (ci.Button0 || input.IsKeyDown(ScanCodes.Space) || input.IsKeyDown(ScanCodes.Enter))
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
            if (ci.Button1 && !input.IsKeyDown(ScanCodes.Alt) || input.IsKeyDown(ScanCodes.Escape))
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
    private static TextStyle LoadSaveStyle(string color, string background = "BKGDCOLOR") => new(UiFonts.SmallFont, color, background);

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
    private string NameForConfirm(string name) => ui.FitText(name, 280, UiFonts.LargeFont);

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

        if (!hooks.Load(save.Path, LsaX + 8, LsaY + 5))
            return false;

        LastSaveGame = save;
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
        if (!lineInput.Run(LsListX, y, ref name, name, true, GameConstants.MaxGameName - 1, LsEditW - 8, LoadSaveStyle("HIGHLIGHT")))
        {
            audio.Play("menu/escape");
            return false;
        }

        var path = existing?.Path ?? hooks.NewSavePath();
        DrawLSAction(1);
        if (!hooks.Save(path, name, LsaX + 8, LsaY + 5))
        {
            hooks.SaveFailed();
            return false;
        }

        LastSaveGame = SaveFile.ReadInfo(path);
        loop.ShootSnd();
        return true;
    }

    internal const int LsaX = 96;
    internal const int LsaY = 80;
    internal const int LsaW = 130;
    internal const int LsaH = 42;

    /// <summary>The "Loading..." or "Saving..." box, with the disk the save code animates.</summary>
    internal void DrawLSAction(int which)
    {
        var language = assets.GetText("en-us");
        draw.DrawWindow(LsaX, LsaY, LsaW, LsaH, "TEXTCOLOR");
        draw.DrawOutline(LsaX, LsaY, LsaW, LsaH, "Black", "HIGHLIGHT");
        graphics.DrawPic("c_diskloading1", LsaX + 8, LsaY + 5);

        var text = which == 0 ? "$STR_LOADING" : "$STR_SAVING";
        ui.TextAt(LsaX + 46, LsaY + 13, new TextStyle(UiFonts.LargeFont, "Black", "TEXTCOLOR")).Print(text.ToLanguageText(language) + "...");

        video.Update();
    }

    ////////////////////////
    //
    // DRAW THE SCREEN
    //

    internal static int LoadSaveRowY(int row, int top) => LsRowY + (row - top) * LsRowH;

    private void DrawLoadSaveScreen(LoadSaveState state, bool editing = false)
    {
        var language = assets.GetText("en-us");

        defs.DrawMenuComponents(state.Saving ? "save-game" : "load-game");

        //
        // HOW THE LIST IS SORTED, AND WHOSE SAVES IT SHOWS
        //
        var status = LoadSaveStyle("READHCOLOR");
        ui.TextAt(LsListX, LsStatusY, status).Print((saveListByName ? "$STR_LS_SORTNAME" : "$STR_LS_SORTDATE").ToLanguageText(language));
        var filter = $"< {(saveListAllGames ? "$STR_LS_FILTERALL" : "$STR_LS_FILTERGAME").ToLanguageText(language)} >";
        ui.TextAt(LsPrevX + LsPrevW - 2 - ui.TextWidth(filter, status.Font), LsStatusY, status).Print(filter);
        video.HorizontalLine(8, 312, LsRowY - 3, "DEACTIVE");

        //
        // THE LIST, SCROLLED SO THE CURSOR SHOWS
        //
        if (state.Row < state.Top)
            state.Top = state.Row;
        if (state.Row >= state.Top + LsRows)
            state.Top = state.Row - LsRows + 1;
        state.Top = Math.Clamp(state.Top, 0, Math.Max(0, state.Rows.Count - LsRows));

        for (int i = 0; i < LsRows && state.Top + i < state.Rows.Count; i++)
        {
            int r = state.Top + i;
            DrawLoadSaveRow(state.Rows[r], LoadSaveRowY(r, state.Top), r == state.Row, editing: false);
        }

        // With nothing to list there's nothing to preview either, so it says so across the window
        if (state.Rows.Count == 0)
        {
            var empty = ui.CenteredText(8, 304, LsRowY + 4 * LsRowH, LoadSaveStyle("TEXTCOLOR"));
            empty.CPrint(ui.FitText("$STR_LS_NOSAVES".ToLanguageText(language), empty.Width, UiFonts.SmallFont));
            if (!saveListAllGames)
                empty.CPrint(ui.FitText("$STR_LS_TRYALL".ToLanguageText(language), empty.Width, UiFonts.SmallFont), LoadSaveStyle("READCOLOR"));
        }

        if (state.Top > 0)
            draw.DrawCustomArrow(LsArrowX, LsRowY, up: true);
        if (state.Top + LsRows < state.Rows.Count)
            draw.DrawCustomArrow(LsArrowX, LsSepY - 7, up: false);

        if (state.Rows.Count > 0)
            video.Bar(LsDividerX, LsRowY - 2, 1, LsSepY - LsRowY + 1, "DEACTIVE");

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
        video.HorizontalLine(8, 312, LsSepY, "DEACTIVE");
        ui.CenteredText(0, 320, LsHelpY, LoadSaveStyle("TEXTCOLOR"))
            .CPrint(ui.FitText((state.Saving ? "$STR_LS_HELPSAVE" : "$STR_LS_HELPLOAD").ToLanguageText(language), 300, UiFonts.SmallFont));

        video.Update();
    }

    /// <summary>A row: the save's name, or the new save row; the cursor's row is a lit box, and an edited row an empty field.</summary>
    private void DrawLoadSaveRow(SaveInfo? save, int y, bool selected, bool editing)
    {
        var language = assets.GetText("en-us");

        if (editing)
        {
            // Across the window, over the preview, so the name has room for all its letters
            video.Bar(LsListX - 3, y - 1, LsEditW, LsRowH, "BKGDCOLOR");
            draw.DrawOutline(LsListX - 3, y - 1, LsEditW, LsRowH, "HIGHLIGHT", "HIGHLIGHT");
            return;     // US_LineInput draws the name
        }

        TextStyle style;
        if (selected)
        {
            video.Bar(LsListX - 3, y - 1, LsListW + 4, LsRowH, "TEXTCOLOR");
            draw.DrawOutline(LsListX - 3, y - 1, LsListW + 4, LsRowH, "Black", "HIGHLIGHT");
            style = LoadSaveStyle("Black", "TEXTCOLOR");
        }
        else
            style = LoadSaveStyle(save == null ? "READCOLOR" : "TEXTCOLOR");

        ui.TextAt(LsListX, y, style).Print(ui.FitText(save?.Name ?? "$STR_LS_NEWSAVE".ToLanguageText(language), LsListW - 2, style.Font));
    }

    /// <summary>The picture, fitted inside its box keeping its shape.</summary>
    private void DrawSaveThumbnail(SaveThumbnail? thumbnail)
    {
        video.Bar(LsThumbX, LsThumbY, LsThumbW, LsThumbH, "Black");
        draw.DrawOutline(LsThumbX - 1, LsThumbY - 1, LsThumbW + 2, LsThumbH + 2, "DEACTIVE", "DEACTIVE");

        if (thumbnail == null)
        {
            ui.CenteredText(LsThumbX, LsThumbW, LsThumbY + LsThumbH / 2 - 5, LoadSaveStyle("DEACTIVE", "Black"))
                .CPrint(ui.FitText("$STR_LS_NOPICTURE".ToLanguageText(assets.GetText("en-us")), LsThumbW, UiFonts.SmallFont));
            return;
        }

        int w = LsThumbW, h = w * thumbnail.Height / thumbnail.Width;
        if (h > LsThumbH)
        {
            h = LsThumbH;
            w = h * thumbnail.Width / thumbnail.Height;
        }
        video.DrawThumbnail(thumbnail, LsThumbX + (LsThumbW - w) / 2, LsThumbY + (LsThumbH - h) / 2, w, h);
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
            return ui.FitText(text, LsPrevW - 4 - ui.TextWidth(note, UiFonts.SmallFont), UiFonts.SmallFont) + note;
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
            ui.CenteredText(LsPrevX, LsPrevW, LsInfoY + i * LsInfoH, LoadSaveStyle(lines[i].Color))
                .CPrint(ui.FitText(lines[i].Text, LsPrevW - 4, UiFonts.SmallFont));
    }
}