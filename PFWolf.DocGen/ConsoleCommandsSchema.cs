using PFWolf.Definitions;
using PFWolf.Managers;

namespace PFWolf.DocGen;

/// <summary>
/// The console commands as the documentation shows them: each a field of one record, its usage,
/// help, aliases and when it can run, straight from the engine's registrations (GameHost.ConsoleCommandList)
/// </summary>
internal static class ConsoleCommandsSchema
{
    internal static FieldSchema Build() => FieldSchema.Record("console",
        "The console opens with the ` key (left of 1). A line can hold several commands separated by ;, and an argument with spaces "
        + "goes in \"double quotes\". The same commands run from autoexec.cfg in the config folder at startup, from exec, from key binds "
        + "(bind), and from the --exec command line option. A cheat runs only once cheats are on (--cheats, or Shift+Alt+Backspace), "
        + "never in a game with others. Arguments in [brackets] can be left out.",
        [.. GameHost.ConsoleCommandList().Select(Field)]);

    internal static FieldSchema Field(ConsoleCommand command) =>
        FieldSchema.Value(command.Name, FieldType.Text, Describe(command));

    internal static string Describe(ConsoleCommand command)
    {
        var notes = new List<string>();
        if (command.Aliases is { Length: > 0 } aliases)
            notes.Add($"Also {string.Join(", ", aliases.Select(alias => $"`{alias}`"))}.");
        if (command.Flags.HasFlag(ConsoleCommandFlags.Cheat))
            notes.Add("A cheat.");
        if (command.Flags.HasFlag(ConsoleCommandFlags.RequiresLevel))
            notes.Add("Only in a level.");
        if (command.Flags.HasFlag(ConsoleCommandFlags.SinglePlayer) && !command.Flags.HasFlag(ConsoleCommandFlags.Cheat))
            notes.Add("Not in a game with others.");
        return string.Join(" ", [$"`{command.Usage}`: {command.Help.TrimEnd('.')}.", .. notes]);
    }
}
