using PFWolf.Definitions;

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
/// Scripting/GameScripts.cs gives scripts (the game table, an actor's fields and methods), the parts
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
        Fn("actors", "game.actors([class])", "Every actor on the level, in the order they think, or those of a class (that class itself: not the classes built on it)"),
        Fn("tagged", "game.tagged(tag[, class])", "The actors with that tag on the map's tag plane (as switches find them), or those of a class (that class itself)"),
        Fn("taggedtiles", "game.taggedtiles(tag)", "The tiles with that tag, each {x, y}: tagged doors and walls"),
        Fn("tag", "game.tag(x, y)", "A tile's tag, 0 for none"),
        Fn("patch", "game.patch(x, y)", "The patch (mapdefs patches) on a wall tile, 0 for none"),
        Fn("floor", "game.floor(x, y)", "The texture on a tile's floor (mapdefs flats or the map's default-floor, as A_ChangeTexture left it), nil where the floor color shows"),
        Fn("removewall", "game.removewall(x, y[, \"arch\"])", "Takes the wall off a tile for good, as A_RemoveWall does, leaving open floor joined to the floor round it; with \"arch\" a tall wall keeps its upper stories as an arch. False where there's no wall that can go (a door, open floor, a moving pushwall, the map's edge)"),
        Fn("breakwall", "game.breakwall(x, y)", "Breaks the wall on a tile as its walls.yaml destructible block says, as A_BreakWall does (sound, actions, crumbling animation, its to: stage or gone); a wall without one is just gone. False where nothing broke (no wall, crumbling already, or a game.onwallbreak hook kept it)"),
        Fn("wallhealth", "game.wallhealth(x, y)", "How much more damage the wall on a tile takes before it breaks (its destructible health less what it's taken; 1 for one with no health), nil for a wall that isn't destructible"),
        Fn("onwallbreak", "game.onwallbreak(function(x, y, wall, type, by) ... end)", "Adds a hook asked as a wall's about to break (a hit's, A_BreakWall's or game.breakwall's): its tile, its wall id, the damage type that broke it (nil for an action or a script) and the player behind it (nil for none). Return false to keep it standing (the damage it's taken stays)"),
        Fn("trigger", "game.trigger(tag, action, ...)",
            "Runs a switch action on a tag, as a switch with it would, used by the first player: game.trigger(3, \"A_OpenDoor\", \"hold\"), "
            + "or the whole call in one string, game.trigger(3, 'A_OpenDoor(\"hold\")'). True if it did anything (false with no player on the level). "
            + "Not the actions that work on a switch's own tile, which a script hasn't got: A_PushWall (A_MoveWall pushes tagged walls), A_Vend, "
            + "A_SmartSwitch, A_FloorSelect, A_Teleport, A_TeleportTag and A_Warp are an error"),
        Fn("onlevel", "game.onlevel(function(reason, map) ... end)", "Adds a hook run as each level starts; reason is \"new\", \"returned\", \"restarted\" or \"loaded\". Add hooks at a script's top level"),
        Fn("ontick", "game.ontick(function(tics) ... end)", "Adds a hook run each frame the world moves, with the tics that went by (70 a second). Add hooks at a script's top level"),
        Fn("time", "game.time()", "The level's time in tics (70 a second), as saved games keep it"),
        Fn("tics", "game.tics()", "The tics this frame moves the world on (usually 1 or 2)"),
        Fn("map", "game.map()", "The level's map name (MAP01)"),
        Fn("skill", "game.skill()", "The skill being played, 0 for the easiest"),
        Fn("message", "game.message(text[, style[, key]])", "Shows a message (a $NAME language key or the text), in the style for locked doors unless one is named. With a key it is that key's message, kept up to date in its place (a clock each second), shown with messages off and not printed; nil text takes it down"),
        Fn("ambient", "game.ambient(sound)", "Loops a sound at the listener as the ambience, in place of the one there was; nil stops it. Asking for the one playing changes nothing, so it can be asked every tick"),
        Fn("objective", "game.objective(name)", "How many of a level objective (game-info objectives) are done, and of how many: two numbers, 0 and 0 for one the level hasn't"),
        Fn("unlock", "game.unlock(name)", "Unlocks a name from game to game (progress.cfg): a starts-locked episode's key, or a script's own name. True if it wasn't already; a demo playing back unlocks nothing"),
        Fn("record", "game.record(map)", "The best done on a map from game to game (records.cfg), as game.onlevelend's result: kills, killtotal, secrets, secrettotal, items, itemtotal, time, par, underpar, undetected, stealthkills, objectives. Nil if it's never been finished"),
        Fn("onlevelend", "game.onlevelend(function(map, result) ... end)", "Adds a hook called as a level's finished (exit, secret exit or the game won), before the tally: result is what the finish came to (as game.record's, with exit: \"exit\", \"secret\" or \"victory\"). Anything it unlocks is told on the tally. Not called while a demo plays back"),
        Fn("ondamage", "game.ondamage(function(victim, amount, attacker, type) ... end)", "Adds a hook called as an actor or player is about to be hurt: how much (an enemy's after its doubling while it isn't attacking; a player's after the skill's and class's share, before armor), by what (nil for a floor or a trap) and its damage type (nil for none). Return a number to change the amount (0: unhurt, though an enemy still turns on the attacker); each hook gets the amount the one before left"),
        Fn("onkill", "game.onkill(function(victim, killer, type) ... end)", "Adds a hook called as a monster's killed: by whom (who fired the projectile, not the projectile; nil for nothing) and with what damage type. Players' deaths are game.onplayerdeath's"),
        Fn("ontrypickup", "game.ontrypickup(function(player, item) ... end)", "Adds a hook asked as a player walks over an item they may take: return false to leave it there. Asked again each tic they stand on it; an item they then can't use (ammo full, a key held) stays anyway"),
        Fn("onpickup", "game.onpickup(function(player, item, amount) ... end)", "Adds a hook called as a player has taken an item, with what it gave (nil for an item without an amount), just before it goes from the level"),
        Fn("onsecret", "game.onsecret(function(player, x, y) ... end)", "Adds a hook called as a player finds a secret, at that tile"),
        Fn("onuse", "game.onuse(function(player, x, y, dir) ... end)", "Adds a hook called at a fresh press of use, facing the tile x, y (dir \"north\", \"east\", \"south\" or \"west\"), before the game looks at what's there: return true to say the script used it, and the game does nothing else"),
        Fn("onplayerdeath", "game.onplayerdeath(function(player, killer) ... end)", "Adds a hook called as a player's killed, by what (nil for nothing: time running out, a hurting floor)"),
        Fn("onspawn", "game.onspawn(function(player, reason) ... end)", "Adds a hook called as a player is put on the level: as it starts, just before game.onlevel (reason \"new\", \"returned\" or \"restarted\"; not for a loaded game), back after dying with others (\"respawn\"), or joining a network game (\"join\")"),
        Fn("onsight", "game.onsight(function(monster, target) ... end)", "Adds a hook called as an enemy notices its foe (a player, or an actor of a side it fights) and sets off after it"),
        Fn("ondoor", "game.ondoor(function(x, y, opening, by) ... end)", "Adds a hook asked as a door is about to open (opening true) or close, with what set it going (the player using it, an enemy walking through; nil for a switch, a linked door or its own closing): return false to leave it as it is. Asked again each time something tries"),
        Fn("onpushwall", "game.onpushwall(function(player, x, y, dir) ... end)", "Adds a hook asked as a pushwall is about to slide from x, y the way dir says: return false to leave it where it is (a secret it is isn't found)"),
        Fn("onswitch", "game.onswitch(function(player, x, y, tag) ... end)", "Adds a hook asked as a switch (a wall's or a patch's) is about to be thrown, once the player has its lock item: return false to leave it be"),
        Fn("onenter", "game.onenter(function(actor, x, y) ... end)", "Adds a hook called as a player steps onto another tile (or is moved there), or an enemy walks to the middle of one"),
        Fn("onexit", "game.onexit(function(player, kind) ... end)", "Adds a hook asked as an exit (A_Exit, A_SecretExit; kind \"exit\" or \"secret\") is about to end the level, once its objectives allow it: return false to keep the level going"),
        Fn("onteleport", "game.onteleport(function(actor, fromx, fromy, tox, toy) ... end)", "Adds a hook called as a teleporter (A_Teleport, A_TeleportTag, A_Warp) has moved a player, or an enemy on a pad, across the level"),
        Fn("onactorspawn", "game.onactorspawn(function(actor) ... end)", "Adds a hook called for each actor put on the level: those a new or restarted level starts with (just before game.onspawn), and any spawned after (enemies, projectiles, drops) at the end of the tic they came in. Not players"),
        Fn("onrevive", "game.onrevive(function(monster, reviver) ... end)", "Adds a hook called as a dead monster is brought back, by whom (nil for no one)"),
        Fn("onfactionchange", "game.onfactionchange(function(actor, from, to) ... end)", "Adds a hook called as an actor goes over to another side (factions.yaml; nil for none)"),
        Fn("onfire", "game.onfire(function(player, weapon) ... end)", "Adds a hook asked as a press of fire is about to start an attack with the weapon (its class), or held fire is carrying an automatic weapon's on: return false to stop it"),
        Fn("onweaponswitch", "game.onweaponswitch(function(player, from, to) ... end)", "Adds a hook called as the weapon in a player's hand has changed (seen at the end of their think; not the first one they're seen with)"),
        Fn("ongive", "game.ongive(function(player, item, amount) ... end)", "Adds a hook called as an item is given to a player, however it came (picked up, a switch, a prize, a gift): its class and amount"),
        Fn("onpowerup", "game.onpowerup(function(player, name, started) ... end)", "Adds a hook called as a power-up starts (or adds to its time: started true) or runs out"),
        Fn("onobjective", "game.onobjective(function(name, done, total) ... end)", "Adds a hook called as more of a level objective is done"),
        Fn("onchain", "game.onchain(function(player, count) ... end)", "Adds a hook called as a kill carries a player's kill chain on (game-info kill-chain), count 2 and up"),
        Fn("onstealthkill", "game.onstealthkill(function(player, victim) ... end)", "Adds a hook called as a player makes a kill unseen (game-info stealth)"),
        Fn("ontimeup", "game.ontimeup(function() ... end)", "Adds a hook asked as a timed level's clock runs out: return true to see to it yourself, and nobody dies (nor is a time-limit-completes level got through)"),
        Fn("onfrag", "game.onfrag(function(victim, killer) ... end)", "Adds a hook called as a frag is counted in a versus game (killer the victim: one taken off for killing themselves)"),
        Fn("onflag", "game.onflag(function(event, player, team) ... end)", "Adds a hook called as something happens to a capture-the-flag flag of a team (\"red\", \"blue\"): \"taken\", \"dropped\", \"returned\" (by a player of its team), \"home\" (back by itself, player nil) or \"captured\""),
        Fn("onmatchend", "game.onmatchend(function(winner) ... end)", "Adds a hook called as a match is over: the winning team (\"red\", \"blue\"), or in deathmatch the player with the most frags; nil for a draw"),
        Fn("onchasedir", "game.onchasedir(function(monster, target, mode) ... end)", "Adds a hook asked as an enemy chooses its next way at a tile's middle, while it chases (mode \"chase\"), dodges closing in (\"dodge\"), runs off (\"run\"), wanders (\"wander\") or follows a player (\"follow\"): return a way (\"north\", \"northeast\"... or \"none\" to stand) to send it that way if it can go, else the game chooses; nil leaves it to the game. return monster:pathto(target) finds the way by A*"),
        Fn("walkable", "game.walkable(x, y[, actor])", "Whether a tile can be walked onto: not a wall or a solid thing, and by an enemy's rules if one's given (the doors it can open, the monster blockers it's kept off). Other actors don't count: they move on"),
        Fn("onhud", "game.onhud(function() ... end)", "Adds a hook called each frame drawn, on this machine alone, over the view: game.drawtext and game.drawpic draw there. It mustn't change the game (game.random, game.trigger, spawning and setting actors' fields are an error)"),
        Fn("onmusic", "game.onmusic(function(map, song) ... end)", "Adds a hook asked, on this machine alone, as the level's song is about to start (or start again after the menu): return a song's name to play that instead"),
        Fn("ontally", "game.ontally(function(map, result) ... end)", "Adds a hook asked, on this machine alone, as a level's finished (result as game.onlevelend's): text returned is shown on the tally (intermission.yaml's script value)"),
        Fn("drawtext", "game.drawtext(text, x, y[, font[, color]])", "In a game.onhud hook: draws text (a $NAME language key or the text) at x, y from the view's top left, in a font (SmallFont when left out) and color (White)"),
        Fn("drawpic", "game.drawpic(picture, x, y)", "In a game.onhud hook: draws a picture at x, y from the view's top left"),
        Fn("viewsize", "game.viewsize()", "How big the view is, in the pixels game.onhud draws in: its width and height"),
        Fn("music", "game.music(song)", "Plays a song now, for the rest of the level (and after the menu); nil plays the level's own again"),
        Fn("command", "game.command(name, help, function(...) ... end)", "Gives the console a command (not while playing with others), called with the words typed after it, numbers as numbers. Add commands at a script's top level; one the game has already wins"),
        Fn("unlocked", "game.unlocked(name)","Whether a name has been unlocked (game.unlock, A_Unlock or the unlock command), in this game or an earlier one"),
        Fn("showtext", "game.showtext(name)", "Shows a text (texts/, in the help article format) as an article, then goes back to the level, its clock not counting the reading (A_ShowText). It does nothing while a demo plays or records, or in a game with others"),
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
        Field("vehicle", "For a player riding a vehicle (Gameplay/Vehicles.cs), the class of what they boarded; nil when they're on foot, and for anything else"),
        Field("shooter", "For a projectile, the actor that fired it"),
        Field("faction", "The side it's on (factions.yaml), nil for none; set it to change sides (the kill total follows)", settable: true),
        Field("follows", "Whether it goes about with the players on its side (monster.follow, A_SetFaction \"follow\"); only an enemy can, and setting it on anything else does nothing", settable: true),
        Field("translation", "The color translation it's drawn through (translations.yaml), nil for its own colors; set nil for its faction's", settable: true),
        Field("temp1", "A whole number of its own, -32768 to 32767, kept in saved games: a script's state for the actor. Set, it's rounded and kept within that", settable: true),
        Field("temp2", "A whole number of its own, -32768 to 32767, kept in saved games", settable: true),
        Field("temp3", "A whole number of its own, -32768 to 32767, kept in saved games", settable: true),
    ];

    public static IReadOnlyList<ScriptApiEntry> ActorMethods { get; } =
    [
        Method("jump", "actor:jump(state)", "Goes to a state of its actordefs class, on its first frame; false if it hasn't got one. From inside one of its own actions, it goes there once the action is done"),
        Method("spawnprojectile", "actor:spawnprojectile(class[, angle[, \"relative\"|\"absolute\"|\"aim\"]])",
            "Fires a projectile, returning it (nil if there's no such class), at the angle in degrees, positive turning left: relative to its facing (the default), absolute from east, or aimed at the nearest player and turned by the angle (A_SpawnProjectile)"),
        Method("spawn", "actor:spawn(class)", "Spawns an actor where it is, returning it (nil if there's no such class)"),
        Method("playsound", "actor:playsound(sound[, positional])", "Plays a sound from where it is (positional false: not placed in the world)"),
        Method("property", "actor:property(key)", "An actordefs property: numbers as numbers, lists as tables, nil if it hasn't got it"),
        Method("hasflag", "actor:hasflag(flag)", "Whether its class has an actordefs flag"),
        Method("distanceto", "actor:distanceto(other)", "How far another actor is, in tiles"),
        Method("angleto", "actor:angleto(other)", "The angle to another actor, in degrees, from where it looks out (a wall's face)"),
        Method("cansee", "actor:cansee(player[, tiles])", "Whether there's a clear line to a player (within that many tiles); one in a wall sees only out of its face"),
        Method("nearestplayer", "actor:nearestplayer()", "The nearest player who can be aimed at, or nil"),
        Method("remove", "actor:remove()", "Takes it off the level (not a player: removing one does nothing)"),
        Method("pathto", "actor:pathto(x, y) or actor:pathto(other)",
            "The way (\"north\", \"northeast\"...) to go from its tile toward a tile (or another actor's) by the shortest path it can walk, found by A*: through the doors it can open, round walls, solid things and the monster blockers it's kept off, diagonally only past open floor. Nil when it's there, or nowhere it can get to is nearer"),
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
        Global("_G", "_G", "The table of globals (the scripts all share it)"),
        Global("_VERSION", "_VERSION", "The Lua version, \"Lua 5.2\""),
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
            ("sinh", "math.sinh(x)", "Hyperbolic sine"), ("cosh", "math.cosh(x)", "Hyperbolic cosine"), ("tanh", "math.tanh(x)", "Hyperbolic tangent"),
            ("frexp", "math.frexp(x)", "Its mantissa and exponent: x = m × 2^e"), ("ldexp", "math.ldexp(m, e)", "m × 2^e"),
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

    /// <summary>The switch actions game.trigger can't run: they work on a switch's own tile</summary>
    public static IReadOnlyList<string> OwnTileActions { get; } = ["A_PushWall", "A_Vend", "A_SmartSwitch", "A_FloorSelect", "A_Teleport", "A_TeleportTag", "A_Warp"];

    /// <summary>
    /// The switch actions game.trigger runs (Definitions/GameActions.cs's), less those that work
    /// on a switch's own tile, which a script hasn't got
    /// </summary>
    public static IReadOnlyList<string> TriggerActions { get; } =
        [.. GameActions.All.Where(a => a.Use.HasFlag(ActionUse.Switch) && !OwnTileActions.Contains(a.Name)).Select(a => a.Name)];

    /// <summary>The scripting API for the documentation: a record of the game table, an actor's fields and methods, and Lua's names, each its own field</summary>
    public static FieldSchema Schema() => FieldSchema.Record("scripting",
        "Scripts (scripts/*.lua, in pfwolf.pk3 and each mod) run at startup, in load order, in one sandboxed Lua 5.2 state. "
        + "A global function whose name starts with A_ or T_ becomes an action or think actordefs states can run like a built-in one "
        + "(`function A_FireRing(self, projectile, count) ... end`, then `action: A_FireRing(\"Fireball\", 8)`): it's called with the actor "
        + "and the state's arguments, numbers as numbers, and a built-in action of the same name wins. Leave chance to game.random and keep "
        + "state on the actors (temp1..temp3): Lua's globals aren't saved, and demos and network games must come out the same.",
        [
            Section("game", "The game table: the level, its actors and tags, switch actions, hooks, time and messages", Game),
            Section("actor", "An actor (self in an action, or what game.actors and the like return): its fields, actor.NAME. "
                + "A field it hasn't got (a typo: actor.hp) reads as nil, and the console says so, once a name; setting one is an error", ActorFields),
            Section("actor-methods", "An actor's methods, actor:NAME(...)", ActorMethods),
            Section("globals", "Lua's basic functions the sandbox leaves in (no load, require, dofile, io or os)", Globals),
            .. Libraries.Where(library => library.Name != "game")
                .Select(library => Section(library.Name, library.Summary, [.. LibraryMembers.Where(member => member.Library == library.Name)])),
        ]);

    private static FieldSchema Section(string key, string description, IReadOnlyList<ScriptApiEntry> entries) =>
        FieldSchema.Record(key, description, [.. entries.Select(entry => FieldSchema.Callable(entry.Name, entry.Signature, $"{entry.Summary.TrimEnd('.')}."))]);

    private static ScriptApiEntry Fn(string name, string signature, string summary) => new(ScriptApiKind.GameFunction, name, signature, summary, "game");

    private static ScriptApiEntry Field(string name, string summary, bool settable = false)
        => new(ScriptApiKind.ActorField, name, $"actor.{name}", summary + (settable ? " (can be set)" : "")) { Settable = settable };

    private static ScriptApiEntry Method(string name, string signature, string summary) => new(ScriptApiKind.ActorMethod, name, signature, summary);

    private static ScriptApiEntry Global(string name, string signature, string summary) => new(ScriptApiKind.Global, name, signature, summary);

    private static IEnumerable<ScriptApiEntry> Members(string library, params (string Name, string Signature, string Summary)[] members)
        => members.Select(member => new ScriptApiEntry(ScriptApiKind.LibraryMember, member.Name, member.Signature, member.Summary, library));
}
