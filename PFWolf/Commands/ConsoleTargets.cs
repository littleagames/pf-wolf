using PFWolf.Constants;
using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Commands;

/// <summary>
/// What console commands act on and how they describe it: a tile (named, or the one the player
/// faces), the enemies chosen by class (or the nearest), a wall sprite, a patch and a light zone
/// </summary>
internal sealed class ConsoleTargets(
    MapManager map,
    Lighting lighting,
    GameSession session)
{
    // Every living enemy of a class, or (no class) the nearest living one
    internal List<Entities.Actors.Monster> ChosenActors(string? className)
    {
        var living = map.GetActors().OfType<Entities.Actors.Monster>()
            .Where(m => !m.IsRemoved && m.RuntimeFlags.HasFlag(ObjFlags.Shootable));
        var chosen = className != null
            ? living.Where(m => m.Name.Equals(className, StringComparison.OrdinalIgnoreCase)).ToList()
            : living.OrderBy(m => Math.Max(Math.Abs(m.X - session.Player.X), Math.Abs(m.Y - session.Player.Y))).Take(1).ToList();
        return chosen.Count > 0 ? chosen
            : throw new ArgumentException(className != null ? $"no living {className} on the level" : "no living enemy on the level");
    }

    /// <summary>"  panel ..." for a wall sprite, else nothing.</summary>
    internal string WallSpriteDescription(Entities.Actors.Actor actor)
    {
        if (!WallSprites.IsWallSprite(actor))
            return "";
        if (WallSprites.GetWallSpriteSpan(actor) is not { } span)
            return "  panel (off its tile)";

        static string Point(double x, double y) =>
            $"{x / MapConstants.TileGlobal:0.##},{y / MapConstants.TileGlobal:0.##}";
        return $"  panel {WallSprites.WallSpriteAxis(actor.Dir)} facing {actor.Dir}, {Point(span.X1, span.Y1)} to {Point(span.X2, span.Y2)}";
    }

    /// <summary>
    /// The tile named by args[index] and args[index + 1], or when they aren't given, the tile in
    /// front of the player, as PressUse picks it.
    /// </summary>
    internal (int x, int y) TileArg(string[] args, int index)
    {
        if (args.Length >= index + 2)
            return (ParseInt(args[index], 0, map.mapwidth - 1), ParseInt(args[index + 1], 0, map.mapheight - 1));

        int x = session.Player.TileX, y = session.Player.TileY;
        switch (PlayerMovement.FacingDir(session.Player.Angle))
        {
            case ControlDir.East: x++; break;
            case ControlDir.North: y--; break;
            case ControlDir.West: x--; break;
            default: y++; break;
        }
        return (x, y);
    }

    // A patch id as the console shows it: its texture and faces, or that mapdefs doesn't list it
    internal string DescribePatch(int patch)
    {
        if (patch == 0)
            return "no patch";
        if (!map.GetMapData().Patches.TryGetValue(patch, out var def))
            return $"patch {patch} (not in the mapdefs patches)";
        var sides = def.Sides.Count == 0 ? "all faces" : string.Join("/", def.Sides);
        return $"patch {patch} ({def.Texture}, {sides}{(def.Switch != null ? ", switch" : "")})";
    }

    internal string DescribeZone(int zone)
    {
        if (zone == 0)
            return $"the level's light, {lighting.LevelShading?.Light ?? 255}";
        if (!lighting.LevelZones.TryGetValue(zone, out var z))
            return "not in the map's zones: the level's light";

        var text = $"light {z.Light}";
        if (z.FadeLeft > 0)
            text += $" (fading, now {z.BaseLight}, {z.FadeLeft} tics to go)";
        if (z.Color != null)
            text += $", tinted {z.Color}";
        if (z.Effect != ZoneEffect.None)
            text += $", {z.Effect.ToString().ToLowerInvariant()} to {z.Low} every {z.Tics} tics"
                + (z.Effect == ZoneEffect.Strobe ? $" ({z.BrightTics} bright)" : "");
        return text;
    }
}
