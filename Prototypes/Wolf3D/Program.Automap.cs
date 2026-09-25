using Wolf3D.Constants;
using Wolf3D.Managers;

namespace Wolf3D;

internal partial class Program
{
    // Theme color names (colors.yaml) and the colors used when a game pack doesn't define them.
    static readonly Dictionary<string, string> AutomapColorFallbacks = new()
    {
        ["AutomapBackground"] = "#000000",
        ["AutomapWall"] = "#C2C2C2",
        ["AutomapDoor"] = "#E6DA00",
        ["AutomapPlayer"] = "#55FF55",
        ["AutomapEnemy"] = "#FF0000",
        ["AutomapCorpse"] = "#710000",
        ["AutomapItem"] = "#20AAFF",
        ["AutomapDecor"] = "#8D8D8D",
        ["AutomapGrid"] = "#004040",
    };

    static string AutomapColor(string name) =>
        _videoManager.IsThemeColor(name) ? name : AutomapColorFallbacks[name];

    /// <summary>
    /// Opens or closes the automap from play (the automap button, or the `automap` command).
    /// Never during a demo: the map isn't part of what a demo records.
    /// </summary>
    internal static void ToggleAutomap()
    {
        if (demoplayback || demorecord)
            return;

        _automapManager.Toggle();
        UpdateAutomap();
    }

    /// <summary>Keeps the automap's view on the player. Called every frame from the play loop.</summary>
    internal static void UpdateAutomap()
    {
        if (_mapManager.Player == null)
            return;

        _automapManager.Update(player.X / (float)MapConstants.TILEGLOBAL, player.Y / (float)MapConstants.TILEGLOBAL);
    }

    /// <summary>
    /// Draws the automap over the 3D view: a solid backdrop, then every wall edge that faces open
    /// floor. Called each frame from ThreeDRefresh after the view is drawn and before the console.
    /// The map is clipped to the view, so the border and status bar are never drawn over.
    /// </summary>
    internal static void DrawAutomap()
    {
        if (!_automapManager.IsOpen || _mapManager.Player == null)
            return;

        int px = _videoManager.scaleFactor;
        int clipX = viewscreenx, clipY = viewscreeny, clipW = viewwidth, clipH = viewheight;

        _videoManager.BarScaledCoord(clipX, clipY, clipW, clipH, AutomapColor("AutomapBackground"));

        float tileSize = _automapManager.Zoom * px;         // screen pixels per tile
        float originX = clipX + clipW / 2f - _automapManager.CenterX * tileSize;
        float originY = clipY + clipH / 2f - _automapManager.CenterY * tileSize;

        // Only the tiles that can reach the view
        int firstX = Math.Max(0, (int)Math.Floor((clipX - originX) / tileSize) - 1);
        int lastX = Math.Min(MapManager.MAPSIZE - 1, (int)Math.Ceiling((clipX + clipW - originX) / tileSize));
        int firstY = Math.Max(0, (int)Math.Floor((clipY - originY) / tileSize) - 1);
        int lastY = Math.Min(MapManager.MAPSIZE - 1, (int)Math.Ceiling((clipY + clipH - originY) / tileSize));

        string wallColor = AutomapColor("AutomapWall");
        int pen = px;
        int penOffset = pen / 2;                            // center the pen on the edge

        void Edge(int x0, int y0, int x1, int y1) =>
            _videoManager.DrawLineScaledCoord(
                (int)MathF.Round(originX + x0 * tileSize) - penOffset, (int)MathF.Round(originY + y0 * tileSize) - penOffset,
                (int)MathF.Round(originX + x1 * tileSize) - penOffset, (int)MathF.Round(originY + y1 * tileSize) - penOffset,
                wallColor, pen, clipX, clipY, clipW, clipH);

        for (int y = firstY; y <= lastY; y++)
        {
            for (int x = firstX; x <= lastX; x++)
            {
                if (!IsAutomapWall(x, y))
                    continue;

                if (!IsAutomapWall(x, y - 1)) Edge(x, y, x + 1, y);             // north face
                if (!IsAutomapWall(x, y + 1)) Edge(x, y + 1, x + 1, y + 1);     // south face
                if (!IsAutomapWall(x - 1, y)) Edge(x, y, x, y + 1);             // west face
                if (!IsAutomapWall(x + 1, y)) Edge(x + 1, y, x + 1, y + 1);     // east face
            }
        }
    }

    /// <summary>Whether a tile is solid wall. Doors count as open floor; off the map counts as wall.</summary>
    static bool IsAutomapWall(int x, int y)
    {
        if (x < 0 || y < 0 || x >= MapManager.MAPSIZE || y >= MapManager.MAPSIZE)
            return true;

        int tile = _mapManager.tilemap[x, y];
        return tile != 0 && (tile & BIT_DOOR) == 0;
    }
}
