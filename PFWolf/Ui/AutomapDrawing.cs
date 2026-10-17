using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Extensions;

namespace PFWolf.Ui;

/// <summary>What drawing the automap needs from the game</summary>
/// <param name="View">The 3D view's rectangle on the screen (x, y, width, height): the map is drawn in it</param>
/// <param name="MapDisplayName">A map's name as the game shows it</param>
/// <param name="FormatPlayTime">The time on the level, as the stats show it</param>
/// <param name="ObjectiveStats">The level objectives' stats lines (Gameplay/Objectives.cs)</param>
internal sealed record AutomapHooks(
    Func<(int X, int Y, int Width, int Height)> View,
    Func<string, string> MapDisplayName,
    Func<int, string> FormatPlayTime,
    Func<IEnumerable<string>> ObjectiveStats);

/// <summary>
/// The automap drawn over the 3D view (AutomapManager has where it's centred, its zoom, style and
/// the rest): the walls, doors and MovingWalls the player has seen, the actors worth marking, the
/// player's arrow, position and the level's stats.
///
/// AUTOMAP GRAPHIC MODE draws it with the level's own art: seen walls filled with their
/// textures, doors and the moving pushwall textured in place, and actors as their sprites. The
/// tile layer is drawn a pixel at a time, mapping each view pixel back to the map, so zoom and
/// rotation cost the same as a plain view.
/// </summary>
internal sealed class AutomapDrawing(
    AssetManager assets,
    VideoManager video,
    GraphicManager graphics,
    FontManager fonts,
    AutomapManager automap,
    MapManager map,
    FactionManager factions,
    TranslationManager translations,
    Doors doors,
    PushWalls pushWalls,
    SpriteDrawing spriteDrawing,
    GameSession session,
    AutomapHooks hooks)
{
    // Theme color names (colors.yaml) and the colors used when a game pack doesn't define them.
    readonly Dictionary<string, string> AutomapColorFallbacks = new()
    {
        ["AutomapBackground"] = "#000000",
        ["AutomapWall"] = "#C2C2C2",
        ["AutomapDoor"] = "#E6DA00",
        ["AutomapPlayer"] = "#55FF55",
        ["AutomapEnemy"] = "#FF0000",
        ["AutomapAlly"] = "#55FFFF",
        ["AutomapCorpse"] = "#710000",
        ["AutomapItem"] = "#20AAFF",
        ["AutomapDecor"] = "#8D8D8D",
        ["AutomapWallSprite"] = "#8D8D8D",
        ["AutomapGrid"] = "#004040",
        ["AutomapTitle"] = "#FFFF55",
        ["AutomapStats"] = "#C2C2C2",
    };

    // Language keys for the compass headings, counterclockwise from east like Wolf3D angles
    readonly string[] AutomapHeadings =
        ["$STR_AM_E", "$STR_AM_NE", "$STR_AM_N", "$STR_AM_NW", "$STR_AM_W", "$STR_AM_SW", "$STR_AM_S", "$STR_AM_SE"];

    string AutomapText(string key, params object[] args) =>
        string.Format(key.ToLanguageText(assets.GetText("en-us")), args);

    // The grid only shows from this zoom (virtual pixels per tile) up; any closer together it's a solid wash
    const float AutomapMinGridZoom = 6f;

    // How bright the 3D view stays under the map in Overlay mode
    const float AutomapOverlayBrightness = 0.4f;

    const string AutomapFont = "SmallFont";

    string AutomapColor(string name) =>
        video.IsThemeColor(name) ? name : AutomapColorFallbacks[name];

    // A locked door is drawn in its key's color: the theme's "Automap<lock item>" if it has one
    // (e.g. AutomapGoldKey), else the door's mapdefs automap-color, else the plain door color.
    string AutomapDoorColor(DoorObject door)
    {
        if (!string.IsNullOrEmpty(door.Lock) && video.IsThemeColor("Automap" + door.Lock))
            return "Automap" + door.Lock;

        return !string.IsNullOrEmpty(door.Xlat.AutomapColor) ? door.Xlat.AutomapColor : AutomapColor("AutomapDoor");
    }

    /// <summary>
    /// Where the automap is being drawn this frame: the view rectangle in screen pixels, and the
    /// map-to-screen transform: offset from the map center, turned by the map's rotation, scaled
    /// to <see cref="TileSize"/> pixels a tile, then placed at the middle of the view.
    /// </summary>
    internal readonly record struct AutomapView(int ClipX, int ClipY, int ClipWidth, int ClipHeight,
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
    internal void DrawAutomap()
    {
        if (!automap.IsOpen || map.Player == null)
            return;

        // Sized with the UI: a layout pixel's worth of screen pixels for the pen, whole
        float tileSize = automap.Zoom * (float)video.UiScale;   // screen pixels per tile
        int pen = Math.Max(1, video.ToScreenLength(1));
        var clip = hooks.View();
        var view = new AutomapView(clip.X, clip.Y, clip.Width, clip.Height, tileSize,
            automap.CenterX, automap.CenterY,
            MathF.Cos(automap.Rotation), MathF.Sin(automap.Rotation), Pen: pen);

        // Overlay: the 3D view just drawn, dimmed, so the game still shows through
        if (automap.Overlay)
            video.ShadeRegionScaledCoord(view.ClipX, view.ClipY, view.ClipWidth, view.ClipHeight,
                video.GetDarkenTable(AutomapOverlayBrightness));
        else
            video.BarScaledCoord(view.ClipX, view.ClipY, view.ClipWidth, view.ClipHeight, AutomapColor("AutomapBackground"));

        bool reveal = (session.MapReveal != 0);

        if (automap.ShowGrid && automap.Zoom >= AutomapMinGridZoom)
            DrawAutomapGrid(view);

        if (automap.Style == AutomapStyle.Graphic)
        {
            DrawAutomapTexels(view, reveal);
            DrawAutomapWallSprites(view, reveal, graphic: true);
            DrawAutomapActors(view, reveal, sprites: automap.Zoom >= AutomapMinSpriteZoom);
        }
        else
        {
            DrawAutomapWalls(view, reveal);
            DrawAutomapPushwall(view, reveal);
            DrawAutomapDoors(view, reveal);
            DrawAutomapWallSprites(view, reveal, graphic: false);
            DrawAutomapActors(view, reveal, sprites: false);
        }

        DrawAutomapPlayer(view);
        DrawAutomapPosition(view);

        if (automap.ShowStats)
            DrawAutomapStats(view);
    }

    /// <summary>A faint one-pixel line along every tile edge of the map that reaches the view, under everything else.</summary>
    void DrawAutomapGrid(AutomapView view)
    {
        var (first, last) = AutomapReach(view.CenterX, view);
        var (firstY, lastY) = AutomapReach(view.CenterY, view);
        string color = AutomapColor("AutomapGrid");

        for (int x = first; x <= last + 1; x++)
            AutomapLine(view, x, firstY, x, lastY + 1, color, pen: 1);
        for (int y = firstY; y <= lastY + 1; y++)
            AutomapLine(view, first, y, last + 1, y, color, pen: 1);
    }

    /// <summary>
    /// The tiles along one axis that can reach the view: within half its diagonal of the center,
    /// whichever way the map is turned, and on the map.
    /// </summary>
    (int First, int Last) AutomapReach(float center, AutomapView view)
    {
        float reach = MathF.Sqrt(view.ClipWidth * view.ClipWidth + view.ClipHeight * view.ClipHeight) / 2 / view.TileSize + 1;
        return (Math.Max(0, (int)MathF.Floor(center - reach)), Math.Min(MapManager.MapSize - 1, (int)MathF.Ceiling(center + reach)));
    }

    /// <summary>
    /// Every seen wall face that borders open space, as a line along the tile edge. A diagonal
    /// wall shows its two solid edges and a line corner to corner for its 45 degree face.
    /// </summary>
    void DrawAutomapWalls(AutomapView view, bool reveal)
    {
        var (firstX, lastX) = AutomapReach(view.CenterX, view);
        var (firstY, lastY) = AutomapReach(view.CenterY, view);

        string color = AutomapColor("AutomapWall");
        const SeenFlags allFaces = SeenFlags.NorthFace | SeenFlags.SouthFace | SeenFlags.WestFace | SeenFlags.EastFace
            | SeenFlags.DiagonalFace;

        for (int y = firstY; y <= lastY; y++)
        {
            for (int x = firstX; x <= lastX; x++)
            {
                if (!IsAutomapWall(x, y))
                    continue;

                // Only the faces the player has looked at, unless the reveal cheat is on
                var faces = reveal ? allFaces : map.Seen[x, y];

                var shape = map.WallShapes[x, y];
                if (shape != WallShape.Square)
                {
                    faces &= Diagonals.DiagonalSolidEdges(shape) | SeenFlags.DiagonalFace;   // its open edges are empty space
                    if ((faces & SeenFlags.DiagonalFace) != 0)
                    {
                        var (u0, v0, u1, v1) = Diagonals.DiagonalFaceEnds(shape);
                        AutomapLine(view, x + u0, y + v0, x + u1, y + v1, color);
                    }
                }

                if ((faces & SeenFlags.NorthFace) != 0 && !IsAutomapSolidEdge(x, y - 1, SeenFlags.SouthFace)) AutomapLine(view, x, y, x + 1, y, color);
                if ((faces & SeenFlags.SouthFace) != 0 && !IsAutomapSolidEdge(x, y + 1, SeenFlags.NorthFace)) AutomapLine(view, x, y + 1, x + 1, y + 1, color);
                if ((faces & SeenFlags.WestFace) != 0 && !IsAutomapSolidEdge(x - 1, y, SeenFlags.EastFace)) AutomapLine(view, x, y, x, y + 1, color);
                if ((faces & SeenFlags.EastFace) != 0 && !IsAutomapSolidEdge(x + 1, y, SeenFlags.WestFace)) AutomapLine(view, x + 1, y, x + 1, y + 1, color);
            }
        }
    }

    /// <summary>The MovingWalls on the move, each as a square outline at its current offset.</summary>
    void DrawAutomapPushwall(AutomapView view, bool reveal)
    {
        string color = AutomapColor("AutomapWall");

        foreach (var wall in pushWalls.MovingWalls)
        {
            int dx = PushWalls.Dirs[(int)wall.Dir][0], dy = PushWalls.Dirs[(int)wall.Dir][1];

            if (!reveal && map.Seen[wall.X, wall.Y] == SeenFlags.None
                        && map.Seen[wall.X + dx, wall.Y + dy] == SeenFlags.None)
                continue;

            float offset = wall.Pos / 64f;
            float x = wall.X + dx * offset, y = wall.Y + dy * offset;

            AutomapLine(view, x, y, x + 1, y, color);
            AutomapLine(view, x, y + 1, x + 1, y + 1, color);
            AutomapLine(view, x, y, x, y + 1, color);
            AutomapLine(view, x + 1, y, x + 1, y + 1, color);
        }
    }

    /// <summary>
    /// Seen doors, as a line through the middle of the door tile covering the part of the door
    /// that's still closed, so it shortens as the door slides open.
    /// </summary>
    void DrawAutomapDoors(AutomapView view, bool reveal)
    {
        for (int i = 0; i < doors.LastDoorObj; i++)
        {
            var door = doors.DoorObjList[i];
            if (!reveal && map.Seen[door.TileX, door.TileY] == SeenFlags.None)
                continue;

            // The door is solid from `position` (0 = closed .. 0xffff = open) to the far side
            float open = door.Position / 65536f;
            if (open >= 0.99f)
                continue;

            string color = AutomapDoorColor(door);

            if (door.Xlat.Split)
            {
                // Two halves, each solid from its outer edge to half of `open` short of the middle
                float half = 0.5f - open / 2;
                if (door.Vertical)
                {
                    AutomapLine(view, door.TileX + 0.5f, door.TileY, door.TileX + 0.5f, door.TileY + half, color);
                    AutomapLine(view, door.TileX + 0.5f, door.TileY + 1 - half, door.TileX + 0.5f, door.TileY + 1, color);
                }
                else
                {
                    AutomapLine(view, door.TileX, door.TileY + 0.5f, door.TileX + half, door.TileY + 0.5f, color);
                    AutomapLine(view, door.TileX + 1 - half, door.TileY + 0.5f, door.TileX + 1, door.TileY + 0.5f, color);
                }
                continue;
            }

            if (door.Vertical)
                AutomapLine(view, door.TileX + 0.5f, door.TileY + open, door.TileX + 0.5f, door.TileY + 1, color);
            else
                AutomapLine(view, door.TileX + open, door.TileY + 0.5f, door.TileX + 1, door.TileY + 0.5f, color);
        }
    }

    /// <summary>
    /// Wall sprites (fences, gates) once their tile or one beside it is seen, as a line along the
    /// panel: in the theme's AutomapWallSprite color, or in graphic style a strip in the color
    /// most of its sprite is.
    /// </summary>
    void DrawAutomapWallSprites(AutomapView view, bool reveal, bool graphic)
    {
        // In graphic style about as wide as a door's strip, so it reads next to them
        int pen = graphic ? Math.Max(view.Pen, (int)(view.TileSize * AutomapDoorWidth / 2)) : view.Pen;

        foreach (var actor in map.GetActors())
        {
            if (actor.IsRemoved || actor.CurrentState == null || !WallSprites.IsWallSprite(actor))
                continue;
            if (WallSprites.GetWallSpriteSpan(actor) is not { } span)
                continue;

            int x = actor.TileX, y = actor.TileY;
            bool seen = reveal || ((map.Seen[x, y] | map.Seen[x + 1, y] | map.Seen[x - 1, y]
                | map.Seen[x, y + 1] | map.Seen[x, y - 1]) & SeenFlags.Floor) != 0;
            if (!seen)
                continue;

            string color = graphic && AutomapSpriteColor(actor.CurrentState.GetShapeName(ObjDir.NoDir)) is { } index
                ? index.ToString()
                : AutomapColor("AutomapWallSprite");

            const float T = MapConstants.TileGlobal;
            AutomapLine(view, (float)(span.X1 / T), (float)(span.Y1 / T), (float)(span.X2 / T), (float)(span.Y2 / T), color, pen);
        }
    }

    // What an actor is drawn as, in drawing order: later kinds go on top of earlier ones sharing a
    // tile (a guard's dropped clip covers its corpse, and a live enemy covers anything).
    internal enum AutomapMark { None, Corpse, Decor, Item, Ally, Enemy }

    /// <summary>
    /// Actors worth marking: enemies while they're in view (their corpses once seen), pickups and
    /// solid decorations once their tile is seen. Projectiles, walk-through decorations and markers
    /// aren't drawn. The reveal cheat shows them all. Each is drawn as its sprite when
    /// <paramref name="sprites"/> is set (falling back to a dot if it has none), else as a dot.
    /// </summary>
    void DrawAutomapActors(AutomapView view, bool reveal, bool sprites)
    {
        // A dot about a third of a tile, but never smaller than 2 virtual pixels
        int size = Math.Max(view.Pen * 2, (int)(view.TileSize / 3));

        var marks = new List<(AutomapMark Mark, Entities.Actors.Actor Actor)>();
        foreach (var actor in map.GetActors())
        {
            var mark = GetAutomapMark(actor, reveal);
            if (mark != AutomapMark.None)
                marks.Add((mark, actor));
        }

        foreach (var (mark, actor) in marks.OrderBy(m => m.Mark))
        {
            var (sx, sy) = view.ToScreen(actor.X / (float)MapConstants.TileGlobal, actor.Y / (float)MapConstants.TileGlobal);

            if (sprites && DrawAutomapSprite(view, actor, sx, sy))
                continue;

            string color = AutomapColor(mark switch
            {
                AutomapMark.Corpse => "AutomapCorpse",
                AutomapMark.Decor => "AutomapDecor",
                AutomapMark.Item => "AutomapItem",
                AutomapMark.Ally => "AutomapAlly",
                _ => "AutomapEnemy",
            });

            AutomapFill(view, (int)MathF.Round(sx) - size / 2, (int)MathF.Round(sy) - size / 2, size, size, color);
        }
    }

    internal AutomapMark GetAutomapMark(Entities.Actors.Actor actor, bool reveal)
    {
        if (actor is PlayerPawn || actor.IsRemoved || actor.Hidden || actor.CurrentState == null)
            return AutomapMark.None;

        bool floorSeen = reveal || (map.Seen[actor.TileX, actor.TileY] & SeenFlags.Floor) != 0;

        if (actor.ResolvedStates.ContainsKey("Chase"))
        {
            bool dead = (actor.RuntimeFlags & ObjFlags.Shootable) == 0 && (actor.RuntimeFlags & ObjFlags.NonMark) != 0;
            if (dead)
                return floorSeen ? AutomapMark.Corpse : AutomapMark.None;

            // The player's own side (factions.yaml) in its own color; one following them is
            // always shown, wherever it is
            if (factions.IsAlly(actor, session.Player))
                return reveal || (actor.RuntimeFlags & ObjFlags.Visible) != 0
                    || actor is Entities.Actors.Monster { Follows: true } ? AutomapMark.Ally : AutomapMark.None;

            return reveal || (actor.RuntimeFlags & ObjFlags.Visible) != 0 ? AutomapMark.Enemy : AutomapMark.None;
        }

        if (actor.Active == ActiveType.Yes || actor.CurrentState.Sprite == "TNT1")
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
    void DrawAutomapPlayer(AutomapView view)
    {
        float x = session.Player.X / (float)MapConstants.TileGlobal;
        float y = session.Player.Y / (float)MapConstants.TileGlobal;

        // Wolf3D angles run counterclockwise from east, and map Y grows southward
        float radians = session.Player.Angle * MathF.PI / 180f;
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
    void DrawAutomapPosition(AutomapView view)
    {
        var font = fonts.Find(AutomapFont);
        if (font == null)
            return;

        int heading = ((session.Player.Angle % GameConstants.Angles + GameConstants.Angles) % GameConstants.Angles + GameConstants.Angles / 16) / (GameConstants.Angles / 8) % 8;
        string text = AutomapText("$STR_AM_POSITION", session.Player.TileX, session.Player.TileY, AutomapText(AutomapHeadings[heading]));
        if (!automap.Follow)
            text += "  " + AutomapText("$STR_AM_PAN");

        // DrawText and Bar work in 320x200 virtual pixels
        var (left, top, right, bottom) = LayoutExtent(view);
        int x = left + 3;
        int y = bottom - font.Height - 2;
        int room = right - left - 6;

        // The smallest view sizes can't fit it all: fall back to just the tile, then to nothing
        int width = font.Measure(text);
        if (width > room)
        {
            text = AutomapText("$STR_AM_POSITIONSHORT", session.Player.TileX, session.Player.TileY);
            width = font.Measure(text);
            if (width > room || font.Height + 2 > bottom - top)
                return;
        }

        // On a backdrop box, so the map under it doesn't make it hard to read
        video.Bar(x - 2, y - 1, width + 4, font.Height + 2, AutomapColor("AutomapBackground"));
        graphics.DrawText(x, y, text, font, AutomapColor("AutomapPlayer"));
    }

    /// <summary>
    /// The 320x200 layout pixels wholly inside the view, for text over the map: the first
    /// column and row in it, and the column and row just past it.
    /// </summary>
    (int Left, int Top, int Right, int Bottom) LayoutExtent(AutomapView view) => (
        video.ToLayoutX(view.ClipX, roundUp: true),
        video.ToLayoutY(view.ClipY, roundUp: true),
        video.ToLayoutX(view.ClipX + view.ClipWidth),
        video.ToLayoutY(view.ClipY + view.ClipHeight));

    /// <summary>
    /// The level's name, then the kills, treasure and secrets found so far out of the level's
    /// totals and the time on the level, one to a line down the top-left corner of the view.
    /// Lines that don't fit the view's width or height are left out.
    /// </summary>
    void DrawAutomapStats(AutomapView view)
    {
        var font = fonts.Find(AutomapFont);
        if (font == null)
            return;

        (string Text, string Color)[] lines =
        [
            (hooks.MapDisplayName(session.GameState.MapOn), AutomapColor("AutomapTitle")),
            (AutomapText("$STR_AM_KILLS", session.GameState.KillCount, session.GameState.KillTotal), AutomapColor("AutomapStats")),
            (AutomapText("$STR_AM_TREASURE", session.GameState.TreasureCount, session.GameState.TreasureTotal), AutomapColor("AutomapStats")),
            (AutomapText("$STR_AM_SECRETS", session.GameState.SecretCount, session.GameState.SecretTotal), AutomapColor("AutomapStats")),
            .. (hooks.ObjectiveStats?.Invoke() ?? []).Select(line => (line, AutomapColor("AutomapStats"))),     // "Sabotages: 2 of 5"
            (AutomapText("$STR_AM_TIME", hooks.FormatPlayTime(session.GameState.TimeCount)), AutomapColor("AutomapStats")),
        ];

        // DrawText and Bar work in 320x200 virtual pixels; the bottom rows are the position line's
        var (left, top, right, viewBottom) = LayoutExtent(view);
        int x = left + 3;
        int y = top + 2;
        int room = right - left - 6;
        int bottom = viewBottom - font.Height - 4;

        foreach (var (text, color) in lines)
        {
            if (y + font.Height + 1 > bottom)
                break;

            int width = font.Measure(text);
            if (width <= room)
            {
                // On a backdrop box, like the position line
                video.Bar(x - 2, y - 1, width + 4, font.Height + 2, AutomapColor("AutomapBackground"));
                graphics.DrawText(x, y, text, font, color);
            }

            y += font.Height + 2;
        }
    }

    /// <summary>
    /// A line between two map positions (in tiles), with the pen centered on it. The pen is one
    /// virtual pixel wide unless <paramref name="pen"/> gives a width in screen pixels.
    /// </summary>
    void AutomapLine(AutomapView view, float x0, float y0, float x1, float y1, string color, int pen = 0)
    {
        if (pen <= 0)
            pen = view.Pen;

        int offset = pen / 2;
        var (sx0, sy0) = view.ToScreen(x0, y0);
        var (sx1, sy1) = view.ToScreen(x1, y1);
        video.DrawLineScaledCoord(
            (int)MathF.Round(sx0) - offset, (int)MathF.Round(sy0) - offset,
            (int)MathF.Round(sx1) - offset, (int)MathF.Round(sy1) - offset,
            color, pen, view.ClipX, view.ClipY, view.ClipWidth, view.ClipHeight);
    }

    /// <summary>A filled rectangle in screen pixels, trimmed to the view.</summary>
    void AutomapFill(AutomapView view, int x, int y, int width, int height, string color)
    {
        int left = Math.Max(x, view.ClipX), top = Math.Max(y, view.ClipY);
        int right = Math.Min(x + width, view.ClipX + view.ClipWidth);
        int bottom = Math.Min(y + height, view.ClipY + view.ClipHeight);

        if (right > left && bottom > top)
            video.BarScaledCoord(left, top, right - left, bottom - top, color);
    }

    /// <summary>
    /// Whether a tile is solid wall. Doors count as open floor, and so do the tiles of a moving
    /// pushwall (drawn separately at its offset); off the map counts as wall.
    /// </summary>
    internal bool IsAutomapWall(int x, int y)
    {
        if (x < 0 || y < 0 || x >= MapManager.MapSize || y >= MapManager.MapSize)
            return true;

        int tile = map.TileMap[x, y];
        return tile != 0 && (tile & GameConstants.BitDoor) == 0 && tile != GameConstants.BitWall;
    }

    /// <summary>
    /// Whether the tile at (x, y) is solid all along its given edge: a wall, and for a diagonal,
    /// one of its solid edges (across an open edge its neighbor's face can be seen).
    /// </summary>
    internal bool IsAutomapSolidEdge(int x, int y, SeenFlags edge)
    {
        if (!IsAutomapWall(x, y))
            return false;
        if (x < 0 || y < 0 || x >= MapManager.MapSize || y >= MapManager.MapSize)
            return true;

        return (Diagonals.DiagonalSolidEdges(map.WallShapes[x, y]) & edge) != 0;
    }

    // Below this zoom (virtual pixels per tile) sprites are too small to make out, so they're drawn as dots
    const float AutomapMinSpriteZoom = 6f;

    // A door is drawn as a strip this wide (in tiles) through the middle of its tile
    const float AutomapDoorWidth = 0.2f;

    // Sprites are scaled so their visible part fits this much of a tile
    const float AutomapSpriteSize = 0.9f;

    const int AutomapTexSize = 64;     // wall textures are 64x64, stored column by column

    readonly Dictionary<string, byte[]?> automapTextures = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, AutomapSprite?> automapSprites = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A sprite and the box around its opaque pixels, which is what the map shows.</summary>
    sealed record AutomapSprite(SpriteAsset Asset, int Left, int Top, int Width, int Height);

    // Filled each frame so the per-pixel loop never looks anything up by name: the textures of each
    // seen wall tile's four faces, and which seen door (index + 1) a tile holds.
    readonly AutomapFaces[,] automapWalls = new AutomapFaces[MapManager.MapSize, MapManager.MapSize];

    /// <summary>The textures on a wall tile's four faces, as the automap draws them</summary>
    readonly record struct AutomapFaces(byte[]? North, byte[]? South, byte[]? East, byte[]? West)
    {
        public bool Any => North != null || South != null || East != null || West != null;
    }
    readonly int[,] automapDoorAt = new int[MapManager.MapSize, MapManager.MapSize];
    byte[]?[] automapDoorTextures = [];

    /// <summary>A wall texture's pixels, or null if it's missing or not 64x64.</summary>
    byte[]? AutomapTexture(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        if (!automapTextures.TryGetValue(name, out var pixels))
        {
            // an animated one (animdefs) shows its first frame
            pixels = assets.Find<TextureAsset>(name, 0)?.BottomStory();
            if (pixels != null && pixels.Length < AutomapTexSize * AutomapTexSize)
                pixels = null;
            automapTextures[name] = pixels;
        }

        return pixels;
    }

    /// <summary>The textures a wall tile shows on its four faces.</summary>
    AutomapFaces AutomapWallTextures(MapObjectTranslationAsset? mapDefs, int tile)
    {
        // A wall beside a door carries the door-side mark (BitWall), which isn't part of its id
        MapTextureTranslation? textures = null;
        if (mapDefs == null || !mapDefs.Walls.TryGetValue(tile & ~GameConstants.BitWall, out textures))
            return default;

        return new AutomapFaces(AutomapTexture(textures.WallFace("north")), AutomapTexture(textures.WallFace("south")),
            AutomapTexture(textures.WallFace("east")), AutomapTexture(textures.WallFace("west")));
    }

    /// <summary>
    /// Fills the seen walls, doors and the moving pushwall with their textures, over the backdrop.
    /// </summary>
    void DrawAutomapTexels(AutomapView view, bool reveal)
    {
        var mapDefs = map.GetMapData();

        //
        // what each tile shows this frame
        //
        var wallTextures = new Dictionary<int, AutomapFaces>();
        for (int x = 0; x < MapManager.MapSize; x++)
        {
            for (int y = 0; y < MapManager.MapSize; y++)
            {
                automapWalls[x, y] = default;
                automapDoorAt[x, y] = 0;

                if (!IsAutomapWall(x, y))
                    continue;

                // Revealed, only the walls that face open space: the solid mass behind them could
                // never be seen, and filling it in buries the level's shape. A diagonal always
                // faces its own open half.
                bool visible = reveal
                    ? !IsAutomapWall(x - 1, y) || !IsAutomapWall(x + 1, y) || !IsAutomapWall(x, y - 1) || !IsAutomapWall(x, y + 1)
                      || map.WallShapes[x, y] != WallShape.Square
                    : map.Seen[x, y] != SeenFlags.None;
                if (!visible)
                    continue;

                int tile = map.TileMap[x, y];
                if (!wallTextures.TryGetValue(tile, out var textures))
                    wallTextures[tile] = textures = AutomapWallTextures(mapDefs, tile);

                automapWalls[x, y] = textures;
            }
        }

        if (automapDoorTextures.Length < doors.LastDoorObj)
            automapDoorTextures = new byte[]?[doors.DoorObjList.Length];

        for (int i = 0; i < doors.LastDoorObj; i++)
        {
            var door = doors.DoorObjList[i];
            if (!reveal && map.Seen[door.TileX, door.TileY] == SeenFlags.None)
                continue;

            automapDoorAt[door.TileX, door.TileY] = i + 1;
            automapDoorTextures[i] = AutomapTexture(door.Vertical ? door.Xlat.East : door.Xlat.North);
        }

        // The MovingWalls on the move that can be seen: each one's corner on the map and textures
        var pushes = new List<(float X, float Y, AutomapFaces Faces)>();
        foreach (var wall in pushWalls.MovingWalls)
        {
            int dx = PushWalls.Dirs[(int)wall.Dir][0], dy = PushWalls.Dirs[(int)wall.Dir][1];
            if (!reveal && map.Seen[wall.X, wall.Y] == SeenFlags.None
                        && map.Seen[wall.X + dx, wall.Y + dy] == SeenFlags.None)
                continue;

            pushes.Add((wall.X + dx * wall.Pos / 64f, wall.Y + dy * wall.Pos / 64f, AutomapWallTextures(mapDefs, wall.Tile)));
        }

        //
        // each view pixel, mapped back onto the map
        //
        float stepX = view.Cos / view.TileSize;     // map distance per pixel to the right
        float stepY = -view.Sin / view.TileSize;

        IntPtr destPtr = video.LockSurface();
        if (destPtr == IntPtr.Zero)
            return;

        unsafe
        {
            byte* dest = (byte*)destPtr;

            for (int sy = view.ClipY; sy < view.ClipY + view.ClipHeight; sy++)
            {
                float dy = sy + 0.5f - view.MidY;
                float dx = view.ClipX + 0.5f - view.MidX;
                float mx = view.CenterX + (dx * view.Cos + dy * view.Sin) / view.TileSize;
                float my = view.CenterY + (-dx * view.Sin + dy * view.Cos) / view.TileSize;

                byte* row = dest + video.YLookup[sy];

                for (int sx = view.ClipX; sx < view.ClipX + view.ClipWidth; sx++, mx += stepX, my += stepY)
                {
                    int texel = int.MinValue;

                    foreach (var push in pushes)
                    {
                        if (mx >= push.X && mx < push.X + 1 && my >= push.Y && my < push.Y + 1)
                        {
                            texel = AutomapWallTexel(push.Faces, mx - push.X, my - push.Y);
                            break;
                        }
                    }
                    if (texel == int.MinValue)
                        texel = AutomapTileTexel(mx, my);

                    if (texel >= 0)
                        row[sx] = (byte)texel;
                }
            }
        }

        video.UnlockSurface();
    }

    /// <summary>The color of a map position in the tile layer, or -1 where the backdrop shows through.</summary>
    int AutomapTileTexel(float mx, float my)
    {
        if (mx < 0 || my < 0)
            return -1;

        int tx = (int)mx, ty = (int)my;
        if (tx >= MapManager.MapSize || ty >= MapManager.MapSize)
            return -1;

        float fx = mx - tx, fy = my - ty;

        var faces = automapWalls[tx, ty];
        if (faces.Any)
        {
            // a diagonal wall fills only its solid half; the backdrop shows through the open one
            var shape = map.WallShapes[tx, ty];
            if (shape != WallShape.Square && !Diagonals.InDiagonalSolid(shape, fx, fy))
                return -1;

            return AutomapWallTexel(faces, fx, fy);
        }

        int door = automapDoorAt[tx, ty];
        if (door == 0)
            return -1;

        var doorobj = doors.DoorObjList[door - 1];
        var texture = automapDoorTextures[door - 1];
        if (texture == null)
            return -1;

        // Across the strip and along the door: the door is solid from `position` to the far side
        // of its tile, and its texture slides with it, as in the 3D view
        float across = doorobj.Vertical ? fx : fy;
        float along = doorobj.Vertical ? fy : fx;
        int alongFixed = Math.Clamp((int)(along * 65536f), 0, 0xffff);

        float edge = 0.5f - AutomapDoorWidth / 2;
        if (across < edge || across >= edge + AutomapDoorWidth || doorobj.OpenAt(alongFixed))
            return -1;

        int column = Math.Clamp((int)(doorobj.TextureAlong(alongFixed) / 65536f * AutomapTexSize), 0, AutomapTexSize - 1);
        int texrow = Math.Min((int)((across - edge) / AutomapDoorWidth * AutomapTexSize), AutomapTexSize - 1);
        return texture[column * AutomapTexSize + texrow];
    }

    /// <summary>
    /// A wall tile's color at (fx, fy) within it. The tile is split corner to corner: each part
    /// uses the texture of the face on its nearest edge (a face with none, the other axis's), so
    /// with Wolf3D's darker east/west faces each block reads as raised with shaded sides.
    /// </summary>
    int AutomapWallTexel(AutomapFaces faces, float fx, float fy)
    {
        bool nearNorthSouth = MathF.Min(fy, 1 - fy) < MathF.Min(fx, 1 - fx);
        var northSouth = fy < 0.5f ? faces.North : faces.South;
        var eastWest = fx < 0.5f ? faces.West : faces.East;
        var texture = nearNorthSouth ? northSouth ?? eastWest : eastWest ?? northSouth;
        if (texture == null)
            return -1;

        int column = Math.Min((int)(fx * AutomapTexSize), AutomapTexSize - 1);
        int row = Math.Min((int)(fy * AutomapTexSize), AutomapTexSize - 1);
        return texture[column * AutomapTexSize + row];
    }

    /// <summary>
    /// Draws an actor as its current sprite, upright whichever way the map is turned, centered on
    /// its screen position (sx, sy). Enemies show their front-facing frame. Returns false if the
    /// actor has no sprite to draw, so the caller can draw a dot instead.
    /// </summary>
    bool DrawAutomapSprite(AutomapView view, Entities.Actors.Actor actor, float sx, float sy)
    {
        var state = actor.CurrentState!;
        string shape = spriteDrawing.HasDirectionalSprites(state.Sprite, state.FrameLetter)
            ? spriteDrawing.FrontShapeName(state.Sprite, state.FrameLetter)
            : state.GetShapeName(ObjDir.NoDir);

        var sprite = FindAutomapSprite(shape);
        if (sprite == null)
            return false;

        // Screen pixels per sprite pixel, fitting the sprite's visible part into most of a tile
        float scale = view.TileSize * AutomapSpriteSize / Math.Max(sprite.Width, sprite.Height);
        float left = sx - sprite.Width * scale / 2, top = sy - sprite.Height * scale / 2;

        int x0 = Math.Max(view.ClipX, (int)MathF.Floor(left));
        int y0 = Math.Max(view.ClipY, (int)MathF.Floor(top));
        int x1 = Math.Min(view.ClipX + view.ClipWidth, (int)MathF.Ceiling(left + sprite.Width * scale));
        int y1 = Math.Min(view.ClipY + view.ClipHeight, (int)MathF.Ceiling(top + sprite.Height * scale));
        if (x1 <= x0 || y1 <= y0)
            return true;                                    // off the view: nothing to draw, but not missing

        var asset = sprite.Asset;
        var translation = translations.TableOf(actor);      // in its colors (an ally's, a player's)

        IntPtr destPtr = video.LockSurface();
        if (destPtr == IntPtr.Zero)
            return true;

        unsafe
        {
            byte* dest = (byte*)destPtr;

            for (int y = y0; y < y1; y++)
            {
                int v = (int)((y + 0.5f - top) / scale);
                if (v < 0 || v >= sprite.Height)
                    continue;

                byte* row = dest + video.YLookup[y];
                int srcRow = (sprite.Top + v) * asset.Width + sprite.Left;

                for (int x = x0; x < x1; x++)
                {
                    int u = (int)((x + 0.5f - left) / scale);
                    if (u < 0 || u >= sprite.Width)
                        continue;

                    if (asset.OpacityMask[srcRow + u] != 0)
                        row[x] = translation == null ? asset.RawData[srcRow + u]
                            : translation.ForRow(sprite.Top + v - asset.TopRow)[asset.RawData[srcRow + u]];
                }
            }
        }

        video.UnlockSurface();
        return true;
    }

    readonly Dictionary<string, byte?> automapSpriteColors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The palette color most of a sprite's opaque pixels are, or null if it's missing or blank.</summary>
    byte? AutomapSpriteColor(string shape)
    {
        if (automapSpriteColors.TryGetValue(shape, out var cached))
            return cached;

        byte? color = null;
        var asset = assets.Find<SpriteAsset>(shape);
        if (asset != null && asset.OpacityMask.Length >= asset.Width * asset.Height)
        {
            var counts = new int[256];
            for (int i = 0; i < asset.Width * asset.Height; i++)
                if (asset.OpacityMask[i] != 0)
                    counts[asset.RawData[i]]++;

            int best = Array.IndexOf(counts, counts.Max());
            if (counts[best] > 0)
                color = (byte)best;
        }

        automapSpriteColors[shape] = color;
        return color;
    }

    /// <summary>A sprite trimmed to its opaque pixels, or null if it's missing or blank.</summary>
    AutomapSprite? FindAutomapSprite(string shape)
    {
        if (automapSprites.TryGetValue(shape, out var cached))
            return cached;

        AutomapSprite? sprite = null;
        var asset = assets.Find<SpriteAsset>(shape);
        if (asset != null && asset.OpacityMask.Length >= asset.Width * asset.Height)
        {
            int left = asset.Width, top = asset.Height, right = -1, bottom = -1;
            for (int y = 0; y < asset.Height; y++)
            {
                for (int x = 0; x < asset.Width; x++)
                {
                    if (asset.OpacityMask[y * asset.Width + x] == 0)
                        continue;

                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }

            if (right >= 0)
                sprite = new AutomapSprite(asset, left, top, right - left + 1, bottom - top + 1);
        }

        automapSprites[shape] = sprite;
        return sprite;
    }
}
