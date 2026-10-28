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
internal sealed class GameScripts(
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
                var state = StringArg(c, 1);
                if (!self.ResolvedStates.TryGetValue(state, out var frame))
                    return c.Return(false);
                self.JumpTo(frame);
                return c.Return(true);
            }),
            ["spawnprojectile"] = Method("spawnprojectile", (c, self) => c.Return(LuaActor.Of(
                projectileActions.SpawnProjectile(self, StringArg(c, 1), (int)Math.Round(NumberArg(c, 2, 0)), OptionalString(c, 3) ?? "relative")))),
            ["spawn"] = Method("spawn", (c, self) => c.Return(LuaActor.Of(map.SpawnAtActor(StringArg(c, 1), self)))),
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
            ["remove"] = Method("remove", (c, self) =>
            {
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
                    else
                        self.Faction = faction;
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
        // game.floor(x, y): the texture on a tile's floor (mapdefs flats, the map's default-floor,
        // through any A_ChangeTexture), nil where the floor color shows
        game["floor"] = new LuaFunction("game.floor", (c, _) =>
        {
            int x = (int)NumberArg(c, 0, -1), y = (int)NumberArg(c, 1, -1);
            if (!map.InMap(x, y))
                throw new LuaRuntimeException(c.State, (LuaValue)$"game.floor: {x},{y} isn't a tile on the map", 1);
            return new(c.Return(map.FlatTextureName(x, y, ceiling: false) is { } floor ? floor : LuaValue.Nil));
        });
        game["trigger"] = new LuaFunction("game.trigger", (c, _) =>
        {
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
        game["onlevel"] = new LuaFunction("game.onlevel", (c, _) => new(c.Return(AddHook(c, host, levelHooks, "game.onlevel"))));
        game["ontick"] = new LuaFunction("game.ontick", (c, _) => new(c.Return(AddHook(c, host, tickHooks, "game.ontick"))));
        game["onlevelend"] = new LuaFunction("game.onlevelend", (c, _) => new(c.Return(AddHook(c, host, levelEndHooks, "game.onlevelend"))));
        game["ondamage"] = new LuaFunction("game.ondamage", (c, _) => new(c.Return(AddHook(c, host, damageHooks, "game.ondamage"))));
        game["onkill"] = new LuaFunction("game.onkill", (c, _) => new(c.Return(AddHook(c, host, killHooks, "game.onkill"))));
        game["ontrypickup"] = new LuaFunction("game.ontrypickup", (c, _) => new(c.Return(AddHook(c, host, tryPickupHooks, "game.ontrypickup"))));
        game["onpickup"] = new LuaFunction("game.onpickup", (c, _) => new(c.Return(AddHook(c, host, pickupHooks, "game.onpickup"))));
        game["onsecret"] = new LuaFunction("game.onsecret", (c, _) => new(c.Return(AddHook(c, host, secretHooks, "game.onsecret"))));
        game["onuse"] = new LuaFunction("game.onuse", (c, _) => new(c.Return(AddHook(c, host, useHooks, "game.onuse"))));
        game["onplayerdeath"] = new LuaFunction("game.onplayerdeath", (c, _) => new(c.Return(AddHook(c, host, playerDeathHooks, "game.onplayerdeath"))));
        game["onspawn"] = new LuaFunction("game.onspawn", (c, _) => new(c.Return(AddHook(c, host, spawnHooks, "game.onspawn"))));
        game["onsight"] = new LuaFunction("game.onsight", (c, _) => new(c.Return(AddHook(c, host, sightHooks, "game.onsight"))));
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
        game["unlock"] = new LuaFunction("game.unlock", (c, _) => new(c.Return(Unlock(StringArg(c, 0)))));
        game["unlocked"] = new LuaFunction("game.unlocked", (c, _) => new(c.Return(IsUnlocked(StringArg(c, 0)))));
        // game.showtext(name): a text shown as an article, then back to the level (A_ShowText)
        game["showtext"] = new LuaFunction("game.showtext", (c, _) =>
        {
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
    game.onlevelend(function(map, result) ... end): called as a level's finished (its exit, a
        secret exit or the game won; not dying or leaving), before the tally, once its record is
        kept (game.record). result has kills, killtotal, secrets, secrettotal, items, itemtotal,
        time (tics), par (seconds, 0 for none), underpar, undetected, stealthkills, objectives
        ({name = {done = n, total = n}}) and exit ("exit", "secret" or "victory"). It's the
        place to game.unlock what the finish earned: anything unlocked is told on the tally.
        Not called while a demo plays back.

    As things happen on the level (each machine of a network game alike, and a demo as when it
    was recorded):
    game.ondamage(function(victim, amount, attacker, type) ... end): an actor or a player about to
        be hurt, by how much (an enemy's after its doubling while it isn't attacking; a player's
        after the skill's and class's share, before armor), by what (the projectile, the player
        or enemy, nil for a floor or a trap) and its damage type (nil for none). Returning a
        number makes that the amount (0: unhurt, though an enemy still turns on the attacker);
        each hook gets the amount the one before left.
    game.onkill(function(victim, killer, type) ... end): an enemy (or any monster) killed, by whom
        (the one who fired a projectile, rather than the projectile; nil for nothing) and with
        what damage type. Not players: game.onplayerdeath.
    game.ontrypickup(function(player, item) ... end): a player walking over an item they may
        take; returning false leaves it where it is. Asked again each tic they stand on it, and
        an item they then can't use (ammo full, a key held) stays anyway.
    game.onpickup(function(player, item, amount) ... end): a player has taken an item, which gave
        amount (nil for an item with no amount), just before it goes from the level.
    game.onsecret(function(player, x, y) ... end): a secret found, at that tile.
    game.onuse(function(player, x, y, dir) ... end): a fresh press of use, facing the tile x, y
        ("north", "east", "south" or "west" of the player), before the game looks at what's
        there; returning true says the script used it, and the game does nothing else.
    game.onplayerdeath(function(player, killer) ... end): a player killed, by what (nil for
        nothing: time running out, a fall).
    game.onspawn(function(player, reason) ... end): a player put on the level: as it starts (just
        before game.onlevel: "new", "returned" or "restarted"; not "loaded"), brought back after
        dying with others ("respawn"), or joining a network game ("join").
    game.onsight(function(monster, target) ... end): an enemy noticing its foe (a player, or an
        actor of a side it fights) and setting off after it.

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

    readonly List<ScriptHook> levelHooks = [];
    readonly List<ScriptHook> tickHooks = [];
    readonly List<ScriptHook> levelEndHooks = [];
    readonly List<ScriptHook> damageHooks = [];
    readonly List<ScriptHook> killHooks = [];
    readonly List<ScriptHook> tryPickupHooks = [];
    readonly List<ScriptHook> pickupHooks = [];
    readonly List<ScriptHook> secretHooks = [];
    readonly List<ScriptHook> useHooks = [];
    readonly List<ScriptHook> playerDeathHooks = [];
    readonly List<ScriptHook> spawnHooks = [];
    readonly List<ScriptHook> sightHooks = [];

    // Errors a hook may have before it's switched off
    const int HookErrorLimit = 3;

    int AddHook(LuaFunctionExecutionContext c, ScriptHost host, List<ScriptHook> hooks, string what)
    {
        var function = Arg(c, 0);
        if (function.Type != LuaValueType.Function)
            throw new LuaRuntimeException(c.State, (LuaValue)$"{what} takes a function", 1);
        if (host.Running == null)
            throw new LuaRuntimeException(c.State, (LuaValue)$"{what}: hooks are added when a script loads (at its top level), not later", 1);
        hooks.Add(new ScriptHook(function, $"{what} hook {hooks.Count + 1} ({host.Running})"));
        return c.Return();
    }

    /// <summary>
    /// The scripts' level hooks, as a level starts (GameLoop, PlayDemo, RecordDemo): first their
    /// spawn hooks for each player on it, unless it was loaded
    /// </summary>
    internal void RunLevelHooks(string reason)
    {
        if (spawnHooks.Count > 0 && reason != "loaded")
            foreach (var player in map.Players)
                RunSpawnHooks(player, reason);
        RunHooks(levelHooks, [reason, session.GameState.MapOn ?? ""]);
    }

    /// <summary>
    /// The scripts' level-end hooks, as a level's finished (before the tally): the map, what this
    /// finish came to, and how it was left ("exit", "secret" or "victory")
    /// </summary>
    internal void RunLevelEndHooks(string map, Gameplay.LevelResult result, string exit)
    {
        if (levelEndHooks.Count > 0)
            RunHooks(levelEndHooks, [map, ResultTable(result, exit)]);
    }

    /// <summary>A level's result (or record) as Lua sees it, for game.onlevelend and game.record</summary>
    static LuaTable ResultTable(Gameplay.LevelResult result, string? exit = null)
    {
        var objectives = new LuaTable();
        foreach (var (name, count) in result.Objectives)
        {
            var entry = new LuaTable();
            entry["done"] = count.Done;
            entry["total"] = count.Total;
            objectives[name] = entry;
        }

        var table = new LuaTable();
        table["kills"] = result.Kills;
        table["killtotal"] = result.KillTotal;
        table["secrets"] = result.Secrets;
        table["secrettotal"] = result.SecretTotal;
        table["items"] = result.Items;
        table["itemtotal"] = result.ItemTotal;
        table["time"] = result.Time;
        table["par"] = result.Par;
        table["underpar"] = result.UnderPar;
        table["undetected"] = result.Undetected;
        table["stealthkills"] = result.StealthKills;
        table["objectives"] = objectives;
        if (exit != null)
            table["exit"] = exit;
        return table;
    }

    /// <summary>The scripts' tick hooks, each frame the world moves (PlayLoop)</summary>
    internal void RunTickHooks()
    {
        if (tickHooks.Count > 0)
            RunHooks(tickHooks, [session.Tics]);
    }

    /// <summary>
    /// The scripts' damage hooks, as an actor or player is about to be hurt (Monster.Damage,
    /// PlayerStats.ApplyDamageToPlayer): the amount they leave it at, 0 or more
    /// </summary>
    internal int RunDamageHooks(Entities.Actors.Actor victim, int amount, Entities.Actors.Actor? attacker, string? type)
    {
        if (damageHooks.Count == 0)
            return amount;
        LuaValue[] arguments = [LuaActor.Of(victim), amount, LuaActor.Of(attacker), type ?? LuaValue.Nil];
        RunHooks(damageHooks, arguments, result =>
        {
            if (result.TryRead<double>(out var number))
                arguments[1] = amount = (int)Math.Clamp(Math.Round(number), 0, short.MaxValue);
            return true;
        });
        return amount;
    }

    /// <summary>The scripts' kill hooks, as a monster's killed (Monster.Kill)</summary>
    internal void RunKillHooks(Entities.Actors.Actor victim, Entities.Actors.Actor? killer, string? type)
    {
        if (killHooks.Count > 0)
            RunHooks(killHooks, [LuaActor.Of(victim), LuaActor.Of(killer), type ?? LuaValue.Nil]);
    }

    /// <summary>The scripts' try-pickup hooks, as a player walks over an item (Pickups.GetBonus): false if one leaves it there</summary>
    internal bool RunTryPickupHooks(Entities.Actors.Actor player, Entities.Actors.Actor item)
    {
        if (tryPickupHooks.Count == 0)
            return true;
        bool take = true;
        RunHooks(tryPickupHooks, [LuaActor.Of(player), LuaActor.Of(item)], result => take = !IsFalse(result));
        return take;
    }

    /// <summary>The scripts' pickup hooks, as a player has taken an item (Pickups.GetBonus)</summary>
    internal void RunPickupHooks(Entities.Actors.Actor player, Entities.Actors.Actor item, int? amount)
    {
        if (pickupHooks.Count > 0)
            RunHooks(pickupHooks, [LuaActor.Of(player), LuaActor.Of(item), amount is { } given ? given : LuaValue.Nil]);
    }

    /// <summary>The scripts' secret hooks, as a secret's found (PlayerUse.ActivateTrigger)</summary>
    internal void RunSecretHooks(Entities.Actors.Actor player, int x, int y)
    {
        if (secretHooks.Count > 0)
            RunHooks(secretHooks, [LuaActor.Of(player), x, y]);
    }

    /// <summary>The scripts' use hooks, as use is pressed facing a tile (PlayerUse.PressUse): true if one used it</summary>
    internal bool RunUseHooks(Entities.Actors.Actor player, int x, int y, ControlDir dir)
    {
        if (useHooks.Count == 0)
            return false;
        bool used = false;
        RunHooks(useHooks, [LuaActor.Of(player), x, y, dir.ToString().ToLowerInvariant()], result => !(used = IsTrue(result)));
        return used;
    }

    /// <summary>The scripts' player death hooks, as a player's killed (PlayerStats)</summary>
    internal void RunPlayerDeathHooks(Entities.Actors.Actor player, Entities.Actors.Actor? killer)
    {
        if (playerDeathHooks.Count > 0)
            RunHooks(playerDeathHooks, [LuaActor.Of(player), LuaActor.Of(killer)]);
    }

    /// <summary>The scripts' spawn hooks, as a player is put on the level (RunLevelHooks, Players)</summary>
    internal void RunSpawnHooks(Entities.Actors.Actor player, string reason)
    {
        if (spawnHooks.Count > 0)
            RunHooks(spawnHooks, [LuaActor.Of(player), reason]);
    }

    /// <summary>The scripts' sight hooks, as an enemy notices its foe (Monster.FirstSighting)</summary>
    internal void RunSightHooks(Entities.Actors.Actor monster, Entities.Actors.Actor? target)
    {
        if (sightHooks.Count > 0)
            RunHooks(sightHooks, [LuaActor.Of(monster), LuaActor.Of(target)]);
    }

    static bool IsFalse(LuaValue value) => value.Type == LuaValueType.Boolean && !value.Read<bool>();

    static bool IsTrue(LuaValue value) => value.Type == LuaValueType.Boolean && value.Read<bool>();

    // Calls each hook in turn, handing what it returned to `then`, which says (false) when to
    // stop asking the rest
    void RunHooks(List<ScriptHook> hooks, LuaValue[] arguments, Func<LuaValue, bool>? then = null)
    {
        if (_scripts == null)
            return;
        for (int i = 0; i < hooks.Count; i++)
        {
            var hook = hooks[i];
            if (_scripts.Call(hook.Function, hook.Name, arguments, out var result))
            {
                if (then != null && !then(result))
                    return;
                continue;
            }
            if (++hook.Errors < HookErrorLimit)
                continue;
            console.Print($"Script {hook.Name} is switched off after {HookErrorLimit} errors");
            hooks.RemoveAt(i--);
        }
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
