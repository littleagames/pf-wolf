using System.Reflection;
using CommandLine;
using PFWolf.Configuration;
using PFWolf.Definitions;

namespace PFWolf.DocGen;

/// <summary>
/// The command-line options as the documentation shows them: each a field of one record, from
/// GameParams' [Option] and [Value] attributes, as the game's own parser reads them
/// </summary>
internal static class CommandLineSchema
{
    internal static FieldSchema Build() => FieldSchema.Record("command-line",
        "Start PFWolf as `PFWolf.exe [options] [mods...]`, for example `PFWolf.exe --game spear --warp MAP02 --skill 4`, or "
        + "`PFWolf.exe --nowait --cheats --warp MAP01 --exec \"god; give all\"` for a scripted run. Mods dropped on the exe arrive as mods. "
        + "--help lists the options.",
        [.. Groups.Select(group => FieldSchema.Group(group.Title, null,
            [.. Options().Where(option => group.Options.Contains(option.Key)).Select(option => FieldSchema.Callable(option.Key, Signature(option.Key, option.Type), option.Help))]))]);

    /// <summary>The options by what they're for, as the page groups them (CommandLineOptionsTests: each option in one)</summary>
    internal static IReadOnlyList<(string Title, string[] Options)> Groups { get; } =
    [
        ("The game and mods", ["--game", "--file", "mods"]),
        ("Starting play", ["--warp", "--skill", "--start", "--nowait", "--exec", "--cheats"]),
        ("Playing with others", ["--host", "--join", "--port", "--name"]),
        ("Video", ["--fullscreen", "--windowed", "--res", "--scale"]),
        ("Folders", ["--configdir", "--savedir", "--demodir"]),
        ("Making a raw data mod", ["--make-mod", "--base", "--out", "--title", "--palette", "--overwrite"]),
        ("Other", ["--console"]),
    ];

    /// <summary>Each option (--game) and the bare mods, in the order GameParams has them</summary>
    internal static IEnumerable<(string Key, FieldType Type, string Help)> Options()
    {
        foreach (var property in typeof(GameParams).GetProperties())
        {
            if (property.GetCustomAttribute<OptionAttribute>() is { } option)
                yield return ($"--{option.LongName}", TypeOf(property.PropertyType), option.HelpText);
            else if (property.GetCustomAttribute<ValueAttribute>() is { } value)
                yield return (value.MetaName, TypeOf(property.PropertyType), value.HelpText);
        }
    }

    /// <summary>How it's written: a switch on its own (--cheats), the rest with a value (--skill &lt;n&gt;), the bare mods as paths</summary>
    internal static string Signature(string key, FieldType type) => (key.StartsWith("--", StringComparison.Ordinal), type) switch
    {
        (false, _) => $"<{key}...>",
        (true, FieldType.Boolean) => key,
        (true, FieldType.Integer) => $"{key} <n>",
        (true, FieldType.List) => $"{key} <path>...",
        _ => $"{key} <text>",
    };

    // A switch (bool) takes no value; the rest one each (a list: one or more)
    static FieldType TypeOf(Type type) =>
        type == typeof(bool) ? FieldType.Boolean
        : Nullable.GetUnderlyingType(type) == typeof(int) || type == typeof(int) ? FieldType.Integer
        : type != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(type) ? FieldType.List
        : FieldType.Text;
}
