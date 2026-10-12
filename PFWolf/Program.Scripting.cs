using System.Globalization;
using Lua;
using PFWolf.Constants;
using PFWolf.Scripting;

namespace PFWolf;

internal partial class Program
{
    /*
    =============================================================================

                                   LUA SCRIPTS

    pfwolf.pk3's and the mods' scripts/*.lua run at startup, in load order, in one sandboxed Lua
    state (Scripting/ScriptHost.cs). Each global function a script defines whose name starts
    with A_ or T_ becomes an action or think actordefs states can call like a built-in one:

        function A_FireRing(self, projectile, count) ... end
        -- actordefs:  action: A_FireRing("Fireball", 8)

    It's called with the actor (self) and the state's arguments, numbers as numbers. A built-in
    action of the same name wins (the script's is ignored, with a warning).

    What the scripts get, besides Lua's basic, string, table, math and bit32 libraries:
      an actor's fields: name, x, y (in tiles: a tile's middle is n + 0.5), tilex, tiley, angle
        (degrees, 0 east, 90 north: the way it faces, or a projectile flies), health, speed,
        state, tag, inwall, removed, isplayer, shooter (who fired a projectile), temp1..temp3
        (whole numbers kept in saved games: a script's own state for the actor belongs there).
        temp1..temp3, angle and speed can be set.
      its methods: jump(state), spawnprojectile(class[, angle[, mode]]), spawn(class),
        playsound(sound[, positional]), property(key), hasflag(flag), distanceto(other),
        angleto(other), cansee(player[, tiles]), nearestplayer(), remove()
      game.random([m[, n]]), game.players(), game.actors([class]), game.time(), game.tics(), game.map(), game.skill(),
        game.message(text[, style]), and the hooks game.onlevel(fn) and game.ontick(fn) (HOOKS below)
    and print, to the console.

    Scripts must leave the outcome to the game's own random numbers (game.random) and to what's
    on the actors, so that demos, network games and saved games come out the same: Lua globals
    aren't saved, and math.random isn't there.

    =============================================================================
    */

    static ScriptHost? _scripts;

    /// <summary>
    /// Runs the scripts and registers their A_/T_ functions as actions. At startup, after the
    /// built-in actions are registered: anything it prints is a startup warning.
    /// </summary>
    static void LoadScripts()
    {
        var scripts = _assetManager.GetScripts();
        if (scripts.Count == 0)
            return;

        _scripts = new ScriptHost(text => _consoleManager.Print(text));
        InstallScriptApi(_scripts);

        foreach (var script in scripts)
            if (!_scripts.Load(script.Text, script.Where))
                Console.WriteLine($"Script {_scripts.Failed[^1]}; it's left out");

        foreach (var name in _scripts.GlobalFunctionNames("A_", "T_"))
        {
            if (Entities.Actors.ActorActionRegistry.IsRegistered(name))
            {
                Console.WriteLine($"Script function {name} is ignored: a built-in action has that name");
                continue;
            }
            var function = name;
            Entities.Actors.ActorActionRegistry.Register(function, (actor, args) => RunScriptAction(function, actor, args));
            scriptActions.Add(function);
        }
    }

    // The actions the scripts gave, by name, for the scripts command
    static readonly List<string> scriptActions = [];

    static void RunScriptAction(string function, Entities.Actors.Actor actor, string[] args)
    {
        var values = new LuaValue[args.Length + 1];
        values[0] = LuaActor.Of(actor);
        for (int i = 0; i < args.Length; i++)
            values[i + 1] = double.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : args[i];
        _scripts!.Call(function, values);
    }

    /*
    =============================================================================
    THE API
    =============================================================================
    */

