using PFWolf.Assets;
using PFWolf.Extensions;

namespace PFWolf.Ui;

/// <summary>A class menu item: the actordefs class and its picture (game-info pic-name)</summary>
internal sealed record PlayerClassChoice(string Class, string? PicName);

/// <summary>
/// New Game: the episode (none to choose with only one), the class (only when there's a
/// choice), then the skill, Esc stepping back one; then the episode's briefing, if it has one
/// </summary>
/// <param name="inGame">Whether a game is being played (starting another asks first)</param>
/// <param name="startGame">Starts the new game: its skill, episode, first map and class</param>
internal sealed class NewGameMenu(GameEngineManager engine, AssetManager assets, InputManager input, AudioManager audio, VideoManager video,
    GraphicManager graphics, MenuDrawing draw, MenuLoop loop, MenuInput menuInput, MenuDefs defs, TextPresenter presenter,
    Func<bool> inGame, Action<short, EpisodeInfo, MapInfo?, string?> startGame)
{
    // Loaded from menudefs/ in CheckForEpisodes
    internal CP_itemtype[] NewEmenu = [];
    internal CP_iteminfo NewEitems = null!;
    internal CP_itemtype[] NewMenu = [];
    internal CP_iteminfo NewItems = null!;
    internal CP_itemtype[] ClassMenu = [];
    internal CP_iteminfo ClassItems = null!;
    internal int Run()
    {
        var language = assets.GetText("en-us");
        int which;
        MapInfo? mapInfo = null;
        EpisodeInfo? episodeInfo = null;
        // Episode, then class (only when there's a choice), then skill; Esc steps back one
        bool chooseClass = ClassMenu.Length > 1;
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
        DrawNewEpisode();
        do
        {
            which = loop.HandleMenu(NewEitems, NewEmenu, NewEitems.selectionPic != null ? DrawNewEpisodePic : null);
            switch (which)
            {
                case -1:
                    draw.MenuFadeOut();
                    return 0;

                default:
                    episodeInfo = (EpisodeInfo?)NewEmenu[which].data;

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
        if (inGame())
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

            playerClass = ((PlayerClassChoice)ClassMenu[which].data!).Class;
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
        if (NewEitems.selectionPic != null)
            DrawNewEpisodePic(NewEitems.curpos);
        else
        {
            for (int i = 0; i < NewEmenu.Length; i++)
            {
                if (NewEmenu[i].data is EpisodeInfo episode && !string.IsNullOrEmpty(episode.PicName))
                    graphics.DrawPic(episode.PicName, NewEitems.x + 32, MenuDrawing.MenuItemY(NewEitems, i));
            }
        }

        video.Update();
        draw.MenuFadeIn();
        menuInput.WaitKeyUp();
    }

    /// <summary>The highlighted episode's picture at the menu's selection-pic, when it has one</summary>
    internal void DrawNewEpisodePic(int w)
    {
        if (NewEitems.selectionPic is { } at && w >= 0 && w < NewEmenu.Length
            && NewEmenu[w].data is EpisodeInfo { PicName: { Length: > 0 } pic })
            graphics.DrawPic(pic, at.X, at.Y);
    }

    internal void DrawNewGame()
    {
        defs.DrawMenuComponents("new-game");

        draw.DrawMenu(NewItems, NewMenu);
        DrawNewGameDiff(NewItems.curpos);
        video.Update();
        draw.MenuFadeIn();
        menuInput.WaitKeyUp();
    }

    internal void DrawNewGameDiff(int w)
    {
        // Face picture for the highlighted skill (pic-name in game-info), at the menu's
        // selection-pic or beside the skills
        if (w >= 0 && w < NewMenu.Length && NewMenu[w].data is SkillInfo skill)
        {
            if (NewItems.selectionPic is { } at)
                graphics.DrawPic(skill.PicName, at.X, at.Y);
            else
                graphics.DrawPic(skill.PicName, NewItems.x + 185, NewItems.y + 7);
        }
    }

    /// <summary>A class menu item: the actordefs class and its picture (game-info pic-name)</summary>
    internal sealed record PlayerClassChoice(string Class, string? PicName);

    internal void DrawNewClass()
    {
        defs.DrawMenuComponents("new-class");
        draw.DrawMenu(ClassItems, ClassMenu);
        DrawNewClassPic(ClassItems.curpos);
        video.Update();
        draw.MenuFadeIn();
        menuInput.WaitKeyUp();
    }

    internal void DrawNewClassPic(int w)
    {
        // The highlighted class's picture; pictures can differ in size, so the last one is cleared first
        int x = ClassItems.x + 175, y = ClassItems.y - 6;
        video.Bar(x, y, 96, 100, "BKGDCOLOR");
        if (w >= 0 && w < ClassMenu.Length && ClassMenu[w].data is PlayerClassChoice { PicName: { Length: > 0 } pic })
            graphics.DrawPic(pic, x, y);
    }
}
