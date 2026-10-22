using System.Text.RegularExpressions;
using Lua;
using Lua.Standard;

namespace PFWolf.Scripting;

/// <summary>
/// One sandboxed Lua state (Lua-CSharp, Lua 5.2) that pfwolf.pk3's and the mods' scripts
/// (scripts/*.lua) all run in, in load order. Scripts get Lua's basic, string, table, math and
/// bit32 libraries, less anything that reaches outside the game (no io, os, require, dofile,
/// load) or outside the game's own random numbers (no math.random: they'd differ between
/// machines and demo runs). What the game gives them is Scripting/GameScripts.cs's.
///
/// Everything runs on the game's thread: the calls are awaited where they're made, and finish
/// at once, since nothing the scripts are given waits on anything.
/// </summary>
internal sealed partial class ScriptHost
{
    // A call (with what it calls) runs at most HookLimit × HookInterval Lua instructions: a script
    // stuck in a loop is stopped at the same point on every machine, unlike a timer
    private const int HookInterval = 1000;
    private const int HookLimit = 1000;

    // Errors shown per function before the rest are kept quiet
    private const int ShownErrors = 3;

    private readonly LuaState _state;
    private readonly Action<string> _print;
    private int _depth;
    private int _hooks;
    private readonly Dictionary<string, int> _errorCounts = new(StringComparer.Ordinal);

    /// <summary>Where each script that loaded came from, in the order they ran</summary>
    public List<string> Loaded { get; } = [];

    /// <summary>The scripts that couldn't be loaded, with why</summary>
    public List<string> Failed { get; } = [];

    public LuaState State => _state;

    public LuaTable Globals => _state.Environment;

    /// <summary>Prints where the scripts' print goes (the game's console), as a warning about a script</summary>
    public void Print(string text) => _print(text);

    public ScriptHost(Action<string> print)
    {
        _print = print;
        _state = LuaState.Create();
        _state.OpenBasicLibrary();
        _state.OpenStringLibrary();
        _state.OpenTableLibrary();
        _state.OpenMathLibrary();
        _state.OpenBitwiseLibrary();

        // Also math.random's own state, a global of the library's, and string.dump, whose code
        // nothing is left to load
        foreach (var name in new[] { "dofile", "loadfile", "load", "loadstring", "require", "collectgarbage", "__lua_mathematics_library_random_instance" })
            Globals[name] = LuaValue.Nil;
        if (Globals["math"].TryRead<LuaTable>(out var math))
        {
            math["random"] = LuaValue.Nil;
            math["randomseed"] = LuaValue.Nil;
        }
        if (Globals["string"].TryRead<LuaTable>(out var strings))
            strings["dump"] = LuaValue.Nil;

        Globals["print"] = new LuaFunction("print", (context, _) =>
        {
            var parts = new string[context.ArgumentCount];
            for (int i = 0; i < parts.Length; i++)
                parts[i] = context.GetArgument(i).ToString();
            print(string.Join("\t", parts));
            return new(context.Return());
        });

        _state.SetHook(new LuaFunction("instruction limit", (context, _) =>
        {
            if (++_hooks > HookLimit)
                throw new LuaRuntimeException(context.State, (LuaValue)$"ran for over {HookLimit * HookInterval:N0} instructions, so it was stopped", 1);
            return new(0);
        }), "", HookInterval);
    }

    /// <summary>
    /// Where the code running now came from, while a script file's top level runs (its Load) or
    /// a line typed in the console does ("console"); null the rest of the time, when only its
    /// functions are called
    /// </summary>
    public string? Running { get; private set; }

    /// <summary>Runs a script file's top level (defining its functions); false, with the reason in Failed, if it can't</summary>
    public bool Load(string text, string where)
    {
        Running = where;
        try
        {
            Run(() => _state.DoStringAsync(text, where));
            Loaded.Add(where);
            return true;
        }
        catch (Exception e) when (e is LuaRuntimeException or LuaCompileException or LuaParseException or OperationCanceledException)
        {
            Failed.Add(Describe(e));
            return false;
        }
        finally
        {
            Running = null;
        }
    }

    /// <summary>Runs a line of Lua typed in the console, returning what it printed as an error, or null</summary>
    public string? Execute(string text)
    {
        Running = "console";
        try
        {
            Run(() => _state.DoStringAsync(text, "console"));
            return null;
        }
        catch (Exception e) when (e is LuaRuntimeException or LuaCompileException or LuaParseException or OperationCanceledException)
        {
            return Describe(e);
        }
        finally
        {
            Running = null;
        }
    }

    /// <summary>The script functions whose names start with the prefix (A_, T_), by name</summary>
    public IEnumerable<string> GlobalFunctionNames(params string[] prefixes)
    {
        foreach (var (key, value) in Globals)
        {
            if (key.TryRead<string>(out var name) && value.Type == LuaValueType.Function
                && prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
                yield return name;
        }
    }

    /// <summary>
    /// Calls a script function by name. An error is shown (the first few times for each
    /// function) and the call skipped; false then.
    /// </summary>
    public bool Call(string name, ReadOnlySpan<LuaValue> arguments) => Call(Globals[name], name, arguments);

    /// <summary>
    /// Calls a script function (a hook a script gave), named for its error messages; as
    /// Call(name, arguments) otherwise
    /// </summary>
    public bool Call(LuaValue function, string name, ReadOnlySpan<LuaValue> arguments)
    {
        if (function.Type != LuaValueType.Function)
            return false;

        var args = arguments.ToArray();
        try
        {
            Run(() => _state.CallAsync(function, args));
            return true;
        }
        catch (Exception e) when (e is LuaRuntimeException or OperationCanceledException)
        {
            int count = _errorCounts.GetValueOrDefault(name) + 1;
            _errorCounts[name] = count;
            if (count <= ShownErrors)
                _print($"Script {name}: {Describe(e)}{(count == ShownErrors ? " (its later errors aren't shown)" : "")}");
            return false;
        }
    }

    // Runs a call to its end, counting its instructions from 0 unless it's inside another call
    private void Run<T>(Func<ValueTask<T>> call)
    {
        if (_depth == 0)
            _hooks = 0;
        _depth++;
        try
        {
            var task = call();
            if (!task.IsCompletedSuccessfully)
                task.AsTask().GetAwaiter().GetResult();
        }
        finally
        {
            _depth--;
        }
    }

    /// <summary>A Lua error as a one-line message: `where:line: what went wrong`</summary>
    public static string Describe(Exception e)
    {
        var message = e.Message.StartsWith("Lua-CSharp: ", StringComparison.Ordinal) ? e.Message["Lua-CSharp: ".Length..] : e.Message;
        return ChunkName().Replace(message, "$1");
    }

    // Lua's [string "fireball-demo: scripts/traps.lua"]:7: in an error, for just the name
    [GeneratedRegex("""\[string "([^"]*)"\]""")]
    private static partial Regex ChunkName();
}
