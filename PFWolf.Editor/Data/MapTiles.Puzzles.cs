using System.Text;
using System.Text.RegularExpressions;
using PFWolf.Assets;

namespace PFWolf.Editor.Data;

/// <summary>A switch's puzzle on a tile: the script's puzzle name, its words (as A_Puzzle's: "key=value" or in order), and what it's on ("wall 64")</summary>
public sealed record TilePuzzle(string Name, IReadOnlyList<string> Args, string Source);

// The puzzles a level's switches open (a script's game.puzzle: the game's Gameplay/Puzzles.cs)
public sealed partial class MapTiles
{
    /// <summary>
    /// The puzzle a switch on the tile opens: a wall's switch, else its patch's; its `puzzle`
    /// field (with its puzzle-args), else an A_Puzzle in its actions. Null when there's none.
    /// </summary>
    public TilePuzzle? PuzzleAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height || KindAt(x, y) != TileKind.Wall)
            return null;
        if (Wall(x, y)?.Switch is { } wallSwitch && FromSwitch(wallSwitch, $"wall {this[0, x, y]}") is { } onWall)
            return onWall;
        if (PatchDef(x, y)?.Switch is { } patchSwitch && FromSwitch(patchSwitch, $"patch {Patch(x, y)}") is { } onPatch)
            return onPatch;
        return null;
    }

    private static TilePuzzle? FromSwitch(MapSwitchTranslation sw, string source)
    {
        if (sw.Puzzle.Trim() is { Length: > 0 } name)
            return new TilePuzzle(name, [.. sw.PuzzleArgs.Select(arg => $"{arg.Key}={arg.Value}")], source);
        foreach (var action in sw.Actions)
        {
            if (PuzzleCall(action) is { } call)
                return new TilePuzzle(call.Name, call.Args, source);
        }
        return null;
    }

    /// <summary>An A_Puzzle("name", "word"...) call's puzzle and words; null for any other action</summary>
    public static (string Name, IReadOnlyList<string> Args)? PuzzleCall(string action)
    {
        var match = Regex.Match(action.Trim(), @"^A_Puzzle\s*\((.*)\)\s*$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
            return null;
        var words = SplitArguments(match.Groups[1].Value);
        return words.Count > 0 && words[0].Length > 0 ? (words[0], words.Skip(1).ToList()) : null;
    }

    // An action call's arguments: split at commas outside quotes, each trimmed and unquoted
    private static List<string> SplitArguments(string text)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        bool quoted = false;
        foreach (char c in text)
        {
            if (c == '"')
                quoted = !quoted;
            else if (c == ',' && !quoted)
            {
                words.Add(word.ToString().Trim());
                word.Clear();
            }
            else
                word.Append(c);
        }
        if (word.ToString().Trim() is { Length: > 0 } last || words.Count > 0)
            words.Add(word.ToString().Trim());
        return words;
    }

    /// <summary>
    /// Open floor beside a wall to use it from, and the way to face it (the game's angles: 0
    /// east, 90 north, 180 west, 270 south): south of it first, then north, east and west. Null
    /// when it's walled in.
    /// </summary>
    public (int X, int Y, int Angle)? StandBeside(int x, int y)
    {
        foreach (var (dx, dy, angle) in new[] { (0, 1, 90), (0, -1, 270), (1, 0, 180), (-1, 0, 0) })
        {
            int sx = x + dx, sy = y + dy;
            if (sx >= 0 && sy >= 0 && sx < Width && sy < Height && KindAt(sx, sy) == TileKind.Floor)
                return (sx, sy, angle);
        }
        return null;
    }
}
