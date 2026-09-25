namespace Wolf3D.Managers;

/// <summary>
/// State of the in-game automap: whether it's up, how far it's zoomed and which map position sits
/// at the center of the view. The map is drawn over the 3D view by Program.DrawAutomap, and the
/// world keeps running underneath it.
/// </summary>
internal class AutomapManager
{
    /// <summary>Zoom when the map first opens, in 320x200 virtual pixels per map tile.</summary>
    internal const float DefaultZoom = 8f;

    internal bool IsOpen { get; private set; }

    /// <summary>Virtual (320x200) pixels per map tile; multiply by the video scale factor for screen pixels.</summary>
    internal float Zoom { get; private set; } = DefaultZoom;

    /// <summary>Whether the view stays centered on the player. Always on until pan mode exists.</summary>
    internal bool Follow { get; private set; } = true;

    /// <summary>The map position at the center of the view, in tiles (1.0 = one tile).</summary>
    internal float CenterX { get; private set; }
    internal float CenterY { get; private set; }

    internal void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    internal void Open()
    {
        IsOpen = true;
        Follow = true;
    }

    internal void Close()
    {
        IsOpen = false;
    }

    /// <summary>
    /// Called once a frame while a level is running, with the player's position in tiles.
    /// </summary>
    internal void Update(float playerX, float playerY)
    {
        if (!IsOpen)
            return;

        if (Follow)
        {
            CenterX = playerX;
            CenterY = playerY;
        }
    }
}
