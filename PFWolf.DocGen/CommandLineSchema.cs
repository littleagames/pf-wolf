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
        + "A true/false option is a switch, given on its own (--cheats), the rest each take a value (--game spear). --help lists them.",
        [.. Options().Select(option => FieldSchema.Value(option.Key, option.Type, option.Help))]);

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

    // A switch (bool) takes no value; the rest one each (a list: one or more)
    static FieldType TypeOf(Type type) =>
        type == typeof(bool) ? FieldType.Boolean
        : Nullable.GetUnderlyingType(type) == typeof(int) || type == typeof(int) ? FieldType.Integer
        : type != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(type) ? FieldType.List
        : FieldType.Text;
}
