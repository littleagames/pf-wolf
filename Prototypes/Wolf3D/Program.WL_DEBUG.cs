using Wolf3D.Constants;
using Wolf3D.Managers;

namespace Wolf3D;

// The old Tab+key debug keys are now console commands (Program.ConsoleCommands.cs); what's
// left here are the helpers those commands use.
internal partial class Program
{
    // Set by the "screenshot" command; ThreeDRefresh takes the shot before drawing the console.
    static bool screenshotPending;

    /// <summary>Saves the screen to the first free WSHOT###.BMP and returns its name.</summary>
    internal static string TakeScreenshot()
    {
        int i;
        string fname = "WSHOT000.BMP";

        for (i = 0; i < 1000; i++)
        {
            fname = $"WSHOT{i:000}.BMP";
            if (!File.Exists(fname))
                break;
        }

        // overwrites WSHOT999.BMP if all wshot files exist

        _videoManager.SaveScreenShot(fname);
        return fname;
    }

    internal static void BasicOverhead()
    {

        int x, y;
        int zoom, temp;
        int offx, offy;
        Actor? tile;
        string color = "Black";

        zoom = 128 / MapManager.MAPSIZE;
        offx = 160;
        offy = (160 - (MapManager.MAPSIZE * zoom)) / 2;

        //
        // right side (raw)
        //
        for (y = 0; y < _mapManager.mapheight; y++)
        {
            for (x = 0; x < _mapManager.mapwidth; x++)
            {
                color = "Black";
                if (_mapManager.actorat[x, y] is Wall) color = "Grey";
                if (_mapManager.actorat[x, y] is Door) color = "White";
                _videoManager.Bar((x * zoom) + offx, (y * zoom) + offy, zoom, zoom, color);
            }
        }

        //
        // left side (filtered)
        //
        offx -= 128;

        for (y = 0; y < _mapManager.mapheight; y++)
        {
            for (x = 0; x < _mapManager.mapwidth; x++)
            {
                tile = _mapManager.actorat[x, y];

                if (tile is null)
                {
                    if (_mapManager.spotvis[x, y])
                        color = "Dark Green";
                    else
                        color = "Black";      // nothing
                }
                else if (_mapManager.MAPSPOT(x, y, 1) == MapDataConstants.PUSHABLETILE)
                    color = "Purple";
                else if (tile is BlockingActor)
                    color = "Dark Blue";
                else if (tile is Wall)
                    color = "Navy";
                else if (tile is Door)
                    color = "Blue";

                _videoManager.Bar((x * zoom) + offx, (y * zoom) + offy, zoom, zoom, color);
            }
        }

        _videoManager.Bar((player.TileX * zoom) + offx, (player.TileY * zoom) + offy, zoom, zoom, "White");

        _videoManager.Update();
        _inputManager.Ack();
    }
}
