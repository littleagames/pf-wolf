using System.Globalization;
using Lua;
using PFWolf.Gameplay;

namespace PFWolf.Scripting;

/// <summary>
/// The scripts' puzzles (Gameplay/Puzzles.cs runs them): game.puzzle makes one, from a table of
/// its settings and functions; game.startpuzzle opens one; game.onpuzzle hears how one ended.
///
///     game.puzzle("needle", {
///       title = "Bypass the lock",
///       pause = true,               -- the level stops while it's open (playing alone)
///       cancel = "nothing",         -- what Esc comes to: "nothing", "fail" or "never"
///       timelimit = 35 * 20,        -- tics before it's a fail (nil: none)
///       width = 240, height = 140,  -- its panel, in layout pixels
///       start = function(pz, args) ... end,
///       tick = function(pz, input) ... return "success" end,  -- the game's think: may change the game
///       draw = function(pz, width, height) ... end,           -- this machine alone: drawing only
///     })
///
/// pz is the open puzzle: state (the puzzle's own table), name, player, x, y, tag (where it was
/// started), args, tics, timeleft; pz:finish(result) ends it, pz:sound(name) plays a sound for
/// the player working it. input is the player's controls: held and pressed (up, down, left,
/// right, use, fire, cancel), digit (0-9 just pressed, else nil), dx, dy (cursor movement) and
/// tics.
/// </summary>
internal sealed partial class GameScripts : IPuzzleScripts
{
    // The hook name a puzzle's draw runs as (local: it mustn't change the game)
    const string PuzzleDrawHook = "a puzzle's draw";

    sealed record PuzzleScript(PuzzleDefinition Definition, LuaValue Start, LuaValue Tick, LuaValue Draw);

    readonly Dictionary<string, PuzzleScript> puzzleScripts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>game.startpuzzle: opens a puzzle for a player (Gameplay/Puzzles.cs Start), from a tile and with a tag</summary>
    internal Func<Entities.Actors.PlayerPawn, string, object?, bool> StartPuzzleFor { get; init; } = (_, _, _) => false;

    /// <summary>The look of a puzzle's panel (game-info puzzle-panel): its default font and text color</summary>
    internal Func<Assets.PuzzlePanelInfo> PuzzlePanel { get; init; } = () => new();

    // Where a puzzle's draw draws from (its panel's top left, layout pixels), and how big it is
    (int X, int Y, int Width, int Height) puzzleArea;

    /// <summary>The names of the puzzles the scripts made, for the puzzle command</summary>
    internal IEnumerable<string> PuzzleNames => puzzleScripts.Keys.Order(StringComparer.OrdinalIgnoreCase);

