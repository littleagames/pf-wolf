namespace PFWolf.Scripting;

/// <summary>What a script can use, by where it's reached</summary>
public enum ScriptApiKind
{
    /// <summary>game.NAME(...)</summary>
    GameFunction,
    /// <summary>actor.NAME</summary>
    ActorField,
    /// <summary>actor:NAME(...)</summary>
    ActorMethod,
    /// <summary>A global: print, pairs...</summary>
    Global,
    /// <summary>A library table: game, string, math...</summary>
    Library,
    /// <summary>LIBRARY.NAME: string.format, math.floor...</summary>
    LibraryMember,
    /// <summary>A Lua keyword</summary>
    Keyword,
}

/// <summary>One name a script can use, how it's written, and what it does</summary>
public sealed record ScriptApiEntry(ScriptApiKind Kind, string Name, string Signature, string Summary, string Library = "")
{
    /// <summary>For an actor field, whether a script may set it</summary>
    public bool Settable { get; init; }
}

/// <summary>
/// The scripting API as a list, for the editor's autocomplete and help: what PFWolf's
/// GameHost.Scripting.cs gives scripts (the game table, an actor's fields and methods), the parts
/// of Lua's libraries the sandbox (Scripting/ScriptHost.cs) leaves in, and the switch actions
/// game.trigger runs. ScriptApiTests checks it against what the engine really registers, so when
/// the API changes, this changes with it.
/// </summary>
public static class ScriptApi
{
    public static IReadOnlyList<ScriptApiEntry> Game { get; } =
    [
        Fn("random", "game.random([m[, n]])", "The game's random numbers (the same on every machine and demo run): 0-255, 1 to m, or m to n"),
        Fn("players", "game.players()", "The players in the game who can be aimed at, as a list of actors"),
        Fn("actors", "game.actors([class])", "Every actor on the level, or those of a class, in the order they think"),
        Fn("tagged", "game.tagged(tag[, class])", "The actors with that tag on the map's tag plane (as switches find them), or those of a class"),
        Fn("taggedtiles", "game.taggedtiles(tag)", "The tiles with that tag, each {x, y}: tagged doors and walls"),
        Fn("tag", "game.tag(x, y)", "A tile's tag, 0 for none"),
        Fn("patch", "game.patch(x, y)", "The patch (mapdefs patches) on a wall tile, 0 for none"),
        Fn("trigger", "game.trigger(tag, action, ...)", "Runs a switch action on a tag, as a switch with it would: game.trigger(3, \"A_OpenDoor\", \"hold\"). True if it did anything"),
        Fn("onlevel", "game.onlevel(function(reason, map) ... end)", "Adds a hook run as each level starts; reason is \"new\", \"returned\", \"restarted\" or \"loaded\". Add hooks at a script's top level"),
        Fn("ontick", "game.ontick(function(tics) ... end)", "Adds a hook run each frame the world moves, with the tics that went by (70 a second). Add hooks at a script's top level"),
        Fn("time", "game.time()", "The level's time in tics (70 a second), as saved games keep it"),
        Fn("tics", "game.tics()", "The tics this frame moves the world on (usually 1 or 2)"),
        Fn("map", "game.map()", "The level's map name (MAP01)"),
        Fn("skill", "game.skill()", "The skill being played, 0 for the easiest"),
        Fn("message", "game.message(text[, style])", "Shows a message (a $NAME language key or the text), in the style for locked doors unless one is named"),
        Fn("ambient", "game.ambient(sound)", "Loops a sound at the listener as the ambience, in place of the one there was; nil stops it. Asking for the one playing changes nothing, so it can be asked every tick"),
        Fn("objective", "game.objective(name)", "How many of a level objective (game-info objectives) are done, and of how many: two numbers, 0 and 0 for one the level hasn't"),
        Fn("unlock", "game.unlock(name)", "Unlocks a name from game to game (progress.cfg): a starts-locked episode's key, or a script's own name. True if it wasn't already; a demo playing back unlocks nothing"),
        Fn("unlocked", "game.unlocked(name)", "Whether a name has been unlocked (game.unlock, A_Unlock or the unlock command), in this game or an earlier one"),
    ];

