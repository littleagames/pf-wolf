namespace PFWolf.Ui;

/// <summary>
/// What the debugging console commands show and save: a screenshot (TakeScreenshot, taken by
/// ThreeDRefresh before the console is drawn when `screenshot` asks for one) and the raw map
/// overhead (BasicOverhead).
/// </summary>
internal sealed class DebugScreens(
    GameEngineManager engine,
    VideoManager video,
    InputManager input,
    MapManager map,
    GameSession session)
{
    // Set by the "screenshot" command; ThreeDRefresh takes the shot before drawing the console.
    internal bool ScreenshotPending;

    /// <summary>
    /// Saves the screen to the first free WSHOT###.BMP in the screenshots folder and says where
    /// it went, or why it couldn't be saved.
    /// </summary>
    internal string TakeScreenshot()
    {
        var directory = engine.ConfigDirectories.ScreenshotsDirectory;
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return $"Couldn't create {directory}: {e.Message}";
        }

        string fname = "";
        for (int i = 0; i < 1000; i++)
        {
            fname = Path.Combine(directory, $"WSHOT{i:000}.BMP");
            if (!File.Exists(fname))
                break;
        }

        // overwrites WSHOT999.BMP if all wshot files exist

        return video.SaveScreenShot(fname)
            ? $"Saved {fname}"
            : $"Couldn't save {fname}: {SDL2.SDL.SDL_GetError()}";
    }

    internal void BasicOverhead()
    {

        int x, y;
        int zoom, temp;
        int offx, offy;
        Actor? tile;
        string color = "Black";

        zoom = 128 / MapManager.MapSize;
        offx = 160;
        offy = (160 - (MapManager.MapSize * zoom)) / 2;

        //
        // right side (raw)
        //
        for (y = 0; y < map.MapHeight; y++)
        {
            for (x = 0; x < map.MapWidth; x++)
            {
                color = "Black";
                if (map.ActorAt[x, y] is Wall) color = "Grey";
                if (map.ActorAt[x, y] is Door) color = "White";
                video.Bar((x * zoom) + offx, (y * zoom) + offy, zoom, zoom, color);
            }
        }

        //
        // left side (filtered)
        //
        offx -= 128;

        for (y = 0; y < map.MapHeight; y++)
        {
            for (x = 0; x < map.MapWidth; x++)
            {
                tile = map.ActorAt[x, y];

                if (tile is null)
                {
                    if (map.Spotvis[x, y])
                        color = "Dark Green";
                    else
                        color = "Black";      // nothing
                }
                else if (map.GetTrigger(x, y) != null)      // a pushwall or other trigger
                    color = "Purple";
                else if (tile is BlockingActor)
                    color = "Dark Blue";
                else if (tile is Wall)
                    color = "Navy";
                else if (tile is Door)
                    color = "Blue";

                video.Bar((x * zoom) + offx, (y * zoom) + offy, zoom, zoom, color);
            }
        }

        video.Bar((session.Player.TileX * zoom) + offx, (session.Player.TileY * zoom) + offy, zoom, zoom, "White");

        video.Update();
        input.Ack();
    }
}