    static void InstallScriptApi(ScriptHost host)
    {
        var methods = new Dictionary<string, LuaFunction>(StringComparer.Ordinal)
        {
            ["jump"] = Method("jump", (c, self) =>
            {
                var state = StringArg(c, 1);
                if (!self.ResolvedStates.TryGetValue(state, out var frame))
                    return c.Return(false);
                self.JumpTo(frame);
                return c.Return(true);
            }),
            ["spawnprojectile"] = Method("spawnprojectile", (c, self) => c.Return(LuaActor.Of(
                SpawnProjectile(self, StringArg(c, 1), (int)Math.Round(NumberArg(c, 2, 0)), OptionalString(c, 3) ?? "relative")))),
            ["spawn"] = Method("spawn", (c, self) => c.Return(LuaActor.Of(_mapManager.SpawnAtActor(StringArg(c, 1), self)))),
            ["playsound"] = Method("playsound", (c, self) =>
            {
                var sound = StringArg(c, 1);
                if (Arg(c, 2).Type == LuaValueType.Boolean && !Arg(c, 2).ToBoolean())
                    _audioManager.Play(sound);
                else
                    PlaySoundLocActor(sound, self);
                return c.Return();
            }),
            ["property"] = Method("property", (c, self) =>
                c.Return(self.Properties.TryGetValue(StringArg(c, 1), out var value) ? ToLua(value) : LuaValue.Nil)),
            ["hasflag"] = Method("hasflag", (c, self) => c.Return(self.HasFlag(StringArg(c, 1)))),
            ["distanceto"] = Method("distanceto", (c, self) =>
            {
                var other = ActorArg(c, 1);
                double dx = (other.X - self.X) / (double)MapConstants.TILEGLOBAL, dy = (other.Y - self.Y) / (double)MapConstants.TILEGLOBAL;
                return c.Return(Math.Sqrt(dx * dx + dy * dy));
            }),
            ["angleto"] = Method("angleto", (c, self) =>
            {
                var other = ActorArg(c, 1);
                var (x, y) = LookoutPoint(self);
                return c.Return(AngleTo(x, y, other.X, other.Y));
            }),
            ["cansee"] = Method("cansee", (c, self) =>
            {
                long reach = Arg(c, 2).Type == LuaValueType.Number ? (long)(NumberArg(c, 2, 0) * MapConstants.TILEGLOBAL) : long.MaxValue;
                return c.Return(ActorArg(c, 1) is Entities.Actors.PlayerPawn pawn && Entities.Actors.Monster.IsTargetable(pawn) && CanSee(self, pawn, reach));
            }),
            ["nearestplayer"] = Method("nearestplayer", (c, self) =>
            {
                var (x, y) = LookoutPoint(self);
                return c.Return(LuaActor.Of(NearestPlayer(x, y)));
            }),
            ["remove"] = Method("remove", (c, self) =>
            {
                if (self is not Entities.Actors.PlayerPawn)
                    _mapManager.MarkForRemoval(self);
                return c.Return();
            }),
        };

        var meta = new LuaTable();
        meta["__index"] = new LuaFunction("actor field", (c, _) =>
        {
            var self = ActorArg(c, 0);
            var key = StringArg(c, 1);
            return new(key switch
            {
                "name" => c.Return(self.Name),
                "x" => c.Return(self.X / (double)MapConstants.TILEGLOBAL),
                "y" => c.Return(self.Y / (double)MapConstants.TILEGLOBAL),
                "tilex" => c.Return(self.TileX),
                "tiley" => c.Return(self.TileY),
                "angle" => c.Return(FacingAngle(self)),
                "health" => c.Return(self is Entities.Actors.PlayerPawn pawn ? pawn.State.health : self.Hitpoints),
                "speed" => c.Return(self.Speed),
                "state" => c.Return(self.CurrentState?.StateName is { } state ? state : LuaValue.Nil),
                "tag" => c.Return(self.Tag),
                "inwall" => c.Return(IsInWall(self)),
                "removed" => c.Return(self.IsRemoved),
                "isplayer" => c.Return(self is Entities.Actors.PlayerPawn),
                "shooter" => c.Return(LuaActor.Of(self.Shooter)),
                "temp1" => c.Return(self.Temp1),
                "temp2" => c.Return(self.Temp2),
                "temp3" => c.Return(self.Temp3),
                _ => c.Return(methods.TryGetValue(key, out var method) ? method : LuaValue.Nil),
            });
        });
        meta["__newindex"] = new LuaFunction("actor field", (c, _) =>
        {
            var self = ActorArg(c, 0);
            var key = StringArg(c, 1);
            switch (key)
            {
                case "temp1": self.Temp1 = ShortArg(c, 2); break;
                case "temp2": self.Temp2 = ShortArg(c, 2); break;
                case "temp3": self.Temp3 = ShortArg(c, 2); break;
                case "speed": self.Speed = (int)Math.Round(NumberArg(c, 2, 0)); break;
                case "angle":
                    self.Angle = (short)(((int)Math.Round(NumberArg(c, 2, 0)) % ANGLES + ANGLES) % ANGLES);
                    if (self is not Entities.Actors.Monster and not Entities.Actors.PlayerPawn)
                        self.Dir = objdirtypes.nodir;       // its facing is its angle from now on
                    break;
                default:
                    throw new LuaRuntimeException(c.State, (LuaValue)$"an actor's {key} can't be set (temp1..temp3, angle and speed can)", 1);
            }
            return new(c.Return());
        });
        meta["__tostring"] = new LuaFunction("tostring", (c, _) =>
        {
            var self = ActorArg(c, 0);
            return new(c.Return($"{self.Name} at {self.TileX},{self.TileY}"));
        });
        LuaActor.Meta = meta;

        var game = new LuaTable();
        game["random"] = new LuaFunction("game.random", (c, _) =>
        {
            if (c.ArgumentCount == 0)
                return new(c.Return(US_RndT()));
            int low = c.ArgumentCount > 1 ? (int)NumberArg(c, 0, 1) : 1;
            int high = (int)NumberArg(c, c.ArgumentCount > 1 ? 1 : 0, 1);
            if (high < low)
                throw new LuaRuntimeException(c.State, (LuaValue)"game.random: the range is empty", 1);
            return new(c.Return(low + GameRandom(high - low + 1)));
        });
        game["players"] = new LuaFunction("game.players", (c, _) =>
        {
            var list = new LuaTable();
            int i = 1;
            foreach (var pawn in _mapManager.Players.Where(Entities.Actors.Monster.IsTargetable))
                list[i++] = LuaActor.Of(pawn);
            return new(c.Return(list));
        });
        game["actors"] = new LuaFunction("game.actors", (c, _) =>
        {
            var className = OptionalString(c, 0);
            var list = new LuaTable();
            int i = 1;
            foreach (var actor in _mapManager.GetActors().Where(a => !a.IsRemoved
                && (className == null || a.Name.Equals(className, StringComparison.OrdinalIgnoreCase))))
                list[i++] = LuaActor.Of(actor);
            return new(c.Return(list));
        });
        game["onlevel"] = new LuaFunction("game.onlevel", (c, _) => new(c.Return(AddHook(c, host, levelHooks, "game.onlevel"))));
        game["ontick"] = new LuaFunction("game.ontick", (c, _) => new(c.Return(AddHook(c, host, tickHooks, "game.ontick"))));
        game["time"] = new LuaFunction("game.time", (c, _) => new(c.Return(gamestate.TimeCount)));
        game["tics"] = new LuaFunction("game.tics", (c, _) => new(c.Return(tics)));
        game["map"] = new LuaFunction("game.map", (c, _) => new(c.Return(gamestate.mapon ?? "")));
        game["skill"] = new LuaFunction("game.skill", (c, _) => new(c.Return(gamestate.difficulty)));
        game["message"] = new LuaFunction("game.message", (c, _) =>
        {
            _hudMessageManager.Show(Managers.HudMessageKind.Lock, StringArg(c, 0), OptionalString(c, 1));
            return new(c.Return());
        });
        host.Globals["game"] = game;
    }