    void InstallPuzzleFunctions(LuaTable game, ScriptHost host)
    {
        game["puzzle"] = new LuaFunction("game.puzzle", (c, _) =>
        {
            var name = StringArg(c, 0).Trim();
            if (!Arg(c, 1).TryRead<LuaTable>(out var settings))
                throw new LuaRuntimeException(c.State, (LuaValue)"game.puzzle takes a name and a table of its settings and functions", 1);
            if (host.Running == null)
                throw new LuaRuntimeException(c.State, (LuaValue)"game.puzzle: puzzles are made when a script loads (at its top level), not later", 1);
            if (name.Length == 0)
                throw new LuaRuntimeException(c.State, (LuaValue)"game.puzzle: a puzzle needs a name", 1);
            var tick = settings["tick"];
            if (tick.Type != LuaValueType.Function)
                throw new LuaRuntimeException(c.State, (LuaValue)$"game.puzzle: {name} needs a tick function (tick = function(pz, input) ... end)", 1);

            var cancel = TextField(settings, "cancel")?.ToLowerInvariant() switch
            {
                null or "nothing" => PuzzleCancel.Nothing,
                "fail" => PuzzleCancel.Fail,
                "never" => PuzzleCancel.Never,
                var other => throw new LuaRuntimeException(c.State, (LuaValue)$"game.puzzle: {name}'s cancel is \"{other}\": it's \"nothing\", \"fail\" or \"never\"", 1),
            };
            var definition = new PuzzleDefinition(name,
                TextField(settings, "title") ?? "",
                TextField(settings, "verb") ?? "working a puzzle",
                settings["pause"].Type != LuaValueType.Boolean || settings["pause"].Read<bool>(),
                cancel,
                Math.Max((int)Math.Round(NumberField(settings, "timelimit") ?? 0), 0),
                Math.Clamp((int)Math.Round(NumberField(settings, "width") ?? PuzzleDefinition.DefaultWidth), 16, 320),
                Math.Clamp((int)Math.Round(NumberField(settings, "height") ?? PuzzleDefinition.DefaultHeight), 16, 200));
            puzzleScripts[name] = new PuzzleScript(definition, settings["start"], tick, settings["draw"]);
            return new(c.Return());
        });

        // game.startpuzzle(player, name[, args]): opens a puzzle for a player, as A_Puzzle does
        game["startpuzzle"] = new LuaFunction("game.startpuzzle", (c, _) =>
        {
            ChangesTheGame(c, "game.startpuzzle");
            if (ActorArg(c, 0) is not Entities.Actors.PlayerPawn player)
                throw new LuaRuntimeException(c.State, (LuaValue)"game.startpuzzle: the first argument should be a player", 1);
            var name = StringArg(c, 1);
            object? args = Arg(c, 2).TryRead<LuaTable>(out var table) ? table : null;
            return new(c.Return(StartPuzzleFor(player, name, args)));
        });

        game["drawrect"] = new LuaFunction("game.drawrect", (c, _) =>
        {
            var (x, y, _, _) = HudView(c, "game.drawrect");
            DrawRect(x + Whole(c, 0), y + Whole(c, 1), Whole(c, 2), Whole(c, 3), StringArg(c, 4));
            return new(c.Return());
        });
        game["drawframe"] = new LuaFunction("game.drawframe", (c, _) =>
        {
            var (x, y, _, _) = HudView(c, "game.drawframe");
            int left = x + Whole(c, 0), top = y + Whole(c, 1), width = Whole(c, 2), height = Whole(c, 3);
            var color = StringArg(c, 4);
            if (width <= 0 || height <= 0)
                return new(c.Return());
            DrawRect(left, top, width, 1, color);
            DrawRect(left, top + height - 1, width, 1, color);
            DrawRect(left, top, 1, height, color);
            DrawRect(left + width - 1, top, 1, height, color);
            return new(c.Return());
        });
        game["drawline"] = new LuaFunction("game.drawline", (c, _) =>
        {
            var (x, y, _, _) = HudView(c, "game.drawline");
            DrawLine(x + NumberArg(c, 0, 0), y + NumberArg(c, 1, 0), x + NumberArg(c, 2, 0), y + NumberArg(c, 3, 0),
                StringArg(c, 4), Math.Max((int)Math.Round(NumberArg(c, 5, 1)), 1));
            return new(c.Return());
        });
        game["drawshade"] = new LuaFunction("game.drawshade", (c, _) =>
        {
            var (x, y, _, _) = HudView(c, "game.drawshade");
            DrawShade(x + Whole(c, 0), y + Whole(c, 1), Whole(c, 2), Whole(c, 3), Math.Clamp(NumberArg(c, 4, 0.5), 0, 1));
            return new(c.Return());
        });
        game["textwidth"] = new LuaFunction("game.textwidth", (c, _) =>
        {
            var (width, height) = MeasureText(StringArg(c, 0), OptionalString(c, 1) ?? DefaultFont());
            return new(c.Return(width, height));
        });
    }

    static int Whole(LuaFunctionExecutionContext c, int index) => (int)Math.Round(NumberArg(c, index, 0));

    static string? TextField(LuaTable table, string key) =>
        table[key] is { Type: LuaValueType.String } value && value.Read<string>() is { Length: > 0 } text ? text : null;

    static double? NumberField(LuaTable table, string key) =>
        table[key] is { Type: LuaValueType.Number } value ? value.Read<double>() : null;

    // The font text in a puzzle's draw prints in when it names none (the panel's), else the HUD's
    string DefaultFont() => _localHook == PuzzleDrawHook ? PuzzlePanel().Font : Ui.UiFonts.SmallFont;

    // The color text in a puzzle's draw prints in when it names none (the panel's); null elsewhere (White)
    string? DefaultTextColor() => _localHook == PuzzleDrawHook ? PuzzlePanel().TextColor : null;

    // A font asked for in a puzzle's draw or game.onhud, or the default there
    string? FontOr(string? font) => font ?? (_localHook == PuzzleDrawHook ? PuzzlePanel().Font : null);

    /// <summary>game.drawrect / drawframe: a filled rectangle (layout pixels)</summary>
    internal Action<int, int, int, int, string> DrawRect { get; init; } = (_, _, _, _, _) => { };

    /// <summary>game.drawline: a line from one point to another, a pen that many layout pixels wide</summary>
    internal Action<double, double, double, double, string, int> DrawLine { get; init; } = (_, _, _, _, _, _) => { };

    /// <summary>game.drawshade: a rectangle dimmed, to that brightness (0 black, 1 as it was)</summary>
    internal Action<int, int, int, int, double> DrawShade { get; init; } = (_, _, _, _, _) => { };

    /// <summary>game.textwidth: how wide and tall a line of text is in a font</summary>
    internal Func<string, string, (int Width, int Height)> MeasureText { get; init; } = (_, _) => (0, 0);

    /*
    -----------------------------------------------------------------------------
    Running them (IPuzzleScripts)
    -----------------------------------------------------------------------------
    */

    public PuzzleDefinition? FindPuzzle(string name) => puzzleScripts.TryGetValue(name, out var script) ? script.Definition : null;

    sealed record PuzzleHandle(PuzzleScript Script, LuaTable Pz, LuaTable Input, LuaTable Held, LuaTable Pressed);

