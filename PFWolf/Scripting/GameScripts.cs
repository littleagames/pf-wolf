using System.Globalization;
using Lua;
using PFWolf.Constants;
using PFWolf.Gameplay;
using PFWolf.Managers;

namespace PFWolf.Scripting;

/// <summary>
/// pfwolf.pk3's and the mods' scripts/*.lua run at startup, in load order, in one sandboxed Lua
/// state (Scripting/ScriptHost.cs). Each global function a script defines whose name starts
/// with A_ or T_ becomes an action or think actordefs states can call like a built-in one:
///
///     function A_FireRing(self, projectile, count) ... end
///     -- actordefs:  action: A_FireRing("Fireball", 8)
///
/// It's called with the actor (self) and the state's arguments, numbers as numbers. A built-in
/// action of the same name wins (the script's is ignored, with a warning).
///
/// What the scripts get, the game table and an actor's fields and methods besides what Lua's
/// basic, string, table, math and bit32 libraries leave in, is listed (and documented) in
/// PFWolf.Common's Scripting/ScriptApi.cs; ScriptApiTests holds that list to what's here.
///
/// Scripts must leave the outcome to the game's own random numbers (game.random) and to what's
/// on the actors, so that demos, network games and saved games come out the same: Lua globals
/// aren't saved, and math.random isn't there.
/// </summary>
/// <param name="translations">The color translations (null where there are none: the API's tests)</param>
/// <param name="playSoundAt">A sound from where an actor is</param>
internal sealed partial class GameScripts(
    AssetManager assets,
    MapManager map,
    AudioManager audio,
    HudMessageManager hudMessages,
    ConsoleManager console,
    TranslationManager? translations,
    ProjectileActions projectileActions,
    GameSession session,
    Action<string, Entities.Actors.Actor> playSoundAt)
{
    ScriptHost? _scripts;

    /// <summary>game.unlock: unlocks a name for good, true if it wasn't already (Gameplay/Unlocks.cs)</summary>
    internal Func<string, bool> Unlock { get; init; } = _ => false;

    /// <summary>game.unlocked: whether a name has been unlocked</summary>
    internal Func<string, bool> IsUnlocked { get; init; } = _ => false;

    /// <summary>game.record: a map's best (Gameplay/LevelRecords.cs), null if never finished</summary>
    internal Func<string, Gameplay.LevelResult?> Record { get; init; } = _ => null;

    /// <summary>game.showtext: a text shown as an article in the middle of the level (A_ShowText)</summary>
    internal Action<string> ShowText { get; init; } = _ => { };

    /// <summary>game.removewall: takes the wall off a tile, an arch left or not (Gameplay/DestructibleWalls.cs); false if it can't go</summary>
    internal Func<int, int, bool, bool> RemoveWall { get; init; } = (_, _, _) => false;

    /// <summary>The scripts' Lua state, once any have loaded (or the lua command has made one)</summary>
    internal ScriptHost? Scripts => _scripts;

    /// <summary>
    /// Runs the scripts and registers their A_/T_ functions as actions. At startup, after the
    /// built-in actions are registered: anything it prints is a startup warning.
    /// </summary>
    internal void LoadScripts()
    {
        var scripts = assets.GetScripts();
        if (scripts.Count == 0)
            return;

        _scripts = new ScriptHost(text => console.Print(text));
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
    readonly List<string> scriptActions = [];

    // The actor fields scripts have read that there aren't, each told once
    readonly HashSet<string> unknownFieldsTold = new(StringComparer.Ordinal);

    void RunScriptAction(string function, Entities.Actors.Actor actor, string[] args)
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

    internal void InstallScriptApi(ScriptHost host)
    {
        var methods = new Dictionary<string, LuaFunction>(StringComparer.Ordinal)
        {
            ["jump"] = Method("jump", (c, self) =>
            {
                ChangesTheGame(c, "actor:jump");
                var state = StringArg(c, 1);
                if (!self.ResolvedStates.TryGetValue(state, out var frame))
                    return c.Return(false);
                self.JumpTo(frame);
                return c.Return(true);
            }),
            ["spawnprojectile"] = Method("spawnprojectile", (c, self) => ChangesTheGame(c, "actor:spawnprojectile") + c.Return(LuaActor.Of(
                projectileActions.SpawnProjectile(self, StringArg(c, 1), (int)Math.Round(NumberArg(c, 2, 0)), OptionalString(c, 3) ?? "relative")))),
            ["spawn"] = Method("spawn", (c, self) => ChangesTheGame(c, "actor:spawn") + c.Return(LuaActor.Of(map.SpawnAtActor(StringArg(c, 1), self)))),
            ["playsound"] = Method("playsound", (c, self) =>
            {
                var sound = StringArg(c, 1);
                if (Arg(c, 2).Type == LuaValueType.Boolean && !Arg(c, 2).ToBoolean())
                    audio.Play(sound);
                else
                    playSoundAt(sound, self);
                return c.Return();
            }),
            ["property"] = Method("property", (c, self) =>
                c.Return(self.Properties.TryGetValue(StringArg(c, 1), out var value) ? ToLua(value) : LuaValue.Nil)),
            ["hasflag"] = Method("hasflag", (c, self) => c.Return(self.HasFlag(StringArg(c, 1)))),
            ["distanceto"] = Method("distanceto", (c, self) =>
            {
                var other = ActorArg(c, 1);
                double dx = (other.X - self.X) / (double)MapConstants.TileGlobal, dy = (other.Y - self.Y) / (double)MapConstants.TileGlobal;
                return c.Return(Math.Sqrt(dx * dx + dy * dy));
            }),
            ["angleto"] = Method("angleto", (c, self) =>
            {
                var other = ActorArg(c, 1);
                var (x, y) = projectileActions.LookoutPoint(self);
                return c.Return(ProjectileActions.AngleTo(x, y, other.X, other.Y));
            }),
            ["cansee"] = Method("cansee", (c, self) =>
            {
                long reach = Arg(c, 2).Type == LuaValueType.Number ? (long)(NumberArg(c, 2, 0) * MapConstants.TileGlobal) : long.MaxValue;
                return c.Return(ActorArg(c, 1) is Entities.Actors.PlayerPawn pawn && session.IsTargetable(pawn) && projectileActions.CanSee(self, pawn, reach));
            }),
            ["nearestplayer"] = Method("nearestplayer", (c, self) =>
            {
                var (x, y) = projectileActions.LookoutPoint(self);
                return c.Return(LuaActor.Of(projectileActions.NearestPlayer(x, y)));
            }),
            // actor:pathto(x, y) or actor:pathto(other): the way to go by the shortest open path (A*)
            ["pathto"] = Method("pathto", (c, self) =>
            {
                var (x, y) = Arg(c, 1).TryRead<LuaActor>(out var other)
                    ? (other.Actor.TileX, other.Actor.TileY)
                    : ((int)Math.Floor(NumberArg(c, 1, -1)), (int)Math.Floor(NumberArg(c, 2, -1)));
                var way = self is Entities.Actors.Monster monster ? monster.PathTo(x, y)
                    : pathfinder.FirstStep(map.MapWidth, map.MapHeight, self.TileX, self.TileY, x, y, OpenTile, OpenDiagonally);
                return c.Return(way is { } dir ? Gameplay.Pathfinder.DirName(dir) : LuaValue.Nil);
            }),
            ["remove"] = Method("remove", (c, self) =>
            {
                ChangesTheGame(c, "actor:remove");
                if (self is not Entities.Actors.PlayerPawn)
                    map.MarkForRemoval(self);
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
                "x" => c.Return(self.X / (double)MapConstants.TileGlobal),
                "y" => c.Return(self.Y / (double)MapConstants.TileGlobal),
                "tilex" => c.Return(self.TileX),
                "tiley" => c.Return(self.TileY),
                "angle" => c.Return(ProjectileActions.FacingAngle(self)),
                "health" => c.Return(self is Entities.Actors.PlayerPawn pawn ? pawn.State.Health : self.Hitpoints),
                "speed" => c.Return(self.Speed),
                "z" => c.Return(self.Z),
                "state" => c.Return(self.CurrentState?.StateName is { } state ? state : LuaValue.Nil),
                "tag" => c.Return(self.Tag),
                "inwall" => c.Return(projectileActions.IsInWall(self)),
                "removed" => c.Return(self.IsRemoved),
                "isplayer" => c.Return(self is Entities.Actors.PlayerPawn),
                "vehicle" => c.Return(self is Entities.Actors.PlayerPawn { State.Vehicle: { } vehicle } ? vehicle : LuaValue.Nil),
                "shooter" => c.Return(LuaActor.Of(self.Shooter)),
                "faction" => c.Return(self.Faction is { } faction ? faction : LuaValue.Nil),
                "follows" => c.Return(self is Entities.Actors.Monster { Follows: true }),
                "translation" => c.Return((translations?.TranslationOf(self) ?? self.Translation) is { } translation ? translation : LuaValue.Nil),
                "temp1" => c.Return(self.Temp1),
                "temp2" => c.Return(self.Temp2),
                "temp3" => c.Return(self.Temp3),
                _ when methods.TryGetValue(key, out var method) => c.Return(method),
                _ => UnknownField(c, key),
            });
        });

        // A field an actor hasn't got (a typo: actor.hp) reads as nil, as Lua's own tables do, but
        // is told on the console, once a name, so the nil isn't a mystery further on
        int UnknownField(LuaFunctionExecutionContext c, string key)
        {
            if (unknownFieldsTold.Add(key))
                host.Print($"Script: an actor has no {key}, so it reads as nil (the scripting reference lists an actor's fields and methods)");
            return c.Return(LuaValue.Nil);
        }
        meta["__newindex"] = new LuaFunction("actor field", (c, _) =>
        {
            var self = ActorArg(c, 0);
            var key = StringArg(c, 1);
            ChangesTheGame(c, $"setting an actor's {key}");
            switch (key)
            {
                case "temp1": self.Temp1 = ShortArg(c, 2); break;
                case "temp2": self.Temp2 = ShortArg(c, 2); break;
                case "temp3": self.Temp3 = ShortArg(c, 2); break;
                case "speed": self.Speed = (int)Math.Round(NumberArg(c, 2, 0)); break;
                case "z": self.Z = NumberArg(c, 2, 0); break;
                case "translation": self.Translation = OptionalString(c, 2) is { Length: > 0 } translation ? translation : null; break;
                case "faction":
                    var faction = OptionalString(c, 2) is { Length: > 0 } side ? side : null;
                    if (self is Entities.Actors.Monster changing)
                        changing.SetFaction(faction);
                    else if (!string.Equals(self.Faction, faction, StringComparison.OrdinalIgnoreCase))
                    {
                        var was = self.Faction;
                        self.Faction = faction;
                        RunFactionHooks(self, was, faction);
                    }
                    break;
                case "follows":
                    if (self is Entities.Actors.Monster follower)
                        follower.Follows = Arg(c, 2).ToBoolean();
                    break;
                case "angle":
                    self.Angle = (short)(((int)Math.Round(NumberArg(c, 2, 0)) % GameConstants.Angles + GameConstants.Angles) % GameConstants.Angles);
                    if (self is not Entities.Actors.Monster and not Entities.Actors.PlayerPawn)
                        self.Dir = ObjDir.NoDir;       // its facing is its angle from now on
                    break;
                default:
                    throw new LuaRuntimeException(c.State, (LuaValue)$"an actor's {key} can't be set (temp1..temp3, angle, speed, z, translation, faction and follows can)", 1);
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
            ChangesTheGame(c, "game.random");
            if (c.ArgumentCount == 0)
                return new(c.Return(session.Random.RndT()));
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
            foreach (var pawn in map.Players.Where(session.IsTargetable))
                list[i++] = LuaActor.Of(pawn);
            return new(c.Return(list));
        });
        game["actors"] = new LuaFunction("game.actors", (c, _) =>
        {
            var className = OptionalString(c, 0);
            var list = new LuaTable();
            int i = 1;
            foreach (var actor in map.GetActors().Where(a => !a.IsRemoved
                && (className == null || a.Name.Equals(className, StringComparison.OrdinalIgnoreCase))))
                list[i++] = LuaActor.Of(actor);
            return new(c.Return(list));
        });
        game["tagged"] = new LuaFunction("game.tagged", (c, _) =>
        {
            var tag = TagArg(c, 0);
            var className = OptionalString(c, 1);
            var list = new LuaTable();
            int i = 1;
            foreach (var actor in map.TaggedActors(tag)
                .Where(a => className == null || a.Name.Equals(className, StringComparison.OrdinalIgnoreCase)))
                list[i++] = LuaActor.Of(actor);
            return new(c.Return(list));
        });
        game["taggedtiles"] = new LuaFunction("game.taggedtiles", (c, _) =>
        {
            var list = new LuaTable();
            int i = 1;
            foreach (var (x, y) in map.TaggedTiles(TagArg(c, 0)))
            {
                var tile = new LuaTable();
                tile["x"] = x;
                tile["y"] = y;
                list[i++] = tile;
            }
            return new(c.Return(list));
        });
        game["tag"] = new LuaFunction("game.tag", (c, _) =>
        {
            int x = (int)NumberArg(c, 0, -1), y = (int)NumberArg(c, 1, -1);
            if (!map.InMap(x, y))
                throw new LuaRuntimeException(c.State, (LuaValue)$"game.tag: {x},{y} isn't a tile on the map", 1);
            return new(c.Return(map.GetTag(x, y)));
        });
        game["patch"] = new LuaFunction("game.patch", (c, _) =>
        {
            int x = (int)NumberArg(c, 0, -1), y = (int)NumberArg(c, 1, -1);
            if (!map.InMap(x, y))
                throw new LuaRuntimeException(c.State, (LuaValue)$"game.patch: {x},{y} isn't a tile on the map", 1);
            return new(c.Return(map.GetPatch(x, y)));
        });
        // game.walkable(x, y[, actor]): whether a tile can be walked onto, by that actor's rules
        // (its doors, the monster blockers it's kept off) or anyone's; other actors don't count
        game["walkable"] = new LuaFunction("game.walkable", (c, _) =>
        {
            int x = (int)Math.Floor(NumberArg(c, 0, -1)), y = (int)Math.Floor(NumberArg(c, 1, -1));
            bool open = Arg(c, 2).TryRead<LuaActor>(out var walker) && walker.Actor is Entities.Actors.Monster monster
                ? monster.CanPassTile(x, y) : OpenTile(x, y);
            return new(c.Return(open));
        });
        // game.floor(x, y): the texture on a tile's floor (mapdefs flats, the map's default-floor,
        // through any A_ChangeTexture), nil where the floor color shows
        game["floor"] = new LuaFunction("game.floor", (c, _) =>
        {
            int x = (int)NumberArg(c, 0, -1), y = (int)NumberArg(c, 1, -1);
            if (!map.InMap(x, y))
                throw new LuaRuntimeException(c.State, (LuaValue)$"game.floor: {x},{y} isn't a tile on the map", 1);
            return new(c.Return(map.FlatTextureName(x, y, ceiling: false) is { } floor ? floor : LuaValue.Nil));
        });
        // game.removewall(x, y[, "arch"]): the wall on the tile is gone, as A_RemoveWall takes one;
        // false where there's none that can go (a door, open floor, the map's edge)
        game["removewall"] = new LuaFunction("game.removewall", (c, _) =>
        {
            ChangesTheGame(c, "game.removewall");
            int x = (int)NumberArg(c, 0, -1), y = (int)NumberArg(c, 1, -1);
            if (!map.InMap(x, y))
                throw new LuaRuntimeException(c.State, (LuaValue)$"game.removewall: {x},{y} isn't a tile on the map", 1);
            bool arch = OptionalString(c, 2)?.Equals("arch", StringComparison.OrdinalIgnoreCase) == true;
            return new(c.Return(RemoveWall(x, y, arch)));
        });
        game["trigger"] = new LuaFunction("game.trigger", (c, _) =>
        {
            ChangesTheGame(c, "game.trigger");
            var tag = TagArg(c, 0);
            var name = StringArg(c, 1);
            string[] args;
            if (c.ArgumentCount <= 2 && name.Contains('('))
                (name, args) = Entities.Actors.ActorActionRegistry.Parse(name);     // a whole call: 'A_OpenDoor("hold")'
            else
                args = [.. Enumerable.Range(2, Math.Max(c.ArgumentCount - 2, 0)).Select(i => OptionalString(c, i) ?? "")];

            if (!Entities.MapTriggerRegistry.IsRegistered(name))
                throw new LuaRuntimeException(c.State, (LuaValue)$"game.trigger: {name} isn't a switch action", 1);
            if (OwnTileActions.TryGetValue(name, out var instead))
                throw new LuaRuntimeException(c.State, (LuaValue)$"game.trigger: {name} works on a switch's own tile, which a script hasn't got{instead}", 1);

            // As if the first player had used a switch with that tag, from nowhere on the map: the
            // same player on every machine, so a network game stays in step
            var activator = map.Players.FirstOrDefault();
            if (activator == null)
                return new(c.Return(false));
            var activation = new Entities.TriggerActivation(-1, -1, PlayerMovement.FacingDir(activator.Angle), activator, tag);
            return new(c.Return(Entities.MapTriggerRegistry.Invoke(name, args, activation)));
        });
        InstallHookFunctions(game, host);       // game.onlevel, game.ontick... and what the hooks use (Scripting/GameScripts.Hooks.cs)
        // game.record(map): the best done on a map, from game to game (records.cfg:
        // Gameplay/LevelRecords.cs), as game.onlevelend's result; nil if it's never been finished
        game["record"] = new LuaFunction("game.record", (c, _) =>
            new(c.Return(Record(StringArg(c, 0)) is { } record ? ResultTable(record) : LuaValue.Nil)));
        game["time"] = new LuaFunction("game.time", (c, _) => new(c.Return(session.GameState.TimeCount)));
        game["tics"] = new LuaFunction("game.tics", (c, _) => new(c.Return(session.Tics)));
        game["map"] = new LuaFunction("game.map", (c, _) => new(c.Return(session.GameState.MapOn ?? "")));
        game["skill"] = new LuaFunction("game.skill", (c, _) => new(c.Return(session.GameState.Difficulty)));
        // game.ambient(sound) loops a sound at the listener as the ambience, in place of the one
        // there was; game.ambient(nil) stops it. Asking for the one playing changes nothing, so
        // a script can ask every tick (Managers/AudioManager.cs SetAmbient).
        game["ambient"] = new LuaFunction("game.ambient", (c, _) =>
        {
            audio.SetAmbient(OptionalString(c, 0));
            return new(c.Return());
        });
        // game.objective(name): how many of a level objective are done, and of how many
        // (game-info objectives: Gameplay/Objectives.cs); 0, 0 for one the level hasn't
        game["objective"] = new LuaFunction("game.objective", (c, _) =>
        {
            var name = StringArg(c, 0);
            return map.ObjectiveCounts.TryGetValue(name, out var count) ? new(c.Return(count.Done, count.Total)) : new(c.Return(0, 0));
        });
        // game.unlock(name): unlocks a name from game to game (a starts-locked episode's key, or a
        // script's own), true if it wasn't already; game.unlocked(name): whether it is
        // (progress.cfg: Gameplay/Unlocks.cs)
        game["unlock"] = new LuaFunction("game.unlock", (c, _) => new(ChangesTheGame(c, "game.unlock") + c.Return(Unlock(StringArg(c, 0)))));
        game["unlocked"] = new LuaFunction("game.unlocked", (c, _) => new(c.Return(IsUnlocked(StringArg(c, 0)))));
        // game.showtext(name): a text shown as an article, then back to the level (A_ShowText)
        game["showtext"] = new LuaFunction("game.showtext", (c, _) =>
        {
            ChangesTheGame(c, "game.showtext");
            ShowText(StringArg(c, 0));
            return new(c.Return());
        });
        game["message"] = new LuaFunction("game.message", (c, _) =>
        {
            // With a key: the message with that key, kept up to date where it is (a clock), shown with messages
            // off too and not printed; nil or empty text takes it down
            if (OptionalString(c, 2) is { Length: > 0 } key)
                hudMessages.ShowKeyed(key, OptionalString(c, 0), OptionalString(c, 1));
            else
                hudMessages.Show(Managers.HudMessageKind.Lock, StringArg(c, 0), OptionalString(c, 1));
            return new(c.Return());
        });
        host.Globals["game"] = game;
    }

    // Switch actions that work on the switch's own tile rather than its tag, which game.trigger
    // can't run, with what to use instead (if there's one)
    internal static readonly Dictionary<string, string> OwnTileActions = new(StringComparer.Ordinal)
    {
        ["A_PushWall"] = " (A_MoveWall pushes the tagged walls)",
        ["A_Vend"] = "",
        ["A_SmartSwitch"] = "",
        ["A_FloorSelect"] = "",
        ["A_Teleport"] = "",
        ["A_TeleportTag"] = "",
        ["A_Warp"] = "",
    };

    // A* for actor:pathto from actors that aren't monsters (monsters use their world's, by their own rules)
    readonly Gameplay.Pathfinder pathfinder = new();

    // A tile anyone could walk onto: on the map, not a wall or a solid thing (a door is open to all)
    bool OpenTile(int x, int y) => map.InMap(x, y) && map.ActorAt[x, y] is not (Wall or BlockingActor);

    // A tile anyone could step onto diagonally: open floor (no door)
    bool OpenDiagonally(int x, int y) => map.InMap(x, y) && map.ActorAt[x, y] is null or ActorMark;

    // 0 to count - 1 from the game's random numbers: one draw for up to 256, more beyond
    int GameRandom(int count)
    {
        if (count <= 256)
            return session.Random.RndT() * count / 256;
        long value = 0, range = 1;
        while (range < count)
        {
            value = value << 8 | (uint)session.Random.RndT();
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

    // A tag on the tag plane: 1 to 65535 (0 is no tag, and finds nothing)
    static ushort TagArg(LuaFunctionExecutionContext c, int index)
    {
        double tag = NumberArg(c, index, -1);
        if (tag < 0 || tag > ushort.MaxValue || tag != Math.Floor(tag))
            throw new LuaRuntimeException(c.State, (LuaValue)$"argument {index + 1} should be a tag: a whole number, 1 to {ushort.MaxValue}", 1);
        return (ushort)tag;
    }

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

    internal void ScriptsCommand(string[] args)
    {
        if (_scripts == null)
        {
            console.Print("No scripts are loaded (scripts/*.lua in pfwolf.pk3 or a mod)");
            return;
        }
        foreach (var where in _scripts.Loaded)
            console.Print(where);
        foreach (var failed in _scripts.Failed)
            console.Print($"left out: {failed}");
        console.Print(scriptActions.Count == 0 ? "No script actions" : $"Actions: {string.Join(", ", scriptActions)}");
    }

    internal void LuaCommand(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: lua <code>");
        _scripts ??= NewConsoleScriptHost();
        // `me` is the player (nil outside a level), for trying the actor methods out
        _scripts.Globals["me"] = LuaActor.Of(session.LocalPlayer?.Pawn);
        if (_scripts.Execute(string.Join(' ', args)) is { } error)
            console.Print(error);
    }

    // With no scripts loaded, the lua command still has a state to try things in
    ScriptHost NewConsoleScriptHost()
    {
        var host = new ScriptHost(text => console.Print(text));
        InstallScriptApi(host);
        return host;
    }
}