    public static IReadOnlyList<ScriptApiEntry> ActorFields { get; } =
    [
        Field("name", "The actor's class (actordefs name)"),
        Field("x", "Where it is east-west, in tiles: a tile's middle is n + 0.5"),
        Field("y", "Where it is north-south, in tiles (y grows southward)"),
        Field("tilex", "The tile it's on, east-west"),
        Field("tiley", "The tile it's on, north-south"),
        Field("angle", "Degrees, 0 east, 90 north: the way it faces, or a projectile flies", settable: true),
        Field("health", "Its health (a player's too)"),
        Field("speed", "Its speed, 1/65536 tile a tic", settable: true),
        Field("z", "How much higher than its art puts it it's drawn, in texels (64 a story; negative is lower): only how it looks", settable: true),
        Field("state", "The state it's on (Spawn, Fire...)"),
        Field("tag", "The tag on the tile it spawned on, 0 for none"),
        Field("inwall", "Whether it stands inside a wall (mapdefs in-wall: true)"),
        Field("removed", "Whether it's gone from the level"),
        Field("isplayer", "Whether it's a player"),
        Field("shooter", "For a projectile, the actor that fired it"),
        Field("faction", "The side it's on (factions.yaml), nil for none; set it to change sides (the kill total follows)", settable: true),
        Field("follows", "Whether it goes about with the players on its side (monster.follow, A_SetFaction \"follow\")", settable: true),
        Field("translation", "The color translation it's drawn through (translations.yaml), nil for its own colors; set nil for its faction's", settable: true),
        Field("temp1", "A whole number of its own, kept in saved games: a script's state for the actor", settable: true),
        Field("temp2", "A whole number of its own, kept in saved games", settable: true),
        Field("temp3", "A whole number of its own, kept in saved games", settable: true),
    ];

    public static IReadOnlyList<ScriptApiEntry> ActorMethods { get; } =
    [
        Method("jump", "actor:jump(state)", "Goes to a state of its actordefs class; false if it hasn't got one"),
        Method("spawnprojectile", "actor:spawnprojectile(class[, angle[, \"relative\"|\"absolute\"|\"aim\"]])", "Fires a projectile, returning it: relative to its facing (the default), absolute from east, or aimed at the nearest player"),
        Method("spawn", "actor:spawn(class)", "Spawns an actor where it is, returning it"),
        Method("playsound", "actor:playsound(sound[, positional])", "Plays a sound from where it is (positional false: not placed in the world)"),
        Method("property", "actor:property(key)", "An actordefs property: numbers as numbers, lists as tables, nil if it hasn't got it"),
        Method("hasflag", "actor:hasflag(flag)", "Whether its class has an actordefs flag"),
        Method("distanceto", "actor:distanceto(other)", "How far another actor is, in tiles"),
        Method("angleto", "actor:angleto(other)", "The angle to another actor, in degrees, from where it looks out (a wall's face)"),
        Method("cansee", "actor:cansee(player[, tiles])", "Whether there's a clear line to a player (within that many tiles); one in a wall sees only out of its face"),
        Method("nearestplayer", "actor:nearestplayer()", "The nearest player who can be aimed at, or nil"),
        Method("remove", "actor:remove()", "Takes it off the level"),
    ];