    static readonly (PuzzleKeys Key, string Name)[] KeyNames =
    [
        (PuzzleKeys.Up, "up"), (PuzzleKeys.Down, "down"), (PuzzleKeys.Left, "left"), (PuzzleKeys.Right, "right"),
        (PuzzleKeys.Use, "use"), (PuzzleKeys.Fire, "fire"), (PuzzleKeys.Cancel, "cancel"),
    ];

    public bool StartPuzzle(OpenPuzzle puzzle, object? args)
    {
        if (_scripts == null || !puzzleScripts.TryGetValue(puzzle.Definition.Name, out var script))
            return false;

        var argsTable = args switch
        {
            LuaTable table => table,
            string[] words => ArgsTable(words),
            _ => new LuaTable(),
        };

        var pz = new LuaTable();
        pz["state"] = new LuaTable();
        pz["name"] = puzzle.Definition.Name;
        pz["title"] = puzzle.Definition.Title;
        pz["player"] = LuaActor.Of(puzzle.Player);
        pz["x"] = puzzle.X;
        pz["y"] = puzzle.Y;
        pz["tag"] = puzzle.Tag;
        pz["args"] = argsTable;
        pz["finish"] = new LuaFunction("pz:finish", (c, _) =>
        {
            ChangesTheGame(c, "pz:finish");
            puzzle.PendingResult = OptionalString(c, 1) is { Length: > 0 } result ? result : "success";
            return new(c.Return());
        });
        pz["sound"] = new LuaFunction("pz:sound", (c, _) =>
        {
            if (ReferenceEquals(puzzle.State, session.LocalPlayer))
                audio.Play(StringArg(c, 1));
            return new(c.Return());
        });
        var held = new LuaTable();
        var pressed = new LuaTable();
        var input = new LuaTable();
        input["held"] = held;
        input["pressed"] = pressed;
        var handle = new PuzzleHandle(script, pz, input, held, pressed);
        puzzle.Handle = handle;
        UpdateClock(puzzle, pz);

        if (script.Start.Type == LuaValueType.Function
            && !_scripts.Call(script.Start, $"puzzle {puzzle.Definition.Name} start", [pz, argsTable]))
            return false;
        return true;
    }

    public PuzzleTickResult TickPuzzle(OpenPuzzle puzzle, in PuzzleInput keys)
    {
        if (_scripts == null || puzzle.Handle is not PuzzleHandle handle)
            return new(null, true);

        UpdateClock(puzzle, handle.Pz);
        foreach (var (key, name) in KeyNames)
        {
            handle.Held[name] = keys.IsHeld(key);
            handle.Pressed[name] = keys.IsPressed(key);
        }
        handle.Input["digit"] = keys.Digit >= 0 ? keys.Digit : LuaValue.Nil;
        handle.Input["dx"] = keys.Dx;
        handle.Input["dy"] = keys.Dy;
        handle.Input["tics"] = keys.Tics;

        if (!_scripts.Call(handle.Script.Tick, $"puzzle {puzzle.Definition.Name} tick", [handle.Pz, handle.Input], out var result))
            return new(null, true);
        return new(TextOf(result), false);
    }

    /// <summary>A puzzle's draw, on this machine alone, at its panel's top left (layout pixels)</summary>
    internal void DrawPuzzle(OpenPuzzle puzzle, int x, int y)
    {
        if (_scripts == null || puzzle.Handle is not PuzzleHandle handle || handle.Script.Draw.Type != LuaValueType.Function)
            return;
        int width = puzzle.Definition.Width, height = puzzle.Definition.Height;
        puzzleArea = (x, y, width, height);
        RunLocal(PuzzleDrawHook, () =>
        {
            UpdateClock(puzzle, handle.Pz);
            _scripts.Call(handle.Script.Draw, $"puzzle {puzzle.Definition.Name} draw", [handle.Pz, width, height]);
        });
    }

    public void PuzzleFinished(OpenPuzzle puzzle, string result) =>
        Tell("onpuzzle", () => [LuaActor.Of(puzzle.Player), puzzle.Definition.Name, result, puzzle.X, puzzle.Y, puzzle.Tag]);

    static void UpdateClock(OpenPuzzle puzzle, LuaTable pz)
    {
        pz["tics"] = puzzle.Tics;
        pz["timeleft"] = puzzle.TimeLeft is { } left ? left : LuaValue.Nil;
    }

    // A_Puzzle's words as the table its start gets: "key=value" by key, the rest in order;
    // numbers as numbers
    static LuaTable ArgsTable(string[] words)
    {
        var table = new LuaTable();
        int i = 1;
        foreach (var word in words)
        {
            int equals = word.IndexOf('=');
            if (equals > 0)
                table[word[..equals].Trim()] = ArgValue(word[(equals + 1)..].Trim());
            else
                table[i++] = ArgValue(word.Trim());
        }
        return table;
    }

    static LuaValue ArgValue(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : text;
}
