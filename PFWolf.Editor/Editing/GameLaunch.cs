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

    /// <summary>
    /// The game's command line to try a switch's puzzle: as Play's, with cheats on, and once the
    /// level's up the player put in front of the switch, facing it (tp), and the puzzle opened
    /// (the puzzle command, with its words), so it can be tried at once and then the switch used
    /// for real
    /// </summary>
    /// <param name="stand">Where to stand, and the way to face (0 east, 90 north...); null to stay at the player start</param>
    public static List<string> PuzzleArguments(string gamePackId, string mapName, int? skill, (int X, int Y, int Angle)? stand,
        string puzzle, IEnumerable<string> puzzleArgs, IEnumerable<string> mods)
    {
        var arguments = Arguments(gamePackId, mapName, skill, null, mods);
        var commands = new List<string> { "wait 10" };
        if (stand is { } at)
            commands.Add(string.Create(CultureInfo.InvariantCulture, $"tp {at.X} {at.Y} {at.Angle}"));
        commands.Add(string.Join(' ', new[] { "puzzle", puzzle }.Concat(puzzleArgs.Select(arg => arg.Replace(' ', '_')))));
        arguments.AddRange(["--cheats", "--exec", string.Join("; ", commands)]);
        return arguments;
    }

    /// <summary>
    /// The game's command line to watch a demo: past the title and menus (--nowait) to the demo
    /// file (playdemo), with the editor's mods; back to the title when it ends
    /// </summary>
    public static List<string> DemoArguments(string gamePackId, string demoPath, IEnumerable<string> mods)
    {
        var arguments = new List<string> { "--game", gamePackId, "--nowait", "--exec", $"playdemo \"{demoPath}\"" };
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
