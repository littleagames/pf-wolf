using PFWolf.Assets;
using PFWolf.Entities;
using PFWolf.Extensions;
using PFWolf.Managers;

namespace PFWolf;

internal partial class Program
{
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

    /// <summary>The demo the title loop plays next</summary>
    static int nextTitleDemo;

    /// <summary>
    /// The demo for the title loop to play, of the four: the next one the game has, so one with
    /// fewer (the Spear demo has only the first) plays the same one each time
    /// </summary>
    static int TakeNextTitleDemo()
    {
        for (int tries = 0; tries < 4; tries++)
        {
            int demonumber = nextTitleDemo++ % 4;
            if (DemoExists(demonumber))
                return demonumber;
        }
        return nextTitleDemo++ % 4;
    }

    /// <summary>
    /// Shows the screens in turn. In the title loop, returns true as soon as one is left with a
    /// key; in the intro, a key only skips the screen it's pressed on.
    /// </summary>
    internal static bool RunTitleScreens(List<TitleScreenInfo> screens, bool titleLoop)
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
    static bool RunTitleScreen(TitleScreenInfo screen)
    {
        if (!string.IsNullOrEmpty(screen.FadeFrom))
            _videoManager.FadeOut(Color.FromHexRGBA(screen.FadeFrom), 20);

        if (screen.StopMusic)
            _audioManager.StopMusic();
        if (!string.IsNullOrEmpty(screen.Music))
            _audioManager.PlayMusic(screen.Music, screen.MusicLoop);

        bool pressed;
        if (!string.IsNullOrEmpty(screen.Movie))
            pressed = PlayMovie(screen.Movie);
        else if (screen.Demo)
        {
            PlayDemo(TakeNextTitleDemo());
            pressed = playstate == playstatetypes.ex_abort;
        }
        else
        {
            if (!string.IsNullOrEmpty(screen.Background))
                _videoManager.FillScreen(screen.Background);
            else
                ClearMargins();
            if (screen.HighScores)
                DrawHighScores();
            if (screen.Title)
                DrawTitle();
            if (!string.IsNullOrEmpty(screen.Pic))
                _graphicManager.DrawPic(screen.Pic, screen.X, screen.Y);
            if (screen.Text is { } text)
                DrawTitleText(text);

            _videoManager.Update();
            FadeInWithPalette(screen.Palette);

            if (!string.IsNullOrEmpty(screen.FizzlePic))
            {
                _graphicManager.DrawPic(screen.FizzlePic, 0, 0);
                _videoManager.Transition(FadeStyle.Fizzle, 0, 0, _videoManager.screenWidth, _videoManager.screenHeight, (uint)screen.FizzleTics);
            }

            pressed = WaitOnTitleScreen(screen);
        }

        if (!string.IsNullOrEmpty(screen.FadeTo))
            _videoManager.FadeOut(Color.FromHexRGBA(screen.FadeTo), 20);
        if (!_videoManager.screenfaded)
            _videoManager.FadeOut();
        _inputManager.ClearKeysDown();
        return pressed;
    }

    /// <summary>Fades the screen in with the named palette, or the game palette</summary>
    static void FadeInWithPalette(string? paletteName)
    {
        var palette = string.IsNullOrEmpty(paletteName) ? null : _assetManager.Find<Palette>(paletteName);
        if (palette == null)
            _videoManager.FadeIn();
        else
            _videoManager.FadeIn(new GamePalette { Colors = palette.ToSDLColors() }, 30);
    }

    /// <summary>Holds a screen for its seconds, or until its music ends; true if a key ended it</summary>
    static bool WaitOnTitleScreen(TitleScreenInfo screen)
    {
        if (screen.UntilMusicEnds && _audioManager.IsMusicPlaying)
        {
            _inputManager.StartAck();
            while (_audioManager.IsMusicPlaying)
            {
                _inputManager.ProcessEvents();
                if (_inputManager.CheckAck())
                    return true;
                GameEngineManager.DelayMs(5);
            }
            return false;
        }

        return screen.Seconds > 0 && _inputManager.UserInput((uint)(screen.Seconds * Timing.TickBase));
    }

    /// <summary>A screen's presenter text, printed into its window</summary>
    static void DrawTitleText(TitleTextInfo text)
    {
        if (PresenterScript(text.Script) is not { } script)
            return;

        TP_Presenter(new PresenterInfo
        {
            Flags = PresenterFlags.CacheNoGfx,
            Script = script,
            X1 = text.X1,
            Y1 = text.Y1,
            X2 = text.X2,
            Y2 = text.Y2,
            Font = text.Font,
            FontColor = text.Color,
            Background = text.Background,
            Light = text.Light,
            Dark = text.Dark,
            Shadow = text.Shadow,
        });
    }

