using PFWolf.Assets;
using PFWolf.Configuration;
using PFWolf.Entities.Actors;
using PFWolf.Fonts;

namespace PFWolf.Ui;

/*
=============================================================================

                            OBJECTIVE MARKERS

    Where a match mode points its players (IMatchObjective.Markers: the hill,
    the control points, the flags, Horde's last enemies), drawn over the view
    each frame. One in view is a diamond over its spot, about a story up, seen
    through walls (filled where its spot is in plain sight, hollow where it's
    hidden), with its label and how many tiles away it is under it, and a
    small arrow up or down when it's on another story. One off to the side or
    behind sits on the view's left or right edge, the way to turn, with an
    arrow. Each is in its team's color; game-info objective-markers has the
    colors, font and height. Drawing only: nothing here is sent, saved or
    played back. Off with the objective-markers setting (hud_markers).

=============================================================================
*/

internal sealed class ObjectiveMarkerDrawing(
    VideoManager video,
    UiText ui,
    RenderState render,
    GameSession game,
    GameOptions options,
    AutomapManager automap,
    MapManager map)
{
    /// <summary>The match being played (Gameplay/Deathmatch.cs); unset, none</summary>
    internal Deathmatch? Match { get; set; }

    /// <summary>Lines kept clear at the top of the view (a match's line across it); unset, none</summary>
    internal Func<int>? TopReserved { get; set; }

    /// <summary>How the markers look (game-info objective-markers); unset, the engine's defaults</summary>
    internal Func<ObjectiveMarkersInfo>? Settings { get; set; }

    /// <summary>How the markers look this frame: read afresh by <see cref="Current"/>, which each drawing starts with</summary>
    internal ObjectiveMarkersInfo Look { get; private set; } = DefaultLook;

    static readonly ObjectiveMarkersInfo DefaultLook = new();

    /// <summary>How high up a marker floats over its spot by default (texels: a story, and a little more, over a flag)</summary>
    internal const double MarkerZ = GameConstants.TextureSize + 8;

    const long Tile = MapConstants.TileGlobal;
    const int Radius = 3;           // the diamond's, in layout pixels (7 across)

    internal enum Side { Left = -1, InView = 0, Right = 1 }

    /// <summary>
    /// Where a marker goes on the view, in view pixels: in view at its spot, or on the side to
    /// turn to (straight behind: the right) at the view's middle row
    /// </summary>
    internal static (Side Side, int X, int Y) Place(RenderState render, long gx, long gy, double z)
    {
        if (ViewProjection.Project(render, gx, gy, z, out int sx, out int sy, out _, out _) && sx >= 0 && sx < render.ViewWidth)
            return (Side.InView, sx, Math.Clamp(sy, 0, Math.Max(0, render.ViewHeight - 1)));
        var (_, ny) = ViewProjection.ToView(render, gx, gy);
        return (ny < 0 ? Side.Left : Side.Right, ny < 0 ? 0 : render.ViewWidth - 1, render.ViewHeight / 2);
    }

    /// <summary>
    /// How high to project a marker on a story (1 the ground's), in texels up from the view's
    /// floor (<paramref name="zbase"/>: RenderState.ZBase): <paramref name="height"/> over that
    /// story's floor
    /// </summary>
    internal static double MarkerHeight(int story, int zbase, int height) =>
        (story - 1) * GameConstants.TextureSize - zbase + height;

    /// <summary>Whether a marker's story is over the view's (1), under it (-1), or the same (0)</summary>
    internal static int StoryHint(int story, int zbase) => Math.Sign(story - MapManager.StoryAtHeight(zbase));

    /// <summary>How many tiles away a spot is from the view, rounded</summary>
    internal static int TilesAway(RenderState render, long gx, long gy)
    {
        double dx = gx - render.ViewX, dy = gy - render.ViewY;
        return (int)Math.Round(Math.Sqrt(dx * dx + dy * dy) / Tile);
    }

    /// <summary>Whether the view stands on the spot (within a tile either way), where a marker would only get in the way</summary>
    internal static bool Underfoot(RenderState render, long gx, long gy) =>
        Math.Abs(gx - render.ViewX) < Tile && Math.Abs(gy - render.ViewY) < Tile;

    /// <summary>The level's own objectives' markers (game-info objectives with marker: Gameplay/Objectives.cs); unset, none</summary>
    internal Func<IEnumerable<ObjectiveMarker>>? LevelMarkers { get; set; }

    /// <summary>The scripts' markers (game.marker: Scripting/GameScripts.Markers.cs); unset, none</summary>
    internal Func<IEnumerable<ObjectiveMarker>>? ScriptMarkers { get; set; }

    /// <summary>
    /// The markers to show now: a match mode's for the player being watched (this machine's, or
    /// the one the camera follows), the level's objectives', and the scripts'. None with the
    /// setting off, or while a match mode keeps the player waiting. The automap draws them too
    /// (Ui/AutomapDrawing.cs).
    /// </summary>
    internal IReadOnlyList<ObjectiveMarker> Current()
    {
        Look = Settings?.Invoke() ?? DefaultLook;
        if (!options.ObjectiveMarkers)
            return [];
        var markers = new List<ObjectiveMarker>();
        if (game.GameMode is not (GameMode.Single or GameMode.Coop) && Match?.Objective is { } objective)
        {
            if (objective.IsWaiting(game.LocalPlayer))
                return [];
            var viewer = game.Camera.Target is PlayerPawn watched ? watched.State : game.LocalPlayer;
            markers.AddRange(objective.Markers(viewer));
        }
        markers.AddRange(LevelMarkers?.Invoke() ?? []);
        markers.AddRange(ScriptMarkers?.Invoke() ?? []);
        return markers;
    }

    /// <summary>The story a marker is on: its own, else the floor of its tile (where a thing there would stand)</summary>
    internal int StoryOf(ObjectiveMarker marker)
    {
        if (marker.Story > 0)
            return marker.Story;
        int tx = (int)(marker.X / Tile), ty = (int)(marker.Y / Tile);
        return map.InMap(tx, ty) ? map.ThingStory(tx, ty) : 1;
    }

    // Whether a marker's tile could be seen this frame (spotvis): in plain sight, not behind a wall
    bool InSight(ObjectiveMarker marker)
    {
        int tx = (int)(marker.X / Tile), ty = (int)(marker.Y / Tile);
        return tx >= 0 && ty >= 0 && tx < map.Spotvis.GetLength(0) && ty < map.Spotvis.GetLength(1) && map.Spotvis[tx, ty];
    }

    /// <summary>The markers over the view (not with the automap open: it has its own)</summary>
    internal void Draw()
    {
        if (automap.IsOpen || render.ViewWidth <= 0 || render.ViewHeight <= 0)
            return;
        var markers = Current();
        if (markers.Count == 0)
            return;

        var look = Look;
        int line = ui.FindFont(look.Font)?.Height is > 0 and var h ? h + 1 : 8;
        int edgeHeight = (look.ShowDistance ? 2 : 1) * line + 2;

        // The view, in the 320x200 layout, under the match's line
        int left = video.ToLayoutX(render.ViewScreenX, roundUp: true);
        int right = video.ToLayoutX(render.ViewScreenX + render.ViewWidth - 1);
        int top = video.ToLayoutY(render.ViewScreenY, roundUp: true) + (TopReserved?.Invoke() ?? 0);
        int bottom = video.ToLayoutY(render.ViewScreenY + render.ViewHeight - 1);
        if (right - left < 40 || bottom - top < 3 * edgeHeight)
            return;

        var atLeft = new List<(ObjectiveMarker Marker, int Distance)>();
        var atRight = new List<(ObjectiveMarker Marker, int Distance)>();
        foreach (var marker in markers)
        {
            if (Underfoot(render, marker.X, marker.Y))
                continue;
            int distance = TilesAway(render, marker.X, marker.Y);
            int story = StoryOf(marker);
            var (side, sx, sy) = Place(render, marker.X, marker.Y, MarkerHeight(story, render.ZBase, look.Height));
            switch (side)
            {
                case Side.InView:
                    int cx = Math.Clamp(video.ToLayoutX(render.ViewScreenX + sx), left + Radius + 1, right - Radius - 7);
                    int cy = Math.Clamp(video.ToLayoutY(render.ViewScreenY + sy), top + Radius + 1, bottom - Radius - 2 - edgeHeight);
                    DrawInView(marker, distance, cx, cy, left, right, line, InSight(marker), StoryHint(story, render.ZBase));
                    break;
                case Side.Left:
                    atLeft.Add((marker, distance));
                    break;
                default:
                    atRight.Add((marker, distance));
                    break;
            }
        }

        DrawEdge(atLeft, Side.Left, left, right, top, bottom, line, edgeHeight);
        DrawEdge(atRight, Side.Right, left, right, top, bottom, line, edgeHeight);
    }

    // A marker in view: the diamond (hollow when its spot's hidden), an arrow beside it up or down
    // to another story, and under it its label and distance, kept inside the view
    void DrawInView(ObjectiveMarker marker, int distance, int cx, int cy, int left, int right, int line, bool inSight, int storyHint)
    {
        var look = Look;
        int r = (marker.Style & MarkerStyle.Quiet) != 0 ? Radius - 1 : Radius;
        if (inSight)
        {
            Diamond(cx, cy, r + 1, OutlineColor(marker));
            Diamond(cx, cy, r, FillColor(marker));
        }
        else
        {
            HollowDiamond(cx, cy, r + 1, OutlineColor(marker));
            HollowDiamond(cx, cy, r, FillColor(marker));
        }
        if (storyHint != 0)
        {
            Chevron(cx + r + 4, cy, storyHint, OutlineColor(marker));
        }

        int y = cy + r + 3;
        Centered(marker.Label, cx, y, left, right, LabelColor(marker));
        if (look.ShowDistance)
            Centered(distance.ToString(), cx, y + line, left, right, look.DistanceColor);
    }

    // The markers off one side: stacked down that edge around the middle, each an arrow the way
    // to turn with its label and distance beside it
    void DrawEdge(List<(ObjectiveMarker Marker, int Distance)> markers, Side side, int left, int right, int top, int bottom, int line, int edgeHeight)
    {
        if (markers.Count == 0)
            return;
        var look = Look;
        int y = Math.Clamp((top + bottom) / 2 - markers.Count * edgeHeight / 2, top, Math.Max(top, bottom - markers.Count * edgeHeight));
        foreach (var (marker, distance) in markers)
        {
            if (y + edgeHeight > bottom + 1)
                break;      // no more room down this edge
            // the outline: one pixel round the arrow, its tip one further out
            int tip = side == Side.Left ? left + 1 : right - 1, dir = (int)side;
            Arrow(tip + dir, y + 4, 6, dir, OutlineColor(marker));
            Arrow(tip, y + 4, 4, dir, FillColor(marker));

            int textLeft = left + 8, textRight = right - 8;
            Beside(marker.Label, side, textLeft, textRight, y, LabelColor(marker));
            if (look.ShowDistance)
                Beside(distance.ToString(), side, textLeft, textRight, y + line, look.DistanceColor);
            y += edgeHeight;
        }
    }

    // A diamond r pixels out from its middle each way, a row at a time
    void Diamond(int cx, int cy, int r, string color)
    {
        for (int i = -r; i <= r; i++)
        {
            int w = r - Math.Abs(i);
            video.Bar(cx - w, cy + i, 2 * w + 1, 1, color);
        }
    }

    // Just a diamond's edge, a pixel wide
    void HollowDiamond(int cx, int cy, int r, string color)
    {
        for (int i = -r; i <= r; i++)
        {
            int w = r - Math.Abs(i);
            video.Bar(cx - w, cy + i, 1, 1, color);
            if (w > 0)
                video.Bar(cx + w, cy + i, 1, 1, color);
        }
    }

    // A small arrowhead pointing up (dir 1) or down (-1), 5 across and 3 high, its middle at (cx, cy)
    void Chevron(int cx, int cy, int dir, string color)
    {
        for (int row = 0; row < 3; row++)
        {
            int w = row;        // from the point out: up, the point's the top row; down, the bottom
            video.Bar(cx - w, cy + dir * (row - 1), 2 * w + 1, 1, color);
        }
    }

    // A triangle pointing left (dir -1) or right (1), its tip at (tipx, cy), n pixels long and 2n-1 high
    void Arrow(int tipx, int cy, int n, int dir, string color)
    {
        for (int i = -(n - 1); i <= n - 1; i++)
        {
            int length = n - Math.Abs(i);
            int x0 = dir < 0 ? tipx + Math.Abs(i) : tipx - (n - 1);
            video.Bar(x0, cy + i, length, 1, color);
        }
    }

    void Centered(string text, int cx, int y, int left, int right, string color)
    {
        if (text.Length == 0)
            return;
        int width = ui.TextWidth(text, Look.Font);
        int x = Math.Clamp(cx - width / 2, left + 1, Math.Max(left + 1, right - width));
        ui.TextAt(x, y, Style(color)).Print(text);
    }

    void Beside(string text, Side side, int textLeft, int textRight, int y, string color)
    {
        if (text.Length == 0)
            return;
        int x = side == Side.Left ? textLeft : textRight - ui.TextWidth(text, Look.Font);
        ui.TextAt(x, y, Style(color)).Print(text);
    }

    TextStyle Style(string color) => new TextStyle(Look.Font, color).WithShadow();

    /// <summary>A team's marker color: red, blue, or nobody's</summary>
    internal string FillColor(int team) => FillColor(Look, team);

    internal static string FillColor(ObjectiveMarkersInfo look, int team) => team switch { 0 => look.Red, 1 => look.Blue, _ => look.Neutral };

    /// <summary>A team's label color</summary>
    internal string LabelColor(int team) => team switch { 0 => Look.RedLabel, 1 => Look.BlueLabel, _ => Look.NeutralLabel };

    /// <summary>A marker's color: its own (a script's, a level objective's), else its team's</summary>
    internal string FillColor(ObjectiveMarker marker) => marker.Color ?? FillColor(marker.Team);

    /// <summary>A marker's label color: its own color, else its team's label color</summary>
    internal string LabelColor(ObjectiveMarker marker) => marker.Color ?? LabelColor(marker.Team);

    /// <summary>A marker's ring: the outline color, and the flash color every other FlashTics while it flashes (<paramref name="time"/>: GameState.TimeCount)</summary>
    internal static string OutlineColor(ObjectiveMarkersInfo look, ObjectiveMarker marker, int time) =>
        (marker.Style & MarkerStyle.Pulse) != 0 && time / Math.Max(1, look.FlashTics) % 2 == 0 ? look.Flash : look.Outline;

    internal string OutlineColor(ObjectiveMarker marker) => OutlineColor(Look, marker, game.GameState.TimeCount);
}
