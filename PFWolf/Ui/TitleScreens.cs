using PFWolf.Assets;
using PFWolf.Extensions;
using PFWolf.Entities;
using PFWolf.Fonts;
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

game-info's title-sequence: its intro (shown once as the game starts) and loop (shown over and
over until a key is pressed, then the menu): lists of screens, each a picture, a movie, a demo,
lines of text, a page of presenter text or the high scores, faded in with its palette and held
for a time. A key does the screen's on-key: by default it skips an intro screen, and in the loop
goes to the menu. Wolf3D's notice, PG-13, title, credits, high scores and demos are all
gamepacks/wolf3d's title-sequence.

=============================================================================
*/

internal sealed class TitleScreens(VideoManager video, AudioManager audio, InputManager input, GraphicManager graphics,
    AssetManager assets, MenuDrawing draw, UiText ui, MoviePlayer movies, TitleScreenHooks hooks)
{
    /// <summary>
    /// Whether Tab was held as the last screen was left with a key: the title loop then records
    /// a demo rather than opening the menu (Wolf3D's), and the screens have cleared the keys by then
    /// </summary>
    internal bool LeftHoldingTab { get; private set; }

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
    /// Shows the screens in turn, until a key does menu or end-intro (which it says) or they've
    /// all been shown (null). <paramref name="listDefault"/> is what a key does on a screen
    /// with no on-key: next in the intro, menu in the loop. <paramref name="showedAny"/> is false
    /// when every screen was left out (pictures the game hasn't got).
    /// </summary>
    internal TitleKeyAction? RunTitleScreens(List<TitleScreenInfo> screens, TitleKeyAction listDefault, out bool showedAny)
    {
        showedAny = false;
        foreach (var screen in screens)
        {
            var ended = RunTitleScreen(screen, KeyActionFor(screen, listDefault), listDefault, out bool shown);
            showedAny |= shown;
            if (ended is TitleKeyAction.Menu or TitleKeyAction.EndIntro)
                return ended;
        }
        return null;
    }

    /// <summary>on-key values and missing pictures already warned about, so a loop doesn't warn each time round</summary>
    readonly HashSet<string> warnedOnKeys = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What a key does on the screen: its on-key, or the list's default</summary>
    internal TitleKeyAction KeyActionFor(TitleScreenInfo screen, TitleKeyAction listDefault)
    {
        if (string.IsNullOrWhiteSpace(screen.OnKey))
            return listDefault;

        if (!TitleKeyActions.TryParse(screen.OnKey, out var action))
        {
            if (warnedOnKeys.Add(screen.OnKey))
                Console.WriteLine($"Unknown title-sequence on-key '{screen.OnKey}' in game-info ({string.Join(", ", TitleKeyActions.Names.Keys)})");
            return listDefault;
        }

        // end-intro has no meaning in the loop, which has no intro left to end
        if (action == TitleKeyAction.EndIntro && listDefault == TitleKeyAction.Menu)
        {
            if (warnedOnKeys.Add("loop:end-intro"))
                Console.WriteLine("title-sequence on-key end-intro is for intro screens; in the loop, a key goes to the menu");
            return listDefault;
        }
        return action;
    }

    /// <summary>
    /// Shows one title screen (<paramref name="shown"/>, unless it's left out). Returns what the
    /// key that ended it does (next, menu or end-intro), or null if nothing did.
    /// </summary>
    TitleKeyAction? RunTitleScreen(TitleScreenInfo screen, TitleKeyAction onKey, TitleKeyAction listDefault, out bool shown)
    {
        // A game with no demos, or without the picture (a mod's replaced graphics with no PG-13), leaves the screen out
        int demoNumber = screen.Demo ? TakeNextTitleDemo() : -1;
        if (screen.Demo)
            shown = hooks.DemoExists(demoNumber);
        else
        {
            shown = string.IsNullOrEmpty(screen.Pic) || !string.IsNullOrEmpty(screen.Movie) || assets.Exists<GraphicAsset>(screen.Pic);
            if (!shown && warnedOnKeys.Add("pic:" + screen.Pic))
                Console.WriteLine($"title-sequence screen with pic '{screen.Pic}' left out: the game has no such picture");
        }
        if (!shown)
            return null;

        if (!string.IsNullOrEmpty(screen.FadeFrom))
            video.FadeOut(Color.FromHexRGBA(screen.FadeFrom), 20);

        if (screen.StopMusic)
            audio.StopMusic();
        if (!string.IsNullOrEmpty(screen.Music))
            audio.PlayMusic(screen.Music, screen.MusicLoop);

        // Movies and demos have nothing to finish, and a demo always ends on a key
        var keyEnds = onKey is TitleKeyAction.Finish or TitleKeyAction.Ignore ? listDefault : onKey;

        bool pressed;
        string? resumeMusic = null;
        if (!string.IsNullOrEmpty(screen.Movie))
            pressed = movies.PlayMovie(screen.Movie, skippable: onKey != TitleKeyAction.Ignore);
        else if (screen.Demo)
        {
            // The demo plays its level's music; what was playing comes back after, unless a key ended it
            var playing = audio.CurrentMusicTrack;
            pressed = hooks.PlayDemo(demoNumber);
            if (!pressed && !string.IsNullOrEmpty(playing))
                resumeMusic = playing;
        }
        else
        {
            // Drawn behind what's on screen, so the last screen (the signon) has to fade out first
            if (!video.ScreenFaded)
                video.FadeOut();

            if (screen.MenuBackground)
                draw.ClearMScreen();
            else if (!string.IsNullOrEmpty(screen.Background))
                video.FillScreen(screen.Background);
            else
                hooks.ClearMargins();
            if (screen.Stripe is { } stripeY)
                draw.DrawStripes(stripeY);
            if (screen.HighScores)
                hooks.DrawHighScores();
            if (screen.Title)
                hooks.DrawTitle();
            if (!string.IsNullOrEmpty(screen.Pic))
                graphics.DrawPic(screen.Pic, screen.X, screen.Y);
            foreach (var line in screen.Lines)
                ui.TextAt(line.X, line.Y, new TextStyle(line.Font ?? UiFonts.MenuFont, line.Color)).Print(line.Text);
            if (screen.Text is { } text)
                hooks.DrawText(text);

            video.Update();
            // a title screen with no palette of its own has the title's (game-info title-palette), as the built-in loop's
            FadeInWithPalette(!string.IsNullOrEmpty(screen.Palette) ? screen.Palette : screen.Title ? hooks.TitlePalette?.Invoke() : null);

            pressed = false;
            if (!string.IsNullOrEmpty(screen.FizzlePic))
            {
                // A key during the fizzle does the on-key; finish shows the picture at once
                graphics.DrawPic(screen.FizzlePic, 0, 0);
                input.StartAck();
                pressed = video.Transition(FadeStyle.Fizzle, 0, 0, video.ScreenWidth, video.ScreenHeight, (uint)screen.FizzleTics,
                    onKey == TitleKeyAction.Ignore ? null : KeyPressed);
                if (pressed && onKey == TitleKeyAction.Finish)
                {
                    // Held on, the key would repeat and do the default straight away
                    var held = input.GetLastKeyPressed();
                    while (held != ScanCodes.None && input.IsKeyDown(held))
                    {
                        input.ProcessEvents();
                        GameEngineManager.DelayMs(5);
                    }
                    pressed = false;
                }
            }

            if (!pressed)
                pressed = WaitOnTitleScreen(screen, ignoreKeys: onKey == TitleKeyAction.Ignore && !screen.WaitForKey);
        }

        if (!string.IsNullOrEmpty(screen.FadeTo))
            video.FadeOut(Color.FromHexRGBA(screen.FadeTo), 20);
        if (!video.ScreenFaded)
            video.FadeOut();
        if (resumeMusic != null)
            audio.PlayMusic(resumeMusic);

        LeftHoldingTab = pressed && input.IsKeyDown(ScanCodes.Tab);
        input.ClearKeysDown();
        return pressed ? keyEnds : null;
    }

    /// <summary>Whether a key or button has been pressed since StartAck</summary>
    bool KeyPressed()
    {
        input.ProcessEvents();
        return input.CheckAck();
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

    /// <summary>
    /// Holds a screen for its seconds, until its music ends or until a key; true if a key ended
    /// it (never when <paramref name="ignoreKeys"/>)
    /// </summary>
    bool WaitOnTitleScreen(TitleScreenInfo screen, bool ignoreKeys)
    {
        input.StartAck();
        if (screen.WaitForKey)
        {
            while (!KeyPressed())
                GameEngineManager.DelayMs(5);
            return true;
        }

        if (screen.UntilMusicEnds && audio.IsMusicPlaying)
        {
            while (audio.IsMusicPlaying)
            {
                if (KeyPressed() && !ignoreKeys)
                    return true;
                GameEngineManager.DelayMs(5);
            }
            return false;
        }

        uint start = GameEngineManager.GetTimeCount(), tics = (uint)Math.Max(screen.Seconds * Timing.TickBase, 0);
        while (GameEngineManager.GetTimeCount() - start < tics)
        {
            if (KeyPressed() && !ignoreKeys)
                return true;
            GameEngineManager.DelayMs(5);
        }
        return false;
    }
}