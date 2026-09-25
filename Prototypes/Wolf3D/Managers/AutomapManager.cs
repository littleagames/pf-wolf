namespace Wolf3D.Managers;

/// <summary>
/// State of the in-game automap: whether it's up, how far it's zoomed, which map position sits at
/// the center of the view and which way the map is turned. The map is drawn over the 3D view by
/// Program.DrawAutomap, and the world keeps running underneath it.
/// </summary>
internal class AutomapManager
{
    /// <summary>Zoom when the game starts, in 320x200 virtual pixels per map tile.</summary>
    internal const float DefaultZoom = 8f;
    internal const float MinZoom = 2f;
    internal const float MaxZoom = 32f;

    /// <summary>How much one zoom step (a mouse wheel notch) scales the map.</summary>
    internal const float ZoomStep = 1.25f;

    internal bool IsOpen { get; private set; }

    /// <summary>Virtual (320x200) pixels per map tile; multiply by the video scale factor for screen pixels.</summary>
    internal float Zoom { get; private set; } = DefaultZoom;

    /// <summary>
    /// Whether the view stays centered on the player. Off is pan mode: the view stays where it's
    /// moved to, and the player's movement controls pan the map instead of walking.
    /// </summary>
    internal bool Follow { get; private set; } = true;

    /// <summary>Whether the map turns with the player so they always face the top of the screen (else north is up).</summary>
    internal bool Rotate { get; private set; }

    /// <summary>The map position at the center of the view, in tiles (1.0 = one tile).</summary>
    internal float CenterX { get; private set; }
    internal float CenterY { get; private set; }

    /// <summary>
    /// How far the map is turned on screen, in radians clockwise (screen Y grows downward). Zero
    /// when north is up; in rotate mode, whatever puts the player's facing at the top.
    /// </summary>
    internal float Rotation { get; private set; }

    internal void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    /// <summary>Opens the map following the player. Zoom and rotate mode carry over from last time.</summary>
    internal void Open()
    {
        IsOpen = true;
        Follow = true;
    }

    /// <summary>Closes the map, which always leaves pan mode.</summary>
    internal void Close()
    {
        IsOpen = false;
        Follow = true;
    }

    /// <summary>Zooms in (positive) or out (negative) by a number of steps, which needn't be whole.</summary>
    internal void ZoomBy(float steps)
    {
        Zoom = Math.Clamp(Zoom * MathF.Pow(ZoomStep, steps), MinZoom, MaxZoom);
    }

    /// <summary>
    /// Moves the view by a distance in virtual pixels along the screen's axes (so "up" is up the
    /// screen even when the map is turned), and switches to pan mode.
    /// </summary>
    internal void Pan(float screenDx, float screenDy)
    {
        if (screenDx == 0 && screenDy == 0)
            return;

        Follow = false;

        // Screen to map: undo the rotation, then the zoom
        float cos = MathF.Cos(Rotation), sin = MathF.Sin(Rotation);
        CenterX += (screenDx * cos + screenDy * sin) / Zoom;
        CenterY += (-screenDx * sin + screenDy * cos) / Zoom;

        CenterX = Math.Clamp(CenterX, 0, MapManager.MAPSIZE);
        CenterY = Math.Clamp(CenterY, 0, MapManager.MAPSIZE);
    }

    /// <summary>Recenters on the player and returns to follow mode.</summary>
    internal void SnapToPlayer() => Follow = true;

    /// <summary>Switches between follow and pan mode, leaving the view where it is.</summary>
    internal void ToggleFollow() => Follow = !Follow;

    internal void ToggleRotate() => Rotate = !Rotate;

    /// <summary>
    /// Called once a frame while a level is running, with the player's position in tiles and
    /// their angle in degrees (counterclockwise from east).
    /// </summary>
    internal void Update(float playerX, float playerY, int playerAngle)
    {
        if (!IsOpen)
            return;

        if (Follow)
        {
            CenterX = playerX;
            CenterY = playerY;
        }

        // The player faces (cos a, -sin a) on the map; turning that by (a - 90 degrees) points it up the screen
        Rotation = Rotate ? (playerAngle - 90) * MathF.PI / 180f : 0f;
    }
}
