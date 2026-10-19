using PFWolf.Assets;
using PFWolf.Extensions;
using PFWolf.Entities;
using PFWolf.Managers;

namespace PFWolf.Ui;

/// <summary>What the title screens need from the rest of the game</summary>
/// <param name="DemoExists">Whether there's a demo of that number</param>
/// <param name="PlayDemo">Plays a demo; true if a key ended it</param>
/// <param name="ClearMargins">Blanks the screen round the 320x200 layout</param>
/// <param name="DrawHighScores">The high scores screen</param>
/// <param name="DrawTitle">The title pictures</param>
/// <param name="DrawText">A screen's presenter text, printed into its window</param>
/// <param name="TitlePalette">game-info's title-palette: a title screen's palette when it names none</param>
internal sealed record TitleScreenHooks(Func<int, bool> DemoExists, Func<int, bool> PlayDemo, Action ClearMargins,
    Action DrawHighScores, Action DrawTitle, Action<TitleTextInfo> DrawText, Func<string?>? TitlePalette = null);

/*
=============================================================================

                                TITLE SCREENS

game-info's intro (shown once as the game starts) and title-loop (shown over and over until a
key is pressed, then the menu): a list of screens, each a picture, a movie, a page of
presenter text or the high scores, faded in with its palette and held for a time. A key
skips an intro screen; in the title loop it goes to the menu. Without them, a game pack
gets Wolf3D's: the notice, PG-13, then title, credits, high scores and a demo.

=============================================================================
*/

internal sealed class TitleScreens(VideoManager video, AudioManager audio, InputManager input, GraphicManager graphics,
    AssetManager assets, MenuDrawing draw, UiText ui, MoviePlayer movies, TitleScreenHooks hooks)
{
    /// <summary>Wolf3D's notice that the game isn't shareware (game-info non-shareware-notice)</summary>
    internal void NonShareware()
    {
        video.FadeOut();

        draw.ClearMScreen();
        draw.DrawStripes(10);

        ui.TextAt(110, 15, UiFonts.MenuStyle("READHCOLOR")).Print("Attention");

        var notice = ui.TextAt(40, 60, UiFonts.MenuStyle("HIGHLIGHT"));
        notice.Print("This game is NOT shareware.\n");
        notice.Print("Please do not distribute it.\n");
        notice.Print("Thanks.\n\n");

        notice.Print("        Id Software\n");

        video.Update();
        video.FadeIn();
        input.Ack();
    }

    /// <summary>Wolf3D's PG-13 rating screen</summary>
    internal void Pg13()
    {
        video.FadeOut();
        video.FillScreen("Light Blue");     // background

        graphics.DrawPic("pg13", 216, 110);
        video.Update();

        video.FadeIn();
        input.UserInput(Timing.TickBase * 7);

        video.FadeOut();
    }

    /// <summary>The demo the title loop plays next</summary>
    int nextTitleDemo;

    /// <summary>Demos the title loop goes round: DEMO0 to DEMO9, those the game has</summary>
    internal const int TitleDemos = 10;

    /// <summary>
    /// The demo for the title loop to play: the next one the game has of DEMO0-9, so the four
    /// the games come with play in turn, a mod's DEMO4 on joins them, and a game with fewer (the
    /// Spear demo has only the first) plays the same one each time
    /// </summary>
    internal int TakeNextTitleDemo()
    {
        for (int tries = 0; tries < TitleDemos; tries++)
        {
            int demonumber = nextTitleDemo++ % TitleDemos;
            if (hooks.DemoExists(demonumber))
                return demonumber;
        }
        return nextTitleDemo++ % TitleDemos;
    }

    /// <summary>
    /// Shows the screens in turn. In the title loop, returns true as soon as one is left with a
    /// key; in the intro, a key only skips the screen it's pressed on.
    /// </summary>
    internal bool RunTitleScreens(List<TitleScreenInfo> screens, bool titleLoop)
    {
        foreach (var screen in screens)
        {
            bool pressed = RunTitleScreen(screen);
            if (pressed && titleLoop)
                return true;
        }
        return false;
    }

    /// <summary>Shows one title screen; returns whether a key cut it short</summary>
    bool RunTitleScreen(TitleScreenInfo screen)
    {
        if (!string.IsNullOrEmpty(screen.FadeFrom))
            video.FadeOut(Color.FromHexRGBA(screen.FadeFrom), 20);

        if (screen.StopMusic)
            audio.StopMusic();
        if (!string.IsNullOrEmpty(screen.Music))
            audio.PlayMusic(screen.Music, screen.MusicLoop);

        bool pressed;
        if (!string.IsNullOrEmpty(screen.Movie))
            pressed = movies.PlayMovie(screen.Movie);
        else if (screen.Demo)
        {
            pressed = hooks.PlayDemo(TakeNextTitleDemo());
        }
        else
        {
            if (!string.IsNullOrEmpty(screen.Background))
                video.FillScreen(screen.Background);
            else
                hooks.ClearMargins();
            if (screen.HighScores)
                hooks.DrawHighScores();
            if (screen.Title)
                hooks.DrawTitle();
            if (!string.IsNullOrEmpty(screen.Pic))
                graphics.DrawPic(screen.Pic, screen.X, screen.Y);
            if (screen.Text is { } text)
                hooks.DrawText(text);

            video.Update();
            // a title screen with no palette of its own has the title's (game-info title-palette), as the built-in loop's
            FadeInWithPalette(!string.IsNullOrEmpty(screen.Palette) ? screen.Palette : screen.Title ? hooks.TitlePalette?.Invoke() : null);

            if (!string.IsNullOrEmpty(screen.FizzlePic))
            {
                graphics.DrawPic(screen.FizzlePic, 0, 0);
                video.Transition(FadeStyle.Fizzle, 0, 0, video.ScreenWidth, video.ScreenHeight, (uint)screen.FizzleTics);
            }

            pressed = WaitOnTitleScreen(screen);
        }

        if (!string.IsNullOrEmpty(screen.FadeTo))
            video.FadeOut(Color.FromHexRGBA(screen.FadeTo), 20);
        if (!video.ScreenFaded)
            video.FadeOut();
        input.ClearKeysDown();
        return pressed;
    }

    /// <summary>Fades the screen in with the named palette, or the game palette</summary>
    void FadeInWithPalette(string? paletteName)
    {
        var palette = string.IsNullOrEmpty(paletteName) ? null : assets.Find<Palette>(paletteName);
        if (palette == null)
            video.FadeIn();
        else
            video.FadeIn(new GamePalette { Colors = palette.ToSDLColors() }, 30);
    }

    /// <summary>Holds a screen for its seconds, or until its music ends; true if a key ended it</summary>
    bool WaitOnTitleScreen(TitleScreenInfo screen)
    {
        if (screen.UntilMusicEnds && audio.IsMusicPlaying)
        {
            input.StartAck();
            while (audio.IsMusicPlaying)
            {
                input.ProcessEvents();
                if (input.CheckAck())
                    return true;
                GameEngineManager.DelayMs(5);
            }
            return false;
        }

        return screen.Seconds > 0 && input.UserInput((uint)(screen.Seconds * Timing.TickBase));
    }
}