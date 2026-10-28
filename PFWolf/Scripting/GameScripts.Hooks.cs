using System.Globalization;
using Lua;

namespace PFWolf.Scripting;

internal sealed partial class GameScripts
{
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
    game.ondoor(function(x, y, opening, by) ... end): a door about to open (opening true) or close,
        and what set it going (the player using it, an enemy walking through; nil for a switch, a
        linked door or its own closing); returning false leaves it as it is. Asked again each
        time something tries.
    game.onpushwall(function(player, x, y, dir) ... end): a pushwall about to slide from x, y the
        way dir says; returning false leaves it where it is (a secret it is isn't found).
    game.onswitch(function(player, x, y, tag) ... end): a switch (a wall's or a patch's) about to
        be thrown, once the player has its lock item; returning false leaves it be.
    game.onenter(function(actor, x, y) ... end): a player stepping onto another tile, or an enemy
        walking to the middle of one.
    game.onexit(function(player, kind) ... end): an exit about to end the level (A_Exit,
        A_SecretExit; kind "exit" or "secret"), once its objectives allow it; returning false
        keeps the level going.
    game.onteleport(function(actor, fromx, fromy, tox, toy) ... end): a player (or an enemy on a
        pad) moved across the level by a teleporter (A_Teleport, A_TeleportTag, A_Warp).
    game.onactorspawn(function(actor) ... end): an actor put on the level: as a new or restarted
        level starts (each one there, just before game.onspawn), and any spawned after (an enemy,
        a projectile, an item dropped), at the end of the tic it came in (still on the level).
        Not players. One this hook spawns is told the tic after.
    game.onrevive(function(monster, reviver) ... end): a dead monster brought back (nil reviver: no one).
    game.onfactionchange(function(actor, from, to) ... end): an actor gone over to another side.
    game.onfire(function(player, weapon) ... end): a press of fire about to start an attack with
        the weapon (its class), or held fire carrying an automatic weapon's on; returning false
        stops it.
    game.onweaponswitch(function(player, from, to) ... end): the weapon in a player's hand
        changed, seen at the end of their think (not the first one they're seen with, nor
        none: a death).
    game.ongive(function(player, item, amount) ... end): an item given to a player, however it
        came (picked up, a switch, a kill chain's prize, an actor's gift): its class and amount.
    game.onpowerup(function(player, name, started) ... end): a power-up starting (or adding to
        its time), or running out.
    game.onobjective(function(name, done, total) ... end): more of a level objective done.
    game.onchain(function(player, count) ... end): a kill carrying a player's kill chain on (2 and up).
    game.onstealthkill(function(player, victim) ... end): a kill made unseen.
    game.ontimeup(function() ... end): a timed level's clock run out; returning true says the
        script sees to it, and nobody dies (nor is a time-limit-completes level got through).
    game.onfrag(function(victim, killer) ... end): a frag counted in a versus game (killer the
        victim: one taken off for killing themselves).
    game.onflag(function(event, player, team) ... end): capture the flag: a team's flag "taken",
        "dropped", "returned" (by a player of its team), "home" (back by itself) or "captured".
    game.onmatchend(function(winner) ... end): a match over: the winning team ("red", "blue"), or
        in deathmatch the player with the most frags; nil for a draw.

    On this machine alone, for what it shows: these mustn't change the game (game.random,
    game.trigger, spawning, setting an actor's fields and the like are an error in them):
    game.onhud(function() ... end): each frame drawn, over the view: game.drawtext and
        game.drawpic draw there, at x, y from its top left (game.viewsize says how big it is).
    game.onmusic(function(map, song) ... end): the level's song about to start (or start again
        after the menu); returning a song's name plays that instead.
    game.ontally(function(map, result) ... end): a level's finished (as game.onlevelend's), before
        its tally: text returned is shown on the tally (intermission.yaml's `script` value).

    Hooks are added when a script loads (at its top level), never later, so that every machine
    and every demo run has the same ones; each mod's run, in load order. A hook that errors
    three times is switched off.

    game.command(name, help, function(...) ... end): a console command the script gives (not
    while playing with others), called with the words typed after it, numbers as numbers. A
    command the game has already wins.
    game.music(song): plays a song now, for the rest of the level (nil: the level's own again).

    =============================================================================
    */

    /// <summary>Every hook a script can add (game.NAME(function ... end)), as the API lists them</summary>
    internal static readonly string[] HookNames =
    [
        "onlevel", "ontick", "onlevelend", "ondamage", "onkill", "ontrypickup", "onpickup", "onsecret", "onuse",
        "onplayerdeath", "onspawn", "onsight", "ondoor", "onpushwall", "onswitch", "onenter", "onexit", "onteleport",
        "onactorspawn", "onrevive", "onfactionchange", "onfire", "onweaponswitch", "ongive", "onpowerup",
        "onobjective", "onchain", "onstealthkill", "ontimeup", "onfrag", "onflag", "onmatchend",
        "onhud", "onmusic", "ontally",
    ];

    sealed class ScriptHook(LuaValue function, string name)
    {
        public LuaValue Function { get; } = function;
        public string Name { get; } = name;
        public int Errors { get; set; }
    }

    readonly Dictionary<string, List<ScriptHook>> hookLists = HookNames.ToDictionary(name => name, _ => new List<ScriptHook>(), StringComparer.Ordinal);

    List<ScriptHook> Hooks(string name) => hookLists[name];

    // Errors a hook may have before it's switched off
    const int HookErrorLimit = 3;

    // The hook running now that runs on this machine alone (game.onhud...), which mustn't change
    // the game; null the rest of the time
    string? _localHook;

    // An error, inside a hook that runs on this machine alone, for something that changes the game
    // (another machine, or the demo, would come out differently): 0 otherwise, to add to a Return
    int ChangesTheGame(LuaFunctionExecutionContext c, string what) => _localHook is { } hook
        ? throw new LuaRuntimeException(c.State, (LuaValue)$"{what} changes the game, which {hook} mustn't: it runs on this machine alone", 1)
        : 0;

    /// <summary>game.drawtext in game.onhud: text at a point of the screen (layout pixels), in a font and color</summary>
    internal Action<string, int, int, string?, string?> DrawText { get; init; } = (_, _, _, _, _) => { };

    /// <summary>game.drawpic in game.onhud: a picture at a point of the screen (layout pixels)</summary>
    internal Action<string, int, int> DrawPic { get; init; } = (_, _, _) => { };

    /// <summary>The 3D view on the screen, in layout pixels (game.viewsize, and where game.onhud draws from)</summary>
    internal Func<(int X, int Y, int Width, int Height)> ViewRect { get; init; } = () => (0, 0, 320, 160);

    /// <summary>game.music: plays a song now (the level's own again for null)</summary>
    internal Action<string?> PlayMusic { get; init; } = _ => { };

    /// <summary>The song game.music put on for the rest of the level, if any</summary>
    internal string? ScriptSong { get; private set; }

    // game.command's commands, given to the console once its own are in (RegisterCommands)
    readonly List<(string Name, string Help, LuaValue Function)> scriptCommands = [];

    void InstallHookFunctions(LuaTable game, ScriptHost host)
    {
        foreach (var name in HookNames)
        {
            var what = "game." + name;
            var hooks = Hooks(name);
            game[name] = new LuaFunction(what, (c, _) => new(c.Return(AddHook(c, host, hooks, what))));
        }

        game["drawtext"] = new LuaFunction("game.drawtext", (c, _) =>
        {
            var (viewX, viewY, _, _) = HudView(c, "game.drawtext");
            DrawText(StringArg(c, 0), viewX + (int)NumberArg(c, 1, 0), viewY + (int)NumberArg(c, 2, 0), OptionalString(c, 3), OptionalString(c, 4));
            return new(c.Return());
        });
        game["drawpic"] = new LuaFunction("game.drawpic", (c, _) =>
        {
            var (viewX, viewY, _, _) = HudView(c, "game.drawpic");
            DrawPic(StringArg(c, 0), viewX + (int)NumberArg(c, 1, 0), viewY + (int)NumberArg(c, 2, 0));
            return new(c.Return());
        });
        game["viewsize"] = new LuaFunction("game.viewsize", (c, _) =>
        {
            var (_, _, width, height) = ViewRect();
            return new(c.Return(width, height));
        });
        game["music"] = new LuaFunction("game.music", (c, _) =>
        {
            ScriptSong = OptionalString(c, 0) is { Length: > 0 } song ? song : null;
            PlayMusic(ScriptSong);
            return new(c.Return());
        });
        game["command"] = new LuaFunction("game.command", (c, _) =>
        {
            var name = StringArg(c, 0).Trim();
            var help = OptionalString(c, 1) ?? "";
            var function = Arg(c, 2);
            if (function.Type != LuaValueType.Function)
                throw new LuaRuntimeException(c.State, (LuaValue)"game.command takes a name, its help and a function", 1);
            if (host.Running == null)
                throw new LuaRuntimeException(c.State, (LuaValue)"game.command: commands are added when a script loads (at its top level), not later", 1);
            if (name.Length == 0 || name.Any(char.IsWhiteSpace))
                throw new LuaRuntimeException(c.State, (LuaValue)$"game.command: \"{name}\" isn't a name to type (one word)", 1);
            scriptCommands.RemoveAll(command => command.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            scriptCommands.Add((name, help, function));
            return new(c.Return());
        });
    }

    // Where game.onhud draws from (the view's top left); an error outside game.onhud
    (int X, int Y, int Width, int Height) HudView(LuaFunctionExecutionContext c, string what) => _localHook == "game.onhud"
        ? ViewRect()
        : throw new LuaRuntimeException(c.State, (LuaValue)$"{what} only draws in a game.onhud hook", 1);

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
    /// Gives the console the scripts' commands (game.command), once the game's own are in: one
    /// of the game's wins, with a warning
    /// </summary>
    internal void RegisterCommands()
    {
        foreach (var (name, help, function) in scriptCommands)
        {
            if (console.TryGetCommand(name, out _))
            {
                Console.WriteLine($"Script command {name} is ignored: the game has a command of that name");
                continue;
            }
            console.Register(new Managers.ConsoleCommand(name, help.Length > 0 ? help : "A script's command (game.command).", name,
                args => _scripts?.Call(function, $"command {name}", [.. args.Select(ArgToLua)]), Managers.ConsoleCommandFlags.SinglePlayer)
            {
                Group = "Script commands",
            });
        }
    }

    // A word typed after a command, as Lua gets it: a number as a number
    static LuaValue ArgToLua(string arg) =>
        double.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : arg;

    /*
    -----------------------------------------------------------------------------
    The level coming and going
    -----------------------------------------------------------------------------
    */

    // The level (MapManager.LevelSerial) whose actors have been told to the actor-spawn hooks:
    // those spawned on it from now on are told as they come
    int _toldLevel = -1;

    // Whether game.ontimeup's hooks have seen to this level's time running out
    bool _timeUpSeenTo;

    /// <summary>
    /// The scripts' level hooks, as a level starts (GameLoop, PlayDemo, RecordDemo): first, unless
    /// it was loaded, their actor-spawn hooks for each actor a new or restarted level has, then
    /// their spawn hooks for each player on it
    /// </summary>
    internal void RunLevelHooks(string reason)
    {
        ScriptSong = null;
        _timeUpSeenTo = false;
        spawned.Clear();
        if (Hooks("onactorspawn").Count > 0)
        {
            _toldLevel = map.LevelSerial;
            if (reason is "new" or "restarted")
                foreach (var actor in map.GetActors().Where(a => !a.IsRemoved && a is not Entities.Actors.PlayerPawn).ToList())
                    RunActorSpawnHooks(actor);
        }
        if (Hooks("onspawn").Count > 0 && reason != "loaded")
            foreach (var player in map.Players)
                RunSpawnHooks(player, reason);
        RunHooks(Hooks("onlevel"), [reason, session.GameState.MapOn ?? ""]);
    }

    /// <summary>
    /// The scripts' level-end hooks, as a level's finished (before the tally): the map, what this
    /// finish came to, and how it was left ("exit", "secret" or "victory")
    /// </summary>
    internal void RunLevelEndHooks(string map, Gameplay.LevelResult result, string exit)
    {
        if (Hooks("onlevelend").Count > 0)
            RunHooks(Hooks("onlevelend"), [map, ResultTable(result, exit)]);
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

    /// <summary>The scripts' tick hooks, each frame the world moves (PlayLoop): after the actor-spawn hooks hear of this tic's</summary>
    internal void RunTickHooks()
    {
        TellSpawned();
        if (Hooks("ontick").Count > 0)
            RunHooks(Hooks("ontick"), [session.Tics]);
    }

    /*
    -----------------------------------------------------------------------------
    What happens on the level
    -----------------------------------------------------------------------------
    */

    /// <summary>
    /// The scripts' damage hooks, as an actor or player is about to be hurt (Monster.Damage,
    /// PlayerStats.ApplyDamageToPlayer): the amount they leave it at, 0 or more
    /// </summary>
    internal int RunDamageHooks(Entities.Actors.Actor victim, int amount, Entities.Actors.Actor? attacker, string? type)
    {
        if (Hooks("ondamage").Count == 0)
            return amount;
        LuaValue[] arguments = [LuaActor.Of(victim), amount, LuaActor.Of(attacker), type ?? LuaValue.Nil];
        RunHooks(Hooks("ondamage"), arguments, result =>
        {
            if (result.TryRead<double>(out var number))
                arguments[1] = amount = (int)Math.Clamp(Math.Round(number), 0, short.MaxValue);
            return true;
        });
        return amount;
    }

    /// <summary>The scripts' kill hooks, as a monster's killed (Monster.Kill)</summary>
    internal void RunKillHooks(Entities.Actors.Actor victim, Entities.Actors.Actor? killer, string? type) =>
        Tell("onkill", () => [LuaActor.Of(victim), LuaActor.Of(killer), type ?? LuaValue.Nil]);

    /// <summary>The scripts' try-pickup hooks, as a player walks over an item (Pickups.GetBonus): false if one leaves it there</summary>
    internal bool RunTryPickupHooks(Entities.Actors.Actor player, Entities.Actors.Actor item) =>
        Allowed("ontrypickup", () => [LuaActor.Of(player), LuaActor.Of(item)]);

    /// <summary>The scripts' pickup hooks, as a player has taken an item (Pickups.GetBonus)</summary>
    internal void RunPickupHooks(Entities.Actors.Actor player, Entities.Actors.Actor item, int? amount) =>
        Tell("onpickup", () => [LuaActor.Of(player), LuaActor.Of(item), amount is { } given ? given : LuaValue.Nil]);

    /// <summary>The scripts' secret hooks, as a secret's found (PlayerUse.ActivateTrigger)</summary>
    internal void RunSecretHooks(Entities.Actors.Actor player, int x, int y) =>
        Tell("onsecret", () => [LuaActor.Of(player), x, y]);

    /// <summary>The scripts' use hooks, as use is pressed facing a tile (PlayerUse.PressUse): true if one used it</summary>
    internal bool RunUseHooks(Entities.Actors.Actor player, int x, int y, ControlDir dir) =>
        Taken("onuse", () => [LuaActor.Of(player), x, y, DirName(dir)]);

    /// <summary>The scripts' player death hooks, as a player's killed (PlayerStats)</summary>
    internal void RunPlayerDeathHooks(Entities.Actors.Actor player, Entities.Actors.Actor? killer) =>
        Tell("onplayerdeath", () => [LuaActor.Of(player), LuaActor.Of(killer)]);

    /// <summary>The scripts' spawn hooks, as a player is put on the level (RunLevelHooks, Players)</summary>
    internal void RunSpawnHooks(Entities.Actors.Actor player, string reason) =>
        Tell("onspawn", () => [LuaActor.Of(player), reason]);

    /// <summary>The scripts' sight hooks, as an enemy notices its foe (Monster.FirstSighting)</summary>
    internal void RunSightHooks(Entities.Actors.Actor monster, Entities.Actors.Actor? target) =>
        Tell("onsight", () => [LuaActor.Of(monster), LuaActor.Of(target)]);

    /// <summary>The scripts' door hooks, as a door's about to open or close (Doors): false if one keeps it as it is</summary>
    internal bool RunDoorHooks(int x, int y, bool opening, Entities.Actors.Actor? by) =>
        Allowed("ondoor", () => [x, y, opening, LuaActor.Of(by)]);

    /// <summary>The scripts' pushwall hooks, as a pushwall's about to slide (PushWalls.PushWall): false if one keeps it still</summary>
    internal bool RunPushWallHooks(Entities.Actors.Actor player, int x, int y, ControlDir dir) =>
        Allowed("onpushwall", () => [LuaActor.Of(player), x, y, DirName(dir)]);

    /// <summary>The scripts' switch hooks, as a switch is about to be thrown (PlayerUse): false if one leaves it be</summary>
    internal bool RunSwitchHooks(Entities.Actors.Actor player, int x, int y, int tag) =>
        Allowed("onswitch", () => [LuaActor.Of(player), x, y, tag]);

    /// <summary>The scripts' enter hooks, as a player steps onto a tile or an enemy reaches one's middle</summary>
    internal void RunEnterHooks(Entities.Actors.Actor actor, int x, int y) =>
        Tell("onenter", () => [LuaActor.Of(actor), x, y]);

    /// <summary>The scripts' exit hooks, as an exit's about to end the level (PlayerUse.ExitAction): false if one keeps it going</summary>
    internal bool RunExitHooks(Entities.Actors.Actor player, string kind) =>
        Allowed("onexit", () => [LuaActor.Of(player), kind]);

    /// <summary>The scripts' teleport hooks, as a teleporter has moved an actor (Gameplay/HubActions.cs)</summary>
    internal void RunTeleportHooks(Entities.Actors.Actor actor, int fromX, int fromY, int toX, int toY) =>
        Tell("onteleport", () => [LuaActor.Of(actor), fromX, fromY, toX, toY]);

    // Actors put on the level since the scripts' actor-spawn hooks were last told, in that order
    readonly List<Entities.Actors.Actor> spawned = [];

    /// <summary>
    /// An actor's been put on the level (MapManager.ActorAdded), once the level's start has told
    /// the scripts' actor-spawn hooks its own (RunLevelHooks): they're told at the tic's end
    /// (TellSpawned), when whoever spawned it has set it going (a projectile's angle and speed)
    /// </summary>
    internal void ActorAdded(Entities.Actors.Actor actor)
    {
        if (Hooks("onactorspawn").Count > 0 && _toldLevel == map.LevelSerial && actor is not Entities.Actors.PlayerPawn)
            spawned.Add(actor);
    }

    // Tells the actor-spawn hooks of those put on the level since last time and still on it; any
    // they spawn wait for the next tic's
    void TellSpawned()
    {
        if (spawned.Count == 0)
            return;
        var told = spawned.ToList();
        spawned.Clear();
        foreach (var actor in told)
            if (!actor.IsRemoved && _toldLevel == map.LevelSerial)
                RunActorSpawnHooks(actor);
    }

    void RunActorSpawnHooks(Entities.Actors.Actor actor) => Tell("onactorspawn", () => [LuaActor.Of(actor)]);

    /// <summary>The scripts' revive hooks, as a dead monster's brought back (Monster.Rise)</summary>
    internal void RunReviveHooks(Entities.Actors.Actor monster, Entities.Actors.Actor? reviver) =>
        Tell("onrevive", () => [LuaActor.Of(monster), LuaActor.Of(reviver)]);

    /// <summary>The scripts' faction hooks, as an actor goes over to another side</summary>
    internal void RunFactionHooks(Entities.Actors.Actor actor, string? from, string? to) =>
        Tell("onfactionchange", () => [LuaActor.Of(actor), from ?? LuaValue.Nil, to ?? LuaValue.Nil]);

    /// <summary>The scripts' fire hooks, as a player's attack is about to start or go on (WeaponInHand): false if one stops it</summary>
    internal bool RunFireHooks(Entities.Actors.Actor player, string weapon) =>
        Allowed("onfire", () => [LuaActor.Of(player), weapon]);

    /// <summary>The scripts' weapon-switch hooks, as the weapon in a player's hand has changed (PlayerThink)</summary>
    internal void RunWeaponSwitchHooks(Entities.Actors.Actor player, string from, string to) =>
        Tell("onweaponswitch", () => [LuaActor.Of(player), from, to]);

    /// <summary>The scripts' give hooks, as an item's been given to a player (PlayerStats.TryApplyInventory)</summary>
    internal void RunGiveHooks(Entities.Actors.Actor player, string item, int amount) =>
        Tell("ongive", () => [LuaActor.Of(player), item, amount]);

    /// <summary>The scripts' power-up hooks, as one starts (or adds to its time) or runs out (PlayerStats)</summary>
    internal void RunPowerupHooks(Entities.Actors.Actor player, string powerup, bool started) =>
        Tell("onpowerup", () => [LuaActor.Of(player), powerup, started]);

    /// <summary>The scripts' objective hooks, as more of a level objective's done (Gameplay/Objectives.cs)</summary>
    internal void RunObjectiveHooks(string name, int done, int total) =>
        Tell("onobjective", () => [name, done, total]);

    /// <summary>The scripts' chain hooks, as a kill carries a player's kill chain on (Monster.Kill)</summary>
    internal void RunChainHooks(Entities.Actors.Actor player, int count) =>
        Tell("onchain", () => [LuaActor.Of(player), count]);

    /// <summary>The scripts' stealth-kill hooks, as a kill's made unseen (Monster.Kill)</summary>
    internal void RunStealthKillHooks(Entities.Actors.Actor player, Entities.Actors.Actor victim) =>
        Tell("onstealthkill", () => [LuaActor.Of(player), LuaActor.Of(victim)]);

    /// <summary>
    /// The scripts' time-up hooks, as a timed level's clock has run out (Gameplay/LevelTimer.cs):
    /// true if one sees to it (for the rest of the level), and the game does nothing
    /// </summary>
    internal bool RunTimeUpHooks()
    {
        if (!_timeUpSeenTo)
            _timeUpSeenTo = Taken("ontimeup", () => []);
        return _timeUpSeenTo;
    }

    /// <summary>The scripts' frag hooks, as a frag's counted (Gameplay/Deathmatch.cs)</summary>
    internal void RunFragHooks(Entities.Actors.Actor? victim, Entities.Actors.Actor? killer) =>
        Tell("onfrag", () => [LuaActor.Of(victim), LuaActor.Of(killer)]);

    /// <summary>The scripts' flag hooks, as something happens to a capture-the-flag flag</summary>
    internal void RunFlagHooks(string what, Entities.Actors.Actor? player, int team) =>
        Tell("onflag", () => [what, LuaActor.Of(player), team is 0 or 1 ? Gameplay.GameModes.TeamNames[team] : LuaValue.Nil]);

    /// <summary>The scripts' match-end hooks, with the winner: a team's name, a player, or nil for a draw</summary>
    internal void RunMatchEndHooks(LuaValue winner) => Tell("onmatchend", () => [winner]);

    /*
    -----------------------------------------------------------------------------
    On this machine alone
    -----------------------------------------------------------------------------
    */

    /// <summary>The scripts' HUD hooks, as a frame's drawn (over the view, with the HUD messages)</summary>
    internal void RunHudHooks()
    {
        if (Hooks("onhud").Count > 0)
            RunLocal("game.onhud", () => RunHooks(Hooks("onhud"), []));
    }

    /// <summary>The song for the level (LevelMusic): game.music's, else one an onmusic hook gives, else its own</summary>
    internal string ChooseSong(string song)
    {
        if (ScriptSong != null)
            return ScriptSong;
        if (Hooks("onmusic").Count == 0)
            return song;
        string? chosen = null;
        RunLocal("game.onmusic", () => RunHooks(Hooks("onmusic"), [session.GameState.MapOn ?? "", song],
            result => (chosen = TextOf(result)) == null));
        return chosen ?? song;
    }

    /// <summary>The scripts' tally hooks, as a level's finished: the text the first gives for the tally, or null</summary>
    internal string? RunTallyHooks(string map, Gameplay.LevelResult result, string exit)
    {
        if (Hooks("ontally").Count == 0)
            return null;
        string? text = null;
        RunLocal("game.ontally", () => RunHooks(Hooks("ontally"), [map, ResultTable(result, exit)],
            returned => (text = TextOf(returned)) == null));
        return text;
    }

    // Runs hooks that run on this machine alone, which may not change the game
    void RunLocal(string hook, Action run)
    {
        var outer = _localHook;
        _localHook = hook;
        try
        {
            run();
        }
        finally
        {
            _localHook = outer;
        }
    }

    /*
    -----------------------------------------------------------------------------
    Running them
    -----------------------------------------------------------------------------
    */

    static string DirName(ControlDir dir) => dir.ToString().ToLowerInvariant();

    static bool IsFalse(LuaValue value) => value.Type == LuaValueType.Boolean && !value.Read<bool>();

    static bool IsTrue(LuaValue value) => value.Type == LuaValueType.Boolean && value.Read<bool>();

    // What a hook returned as text (a string, not empty), else null
    static string? TextOf(LuaValue value) =>
        value.Type == LuaValueType.String && value.Read<string>() is { Length: > 0 } text ? text : null;

    // Tells each of a hook's functions what happened (the arguments made only when there are some)
    void Tell(string hook, Func<LuaValue[]> arguments)
    {
        if (Hooks(hook).Count > 0)
            RunHooks(Hooks(hook), arguments());
    }

    // Asks a hook's functions whether something may happen: false once one says false, and the rest aren't asked
    bool Allowed(string hook, Func<LuaValue[]> arguments)
    {
        if (Hooks(hook).Count == 0)
            return true;
        bool allowed = true;
        RunHooks(Hooks(hook), arguments(), result => allowed = !IsFalse(result));
        return allowed;
    }

    // Asks a hook's functions to see to something: true once one says true, and the rest aren't asked
    bool Taken(string hook, Func<LuaValue[]> arguments)
    {
        if (Hooks(hook).Count == 0)
            return false;
        bool taken = false;
        RunHooks(Hooks(hook), arguments(), result => !(taken = IsTrue(result)));
        return taken;
    }

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
}
