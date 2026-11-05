using Lua;
using PFWolf.Constants;

namespace PFWolf.Scripting;

/*
=============================================================================

                            PARTICLES FROM SCRIPTS

Particles are only for show (Rendering/Particles.cs): these change nothing in the game, so
they work in every hook, those that run on one machine alone (game.onhud) too.

  game.particles(name, x, y[, z[, changes]])     a burst at a spot (tiles; z texels up)
  actor:particles(name[, changes])               a burst where an actor is, facing its way
  game.emitter(name, x, y[, z[, changes]])       a running emitter at a spot, or
  game.emitter(name, actor[, changes])           on an actor: its handle (emitter:stop()...)
  game.defineparticle(name, fields)              a particle type of the script's
  game.defineemitter(name, fields)               an emitter of the script's
  game.particlecount()                           how many particles there are now

name is a particles.yaml emitter, or a particle type (thrown out every way). changes is a table
of an emitter's or its type's fields as particles.yaml writes them ({ count = 20, color =
"#80FF80", ["size-end"] = 4 }, or size_end).

=============================================================================
*/

internal sealed partial class GameScripts
{
    /// <summary>The particles (Rendering/Particles.cs), once they're made; null leaves the particle functions doing nothing</summary>
    internal Func<Rendering.Particles?> ParticleSystem { get; init; } = () => null;

    const double TexelsPerTile = GameConstants.TextureSize;

    void InstallParticleApi(LuaTable game, Dictionary<string, LuaFunction> methods)
    {
        game["particles"] = new LuaFunction("game.particles", (c, _) =>
        {
            var name = StringArg(c, 0);
            double x = NumberArg(c, 1, double.NaN), y = NumberArg(c, 2, double.NaN);
            if (double.IsNaN(x) || double.IsNaN(y))
                throw new LuaRuntimeException(c.State, (LuaValue)"game.particles: x and y (in tiles) are needed", 1);
            double z = NumberArg(c, 3, 0);
            var changes = Changes(c, 4, "game.particles");
            if (ParticleSystem() is not { } particles)
                return new(c.Return(0));
            int made = particles.ScriptBurst(name, changes, x * TexelsPerTile, y * TexelsPerTile, z, 0, Fail(c, "game.particles"));
            return new(c.Return(made));
        });

        methods["particles"] = Method("particles", (c, self) =>
        {
            var name = StringArg(c, 1);
            var changes = Changes(c, 2, "actor:particles");
            if (ParticleSystem() is not { } particles)
                return c.Return(0);
            int made = particles.ScriptBurst(name, changes, self.X / (double)MapConstants.TileGlobal * TexelsPerTile,
                self.Y / (double)MapConstants.TileGlobal * TexelsPerTile, self.Z, Rendering.Particles.Facing(self), Fail(c, "actor:particles"));
            return c.Return(made);
        });

        game["emitter"] = new LuaFunction("game.emitter", (c, _) =>
        {
            var name = StringArg(c, 0);
            if (ParticleSystem() is not { } particles)
                return new(c.Return(LuaValue.Nil));

            Rendering.Particles.ScriptEmitter? made;
            if (Arg(c, 1).TryRead<LuaActor>(out var on))
                made = particles.StartScriptEmitter(name, Changes(c, 2, "game.emitter"), Fail(c, "game.emitter"), actor: on.Actor);
            else
            {
                double x = NumberArg(c, 1, double.NaN), y = NumberArg(c, 2, double.NaN);
                if (double.IsNaN(x) || double.IsNaN(y))
                    throw new LuaRuntimeException(c.State, (LuaValue)"game.emitter: an actor, or x and y (in tiles), are needed", 1);
                made = particles.StartScriptEmitter(name, Changes(c, 4, "game.emitter"), Fail(c, "game.emitter"),
                    x: x * TexelsPerTile, y: y * TexelsPerTile, z: NumberArg(c, 3, 0));
            }
            return new(c.Return(made == null ? LuaValue.Nil : EmitterHandle(particles, made)));
        });

        game["defineparticle"] = new LuaFunction("game.defineparticle", (c, _) =>
        {
            var name = StringArg(c, 0);
            var fields = Changes(c, 1, "game.defineparticle");
            ParticleSystem()?.DefineType(name, fields, Fail(c, "game.defineparticle"));
            return new(c.Return());
        });

        game["defineemitter"] = new LuaFunction("game.defineemitter", (c, _) =>
        {
            var name = StringArg(c, 0);
            var fields = Changes(c, 1, "game.defineemitter");
            ParticleSystem()?.DefineEmitter(name, fields, Fail(c, "game.defineemitter"));
            return new(c.Return());
        });

        game["particlecount"] = new LuaFunction("game.particlecount", (c, _) => new(c.Return(ParticleSystem()?.Count ?? 0)));
    }

