using Lua;
using PFWolf.Gameplay;

namespace PFWolf.Scripting;

/*
=============================================================================

                            SCRIPTS' MARKERS

    game.marker(id, x, y, label[, color]) marks a tile, and game.marker(id,
    actor, label[, color]) an actor, following it until it's removed or dies:
    drawn as an objective marker over the view and on the automap
    (Ui/ObjectiveMarkerDrawing.cs), so a mod can point the player at its own
    goals. The color is "red" or "blue" (a team's look), a color name, #RRGGBB
    or palette index, or none for nobody's look. game.clearmarker(id) takes one
    away, game.clearmarker() all of them.

    A marker lasts for the level it was set on: they aren't saved, so a script
    sets them again in game.onlevel, which a loaded game runs too ("loaded").
    They're drawing only: setting one changes nothing in the game.

=============================================================================
*/

internal sealed partial class GameScripts
{
    // A script's marker: on a tile, or following an actor; on the level it was set on (LevelSerial)
    sealed record ScriptMarker(string Id, int TileX, int TileY, Entities.Actors.Actor? Follow, string Label, int Team, string? Color, int Level);

    // In the order they were set (one set again keeps its place)
    readonly List<ScriptMarker> scriptMarkers = [];

    /// <summary>The scripts' markers on this level: those following an actor that's gone or dead are dropped</summary>
    internal IReadOnlyList<ObjectiveMarker> Markers()
    {
        scriptMarkers.RemoveAll(m => m.Level != map.LevelSerial || m.Follow is { } actor && Gone(actor));
        return scriptMarkers.Select(m => m.Follow is { } actor
            ? ObjectiveMarker.OnActor(actor, m.Label, m.Team, m.Color)
            : ObjectiveMarker.OnTile((m.TileX, m.TileY), m.Label, m.Team, color: m.Color)).ToList();
    }

    // Gone: removed, dead, or off the level's list (an item picked up is taken off it, not marked removed)
    bool Gone(Entities.Actors.Actor actor) =>
        actor.IsRemoved || actor is Entities.Actors.Monster { Hitpoints: <= 0 } || !map.GetActors().Contains(actor);

    /// <summary>A marker's color argument: "red" and "blue" are the teams' looks; any other a color of its own; none, nobody's</summary>
    internal static (int Team, string? Color) MarkerColor(string? color) => color?.Trim().ToLowerInvariant() switch
    {
        null or "" => (-1, null),
        "red" => (0, null),
        "blue" => (1, null),
        _ => (-1, color.Trim()),
    };

    void InstallMarkerFunctions(LuaTable game)
    {
        game["marker"] = new LuaFunction("game.marker", (c, _) =>
        {
            var id = StringArg(c, 0);
            Entities.Actors.Actor? follow = null;
            int x = -1, y = -1, next;
            if (Arg(c, 1).TryRead<LuaActor>(out var who))
            {
                follow = who.Actor;
                next = 2;
            }
            else
            {
                x = (int)Math.Floor(NumberArg(c, 1, -1));
                y = (int)Math.Floor(NumberArg(c, 2, -1));
                if (!map.InMap(x, y))
                    throw new LuaRuntimeException(c.State, (LuaValue)$"game.marker: {x},{y} isn't a tile on the map", 1);
                next = 3;
            }
            var (team, color) = MarkerColor(OptionalString(c, next + 1));
            var marker = new ScriptMarker(id, x, y, follow, OptionalString(c, next) ?? "", team, color, map.LevelSerial);

            int at = scriptMarkers.FindIndex(m => m.Id == id);
            if (at >= 0)
                scriptMarkers[at] = marker;
            else
                scriptMarkers.Add(marker);
            return new(c.Return());
        });
        game["clearmarker"] = new LuaFunction("game.clearmarker", (c, _) =>
        {
            if (OptionalString(c, 0) is { } id)
                scriptMarkers.RemoveAll(m => m.Id == id);
            else
                scriptMarkers.Clear();
            return new(c.Return());
        });
    }
}
