namespace PFWolf.Gameplay;

/// <summary>How an objective marker is drawn, besides its team's color</summary>
[Flags]
internal enum MarkerStyle : byte
{
    None = 0,
    Pulse = 1,      // flashing: it's just moved, or wants seeing to (a dropped flag)
    Quiet = 2,      // smaller: somewhere to go back to rather than something to take
}

/// <summary>
/// A spot a match mode points its players to (IMatchObjective.Markers): drawn over the view, seen
/// through walls, and at the view's edge when it's off to the side or behind (Ui/ObjectiveMarkerDrawing.cs).
/// </summary>
/// <param name="X">Where it is across, in global units (a carried flag follows its carrier)</param>
/// <param name="Y">Where it is down, in global units</param>
/// <param name="Label">A word or two under it: "Hill", "A 40%", "Flag"</param>
/// <param name="Team">The team whose color it's in: 0 red, 1 blue, -1 nobody's</param>
/// <param name="Zone">How many tiles out from its middle the zone it stands for reaches (1: a 3x3 square, outlined on the automap); 0, just a spot</param>
/// <param name="Story">The story it's on (1 the ground's), for an actor (a flag's carrier, an enemy); 0, the floor of its tile</param>
internal readonly record struct ObjectiveMarker(long X, long Y, string Label, int Team, MarkerStyle Style = MarkerStyle.None, int Zone = 0, int Story = 0)
{
    /// <summary>A marker in the middle of a tile</summary>
    internal static ObjectiveMarker OnTile((int x, int y) tile, string label, int team, MarkerStyle style = MarkerStyle.None, int zone = 0) =>
        new(tile.x * (long)MapConstants.TileGlobal + (long)MapConstants.TileGlobal / 2,
            tile.y * (long)MapConstants.TileGlobal + (long)MapConstants.TileGlobal / 2, label, team, style, zone);
}