    public static IReadOnlyList<ScriptApiEntry> Globals { get; } =
    [
        Global("print", "print(...)", "Prints to the game's console"),
        Global("pairs", "pairs(t)", "Each key and value of a table (in the order they were added)"),
        Global("ipairs", "ipairs(t)", "Each index and value of a list, from 1"),
        Global("next", "next(t[, key])", "A table's next key and value"),
        Global("select", "select(n, ...)", "The arguments from the nth on, or with \"#\" how many there are"),
        Global("type", "type(v)", "What a value is: \"nil\", \"number\", \"string\", \"table\", \"function\", \"userdata\" (an actor)..."),
        Global("tostring", "tostring(v)", "A value as a string"),
        Global("tonumber", "tonumber(v[, base])", "A value as a number, or nil"),
        Global("error", "error(message[, level])", "Raises an error (reported on the console with the script's line)"),
        Global("assert", "assert(v[, message])", "Raises an error when v is false or nil"),
        Global("pcall", "pcall(f, ...)", "Calls f, catching an error: true and its results, or false and the error"),
        Global("xpcall", "xpcall(f, handler, ...)", "Calls f, passing an error to handler"),
        Global("setmetatable", "setmetatable(t, mt)", "Sets a table's metatable"),
        Global("getmetatable", "getmetatable(v)", "A value's metatable"),
        Global("rawget", "rawget(t, key)", "A table's value, without its metatable"),
        Global("rawset", "rawset(t, key, value)", "Sets a table's value, without its metatable"),
        Global("rawequal", "rawequal(a, b)", "Whether two values are the same, without metatables"),
        Global("rawlen", "rawlen(v)", "A table's or string's length, without its metatable"),
    ];

    public static IReadOnlyList<ScriptApiEntry> Libraries { get; } =
    [
        new(ScriptApiKind.Library, "game", "game", "The game: its random numbers, players, actors, tags, switch actions, hooks, time and messages"),
        new(ScriptApiKind.Library, "string", "string", "Lua's string functions"),
        new(ScriptApiKind.Library, "table", "table", "Lua's table functions"),
        new(ScriptApiKind.Library, "math", "math", "Lua's math functions (not math.random: use game.random)"),
        new(ScriptApiKind.Library, "bit32", "bit32", "Lua 5.2's bitwise functions"),
    ];

    public static IReadOnlyList<ScriptApiEntry> LibraryMembers { get; } =
    [
        .. Members("string", ("byte", "string.byte(s[, i[, j]])", "Character codes"), ("char", "string.char(...)", "A string of these character codes"),
            ("find", "string.find(s, pattern[, init[, plain]])", "Where a pattern is found"), ("format", "string.format(format, ...)", "A formatted string (%d, %s, %.2f...)"),
            ("gmatch", "string.gmatch(s, pattern)", "Each match of a pattern"), ("gsub", "string.gsub(s, pattern, replacement[, n])", "The string with matches replaced"),
            ("len", "string.len(s)", "Its length"), ("lower", "string.lower(s)", "In lower case"), ("match", "string.match(s, pattern[, init])", "The first match's captures"),
            ("rep", "string.rep(s, n[, sep])", "The string n times"), ("reverse", "string.reverse(s)", "Backwards"), ("sub", "string.sub(s, i[, j])", "Part of it"),
            ("upper", "string.upper(s)", "In upper case")),
        .. Members("table", ("concat", "table.concat(list[, sep[, i[, j]]])", "A list's values joined into a string"),
            ("insert", "table.insert(list, [pos,] value)", "Adds a value to a list"), ("pack", "table.pack(...)", "The arguments as a list, with n their count"),
            ("remove", "table.remove(list[, pos])", "Takes a value out of a list"), ("sort", "table.sort(list[, less])", "Sorts a list"),
            ("unpack", "table.unpack(list[, i[, j]])", "A list's values, as separate values")),
        .. Members("math", ("abs", "math.abs(x)", "Its size"), ("ceil", "math.ceil(x)", "Rounded up"), ("floor", "math.floor(x)", "Rounded down"),
            ("max", "math.max(x, ...)", "The largest"), ("min", "math.min(x, ...)", "The smallest"), ("fmod", "math.fmod(x, y)", "The remainder of x / y"),
            ("modf", "math.modf(x)", "Its whole and fractional parts"), ("sqrt", "math.sqrt(x)", "Its square root"), ("pow", "math.pow(x, y)", "x to the power y"),
            ("exp", "math.exp(x)", "e to the power x"), ("log", "math.log(x[, base])", "Its logarithm"), ("sin", "math.sin(x)", "Sine (radians)"),
            ("cos", "math.cos(x)", "Cosine (radians)"), ("tan", "math.tan(x)", "Tangent (radians)"), ("asin", "math.asin(x)", "Arc sine, in radians"),
            ("acos", "math.acos(x)", "Arc cosine, in radians"), ("atan", "math.atan(x)", "Arc tangent, in radians"), ("atan2", "math.atan2(y, x)", "The angle of (x, y), in radians"),
            ("deg", "math.deg(x)", "Radians in degrees"), ("rad", "math.rad(x)", "Degrees in radians"), ("pi", "math.pi", "π"), ("huge", "math.huge", "Infinity")),
        .. Members("bit32", ("band", "bit32.band(...)", "Bitwise and"), ("bor", "bit32.bor(...)", "Bitwise or"), ("bxor", "bit32.bxor(...)", "Bitwise exclusive or"),
            ("bnot", "bit32.bnot(x)", "Bitwise not"), ("btest", "bit32.btest(...)", "Whether the and of them isn't 0"), ("lshift", "bit32.lshift(x, n)", "Shifted left"),
            ("rshift", "bit32.rshift(x, n)", "Shifted right"), ("arshift", "bit32.arshift(x, n)", "Shifted right, keeping the sign"),
            ("lrotate", "bit32.lrotate(x, n)", "Rotated left"), ("rrotate", "bit32.rrotate(x, n)", "Rotated right"),
            ("extract", "bit32.extract(x, field[, width])", "Some of its bits"), ("replace", "bit32.replace(x, v, field[, width])", "Some of its bits replaced")),
    ];

