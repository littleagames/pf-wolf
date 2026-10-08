using System.Globalization;
using PFWolf.Assets;
using PFWolf.Configuration;

namespace PFWolf;

/*
=============================================================================

                        WARP FROM THE COMMAND LINE

    --warp MAP01 starts a new game on that level as the game comes up, past
    the title and menus: what the editor's Play does. --skill picks the skill
    and --start a tile to begin on in place of the level's player start.

=============================================================================
*/

internal partial class Program
{
    // The level --warp asked for, until the demo loop starts it
    static string? pendingWarp;
    static int? pendingWarpSkill;

    // --start: where the player begins once the level has loaded (its angle in degrees, if given)
    static (int X, int Y, int? Angle)? pendingWarpStart;

    static void SetWarpParams(GameParams gameParams)
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
    /// Starts the new game --warp asked for (once): true if it did, for the demo loop to play. A
    /// level game-info doesn't list, or that nothing supplies, is reported and the menus come up.
    /// </summary>
    internal static bool RunPendingWarp()
    {
        if (pendingWarp is not { } name)
            return false;
        pendingWarp = null;

        var gameInfo = _gameEngineManager.GetGameInfo();
        var map = gameInfo.Maps.Keys.FirstOrDefault(key => key.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (map == null || !_assetManager.Exists<MapAsset>(map))
        {
            Console.WriteLine(map == null
                ? $"--warp {name}: game-info doesn't list a level of that name"
                : $"--warp {name}: no level file supplies it");
            pendingWarpStart = null;
            return false;
        }

        // 1 is the easiest; left out, the third, as Wolf3D's Bring 'em on
        int skills = Math.Max(gameInfo.Skills.Count, 1);
        var difficulty = (short)(pendingWarpSkill is { } skill ? Math.Clamp(skill - 1, 0, skills - 1) : Math.Min(2, skills - 1));

        // The episode the level starts, else the first: only where a new game starts matters here
        var episode = gameInfo.Episodes.Values.FirstOrDefault(ep => map.Equals(ep.StartMap, StringComparison.OrdinalIgnoreCase))
                      ?? gameInfo.Episodes.Values.FirstOrDefault()
                      ?? new EpisodeInfo { StartMap = map, Name = "" };
        NewGame(difficulty, episode, gameInfo.Maps[map]);
        gamestate.mapon = map;
        Console.WriteLine($"Warping to {map}, skill {difficulty + 1}");
        return true;
    }

    /// <summary>Puts the player where --start said, once, after the warped-to level has loaded</summary>
    static void ApplyPendingWarpStart()
    {
        if (pendingWarpStart is not { } start)
            return;
        pendingWarpStart = null;

        if (TryTeleportPlayer(start.X, start.Y, start.Angle, out var problem))
            Console.WriteLine($"Starting on {start.X},{start.Y}");
        else
            Console.WriteLine($"--start {start.X},{start.Y}: {problem}; starting on the level's player start");
    }
}
