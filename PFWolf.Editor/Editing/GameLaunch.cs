using System.Diagnostics;
using System.Globalization;

namespace PFWolf.Editor.Editing;

/// <summary>Starting PFWolf on a level: what the editor's Play runs</summary>
public static class GameLaunch
{
    /// <summary>
    /// The game's command line: the game pack, past the title and menus (--nowait) straight to
    /// the level (--warp) on a skill, from a tile when one's given, with the editor's mods
    /// </summary>
    /// <param name="skill">1 for the easiest; null for the game's default</param>
    public static List<string> Arguments(string gamePackId, string mapName, int? skill, (int X, int Y)? start, IEnumerable<string> mods)
    {
        var arguments = new List<string> { "--game", gamePackId, "--nowait", "--warp", mapName.ToUpperInvariant() };
        if (skill is { } chosen)
            arguments.AddRange(["--skill", chosen.ToString(CultureInfo.InvariantCulture)]);
        if (start is { } tile)
            arguments.AddRange(["--start", $"{tile.X.ToString(CultureInfo.InvariantCulture)},{tile.Y.ToString(CultureInfo.InvariantCulture)}"]);
        foreach (var mod in mods)
            arguments.AddRange(["--file", mod]);
        return arguments;
    }

    /// <summary>Starts the game in its own folder, as it expects, and leaves it running</summary>
    public static void Start(string exe, string gameFolder, IEnumerable<string> arguments)
    {
        var start = new ProcessStartInfo(exe) { WorkingDirectory = gameFolder, UseShellExecute = false };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        Process.Start(start)?.Dispose();
    }
}
