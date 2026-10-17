using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Commands;

/// <summary>Messages over the view: msg, msg_enabled, msg_clear and msg_styles</summary>
internal sealed class MessageCommands(ConsoleManager console, HudMessageManager messages)
{
    internal void Register()
    {
        console.Register("msg", "Shows a message over the view, in a style from hud-messages.yaml if the first word names one.",
            "msg [style] <text...>", MsgCommand, InLevel, complete: (_, i) => i == 0 ? messages.StyleNames : []);
        console.Register("msg_enabled", "Whether item pickups, locked doors and deaths show messages over the view; default goes back to the game's own choice.",
            "msg_enabled [0|1|default]", MsgEnabledCommand, complete: Values("0", "1", "default"));
        console.Register("msg_clear", "Takes away the messages shown over the view.", "msg_clear", _ => messages.Clear());
        console.Register("msg_styles", "Lists the message styles and where each puts its messages.", "msg_styles", MsgStylesCommand);
    }

    private void MsgCommand(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: msg [style] <text...>");

        // A first word naming a style is the style, as long as there's text after it
        if (args.Length > 1 && messages.StyleExists(args[0]))
            messages.Show(HudMessageKind.Other, string.Join(' ', args[1..]), args[0]);
        else
            messages.Show(HudMessageKind.Other, string.Join(' ', args));
    }

    private void MsgEnabledCommand(string[] args)
    {
        if (args.Length > 0)
        {
            messages.EnabledSetting = args[0].Equals("default", StringComparison.OrdinalIgnoreCase)
                ? null
                : ParseBool(args[0]);
            if (!messages.Enabled)
                messages.Clear();
        }
        console.Print($"msg_enabled is {(messages.Enabled ? 1 : 0)}" +
            (messages.EnabledSetting == null ? " (the game's default)" : ""));
    }

    private void MsgStylesCommand(string[] args)
    {
        foreach (var name in messages.StyleNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            var s = messages.FindStyle(name);
            console.Print($"{s.Name}: {s.Anchor} {s.X:+0;-0;0},{s.Y:+0;-0;0} margin {s.Margin}, {s.Font} in {s.Color}, " +
                $"{s.Duration} tics, {s.MaxLines} line{(s.MaxLines == 1 ? "" : "s")}");
        }
    }
}
