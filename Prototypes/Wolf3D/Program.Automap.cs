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

    /// <summary>The keys that work while the automap is open, as indexes into <see cref="automapscan"/>.</summary>
    internal enum automapkeys
    {
        am_zoomin,
        am_zoomout,
        am_panup,
        am_pandown,
        am_panleft,
        am_panright,
        am_center,
        am_follow,
        am_rotate,

        NUMAUTOMAPKEYS
    }

    internal static ScanCodes[] automapscan = new ScanCodes[(int)automapkeys.NUMAUTOMAPKEYS]
    {
        ScanCodes.sc_Equal, ScanCodes.sc_Minus,
        ScanCodes.sc_KeyPad8, ScanCodes.sc_KeyPad2, ScanCodes.sc_KeyPad4, ScanCodes.sc_KeyPad6,
        ScanCodes.sc_C, ScanCodes.sc_F, ScanCodes.sc_R,
    };

    // Map panning speed at the default zoom: 8 tiles a second at walking pace, in virtual pixels a tic
    const float AUTOMAP_PANSPEED = 8 * AutomapManager.DefaultZoom / 70f;

    // Zoom steps a second while a zoom key is held
    const float AUTOMAP_KEYZOOMRATE = 5f;

    static bool IsAutomapKeyDown(automapkeys key) => _inputManager.IsKeyDown(automapscan[(int)key]);

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

        _automapManager.Toggle();       // the play loop's next UpdateAutomap centers it before it's drawn
    }

    /// <summary>
    /// Applies the held automap keys and the mouse wheel, then keeps the view on the player.
    /// Called every frame from the play loop.
    /// </summary>
    internal static void UpdateAutomap()
    {
        int wheel = _inputManager.TakeWheelDelta();     // taken even while closed, so turns don't pile up

        if (_mapManager.Player == null)
            return;

        if (_automapManager.IsOpen)
        {
            float zoom = wheel;
            if (IsAutomapKeyDown(automapkeys.am_zoomin) || _inputManager.IsKeyDown(ScanCodes.sc_KeyPadPlus))
                zoom += AUTOMAP_KEYZOOMRATE * tics / 70f;
            if (IsAutomapKeyDown(automapkeys.am_zoomout) || _inputManager.IsKeyDown(ScanCodes.sc_KeyPadMinus))
                zoom -= AUTOMAP_KEYZOOMRATE * tics / 70f;
            if (zoom != 0)
                _automapManager.ZoomBy(zoom);

            float pan = AUTOMAP_PANSPEED * tics * (_inputManager.IsButtonPressed(buttontypes.bt_run) ? 2 : 1);
            float panx = 0, pany = 0;
            if (IsAutomapKeyDown(automapkeys.am_panup)) pany -= pan;
            if (IsAutomapKeyDown(automapkeys.am_pandown)) pany += pan;
            if (IsAutomapKeyDown(automapkeys.am_panleft)) panx -= pan;
            if (IsAutomapKeyDown(automapkeys.am_panright)) panx += pan;
            _automapManager.Pan(panx, pany);
        }

        _automapManager.Update(player.X / (float)MapConstants.TILEGLOBAL, player.Y / (float)MapConstants.TILEGLOBAL, player.Angle);
    }

    /// <summary>
    /// Handles a fresh key press (from CheckKeys) if it's one of the automap's toggles and the map
    /// is open. Returns true if the key was used, so it doesn't also run a console bind.
    /// </summary>
    internal static bool HandleAutomapKey(ScanCodes key)
    {
        if (!_automapManager.IsOpen)
            return false;

        if (key == automapscan[(int)automapkeys.am_center])
            _automapManager.SnapToPlayer();
        else if (key == automapscan[(int)automapkeys.am_follow])
            _automapManager.ToggleFollow();
        else if (key == automapscan[(int)automapkeys.am_rotate])
            _automapManager.ToggleRotate();
        else
            return Array.IndexOf(automapscan, key) >= 0;    // held keys: used in UpdateAutomap, not binds

        return true;
    }

    /// <summary>
    /// In pan mode the player's movement controls move the map instead: called from PollControls
    /// once the movement is gathered, it hands the forward/back, turn and strafe input to the map
    /// and clears it so the player stands still. Fire, use and weapon buttons are left alone.
    /// </summary>
    internal static void RouteMovementToAutomap()
    {
        if (!_automapManager.IsOpen || _automapManager.Follow)
            return;

        // controlx/controly run BASEMOVE a tic at walking pace (RUNMOVE running)
        float dx = controlx, dy = controly;
        int strafe = (_inputManager.IsButtonPressed(buttontypes.bt_run) ? RUNMOVE : BASEMOVE) * (int)tics;
        if (_inputManager.IsButtonPressed(buttontypes.bt_strafeleft))
            dx -= strafe;
        if (_inputManager.IsButtonPressed(buttontypes.bt_straferight))
            dx += strafe;

        _automapManager.Pan(dx * AUTOMAP_PANSPEED / BASEMOVE, dy * AUTOMAP_PANSPEED / BASEMOVE);

        controlx = controly = 0;
        _inputManager.SetButtonPressed(buttontypes.bt_strafeleft, false);
        _inputManager.SetButtonPressed(buttontypes.bt_straferight, false);
    }

    /// <summary>
    /// Where the automap is being drawn this frame: the view rectangle in screen pixels, and the
    /// map-to-screen transform: offset from the map center, turned by the map's rotation, scaled
    /// to <see cref="TileSize"/> pixels a tile, then placed at the middle of the view.
    /// </summary>
    readonly record struct AutomapView(int ClipX, int ClipY, int ClipWidth, int ClipHeight,
        float TileSize, float CenterX, float CenterY, float Cos, float Sin, int Pen)
    {
        public float MidX => ClipX + ClipWidth / 2f;
        public float MidY => ClipY + ClipHeight / 2f;

        public (float X, float Y) ToScreen(float mapX, float mapY)
        {
            float x = (mapX - CenterX) * TileSize, y = (mapY - CenterY) * TileSize;
            return (MidX + x * Cos - y * Sin, MidY + x * Sin + y * Cos);
        }
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
            _automapManager.CenterX, _automapManager.CenterY,
            MathF.Cos(_automapManager.Rotation), MathF.Sin(_automapManager.Rotation), Pen: px);

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
        // Only the tiles that can reach the view: within half its diagonal of the center, whichever way it's turned
        float reach = MathF.Sqrt(view.ClipWidth * view.ClipWidth + view.ClipHeight * view.ClipHeight) / 2 / view.TileSize + 1;
        int firstX = Math.Max(0, (int)MathF.Floor(view.CenterX - reach));
        int lastX = Math.Min(MapManager.MAPSIZE - 1, (int)MathF.Ceiling(view.CenterX + reach));
        int firstY = Math.Max(0, (int)MathF.Floor(view.CenterY - reach));
        int lastY = Math.Min(MapManager.MAPSIZE - 1, (int)MathF.Ceiling(view.CenterY + reach));

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

            var (sx, sy) = view.ToScreen(x / (float)MapConstants.TILEGLOBAL, y / (float)MapConstants.TILEGLOBAL);
            AutomapFill(view, (int)MathF.Round(sx) - size / 2, (int)MathF.Round(sy) - size / 2, size, size, color);
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
        if (!_automapManager.Follow)
            text += "  PAN";

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
        var (sx0, sy0) = view.ToScreen(x0, y0);
        var (sx1, sy1) = view.ToScreen(x1, y1);
        _videoManager.DrawLineScaledCoord(
            (int)MathF.Round(sx0) - offset, (int)MathF.Round(sy0) - offset,
            (int)MathF.Round(sx1) - offset, (int)MathF.Round(sy1) - offset,
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
