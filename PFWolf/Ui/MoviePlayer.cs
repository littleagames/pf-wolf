using PFWolf.Assets;
using PFWolf.Extensions;
using PFWolf.Managers;

namespace PFWolf.Ui;

/*
=============================================================================

                                MOVIES

=============================================================================
*/

/// <summary>Blake Stone's JAM movies, played full screen</summary>
internal sealed class MoviePlayer(AssetManager assets, VideoManager video, InputManager input, AudioManager audio, MenuInput menuInput)
{
    MoviesAsset? moviesAsset;
    MoviesAsset Movies => moviesAsset ??= assets.FindInGamePack<MoviesAsset>("movies") ?? new MoviesAsset();

    /// <summary>
    /// Plays a JAM movie (bstone's movie_play): frames at least frame-tics apart, faded in and out
    /// where the movie says. A key or button ends it (fading out if it had faded in), unless
    /// it's not <paramref name="skippable"/>. Returns whether it was cut short that way; a movie
    /// that isn't there is skipped.
    /// </summary>
    internal bool PlayMovie(string name, bool skippable = true)
    {
        if (!assets.Exists<JamMovieAsset>(name) || assets.Find<JamMovieAsset>(name) is not { } movie)
            return false;

        var info = Movies.Movies.GetValueOrDefault(name) ?? new MovieInfo();
        var palette = string.IsNullOrEmpty(info.Palette) ? null : assets.Find<Palette>(info.Palette);
        var gamePalette = palette == null ? null : new GamePalette { Colors = palette.ToSDLColors() };

        var screen = new byte[JamMovieAsset.ScreenWidth * JamMovieAsset.ScreenHeight];
        if (!video.ScreenFaded)
            video.FadeOut();
        video.ClearScreen(0);
        input.ClearKeysDown();
        input.ClearLastKey();

        bool fill = true, everFaded = false, skipped = false;
        uint lastFrame = GameEngineManager.GetTimeCount();
        foreach (var chunk in movie.Chunks)
        {
            switch (chunk.Code)
            {
                case "SD":
                    if (chunk.Data.Length >= 2 && info.Sounds.TryGetValue(BitConverter.ToUInt16(chunk.Data, 0), out var sound))
                        audio.Play(sound);
                    break;

                case "FI":
                    if (gamePalette == null)
                        video.FadeIn();
                    else
                        video.FadeIn(gamePalette, 30);
                    everFaded = true;
                    break;

                case "FO":
                    video.FadeOut();
                    break;

                case "PA":
                    if (chunk.Data.Length >= 2)
                    {
                        uint pause = BitConverter.ToUInt16(chunk.Data, 0);
                        if (skippable)
                            input.UserInput(pause);
                        else
                            WaitTics(pause);
                    }
                    input.ClearKeysDown();
                    input.ClearLastKey();
                    break;

                case "GR":
                    JamMovieAsset.DrawFrame(screen, chunk.Data, fill);
                    fill = false;
                    video.MemToScreen(screen, JamMovieAsset.ScreenWidth, JamMovieAsset.ScreenHeight, 0, 0);
                    video.Update();

                    // Each frame stays up for frame-tics (but no more than 2 seconds behind), or a tic
                    uint target = lastFrame + (uint)Math.Clamp(info.FrameTics, 1, 2 * Timing.TickBase);
                    do
                    {
                        input.ProcessEvents();
                        GameEngineManager.DelayMs(2);
                    }
                    while (GameEngineManager.GetTimeCount() < target);
                    lastFrame = GameEngineManager.GetTimeCount();

                    menuInput.ReadAnyControl(out var ci);
                    if (skippable && !video.ScreenFaded && (ci.Button0 || ci.Button1 || input.GetLastKeyPressed() != ScanCodes.None))
                    {
                        skipped = true;
                        if (everFaded)
                            video.FadeOut();
                    }
                    break;

                case "XX":
                    break;
            }

            if (skipped || chunk.Code == "XX")
                break;
        }

        audio.StopAll();
        input.ClearKeysDown();
        return skipped;
    }

    /// <summary>Waits the tics out, keeping the window answering, whatever's pressed</summary>
    void WaitTics(uint tics)
    {
        uint start = GameEngineManager.GetTimeCount();
        while (GameEngineManager.GetTimeCount() - start < tics)
        {
            input.ProcessEvents();
            GameEngineManager.DelayMs(5);
        }
    }

}