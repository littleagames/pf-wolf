using PFWolf.Assets;
using PFWolf.Extensions;

namespace PFWolf.Ui;

/// <summary>A class menu item: the actordefs class and its picture (game-info pic-name)</summary>
internal sealed record PlayerClassChoice(string Class, string? PicName);

/// <summary>
/// New Game: the episode (none to choose with only one), the class (only when there's a
/// choice), then the skill, Esc stepping back one; then the episode's briefing, if it has one
/// </summary>
/// <param name="startGame">Starts the new game: its skill, episode, first map and class</param>
/// <param name="episodeOpen">Whether an episode can be picked: not starts-locked, or unlocked since (Gameplay/Unlocks.cs)</param>
internal sealed class NewGameMenu(GameEngineManager engine, AssetManager assets, InputManager input, AudioManager audio, VideoManager video,
    GraphicManager graphics, MenuDrawing draw, MenuLoop loop, MenuInput menuInput, MenuDefs defs, TextPresenter presenter,
    InventoryManager inventory, GameSession session, Action<short, EpisodeInfo, MapInfo?, string?> startGame, Func<List<string>> playerClasses,
    Func<EpisodeInfo, bool> episodeOpen)
{
    /// <summary>
    /// Greys out the episodes that aren't open yet (starts-locked and not unlocked), named by their
    /// locked-name if they have one, and moves the cursor off one onto the first that's open
    /// </summary>
    /// <returns>Whether any episode is open</returns>
    internal static bool ShowEpisodeLocks(ControlPanelItem[] rows, ControlPanelMenuInfo info, Func<EpisodeInfo, bool> open, Func<string, string> text)
    {
        bool any = false;
        foreach (var row in rows)
        {
            if (row.Data is not EpisodeInfo episode)
                continue;
            bool isOpen = open(episode);
            row.Active = (short)(isOpen ? 1 : 0);
            row.Text = text(!isOpen && episode.LockedName is { Length: > 0 } lockedName ? lockedName : episode.Name);
            any |= isOpen;
        }

        if (any && (info.CurPos < 0 || info.CurPos >= rows.Length || rows[info.CurPos].Active == 0))
            info.CurPos = (short)Array.FindIndex(rows, row => row.Active != 0);
        return any;
    }

    // Loaded from menudefs/ in CheckForEpisodes
    internal ControlPanelItem[] NewEmenu = [];
    internal ControlPanelMenuInfo NewEitems = null!;
    internal ControlPanelItem[] NewMenu = [];
    internal ControlPanelMenuInfo NewItems = null!;
    internal ControlPanelItem[] ClassMenu = [];
    internal ControlPanelMenuInfo ClassItems = null!;
    internal int Run()
    {
        var language = assets.GetText("en-us");
        int which;
        MapInfo? mapInfo = null;
        EpisodeInfo? episodeInfo = null;
        // Episode, then class (only when there's a choice and game-info's class-menu allows it),
        // then skill; Esc steps back one
        bool chooseClass = ClassMenu.Length > 1 && engine.GetGameInfo().ClassMenu;
        string? playerClass = null;

        // A game with one episode (Spear) has no episode menu: straight to the class or difficulty menu
        var episodes = engine.GetGameInfo().Episodes;
        bool singleEpisode = episodes.Count == 1;
        if (singleEpisode)
        {
            episodeInfo = episodes.Values.First();
            if (!engine.GetGameInfo().Maps.TryGetValue(episodeInfo.StartMap, out mapInfo))
            {
                audio.Play("player/usefail");
                draw.Message($"Starting Map \"{episodeInfo.StartMap}\" unavailable!");
                input.ClearKeysDown();
                input.Ack();
                draw.MenuFadeOut();
                return 0;
            }
            goto confirm;
        }

    firstpart:
        if (!ShowEpisodeLocks(NewEmenu, NewEitems, episodeOpen, name => name.ToLanguageText(language)))
        {
            audio.Play("player/usefail");
            draw.Message("No episode is unlocked!");
            input.ClearKeysDown();
            input.Ack();
            draw.MenuFadeOut();
            return 0;
        }
        DrawNewEpisode();
        do
        {
            which = loop.HandleMenu(NewEitems, NewEmenu, NewEitems.SelectionPic != null ? DrawNewEpisodePic : null);
            switch (which)
            {
                case -1:
                    draw.MenuFadeOut();
                    return 0;

                default:
                    episodeInfo = (EpisodeInfo?)NewEmenu[which].Data;

                    string? refusal = null;
                    if (episodeInfo == null)
                        refusal = "Episode unavailable!";
                    else if (episodeInfo.Locked)
                        refusal = episodeInfo.LockedMessage!.ToLanguageText(language);   // the shareware's "Read This!" note
                    else if (!engine.GetGameInfo().Maps.TryGetValue(episodeInfo.StartMap, out mapInfo))
                        refusal = $"Starting Map \"{episodeInfo.StartMap}\" unavailable!";

                    if (refusal != null)
                    {
                        audio.Play("player/usefail");
                        draw.Message(refusal);
                        input.ClearKeysDown();
                        input.Ack();
                        DrawNewEpisode();
                        which = 0;
                    }
                    else
                        which = 1;
                    break;
            }

        }
        while (which == 0);

        loop.ShootSnd();

    confirm:
        //
        // ALREADY IN A GAME?
        //
        if (session.InGame)
            if (loop.Confirm($"$CURGAME".ToLanguageText(language)) == 0)
            {
                draw.MenuFadeOut();
                return 0;
            }

    classpart:
        if (chooseClass)
        {
            draw.MenuFadeOut();
            DrawNewClass();
            which = loop.HandleMenu(ClassItems, ClassMenu, DrawNewClassPic);
            if (which < 0)
            {
                draw.MenuFadeOut();
                if (singleEpisode)
                    return 0;
                goto firstpart;
            }

            playerClass = ((PlayerClassChoice)ClassMenu[which].Data!).Class;
            loop.ShootSnd();
        }

    skillpart:
        draw.MenuFadeOut();
        DrawNewGame();
        which = loop.HandleMenu(NewItems, NewMenu, DrawNewGameDiff);
        if (which < 0)
        {
            if (chooseClass)
                goto classpart;
            draw.MenuFadeOut();
            if (singleEpisode)
                return 0;
            goto firstpart;
        }

        loop.ShootSnd();

        // The episode's briefing (Blake Stone's missions); Esc there goes back to the skills
        if (!string.IsNullOrEmpty(episodeInfo!.Briefing))
        {
            draw.MenuFadeOut();
            if (presenter.ShowBriefing(episodeInfo.Briefing))
                goto skillpart;
        }

        // The game starts, the control panel leaves, and "Read This!" goes back to its normal color
        startGame((short)which, episodeInfo, mapInfo, playerClass);     // one menu item per skill, in order
        draw.MenuFadeOut();

        return 0;
    }

    internal void DrawNewEpisode()
    {
        defs.DrawMenuComponents("new-episode");
        draw.DrawMenu(NewEitems, NewEmenu);

        // Each episode's picture (pic-name in game-info) sits between the cursor and the name,
        // or the highlighted one's at the menu's selection-pic
        if (NewEitems.SelectionPic != null)
            DrawNewEpisodePic(NewEitems.CurPos);
        else
        {
            for (int i = 0; i < NewEmenu.Length; i++)
            {
                if (NewEmenu[i].Data is EpisodeInfo episode && !string.IsNullOrEmpty(episode.PicName))
                    graphics.DrawPic(episode.PicName, NewEitems.X + 32, MenuDrawing.MenuItemY(NewEitems, i));
            }
        }

        video.Update();
        draw.MenuFadeIn();
        menuInput.WaitKeyUp();
    }

    /// <summary>The highlighted episode's picture at the menu's selection-pic, when it has one</summary>
    internal void DrawNewEpisodePic(int w)
    {
        if (NewEitems.SelectionPic is { } at && w >= 0 && w < NewEmenu.Length
            && NewEmenu[w].Data is EpisodeInfo { PicName: { Length: > 0 } pic })
            graphics.DrawPic(pic, at.X, at.Y);
    }

    internal void DrawNewGame()
    {
        defs.DrawMenuComponents("new-game");

        draw.DrawMenu(NewItems, NewMenu);
        DrawNewGameDiff(NewItems.CurPos);
        video.Update();
        draw.MenuFadeIn();
        menuInput.WaitKeyUp();
    }

    internal void DrawNewGameDiff(int w)
    {
        // Face picture for the highlighted skill (pic-name in game-info), at the menu's
        // selection-pic or beside the skills
        if (w >= 0 && w < NewMenu.Length && NewMenu[w].Data is SkillInfo skill)
        {
            if (NewItems.SelectionPic is { } at)
                graphics.DrawPic(skill.PicName, at.X, at.Y);
            else
                graphics.DrawPic(skill.PicName, NewItems.X + 185, NewItems.Y + 7);
        }
    }

    /// <summary>A class menu item: the actordefs class and its picture (game-info pic-name)</summary>
    internal sealed record PlayerClassChoice(string Class, string? PicName);

    internal void DrawNewClass()
    {
        defs.DrawMenuComponents("new-class");
        draw.DrawMenu(ClassItems, ClassMenu);
        DrawNewClassPic(ClassItems.CurPos);
        video.Update();
        draw.MenuFadeIn();
        menuInput.WaitKeyUp();
    }

    internal void DrawNewClassPic(int w)
    {
        // The highlighted class's picture; pictures can differ in size, so the last one is cleared first
        int x = ClassItems.X + 175, y = ClassItems.Y - 6;
        video.Bar(x, y, 96, 100, "BKGDCOLOR");
        if (w >= 0 && w < ClassMenu.Length && ClassMenu[w].Data is PlayerClassChoice { PicName: { Length: > 0 } pic })
            graphics.DrawPic(pic, x, y);
    }

    /// <summary>The new-class menu's rows: named by game-info, else by the class's tag (the name obituaries use), else the class</summary>
    internal ControlPanelItem[] PlayerClassRows()
    {
        var gameInfo = engine.GetGameInfo();
        var language = assets.GetText("en-us");
        return playerClasses().Select(playerClass =>
        {
            var info = gameInfo.PlayerClasses.FirstOrDefault(c => string.Equals(c.Key, playerClass, StringComparison.OrdinalIgnoreCase)).Value;
            var name = info?.Name ?? inventory.GetStringProperty(playerClass, "tag") ?? playerClass;
            return new ControlPanelItem(1, name.ToLanguageText(language), null, new PlayerClassChoice(playerClass, info?.PicName));
        }).ToArray();
    }
}
