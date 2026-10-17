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
    /// where the movie says. A key or button ends it (fading out if it had faded in). Returns
    /// whether it was cut short that way; a movie that isn't there is skipped.
    /// </summary>
    internal bool PlayMovie(string name)
    {
        if (!assets.Exists<JamMovieAsset>(name) || assets.Find<JamMovieAsset>(name) is not { } movie)
            return false;

        var info = Movies.Movies.GetValueOrDefault(name) ?? new MovieInfo();
        var palette = string.IsNullOrEmpty(info.Palette) ? null : assets.Find<Palette>(info.Palette);
        var gamePalette = palette == null ? null : new GamePalette { Colors = palette.ToSDLColors() };

        var screen = new byte[JamMovieAsset.ScreenWidth * JamMovieAsset.ScreenHeight];
        if (!video.screenfaded)
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
                        input.UserInput(BitConverter.ToUInt16(chunk.Data, 0));
                    input.ClearKeysDown();
                    input.ClearLastKey();
                    break;

                case "GR":
                    DrawMovieFrame(screen, chunk.Data, fill);
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
                    if (!video.screenfaded && (ci.button0 || ci.button1 || input.GetLastKeyPressed() != ScanCodes.None))
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

    /// <summary>
    /// Draws a movie frame into the screen: runs of pixels, each its byte offset into the screen
    /// and length, until one marked 0. The movie's first frame starts with the color to fill the
    /// screen with first.
    /// </summary>
    internal static void DrawMovieFrame(byte[] screen, byte[] data, bool fill)
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