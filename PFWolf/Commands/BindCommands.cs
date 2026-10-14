using PFWolf.Configuration;
using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Commands;

/// <summary>
/// Binds (saved to binds.cfg on exit) and controls (saved to controls.cfg on exit): bind,
/// unbind, unbindall, binds, bindaction, resetcontrols, m_* and joy_*
/// </summary>
internal sealed class BindCommands(ConsoleManager console, ControlBindings controls, ControlSettings settings)
{
    internal void Register()
    {
        //
        // binds (saved to binds.cfg on exit)
        //
        console.Register("bind", "Binds a key, mouse button, wheel turn or controller button to a command run when it's pressed in play, or shows its bind.",
            "bind <key or button> [command]", Cmd_Bind, complete: CompleteBind);
        console.Register("unbind", "Removes a key or button's bind.", "unbind <key or button>", Cmd_Unbind,
            complete: (_, i) => i == 0 ? console.Binds.Keys.Select(input => input.ToString()) : []);
        console.Register("unbindall", "Removes every bind.", "unbindall", _ =>
        {
            console.UnbindAll();
            console.Print("All binds removed");
        });
        console.Register("binds", "Lists the key binds.", "binds", Cmd_Binds);

        //
        // controls (saved to controls.cfg on exit)
        //
        console.Register("bindaction", "Sets a control's keys and buttons: up to two keys, mouse buttons or wheel turns and one controller button. Lists them all with no control.",
            "bindaction [control] [key|\"Mouse 1-5\"|\"Wheel Up\"|\"Pad A\"|\"Joy 1-32\"...|none]", Cmd_BindAction, complete: CompleteBindAction);
        console.Register("resetcontrols", "Puts every control back to its default keys and buttons.", "resetcontrols", _ =>
        {
            controls.SetDefaults();
            console.Print("Every control is back to its default");
        });
        console.Register("joy_deadzone", "How much of a controller stick's travel around the middle is ignored, in percent.",
            "joy_deadzone [0-50]", args => SetOrShow("joy_deadzone", args, ref settings.joydeadzone, 0, 50));
        console.Register("joy_turnspeed", "How fast a controller's stick turns you, pushed all the way.",
            "joy_turnspeed [0-9]", args => SetOrShow("joy_turnspeed", args, ref settings.joyturnspeed, 0, ControlSettings.JOYTURNSPEEDS - 1));
        console.Register("m_look", "Whether moving the mouse up and down looks up and down (1) or walks (0).",
            "m_look [0|1]", args => ToggleOrShow("m_look", args, ref settings.mouselook), complete: Values("0", "1"));
        console.Register("m_invert", "Whether mouse look is upside down: pushing the mouse away looks down.",
            "m_invert [0|1]", args => ToggleOrShow("m_invert", args, ref settings.mouseinvert), complete: Values("0", "1"));
        console.Register("joy_sticks", "Which controller stick turns: the right (modern) or the left, with the right strafing (classic).",
            "joy_sticks [modern|classic]", Cmd_JoySticks, complete: Values("modern", "classic"));
    }

    IEnumerable<string> CompleteBind(string[] args, int index) => index switch
    {
        0 => InputCode.AllNames(),
        1 => console.Commands.Select(c => c.Name),
        _ => [],
    };

    static IEnumerable<string> CompleteBindAction(string[] args, int index) =>
        index == 0 ? ControlAction.All.Select(action => action.Name) : ["none", .. InputCode.AllNames()];

    private void Cmd_Bind(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: bind <key or button> [command]");

        var input = ParseInput(args[0]);

        if (args.Length == 1)
        {
            console.Print(console.Binds.TryGetValue(input, out var bound)
                ? $"\"{input}\" = \"{bound}\""
                : $"\"{input}\" is not bound");
            return;
        }

        // ` always toggles the console and Escape always opens the menu, before binds are seen.
        if (input.Key is ScanCodes.sc_Grave or ScanCodes.sc_Escape)
            throw new ArgumentException($"\"{input}\" is reserved");

        // One argument is the command line as-is (`bind F5 "god; noclip"`); several are
        // rejoined, re-quoting any that need it (`bind F5 give GoldKey`).
        var command = args.Length == 2 ? args[1] : string.Join(' ', args[1..].Select(QuoteArg));

        console.Bind(input, command);
        console.Print($"\"{input}\" = \"{command}\"");
    }

    private void Cmd_Unbind(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: unbind <key or button>");

        var input = ParseInput(args[0]);
        console.Print(console.Unbind(input)
            ? $"\"{input}\" unbound"
            : $"\"{input}\" is not bound");
    }

    private void Cmd_Binds(string[] args)
    {
        if (console.Binds.Count == 0)
        {
            console.Print("Nothing is bound");
            return;
        }

        foreach (var (input, command) in console.Binds.OrderBy(b => b.Key.ToString(), StringComparer.OrdinalIgnoreCase))
            console.Print($"\"{input}\" = \"{command}\"");
    }

    private void Cmd_BindAction(string[] args)
    {
        if (args.Length == 0)
        {
            foreach (var each in ControlAction.All)
                console.Print($"{each.Name} = {controls.FormatInputs(each)}");
            return;
        }

        if (!ControlAction.TryParse(args[0], out var action))
            throw new ArgumentException($"unknown control \"{args[0]}\"");

        if (args.Length > 1)
        {
            // All read before any is set, so a bad name leaves the control as it was
            List<InputCode> codes = args[1..] is ["none"] ? [] : args[1..].Select(ParseInput).ToList();
            if (codes.Count(code => !code.IsController) > ControlBindings.KeySlots || codes.Count(code => code.IsController) > 1)
                throw new ArgumentException($"a control takes up to {ControlBindings.KeySlots} keys or mouse buttons and one joystick button");

            controls.Clear(action);
            foreach (var code in codes)
                controls.Add(action, code);

            if (console.IsRunningScript)    // controls.cfg sets every control at startup
                return;
        }

        console.Print($"{action.Name} = {controls.FormatInputs(action)}");
    }

    /// <summary>
    /// Sets a number setting from args[0] if given, then shows it (quietly when controls.cfg
    /// sets it at startup).
    /// </summary>
    void SetOrShow(string name, string[] args, ref int setting, int min, int max)
    {
        if (args.Length > 0)
        {
            setting = ParseInt(args[0], min, max);
            if (console.IsRunningScript)
                return;
        }

        console.Print($"{name} = {setting}");
    }

    /// <summary>
    /// Sets an on/off setting from args[0] if given (with none, shows it), quietly when
    /// controls.cfg sets it at startup.
    /// </summary>
    void ToggleOrShow(string name, string[] args, ref bool setting)
    {
        if (args.Length > 0)
        {
            setting = Toggle(args, setting);
            if (console.IsRunningScript)
                return;
        }

        console.Print($"{name} = {(setting ? 1 : 0)}");
    }

    private void Cmd_JoySticks(string[] args)
    {
        if (args.Length > 0)
        {
            settings.joyclassicsticks = args[0].ToLowerInvariant() switch
            {
                "modern" => false,
                "classic" => true,
                _ => throw new ArgumentException($"expected modern or classic, got \"{args[0]}\""),
            };
            if (console.IsRunningScript)
                return;
        }

        console.Print($"joy_sticks = {settings.JoySticksName}");
    }
}