    /*
    =============================================================================

                                    MOVIES

    =============================================================================
    */

    static MoviesAsset? moviesAsset;
    static MoviesAsset Movies => moviesAsset ??= _assetManager.FindInGamePack<MoviesAsset>("movies") ?? new MoviesAsset();

    /// <summary>
    /// Plays a JAM movie (bstone's movie_play): frames at least frame-tics apart, faded in and out
    /// where the movie says. A key or button ends it (fading out if it had faded in). Returns
    /// whether it was cut short that way; a movie that isn't there is skipped.
    /// </summary>
    internal static bool PlayMovie(string name)
    {
        if (!_assetManager.Exists<JamMovieAsset>(name) || _assetManager.Find<JamMovieAsset>(name) is not { } movie)
            return false;

        var info = Movies.Movies.GetValueOrDefault(name) ?? new MovieInfo();
        var palette = string.IsNullOrEmpty(info.Palette) ? null : _assetManager.Find<Palette>(info.Palette);
        var gamePalette = palette == null ? null : new GamePalette { Colors = palette.ToSDLColors() };

        var screen = new byte[JamMovieAsset.ScreenWidth * JamMovieAsset.ScreenHeight];
        if (!_videoManager.screenfaded)
            _videoManager.FadeOut();
        _videoManager.ClearScreen(0);
        _inputManager.ClearKeysDown();
        _inputManager.ClearLastKey();

        bool fill = true, everFaded = false, skipped = false;
        uint lastFrame = GameEngineManager.GetTimeCount();
        foreach (var chunk in movie.Chunks)
        {
            switch (chunk.Code)
            {
                case "SD":
                    if (chunk.Data.Length >= 2 && info.Sounds.TryGetValue(BitConverter.ToUInt16(chunk.Data, 0), out var sound))
                        _audioManager.Play(sound);
                    break;

                case "FI":
                    if (gamePalette == null)
                        _videoManager.FadeIn();
                    else
                        _videoManager.FadeIn(gamePalette, 30);
                    everFaded = true;
                    break;

                case "FO":
                    _videoManager.FadeOut();
                    break;

                case "PA":
                    if (chunk.Data.Length >= 2)
                        _inputManager.UserInput(BitConverter.ToUInt16(chunk.Data, 0));
                    _inputManager.ClearKeysDown();
                    _inputManager.ClearLastKey();
                    break;

                case "GR":
                    DrawMovieFrame(screen, chunk.Data, fill);
                    fill = false;
                    _videoManager.MemToScreen(screen, JamMovieAsset.ScreenWidth, JamMovieAsset.ScreenHeight, 0, 0);
                    _videoManager.Update();

                    // Each frame stays up for frame-tics (but no more than 2 seconds behind), or a tic
                    uint target = lastFrame + (uint)Math.Clamp(info.FrameTics, 1, 2 * Timing.TickBase);
                    do
                    {
                        _inputManager.ProcessEvents();
                        GameEngineManager.DelayMs(2);
                    }
                    while (GameEngineManager.GetTimeCount() < target);
                    lastFrame = GameEngineManager.GetTimeCount();

                    ReadAnyControl(out var ci);
                    if (!_videoManager.screenfaded && (ci.button0 || ci.button1 || _inputManager.GetLastKeyPressed() != ScanCodes.sc_None))
                    {
                        skipped = true;
                        if (everFaded)
                            _videoManager.FadeOut();
                    }
                    break;

                case "XX":
                    break;
            }

            if (skipped || chunk.Code == "XX")
                break;
        }

        _audioManager.StopAll();
        _inputManager.ClearKeysDown();
        return skipped;
    }

    /// <summary>
    /// Draws a movie frame into the screen: runs of pixels, each its byte offset into the screen
    /// and length, until one marked 0. The movie's first frame starts with the color to fill the
    /// screen with first.
    /// </summary>
    static void DrawMovieFrame(byte[] screen, byte[] data, bool fill)
    {
        int pos = 0;
        if (fill && data.Length > 0)
            Array.Fill(screen, data[pos++]);

        while (pos + 6 <= data.Length)
        {
            int opt = BitConverter.ToUInt16(data, pos);
            if (opt == 0)
                break;
            int offset = BitConverter.ToUInt16(data, pos + 2);
            int length = BitConverter.ToUInt16(data, pos + 4);
            pos += 6;

            int copied = Math.Min(length, Math.Min(data.Length - pos, screen.Length - offset));
            if (copied > 0)
                Array.Copy(data, pos, screen, offset, copied);
            pos += length;
        }
    }
}
