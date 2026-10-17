using PFWolf.Configuration;

namespace PFWolf.Commands;

/// <summary>What the console commands share: registering them, completing and reading their arguments</summary>
internal static class ConsoleArgs
{
    internal const ConsoleCommandFlags Cheat = ConsoleCommandFlags.Cheat;
    internal const ConsoleCommandFlags InLevel = ConsoleCommandFlags.RequiresLevel;
    internal const ConsoleCommandFlags Solo = ConsoleCommandFlags.SinglePlayer;

    internal static void Register(this ConsoleManager console, string name, string help, string usage, Action<string[]> run,
        ConsoleCommandFlags flags = ConsoleCommandFlags.None, string[]? aliases = null, ConsoleCompleter? complete = null) =>
        console.Register(new ConsoleCommand(name, help, usage, run, flags, aliases, complete));

    /*
    =============================================================================

                                TAB COMPLETION

    =============================================================================
    */

    /// <summary>A completer offering a fixed set of values for the first argument.</summary>
    internal static ConsoleCompleter Values(params string[] values) => (_, i) => i == 0 ? values : [];

    /*
    =============================================================================

                                KEY AND BUTTON NAMES

    Binds and controls use SDL's key names ("F5", "Keypad 1", "Left Shift"),
    "Mouse 1", "Wheel Up", "Joy 1" and "Pad A" (see InputCode), matched without
    regard to case, so they read the same in the .cfg files as on screen.

    =============================================================================
    */

    /// <summary>
    /// A key or button by name. Keys are folded as InputManager reports them: right-hand
    /// modifiers into left-hand ones, keypad Enter into Enter. Not the keypad arrows, which
    /// InputManager only folds while Num Lock is off: a keypad key stays itself, and works as
    /// that key when it's pressed with Num Lock on.
    /// </summary>
    internal static InputCode ParseInput(string name)
    {
        if (!InputCode.TryParse(name, out var code))
            throw new ArgumentException($"unknown key or button \"{name}\"");

        return code.Device == InputDevice.Key ? InputCode.FromKey(InputManager.FoldKey(code.Key)) : code;
    }

    /// <summary>Quotes an argument if Tokenize would otherwise split it.</summary>
    internal static string QuoteArg(string arg) =>
        arg.Length == 0 || arg.Any(c => char.IsWhiteSpace(c) || c == ';') ? $"\"{arg}\"" : arg;

    /*
    =============================================================================

                                ARGUMENT PARSING

    Throwing ArgumentException is the way to reject bad input: ConsoleManager
    prints the message and the command does nothing else.

    =============================================================================
    */

    internal static int ParseInt(string arg, int min, int max)
    {
        if (!int.TryParse(arg, out var value) || value < min || value > max)
            throw new ArgumentException($"expected a number from {min} to {max}, got \"{arg}\"");
        return value;
    }

    internal static bool ParseBool(string arg) => arg.ToLowerInvariant() switch
    {
        "1" or "on" or "true" => true,
        "0" or "off" or "false" => false,
        _ => throw new ArgumentException($"expected 0 or 1, got \"{arg}\""),
    };

    /// <summary>Sets a toggle from args[0] if given, otherwise flips it.</summary>
    internal static bool Toggle(string[] args, bool current) => args.Length > 0 ? ParseBool(args[0]) : !current;

    internal static double ParseTiles(string arg)
    {
        if (!double.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tiles)
            || tiles < 0 || tiles > MapManager.MapSize)
            throw new ArgumentException($"expected a number of tiles from 0 to {MapManager.MapSize}, got \"{arg}\"");
        return tiles;
    }

    internal static ScaleFilter ParseFilter(string arg) =>
        Enum.TryParse<ScaleFilter>(arg, ignoreCase: true, out var filter) && Enum.IsDefined(filter)
            ? filter
            : throw new ArgumentException($"expected nearest or linear, got \"{arg}\"");
}
