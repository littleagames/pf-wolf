using Wolf3D.Assets;
using Wolf3D.Constants;
using Wolf3D.Entities.Actors;
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

    // Locked doors are drawn in their key's color: the theme's "Automap<lock item>" if it has one
    // (e.g. AutomapGoldKey), else these, else the plain door color.
    static readonly Dictionary<string, string> AutomapLockFallbacks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GoldKey"] = "#FFAA00",
        ["SilverKey"] = "#FFFFFF",
    };

    static readonly string[] AutomapHeadings = ["E", "NE", "N", "NW", "W", "SW", "S", "SE"];

    const string AUTOMAP_FONT = "SmallFont";

    static string AutomapColor(string name) =>
        _videoManager.IsThemeColor(name) ? name : AutomapColorFallbacks[name];

    static string AutomapDoorColor(string lockItem)
    {
        if (string.IsNullOrEmpty(lockItem))
            return AutomapColor("AutomapDoor");

        string themeName = "Automap" + lockItem;
        if (_videoManager.IsThemeColor(themeName))
            return themeName;

        return AutomapLockFallbacks.TryGetValue(lockItem, out var color) ? color : AutomapColor("AutomapDoor");
    }

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
    /// Where the automap is being drawn this frame: the view rectangle in screen pixels, and the
    /// map-to-screen mapping (map position in tiles × <see cref="TileSize"/> + origin).
    /// </summary>
    readonly record struct AutomapView(int ClipX, int ClipY, int ClipWidth, int ClipHeight,
        float TileSize, float OriginX, float OriginY, int Pen)
    {
        public float ScreenX(float mapX) => OriginX + mapX * TileSize;
        public float ScreenY(float mapY) => OriginY + mapY * TileSize;
    }

    /// <summary>
    /// Draws the automap over the 3D view: a solid backdrop, the walls, doors and moving pushwall
    /// the player has seen, the actors worth marking, then the player's arrow and position. Called
    /// each frame from ThreeDRefresh after the view is drawn and before the console. The map is
    /// clipped to the view, so the border and status bar are never drawn over.
    /// </summary>
    internal static void DrawAutomap()
    {
        if (!_automapManager.IsOpen || _mapManager.Player == null)
            return;

        int px = _videoManager.scaleFactor;
        float tileSize = _automapManager.Zoom * px;         // screen pixels per tile
        var view = new AutomapView(viewscreenx, viewscreeny, viewwidth, viewheight, tileSize,
            OriginX: viewscreenx + viewwidth / 2f - _automapManager.CenterX * tileSize,
            OriginY: viewscreeny + viewheight / 2f - _automapManager.CenterY * tileSize,
            Pen: px);

        _videoManager.BarScaledCoord(view.ClipX, view.ClipY, view.ClipWidth, view.ClipHeight, AutomapColor("AutomapBackground"));

        bool reveal = mapreveal != 0;

        DrawAutomapWalls(view, reveal);
        DrawAutomapPushwall(view, reveal);
        DrawAutomapDoors(view, reveal);
        DrawAutomapActors(view, reveal);
        DrawAutomapPlayer(view);
        DrawAutomapPosition(view);
    }

    /// <summary>Every seen wall face that borders open floor, as a line along the tile edge.</summary>
    static void DrawAutomapWalls(AutomapView view, bool reveal)
    {
        // Only the tiles that can reach the view
        int firstX = Math.Max(0, (int)Math.Floor((view.ClipX - view.OriginX) / view.TileSize) - 1);
        int lastX = Math.Min(MapManager.MAPSIZE - 1, (int)Math.Ceiling((view.ClipX + view.ClipWidth - view.OriginX) / view.TileSize));
        int firstY = Math.Max(0, (int)Math.Floor((view.ClipY - view.OriginY) / view.TileSize) - 1);
        int lastY = Math.Min(MapManager.MAPSIZE - 1, (int)Math.Ceiling((view.ClipY + view.ClipHeight - view.OriginY) / view.TileSize));

        string color = AutomapColor("AutomapWall");
        const SeenFlags allFaces = SeenFlags.NorthFace | SeenFlags.SouthFace | SeenFlags.WestFace | SeenFlags.EastFace;

        for (int y = firstY; y <= lastY; y++)
        {
            for (int x = firstX; x <= lastX; x++)
            {
                if (!IsAutomapWall(x, y))
                    continue;

                // Only the faces the player has looked at, unless the reveal cheat is on
                var faces = reveal ? allFaces : _mapManager.seen[x, y];

                if ((faces & SeenFlags.NorthFace) != 0 && !IsAutomapWall(x, y - 1)) AutomapLine(view, x, y, x + 1, y, color);
                if ((faces & SeenFlags.SouthFace) != 0 && !IsAutomapWall(x, y + 1)) AutomapLine(view, x, y + 1, x + 1, y + 1, color);
                if ((faces & SeenFlags.WestFace) != 0 && !IsAutomapWall(x - 1, y)) AutomapLine(view, x, y, x, y + 1, color);
                if ((faces & SeenFlags.EastFace) != 0 && !IsAutomapWall(x + 1, y)) AutomapLine(view, x + 1, y, x + 1, y + 1, color);
            }
        }
    }

    /// <summary>A pushwall on the move, as a square outline at its current offset.</summary>
    static void DrawAutomapPushwall(AutomapView view, bool reveal)
    {
        if (pwallstate == 0)
            return;

        int dx = dirs[(int)pwalldir][0], dy = dirs[(int)pwalldir][1];

        if (!reveal && _mapManager.seen[pwallx, pwally] == SeenFlags.None
                    && _mapManager.seen[pwallx + dx, pwally + dy] == SeenFlags.None)
            return;

        float offset = pwallpos / 64f;
        float x = pwallx + dx * offset, y = pwally + dy * offset;
        string color = AutomapColor("AutomapWall");

        AutomapLine(view, x, y, x + 1, y, color);
        AutomapLine(view, x, y + 1, x + 1, y + 1, color);
        AutomapLine(view, x, y, x, y + 1, color);
        AutomapLine(view, x + 1, y, x + 1, y + 1, color);
    }

    /// <summary>
    /// Seen doors, as a line through the middle of the door tile covering the part of the door
    /// that's still closed, so it shortens as the door slides open.
    /// </summary>
    static void DrawAutomapDoors(AutomapView view, bool reveal)
    {
        for (int i = 0; i < lastdoorobj; i++)
        {
            var door = doorobjlist[i];
            if (!reveal && _mapManager.seen[door.tilex, door.tiley] == SeenFlags.None)
                continue;

            // The door is solid from `position` (0 = closed .. 0xffff = open) to the far side
            float open = door.position / 65536f;
            if (open >= 0.99f)
                continue;

            string color = AutomapDoorColor(door.xlat.Lock);

            if (door.vertical)
                AutomapLine(view, door.tilex + 0.5f, door.tiley + open, door.tilex + 0.5f, door.tiley + 1, color);
            else
                AutomapLine(view, door.tilex + open, door.tiley + 0.5f, door.tilex + 1, door.tiley + 0.5f, color);
        }
    }

    // What an actor is drawn as, in drawing order: later kinds go on top of earlier ones sharing a
    // tile (a guard's dropped clip covers its corpse, and a live enemy covers anything).
    enum AutomapMark { None, Corpse, Decor, Item, Enemy }

    /// <summary>
    /// Actors as dots: enemies while they're in view (their corpses once seen), pickups and solid
    /// decorations once their tile is seen. Projectiles, walk-through decorations and markers
    /// aren't drawn. The reveal cheat shows them all.
    /// </summary>
    static void DrawAutomapActors(AutomapView view, bool reveal)
    {
        // A dot about a third of a tile, but never smaller than 2 virtual pixels
        int size = Math.Max(view.Pen * 2, (int)(view.TileSize / 3));

        var marks = new List<(AutomapMark Mark, int X, int Y)>();
        foreach (var actor in _mapManager.GetActors())
        {
            var mark = GetAutomapMark(actor, reveal);
            if (mark != AutomapMark.None)
                marks.Add((mark, actor.X, actor.Y));
        }

        foreach (var (mark, x, y) in marks.OrderBy(m => m.Mark))
        {
            string color = AutomapColor(mark switch
            {
                AutomapMark.Corpse => "AutomapCorpse",
                AutomapMark.Decor => "AutomapDecor",
                AutomapMark.Item => "AutomapItem",
                _ => "AutomapEnemy",
            });

            int sx = (int)MathF.Round(view.ScreenX(x / (float)MapConstants.TILEGLOBAL)) - size / 2;
            int sy = (int)MathF.Round(view.ScreenY(y / (float)MapConstants.TILEGLOBAL)) - size / 2;
            AutomapFill(view, sx, sy, size, size, color);
        }
    }

    static AutomapMark GetAutomapMark(Entities.Actors.Actor actor, bool reveal)
    {
        if (actor is PlayerPawn || actor.IsRemoved || actor.Hidden || actor.CurrentState == null)
            return AutomapMark.None;

        bool floorSeen = reveal || (_mapManager.seen[actor.TileX, actor.TileY] & SeenFlags.Floor) != 0;

        if (actor.ResolvedStates.ContainsKey("Chase"))
        {
            bool dead = (actor.RuntimeFlags & objflags.FL_SHOOTABLE) == 0 && (actor.RuntimeFlags & objflags.FL_NONMARK) != 0;
            if (dead)
                return floorSeen ? AutomapMark.Corpse : AutomapMark.None;

            return reveal || (actor.RuntimeFlags & objflags.FL_VISABLE) != 0 ? AutomapMark.Enemy : AutomapMark.None;
        }

        if (actor.Active == activetypes.ac_yes || actor.CurrentState.Sprite == "TNT1")
            return AutomapMark.None;                            // projectiles, smoke, patrol points

        if (!floorSeen)
            return AutomapMark.None;

        if (actor is Inventory)
            return AutomapMark.Item;

        if (actor.Flags.Any(f => f.Equals("SOLID", StringComparison.OrdinalIgnoreCase)))
            return AutomapMark.Decor;

        return AutomapMark.None;                                // decorations you can walk through
    }

    /// <summary>An arrow on the player, pointing the way they face.</summary>
    static void DrawAutomapPlayer(AutomapView view)
    {
        float x = player.X / (float)MapConstants.TILEGLOBAL;
        float y = player.Y / (float)MapConstants.TILEGLOBAL;

        // Wolf3D angles run counterclockwise from east, and map Y grows southward
        float radians = player.Angle * MathF.PI / 180f;
        float dirX = MathF.Cos(radians), dirY = -MathF.Sin(radians);

        // Most of a tile long, but at least 8 virtual pixels when zoomed far out
        float length = Math.Max(0.9f, 8f * view.Pen / view.TileSize);
        float half = length / 2;
        float head = length * 0.45f;

        float tipX = x + dirX * half, tipY = y + dirY * half;
        float tailX = x - dirX * half, tailY = y - dirY * half;

        // The two barbs run back from the tip at 30 degrees either side of the shaft
        const float barb = 150f * MathF.PI / 180f;
        float leftX = tipX + head * (dirX * MathF.Cos(barb) - dirY * MathF.Sin(barb));
        float leftY = tipY + head * (dirX * MathF.Sin(barb) + dirY * MathF.Cos(barb));
        float rightX = tipX + head * (dirX * MathF.Cos(-barb) - dirY * MathF.Sin(-barb));
        float rightY = tipY + head * (dirX * MathF.Sin(-barb) + dirY * MathF.Cos(-barb));

        string color = AutomapColor("AutomapPlayer");
        AutomapLine(view, tailX, tailY, tipX, tipY, color);
        AutomapLine(view, tipX, tipY, leftX, leftY, color);
        AutomapLine(view, tipX, tipY, rightX, rightY, color);
    }

    /// <summary>The player's tile and compass heading, in the bottom-left corner of the view.</summary>
    static void DrawAutomapPosition(AutomapView view)
    {
        var font = _assetManager.Find<FontAsset>(AUTOMAP_FONT);
        if (font == null)
            return;

        int heading = ((player.Angle % ANGLES + ANGLES) % ANGLES + ANGLES / 16) / (ANGLES / 8) % 8;
        string text = $"X {player.TileX}  Y {player.TileY}  {AutomapHeadings[heading]}";

        // DrawPropString works in 320x200 virtual pixels
        int px = _videoManager.scaleFactor;
        int x = view.ClipX / px + 3;
        int y = (view.ClipY + view.ClipHeight) / px - font.Height - 2;
        _videoManager.DrawPropString(x, y, text, AutomapColor("AutomapPlayer"), font);
    }

    /// <summary>A line between two map positions (in tiles), with the pen centered on it.</summary>
    static void AutomapLine(AutomapView view, float x0, float y0, float x1, float y1, string color)
    {
        int offset = view.Pen / 2;
        _videoManager.DrawLineScaledCoord(
            (int)MathF.Round(view.ScreenX(x0)) - offset, (int)MathF.Round(view.ScreenY(y0)) - offset,
            (int)MathF.Round(view.ScreenX(x1)) - offset, (int)MathF.Round(view.ScreenY(y1)) - offset,
            color, view.Pen, view.ClipX, view.ClipY, view.ClipWidth, view.ClipHeight);
    }

    /// <summary>A filled rectangle in screen pixels, trimmed to the view.</summary>
    static void AutomapFill(AutomapView view, int x, int y, int width, int height, string color)
    {
        int left = Math.Max(x, view.ClipX), top = Math.Max(y, view.ClipY);
        int right = Math.Min(x + width, view.ClipX + view.ClipWidth);
        int bottom = Math.Min(y + height, view.ClipY + view.ClipHeight);

        if (right > left && bottom > top)
            _videoManager.BarScaledCoord(left, top, right - left, bottom - top, color);
    }

    /// <summary>
    /// Whether a tile is solid wall. Doors count as open floor, and so do the tiles of a moving
    /// pushwall (drawn separately at its offset); off the map counts as wall.
    /// </summary>
    static bool IsAutomapWall(int x, int y)
    {
        if (x < 0 || y < 0 || x >= MapManager.MAPSIZE || y >= MapManager.MAPSIZE)
            return true;

        int tile = _mapManager.tilemap[x, y];
        return tile != 0 && (tile & BIT_DOOR) == 0 && tile != BIT_WALL;
    }

    private static void Cmd_AmReveal(string[] args)
    {
        mapreveal = (byte)(Toggle(args, mapreveal != 0) ? 1 : 0);
        _consoleManager.Print(mapreveal != 0 ? "Automap reveal ON" : "Automap reveal OFF");
    }
}