    /*
    =============================================================================
    HOOKS

    game.onlevel(function(reason, map) ... end): called as each level starts, once it's built
    and the players are on it, before its first tic. reason is
        "new"        the level entered afresh (a new game, the next level, a demo)
        "returned"   a hub level come back to, as it was left (Blake Stone's floors)
        "restarted"  the level again after the player died
        "loaded"     a saved game loaded, or a network game joined. Only this machine runs it,
                     so it mustn't change the game: it's for rebuilding what the script keeps
                     in Lua variables, which saves don't hold.
    game.ontick(function(tics) ... end): called each frame the world moves (not while it's
        paused), after the actors, with the tics that went by (70 a second; usually 1 or 2).

    Hooks are added when a script loads (at its top level), never later, so that every machine
    and every demo run has the same ones; each mod's run, in load order. A hook that errors
    three times is switched off.

    =============================================================================
    */

    sealed class ScriptHook(LuaValue function, string name)
    {
        public LuaValue Function { get; } = function;
        public string Name { get; } = name;
        public int Errors { get; set; }
    }

    static readonly List<ScriptHook> levelHooks = [];
    static readonly List<ScriptHook> tickHooks = [];

    // Errors a hook may have before it's switched off
    const int HookErrorLimit = 3;

    static int AddHook(LuaFunctionExecutionContext c, ScriptHost host, List<ScriptHook> hooks, string what)
    {
        var function = Arg(c, 0);
        if (function.Type != LuaValueType.Function)
            throw new LuaRuntimeException(c.State, (LuaValue)$"{what} takes a function", 1);
        if (host.Running == null)
            throw new LuaRuntimeException(c.State, (LuaValue)$"{what}: hooks are added when a script loads (at its top level), not later", 1);
        hooks.Add(new ScriptHook(function, $"{what} hook {hooks.Count + 1} ({host.Running})"));
        return c.Return();
    }

    /// <summary>The scripts' level hooks, as a level starts (GameLoop, PlayDemo, RecordDemo)</summary>
    static void RunLevelHooks(string reason) =>
        RunHooks(levelHooks, [reason, gamestate.mapon ?? ""]);

    /// <summary>The scripts' tick hooks, each frame the world moves (PlayLoop)</summary>
    static void RunTickHooks()
    {
        if (tickHooks.Count > 0)
            RunHooks(tickHooks, [tics]);
    }

