using PFWolf.Configuration;
using PFWolf.Entities.Actors;
using PFWolf.Fonts;

namespace PFWolf.Ui;

/*
=============================================================================

                            OBJECTIVE MARKERS

    Where a match mode points its players (IMatchObjective.Markers: the hill,
    the control points, the flags), drawn over the view each frame. One in
    view is a diamond over its spot, about a story up, seen through walls,
    with its label and how many tiles away it is under it. One off to the side
    or behind sits on the view's left or right edge, the way to turn, with an
    arrow. Each is in its team's color. Drawing only: nothing here is sent,
    saved or played back. Off with the objective-markers setting (hud_markers).

=============================================================================
*/

internal sealed class ObjectiveMarkerDrawing(
    VideoManager video,
    UiText ui,
    RenderState render,
    GameSession game,
    GameOptions options,
    AutomapManager automap)
{
    /// <summary>The match being played (Gameplay/Deathmatch.cs); unset, none</summary>
    internal Deathmatch? Match { get; set; }

    /// <summary>Lines kept clear at the top of the view (a match's line across it); unset, none</summary>
    internal Func<int>? TopReserved { get; set; }

    /// <summary>How high up a marker floats over its spot (texels: a story, and a little more, over a flag)</summary>
    internal const double MarkerZ = GameConstants.TextureSize + 8;

    const long Tile = MapConstants.TileGlobal;
    const int Radius = 3;           // the diamond's, in layout pixels (7 across)
    const int LineHeight = 8;       // SmallFont's lines
    const int EdgeHeight = 2 * LineHeight + 2;  // a marker at the edge: its label and distance

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

    /// <summary>How many tiles away a spot is from the view, rounded</summary>
    internal static int TilesAway(RenderState render, long gx, long gy)
    {
        double dx = gx - render.ViewX, dy = gy - render.ViewY;
        return (int)Math.Round(Math.Sqrt(dx * dx + dy * dy) / Tile);
    }

    /// <summary>Whether the view stands on the spot (within a tile either way), where a marker would only get in the way</summary>
    internal static bool Underfoot(RenderState render, long gx, long gy) =>
        Math.Abs(gx - render.ViewX) < Tile && Math.Abs(gy - render.ViewY) < Tile;

    /// <summary>
    /// The markers to show now, for the player being watched (this machine's, or the one the
    /// camera follows): none with the setting off, outside a match mode, or while the mode keeps
    /// the player waiting. The automap draws them too (Ui/AutomapDrawing.cs).
    /// </summary>
    internal IReadOnlyList<ObjectiveMarker> Current()
    {
        if (!options.ObjectiveMarkers || Match?.Objective is not { } objective
            || game.GameMode is GameMode.Single or GameMode.Coop || objective.IsWaiting(game.LocalPlayer))
            return [];
        var viewer = game.Camera.Target is PlayerPawn watched ? watched.State : game.LocalPlayer;
        return objective.Markers(viewer).ToList();
    }

    /// <summary>The markers over the view (not with the automap open: it has its own)</summary>
    internal void Draw()
    {
        if (automap.IsOpen || render.ViewWidth <= 0 || render.ViewHeight <= 0)
            return;
        var markers = Current();
        if (markers.Count == 0)
            return;

        // The view, in the 320x200 layout, under the match's line
        int left = video.ToLayoutX(render.ViewScreenX, roundUp: true);
        int right = video.ToLayoutX(render.ViewScreenX + render.ViewWidth - 1);
        int top = video.ToLayoutY(render.ViewScreenY, roundUp: true) + (TopReserved?.Invoke() ?? 0);
        int bottom = video.ToLayoutY(render.ViewScreenY + render.ViewHeight - 1);
        if (right - left < 40 || bottom - top < 3 * EdgeHeight)
            return;

        var atLeft = new List<(ObjectiveMarker Marker, int Distance)>();
        var atRight = new List<(ObjectiveMarker Marker, int Distance)>();
        foreach (var marker in markers)
        {
            if (Underfoot(render, marker.X, marker.Y))
                continue;
            int distance = TilesAway(render, marker.X, marker.Y);
            var (side, sx, sy) = Place(render, marker.X, marker.Y, MarkerZ);
            switch (side)
            {
                case Side.InView:
                    int cx = Math.Clamp(video.ToLayoutX(render.ViewScreenX + sx), left + Radius + 1, right - Radius - 1);
                    int cy = Math.Clamp(video.ToLayoutY(render.ViewScreenY + sy), top + Radius + 1, bottom - Radius - 2 - 2 * LineHeight);
                    DrawInView(marker, distance, cx, cy, left, right);
                    break;
                case Side.Left:
                    atLeft.Add((marker, distance));
                    break;
                default:
                    atRight.Add((marker, distance));
                    break;
            }
        }

        DrawEdge(atLeft, Side.Left, left, right, top, bottom);
        DrawEdge(atRight, Side.Right, left, right, top, bottom);
    }

    // A marker in view: the diamond, and under it its label and distance, kept inside the view
    void DrawInView(ObjectiveMarker marker, int distance, int cx, int cy, int left, int right)
    {
        int r = (marker.Style & MarkerStyle.Quiet) != 0 ? Radius - 1 : Radius;
        Diamond(cx, cy, r + 1, OutlineColor(marker));
        Diamond(cx, cy, r, FillColor(marker.Team));
        int y = cy + r + 3;
        Centered(marker.Label, cx, y, left, right, LabelColor(marker.Team));
        Centered(distance.ToString(), cx, y + LineHeight, left, right, DistanceColor);
    }

    // The markers off one side: stacked down that edge around the middle, each an arrow the way
    // to turn with its label and distance beside it
    void DrawEdge(List<(ObjectiveMarker Marker, int Distance)> markers, Side side, int left, int right, int top, int bottom)
    {
        if (markers.Count == 0)
            return;
        int y = Math.Clamp((top + bottom) / 2 - markers.Count * EdgeHeight / 2, top, Math.Max(top, bottom - markers.Count * EdgeHeight));
        foreach (var (marker, distance) in markers)
        {
            if (y + EdgeHeight > bottom + 1)
                break;      // no more room down this edge
            // the outline: one pixel round the arrow, its tip one further out
            int tip = side == Side.Left ? left + 1 : right - 1, dir = (int)side;
            Arrow(tip + dir, y + 4, 6, dir, OutlineColor(marker));
            Arrow(tip, y + 4, 4, dir, FillColor(marker.Team));

            int textLeft = left + 8, textRight = right - 8;
            Beside(marker.Label, side, textLeft, textRight, y, LabelColor(marker.Team));
            Beside(distance.ToString(), side, textLeft, textRight, y + LineHeight, DistanceColor);
            y += EdgeHeight;
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
        int width = ui.TextWidth(text, UiFonts.SmallFont);
        int x = Math.Clamp(cx - width / 2, left + 1, Math.Max(left + 1, right - width));
        ui.TextAt(x, y, Style(color)).Print(text);
    }

    void Beside(string text, Side side, int textLeft, int textRight, int y, string color)
    {
        int x = side == Side.Left ? textLeft : textRight - ui.TextWidth(text, UiFonts.SmallFont);
        ui.TextAt(x, y, Style(color)).Print(text);
    }

    static TextStyle Style(string color) => new TextStyle(UiFonts.SmallFont, color).WithShadow();

    const string DistanceColor = "#C0C0C0";

    internal static string FillColor(int team) => team switch { 0 => "#FC0000", 1 => "#0000FC", _ => "#A8A8A8" };

    internal static string LabelColor(int team) => team switch { 0 => "#FC5454", 1 => "#5454FC", _ => "#FCFCFC" };

    /// <summary>A marker's outline: white, flashing yellow while it pulses (<paramref name="time"/>: GameState.TimeCount)</summary>
    internal static string OutlineColor(ObjectiveMarker marker, int time) =>
        (marker.Style & MarkerStyle.Pulse) != 0 && time / 8 % 2 == 0 ? "#FCFC54" : "#FCFCFC";

    string OutlineColor(ObjectiveMarker marker) => OutlineColor(marker, game.GameState.TimeCount);
}
