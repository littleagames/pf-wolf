using PFWolf.Constants;

namespace PFWolf.Gameplay;

/// <summary>
/// A_Warp's arguments (Gameplay/HubActions.cs WarpAction): the way the player comes out facing
/// (east, north, west, south; none, as they were), the flags "telefrag", "swapcolors", "fizzle"
/// and "monsters", and anything else the sound.
/// </summary>
internal sealed record WarpArgs(string? Facing, string? Sound, bool Telefrag, bool SwapColors, bool Fizzle, bool Monsters)
{
    static readonly string[] Facings = ["east", "north", "west", "south"];
    static readonly string[] Flags = ["telefrag", "swapcolors", "fizzle", "monsters"];

    internal static WarpArgs Parse(IEnumerable<string> args)
    {
        string? facing = null, sound = null;
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in args)
        {
            var a = raw.Trim();
            if (a.Length == 0)
                continue;
            if (Facings.Contains(a, StringComparer.OrdinalIgnoreCase))
                facing ??= a.ToLowerInvariant();
            else if (Flags.Contains(a, StringComparer.OrdinalIgnoreCase))
                flags.Add(a);
            else
                sound ??= a;
        }
        return new WarpArgs(facing, sound, flags.Contains("telefrag"), flags.Contains("swapcolors"), flags.Contains("fizzle"), flags.Contains("monsters"));
    }

    /// <summary>The player's angle coming out (degrees, east 0, counterclockwise); null keeps theirs</summary>
    internal short? Angle => Facing switch
    {
        "east" => 0,
        "north" => 90,
        "west" => 180,
        "south" => 270,
        _ => null,
    };

    /// <summary>An enemy's way coming out</summary>
    internal ObjDir? Dir => Facing switch
    {
        "east" => ObjDir.East,
        "north" => ObjDir.North,
        "west" => ObjDir.West,
        "south" => ObjDir.South,
        _ => null,
    };

    /// <summary>The next tile the way they face (telefrag's second tile); none without a facing</summary>
    internal (int Dx, int Dy) Step => Facing switch
    {
        "east" => (1, 0),
        "north" => (0, -1),
        "west" => (-1, 0),
        "south" => (0, 1),
        _ => (0, 0),
    };

    /// <summary>
    /// The exit among a tag's tiles (row by row): the last that isn't a pad, as EoD kept the
    /// last exit it scanned (warpex/warpey)
    /// </summary>
    internal static (int X, int Y)? ExitOf(IEnumerable<(int X, int Y)> tagged, Func<(int X, int Y), bool> isPad) =>
        tagged.Where(t => !isPad(t)).Select(t => ((int X, int Y)?)t).LastOrDefault();
}
