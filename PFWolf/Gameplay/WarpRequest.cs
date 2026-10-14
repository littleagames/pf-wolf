using System.Globalization;
using PFWolf.Assets;
using PFWolf.Configuration;

namespace PFWolf.Gameplay;

/*
=============================================================================

                        WARP FROM THE COMMAND LINE

    --warp MAP01 starts a new game on that level as the game comes up, past
    the title and menus: what the editor's Play does. --skill picks the skill
    and --start a tile to begin on in place of the level's player start.

    This holds what was asked for until it's used; Program starts the game
    (RunPendingWarp) and puts the player on the tile (ApplyPendingWarpStart).

=============================================================================
*/

internal sealed class WarpRequest
{
    // The level --warp asked for, until the demo loop starts it
    string? pendingWarp;
    int? pendingWarpSkill;

    // --start: where the player begins once the level has loaded (its angle in degrees, if given)
    (int X, int Y, int? Angle)? pendingWarpStart;

    internal void SetParams(GameParams gameParams)
    {
        if (string.IsNullOrWhiteSpace(gameParams.Warp))
            return;

        pendingWarp = gameParams.Warp.Trim();
        pendingWarpSkill = gameParams.Skill;

        if (string.IsNullOrWhiteSpace(gameParams.Start))
            return;
        var parts = gameParams.Start.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length is 2 or 3
            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
        {
            int? angle = parts.Length == 3 && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) ? a : null;
            pendingWarpStart = (x, y, angle);
        }
        else
            Console.WriteLine($"--start '{gameParams.Start}' isn't x,y or x,y,angle; starting on the level's player start");
    }

    /// <summary>
    /// The new game --warp asked for (once): its level, skill and episode, or null when there's
    /// none to start. A level game-info doesn't list, or that nothing supplies
    /// (<paramref name="levelExists"/>), is reported and the menus come up.
    /// </summary>
    internal (string Map, short Difficulty, EpisodeInfo Episode)? TakeWarp(GameInfoAsset gameInfo, Func<string, bool> levelExists)
    {
        if (pendingWarp is not { } name)
            return null;
        pendingWarp = null;

        var map = gameInfo.Maps.Keys.FirstOrDefault(key => key.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (map == null || !levelExists(map))
        {
            Console.WriteLine(map == null
                ? $"--warp {name}: game-info doesn't list a level of that name"
                : $"--warp {name}: no level file supplies it");
            pendingWarpStart = null;
            return null;
        }

        // 1 is the easiest; left out, the third, as Wolf3D's Bring 'em on
        int skills = Math.Max(gameInfo.Skills.Count, 1);
        var difficulty = (short)(pendingWarpSkill is { } skill ? Math.Clamp(skill - 1, 0, skills - 1) : Math.Min(2, skills - 1));

        // The episode the level starts, else the first: only where a new game starts matters here
        var episode = gameInfo.Episodes.Values.FirstOrDefault(ep => map.Equals(ep.StartMap, StringComparison.OrdinalIgnoreCase))
                      ?? gameInfo.Episodes.Values.FirstOrDefault()
                      ?? new EpisodeInfo { StartMap = map, Name = "" };
        return (map, difficulty, episode);
    }

    /// <summary>The tile --start asked for (once), for after the warped-to level has loaded</summary>
    internal (int X, int Y, int? Angle)? TakeStart()
    {
        var start = pendingWarpStart;
        pendingWarpStart = null;
        return start;
    }
}