    static void RunHooks(List<ScriptHook> hooks, LuaValue[] arguments)
    {
        if (_scripts == null)
            return;
        for (int i = 0; i < hooks.Count; i++)
        {
            var hook = hooks[i];
            if (_scripts.Call(hook.Function, hook.Name, arguments) || ++hook.Errors < HookErrorLimit)
                continue;
            _consoleManager.Print($"Script {hook.Name} is switched off after {HookErrorLimit} errors");
            hooks.RemoveAt(i--);
        }
    }

    // 0 to count - 1 from the game's random numbers: one draw for up to 256, more beyond
    static int GameRandom(int count)
    {
        if (count <= 256)
            return US_RndT() * count / 256;
        long value = 0, range = 1;
        while (range < count)
        {
            value = value << 8 | (uint)US_RndT();
            range <<= 8;
        }
        return (int)(value * count / range);
    }

    // An actor method: its first argument is the actor (actor:method(...))
    static LuaFunction Method(string name, Func<LuaFunctionExecutionContext, Entities.Actors.Actor, int> body) =>
        new(name, (c, _) => new(body(c, ActorArg(c, 0))));

    static LuaValue Arg(LuaFunctionExecutionContext c, int index) => index < c.ArgumentCount ? c.GetArgument(index) : LuaValue.Nil;

    static Entities.Actors.Actor ActorArg(LuaFunctionExecutionContext c, int index) =>
        Arg(c, index).TryRead<LuaActor>(out var actor) ? actor.Actor
            : throw new LuaRuntimeException(c.State, (LuaValue)$"argument {index + 1} should be an actor (use actor:method(), with a colon)", 1);

    static string StringArg(LuaFunctionExecutionContext c, int index) =>
        OptionalString(c, index) ?? throw new LuaRuntimeException(c.State, (LuaValue)$"argument {index + 1} should be a string", 1);

    static string? OptionalString(LuaFunctionExecutionContext c, int index) => Arg(c, index) switch
    {
        { Type: LuaValueType.String } value => value.Read<string>(),
        { Type: LuaValueType.Number } value => value.Read<double>().ToString(CultureInfo.InvariantCulture),
        _ => null,
    };

    static double NumberArg(LuaFunctionExecutionContext c, int index, double fallback) => Arg(c, index) switch
    {
        { Type: LuaValueType.Number } value => value.Read<double>(),
        { Type: LuaValueType.Nil } => fallback,
        { Type: LuaValueType.String } value when double.TryParse(value.Read<string>(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) => n,
        _ => throw new LuaRuntimeException(c.State, (LuaValue)$"argument {index + 1} should be a number", 1),
    };

    static short ShortArg(LuaFunctionExecutionContext c, int index) =>
        (short)Math.Clamp(Math.Round(NumberArg(c, index, 0)), short.MinValue, short.MaxValue);

    // An actordefs property as Lua sees it: numbers as numbers, lists as tables
    static LuaValue ToLua(object? value) => value switch
    {
        null => LuaValue.Nil,
        bool b => b,
        int or long or short or byte or float or double => Convert.ToDouble(value, CultureInfo.InvariantCulture),
        string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) => n,
        string s => s,
        System.Collections.IEnumerable list => ListToLua(list),
        _ => value.ToString() ?? "",
    };

    static LuaTable ListToLua(System.Collections.IEnumerable list)
    {
        var table = new LuaTable();
        int i = 1;
        foreach (var item in list)
            table[i++] = ToLua(item);
        return table;
    }

    /*
    =============================================================================
    CONSOLE
    =============================================================================
    */

    static void Cmd_Scripts(string[] args)
    {
        if (_scripts == null)
        {
            _consoleManager.Print("No scripts are loaded (scripts/*.lua in pfwolf.pk3 or a mod)");
            return;
        }
        foreach (var where in _scripts.Loaded)
            _consoleManager.Print(where);
        foreach (var failed in _scripts.Failed)
            _consoleManager.Print($"left out: {failed}");
        _consoleManager.Print(scriptActions.Count == 0 ? "No script actions" : $"Actions: {string.Join(", ", scriptActions)}");
    }

    static void Cmd_Lua(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: lua <code>");
        _scripts ??= NewConsoleScriptHost();
        // `me` is the player (nil outside a level), for trying the actor methods out
        _scripts.Globals["me"] = LuaActor.Of(localplayer?.Pawn);
        if (_scripts.Execute(string.Join(' ', args)) is { } error)
            _consoleManager.Print(error);
    }

    // With no scripts loaded, the lua command still has a state to try things in
    static ScriptHost NewConsoleScriptHost()
    {
        var host = new ScriptHost(text => _consoleManager.Print(text));
        InstallScriptApi(host);
        return host;
    }
}