    public static IReadOnlyList<ScriptApiEntry> Keywords { get; } =
        [.. new[] { "and", "break", "do", "else", "elseif", "end", "false", "for", "function", "goto", "if", "in", "local", "nil", "not",
            "or", "repeat", "return", "then", "true", "until", "while", "self" }
            .Select(word => new ScriptApiEntry(ScriptApiKind.Keyword, word, word, "Lua"))];

    /// <summary>
    /// The switch actions game.trigger runs (mapdefs/wolf3d/walls.yaml describes them at wall
    /// 21), less those that work on a switch's own tile, which a script hasn't got
    /// </summary>
    public static IReadOnlyList<string> TriggerActions { get; } =
    [
        "A_OpenDoor", "A_CloseDoor", "A_ToggleDoor", "A_MoveWall", "A_SetWall", "A_ChangeTexture",
        "A_Activate", "A_Deactivate", "A_ToggleActive", "A_Message", "A_GiveInventory", "A_SpawnItem", "A_SetLight", "A_LightOff",
        "A_SetZoneLight", "A_ToggleZoneLight", "A_SetZoneEffect", "A_UnlockFloor", "A_DropDetonator",
        "A_VictoryTile", "A_Exit", "A_SecretExit", "A_VictoryRun", "A_CompleteObjective", "A_AddObjective", "A_Unlock",
    ];

    /// <summary>The switch actions game.trigger can't run: they work on a switch's own tile</summary>
    public static IReadOnlyList<string> OwnTileActions { get; } = ["A_PushWall", "A_Vend", "A_SmartSwitch", "A_FloorSelect", "A_Teleport", "A_TeleportTag"];

    private static ScriptApiEntry Fn(string name, string signature, string summary) => new(ScriptApiKind.GameFunction, name, signature, summary, "game");

    private static ScriptApiEntry Field(string name, string summary, bool settable = false)
        => new(ScriptApiKind.ActorField, name, $"actor.{name}", summary + (settable ? " (can be set)" : "")) { Settable = settable };

    private static ScriptApiEntry Method(string name, string signature, string summary) => new(ScriptApiKind.ActorMethod, name, signature, summary);

    private static ScriptApiEntry Global(string name, string signature, string summary) => new(ScriptApiKind.Global, name, signature, summary);

    private static IEnumerable<ScriptApiEntry> Members(string library, params (string Name, string Signature, string Summary)[] members)
        => members.Select(member => new ScriptApiEntry(ScriptApiKind.LibraryMember, member.Name, member.Signature, member.Summary, library));
}