    /// <summary>The names an emitter's handle answers to (emitter:NAME(...)), for ScriptApi's list</summary>
    internal static readonly string[] EmitterMethodNames = ["stop", "start", "running", "moveto", "set", "remove"];

    // A running emitter as a script holds it: a table of its methods, each called with a colon
    LuaTable EmitterHandle(Rendering.Particles particles, Rendering.Particles.ScriptEmitter emitter)
    {
        var handle = new LuaTable();
        handle["stop"] = new LuaFunction("emitter:stop", (c, _) =>
        {
            emitter.Running = false;
            return new(c.Return());
        });
        handle["start"] = new LuaFunction("emitter:start", (c, _) =>
        {
            emitter.Running = true;
            return new(c.Return());
        });
        handle["running"] = new LuaFunction("emitter:running", (c, _) => new(c.Return(emitter.Running && !emitter.Gone)));
        handle["moveto"] = new LuaFunction("emitter:moveto", (c, _) =>
        {
            double x = NumberArg(c, 1, double.NaN), y = NumberArg(c, 2, double.NaN);
            if (double.IsNaN(x) || double.IsNaN(y))
                throw new LuaRuntimeException(c.State, (LuaValue)"emitter:moveto: x and y (in tiles) are needed", 1);
            emitter.Actor = null;           // standing on its own from now on
            emitter.X = x * TexelsPerTile;
            emitter.Y = y * TexelsPerTile;
            emitter.Z = NumberArg(c, 3, emitter.Z);
            return new(c.Return());
        });
        handle["set"] = new LuaFunction("emitter:set", (c, _) =>
        {
            particles.ChangeScriptEmitter(emitter, Changes(c, 1, "emitter:set"), Fail(c, "emitter:set"));
            return new(c.Return());
        });
        handle["remove"] = new LuaFunction("emitter:remove", (c, _) =>
        {
            particles.RemoveScriptEmitter(emitter);
            return new(c.Return());
        });
        return handle;
    }

    // An error raised in the script, at its line
    static Action<string> Fail(LuaFunctionExecutionContext c, string what) =>
        message => throw new LuaRuntimeException(c.State, (LuaValue)$"{what}: {message}", 1);

    /// <summary>
    /// A table of fields as the particle functions take them: names to numbers, strings,
    /// booleans or lists (of strings or numbers); nil or left out for none
    /// </summary>
    static List<(string Key, object? Value)> Changes(LuaFunctionExecutionContext c, int index, string what)
    {
        var value = Arg(c, index);
        if (value.Type == LuaValueType.Nil)
            return [];
        if (!value.TryRead<LuaTable>(out var table))
            throw new LuaRuntimeException(c.State, (LuaValue)$"{what}: argument {index + 1} should be a table of fields", 1);

        var fields = new List<(string Key, object? Value)>();
        foreach (var (key, field) in table)
        {
            if (key.Type != LuaValueType.String)
                throw new LuaRuntimeException(c.State, (LuaValue)$"{what}: its fields are named (count = 10), not numbered", 1);
            fields.Add((key.Read<string>(), FieldValue(c, key.Read<string>(), field, what)));
        }
        return fields;
    }

    static object? FieldValue(LuaFunctionExecutionContext c, string key, LuaValue value, string what) => value.Type switch
    {
        LuaValueType.Number => value.Read<double>(),
        LuaValueType.String => value.Read<string>(),
        LuaValueType.Boolean => value.ToBoolean(),
        LuaValueType.Nil => null,
        LuaValueType.Table => ListOf(c, key, value.Read<LuaTable>(), what),
        _ => throw new LuaRuntimeException(c.State, (LuaValue)$"{what}: {key} should be a number, a string, true or false, or a list", 1),
    };

    static List<string> ListOf(LuaFunctionExecutionContext c, string key, LuaTable table, string what)
    {
        var list = new List<string>();
        for (int i = 1; table[i] is { Type: not LuaValueType.Nil } item; i++)
            list.Add(item.Type switch
            {
                LuaValueType.String => item.Read<string>(),
                LuaValueType.Number => item.Read<double>().ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => throw new LuaRuntimeException(c.State, (LuaValue)$"{what}: {key}'s list should hold strings or numbers", 1),
            });
        return list;
    }
}
