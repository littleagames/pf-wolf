using SDL2;
using System.Text;
using PFWolf.Assets;
using PFWolf.Assets.Sounds;
using PFWolf.Configuration;
using PFWolf.Enums;
using PFWolf.Loaders;
using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf;

internal partial class Program
{

    /// <summary>
    /// Registers the in-game console's commands. Commands that need Program's game state live
    /// here so they can reach it directly, the same way the actor actions do.
    /// </summary>
    internal static void RegisterConsoleCommands()
    {
        _consoleManager.CheatsEnabled = () => DebugOk != 0 && !netgame;      // never with others: every machine has to play alike
        _consoleManager.PlayingWithOthers = () => netgame;
        _consoleManager.LevelLoaded = () => _mapManager.Player != null;

        // the console itself, binds and controls, video, messages and assets (Commands/)
        new CoreCommands(_consoleManager, _gameEngineManager.GetConfigFilePath).Register();
        new BindCommands(_consoleManager, controls, controlSettings).Register();
        new VideoCommands(_consoleManager, _videoManager, RedrawAfterVideoChange).Register();
        new MessageCommands(_consoleManager, _hudMessageManager).Register();
        new AssetCommands(_consoleManager, _assetManager, _audioManager, _gameEngineManager).Register();

        //
        // automap
        //
        Register("automap", "Opens or closes the automap.", "automap", _ => ToggleAutomap(), InLevel);
        Register("am_style", "How the automap draws the level: wall textures and sprites, or flat colors.",
            "am_style [graphic|color]", Cmd_AmStyle, complete: Values("graphic", "color"));
        Register("am_overlay", "Whether the automap is drawn over the dimmed game view (1) or a solid backdrop (0).",
            "am_overlay [0|1]", Cmd_AmOverlay, complete: Values("0", "1"));
        Register("am_grid", "Whether the automap marks every tile edge with a faint grid when zoomed in.",
            "am_grid [0|1]", Cmd_AmGrid, complete: Values("0", "1"));
        Register("am_stats", "Whether the automap shows the level's name, kills, treasure, secrets and time.",
            "am_stats [0|1]", Cmd_AmStats, complete: Values("0", "1"));
        Register("am_reveal", "Shows the whole map on the automap, seen or not.", "am_reveal [0|1]", Cmd_AmReveal, Cheat,
            complete: Values("0", "1"));

        //
        // messages over the view
        Register("net_find", "Looks for games hosted on the local network (two seconds) and lists them.", "net_find", Cmd_NetFind);
        Register("net_portcheck", "Checks whether players on the internet can reach a game hosted here (the UDP port, default 10645): needs the port free, so not while hosting.", "net_portcheck [port]", Cmd_NetPortCheck);
        Register("net_status", "Shows the game with others: the level and frame, the players, and (hosting) how many checks found every machine agreeing.", "net_status", Cmd_NetStatus);
        Register("scoreboard", "Shows or hides the scoreboard over the view, playing with others (bind it to a key).", "scoreboard [0|1]", Cmd_Scoreboard, complete: Values("0", "1"));

        //
        // cheats (the old Tab debug keys, plus a few new ones)
        //
        Register("god", "God mode: 1 = on, 2 = on without the damage flash.", "god [0|1|2]", Cmd_God, Cheat | InLevel,
            complete: Values("0", "1", "2"));
        Register("noclip", "Walk through walls.", "noclip [0|1]", Cmd_Noclip, Cheat | InLevel,
            complete: Values("0", "1"));
        Register("give", "Gives an item (weapon, ammo, key or armor), or health, points, keys, weapons, ammo or all.",
            "give <item|health|points|keys|weapons|ammo|all> [amount]", Cmd_Give, Cheat | InLevel,
            complete: (_, i) => i == 0 ? ["all", "health", "points", "keys", "weapons", "ammo", .. GivableItems()] : []);
        Register("map", "Warps to a level.", "map <MAP##>", Cmd_Map, Cheat | InLevel, aliases: ["warp"],
            complete: (_, i) => i == 0 ? _gameEngineManager.GetGameInfo().Maps.Keys : []);
        Register("exitlevel", "Completes the current level.", "exitlevel",
            _ => playstate = playstatetypes.ex_completed, Cheat | InLevel);
        Register("tp", "Teleports to a tile.", "tp <tilex> <tiley> [angle]", Cmd_Teleport, Cheat | InLevel);
        Register("killall", "Kills every enemy on the level.", "killall", Cmd_KillAll, Cheat | InLevel);
        Register("overhead", "Shows the raw and filtered map overview.", "overhead", Cmd_Overhead, Cheat | InLevel);
        Register("diag", "Makes a wall tile (default: the one you face) a 45 degree wall, named by its solid corner.",
            "diag <square|solidnw|solidne|solidsw|solidse> [tilex tiley]", Cmd_Diag, Cheat | InLevel,
            complete: (_, i) => i == 0 ? Enum.GetNames<WallShape>().Select(n => n.ToLowerInvariant()) : []);
        Register("summon", "Spawns an actor on an open tile (default: the one you face), facing an angle (default: back at you).",
            "summon <class> [angle] [tilex tiley]", Cmd_Summon, Cheat | InLevel,
            complete: (_, i) => i == 0 ? _assetManager.GetActorMetadata().Actors.Keys : i == 1 ? ["0", "45", "90", "135", "180", "225", "270", "315"] : []);
        Register("factions", "Lists the factions (factions.yaml), how each treats the others, and who's on each in the level.",
            "factions", Cmd_Factions);
        Register("setfaction", "Puts the nearest living enemy, or every actor of a class, on a faction (for trying factions out).",
            "setfaction <faction> [class]", Cmd_SetFaction, Cheat | InLevel,
            complete: (_, i) => i == 0 ? _factionManager.Table.Names : i == 1 ? _assetManager.GetActorMetadata().Actors.Keys : []);
        Register("settranslation", "Draws the nearest living enemy, or every actor of a class, through a color translation (translations.yaml); none goes back to its own (or its faction's).",
            "settranslation <translation|none> [class]", Cmd_SetTranslation, Cheat | InLevel,
            complete: (_, i) => i == 0 ? _translationManager.Names.Append("none") : i == 1 ? _assetManager.GetActorMetadata().Actors.Keys : []);
        Register("translations", "Lists the color translations (translations.yaml).", "translations",
            _ => _consoleManager.Print(string.Join(", ", _translationManager.Names.DefaultIfEmpty("(none)"))));
        levelEditCommands.Register();       // wallheight, sky, weather, fog, light(s), zone*, height, tag, patch, flat(s) (Commands/LevelEditCommands.cs)
        Register("spectate", "Watches through another actor's eyes while you play on: the next or previous enemy (default next), the next of a class, or back to the player. Not saved; each level starts on the player.",
            "spectate [next|prev|player|<class>]", Cmd_Spectate, Cheat | InLevel,
            complete: (_, i) => i == 0 ? ["next", "prev", "player", .. SpectateCandidates().Select(a => a.Name).Distinct()] : []);
        playerCommands.Register();      // addplayer, controlplayer, gamemode (Commands/PlayerCommands.cs)
        Register("playercolor", "Shows a player (default: you) in a color (a translation, as game-info player-colors offers); none for their class's own.",
            "playercolor <color|none> [n]", Cmd_PlayerColor, Cheat | InLevel,
            complete: (_, i) => i == 0 ? _translationManager.Names.Append("none") : []);

        //
        // debugging aids and information
        //
        Register("pitch", "Looks up (positive) or down (negative) by that many degrees, as far as the view allows; 0 looks straight ahead.",
            "pitch [degrees]", Cmd_Pitch, InLevel);
        Register("eyeheight", $"Sets how high the view is above the floor, in texels (64 a story, {EYEDEFAULT} standing), until the level is left or reloaded.",
            $"eyeheight [{MINEYE}-{MAXEYE}]", Cmd_EyeHeight, InLevel);
        Register("hurt", "Damages the player.", "hurt [points]", Cmd_Hurt, InLevel | Solo);
        Register("playerclass", "Shows the class being played as and the classes there are; with a name, the class new games are played as.",
            "playerclass [class]", Cmd_PlayerClass, complete: (_, i) => i == 0 ? PlayerClasses() : []);
        Register("where", "Shows the player's position and what's at their tile.", "where", Cmd_Where, InLevel, aliases: ["pos"]);
        Register("count", "Counts doors and actors.", "count", Cmd_Count, InLevel);
        Register("actors", "Lists actors, optionally only those whose name contains the filter.", "actors [filter]", Cmd_Actors, InLevel,
            complete: (_, i) => i == 0 ? _mapManager.GetActors().Select(a => a.Name) : []);
        Register("maps", "Lists the levels that map can warp to.", "maps", Cmd_Maps);
        Register("scripts", "Lists the Lua scripts that loaded (scripts/*.lua), those left out and why, and the actions they give.", "scripts", Cmd_Scripts);
        Register("lua", "Runs Lua in the scripts' state (`me` is the player). Strings take 'single quotes'; separate statements with spaces, not ;.",
            "lua <code>", Cmd_Lua, Cheat);
        demoCommands.Register();        // playdemo, demotest, recorddemo (Commands/DemoCommands.cs)
        saveCommands.Register();        // saves, autosave, save, load (Commands/SaveCommands.cs)
        Register("fps", "Toggles the frame rate counter.", "fps [0|1]", Cmd_Fps, complete: Values("0", "1"));
        Register("slowmo", "Waits extra VBLs every frame (0 = off).", "slowmo [0-50]", Cmd_SlowMo);
        Register("vbls", "Adds extra VBLs per frame (0 = off).", "vbls [0-8]", Cmd_Vbls);
        Register("fade", "Fades the view out to black and back in, to try a fade style.",
            "fade <palette|fizzle|melt|mosaic> [tics]", Cmd_Fade, InLevel,
            complete: Values(Enum.GetNames<FadeStyle>().Select(n => n.ToLowerInvariant()).ToArray()));
        Register("screenshot","Saves the screen, without the console, to WSHOT###.BMP in the screenshots folder.", "screenshot", _ => screenshotPending = true);
        Register("quit", "Quits the game immediately.", "quit", _ => _gameEngineManager.Quit(""));
    }

    // What commands act on, and how they describe it (Commands/ConsoleTargets.cs)
    static ConsoleTargets consoleTargets = null!;       // made in InitServices

    static (int x, int y) TileArg(string[] args, int index) => consoleTargets.TileArg(args, index);
    static List<Entities.Actors.Monster> ChosenActors(string? className) => consoleTargets.ChosenActors(className);
    static string WallSpriteDescription(Entities.Actors.Actor actor) => consoleTargets.WallSpriteDescription(actor);
    static string DescribePatch(int patch) => consoleTargets.DescribePatch(patch);
    static string DescribeZone(int zone) => consoleTargets.DescribeZone(zone);

    static void Register(string name, string help, string usage, Action<string[]> run,
        ConsoleCommandFlags flags = ConsoleCommandFlags.None, string[]? aliases = null, ConsoleCompleter? complete = null) =>
        _consoleManager.Register(name, help, usage, run, flags, aliases, complete);

    /// <summary>The play screen round the view, drawn again after a video mode change blanks it</summary>
    static void RedrawAfterVideoChange()
    {
        if (_mapManager.Player != null && viewsize != 21)
            DrawPlayScreen();
    }

    /// <summary>The mouse look and controller settings as console commands, for saving to controls.cfg.</summary>
    internal static IEnumerable<string> GetControllerSettingCommands() => controlSettings.SettingCommands();

    /// <summary>The item classes `give` accepts by name (see GiveItem).</summary>
    static IEnumerable<string> GivableItems() =>
        new[] { "Weapon", "Ammo", "Key", "BasicArmor" }
            .SelectMany(_inventoryManager.GetClassesDerivedFrom)
            .Where(item => _inventoryManager.FindClass(item, "WeaponGiver") == null);

    /*
    =============================================================================

                                    CHEATS

    =============================================================================
    */

    private static void Cmd_God(string[] args)
    {
        // 0 = off, 1 = on, 2 = on without the damage flash (ApplyDamageToPlayer)
        godmode = (byte)(args.Length > 0 ? ParseInt(args[0], 0, 2) : godmode == 0 ? 1 : 0);

        _consoleManager.Print(godmode switch
        {
            0 => "God mode OFF",
            1 => "God mode ON",
            _ => "God mode ON (no flash)",
        });
    }

    private static void Cmd_Noclip(string[] args)
    {
        noclip = (byte)(Toggle(args, noclip != 0) ? 1 : 0);
        _consoleManager.Print(noclip != 0 ? "No clipping ON" : "No clipping OFF");
    }

    private static void Cmd_Pitch(string[] args)
    {
        double max = MaxPitch();
        if (args.Length > 0)
        {
            if (!double.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double degrees))
                throw new ArgumentException("usage: pitch [degrees]");
            playerpitch = Math.Clamp(degrees, -max, max);
        }
        _consoleManager.Print($"Pitch is {playerpitch:0.#} degrees (up to {max:0.#} either way)");
    }

    private static void Cmd_EyeHeight(string[] args)
    {
        if (args.Length > 0)
        {
            if (!int.TryParse(args[0], out int z))
                throw new ArgumentException($"usage: eyeheight [{MINEYE}-{MAXEYE}]");
            SetEyeHeight(z);
        }
        _consoleManager.Print($"Eye height is {playereyez} ({MINEYE}-{MAXEYE}, 64 a story, {EYEDEFAULT} standing)");
    }

    private static void Cmd_PlayerClass(string[] args)
    {
        var classes = PlayerClasses();
        if (args.Length > 0)
        {
            // Any class descended from Player works, listed in game-info or not (for trying one out)
            newGamePlayerClass = FindPlayerClass(args[0])
                ?? throw new ArgumentException($"no player class {args[0]}; there's {string.Join(", ", classes)}");
        }

        if (ingame)
            _consoleManager.Print($"Playing as {playerstate.playerclass}");
        _consoleManager.Print($"New games are played as {newGamePlayerClass ?? DefaultPlayerClass}");
        _consoleManager.Print($"Classes: {string.Join(", ", classes)}");
    }

    private static void Cmd_Give(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("give what? e.g. give all, give health 50, give GoldKey");

        int? amount = args.Length > 1 ? ParseInt(args[1], 1, 1_000_000) : null;

        switch (args[0].ToLowerInvariant())
        {
            case "all":
                HealSelf(MaxHealth);
                GiveAllWeapons();
                GiveAllAmmo(int.MaxValue);
                GiveAllKeys();
                _consoleManager.Print("Gave everything");
                break;

            case "health":
                HealSelf(amount ?? MaxHealth);
                _consoleManager.Print($"Health is {playerstate.health}");
                break;

            case "points":
                GivePoints(amount ?? 100000);
                _consoleManager.Print($"Score is {playerstate.score}");
                break;

            case "keys":
                GiveAllKeys();
                _consoleManager.Print("Gave all keys");
                break;

            case "weapons":
                GiveAllWeapons();
                _consoleManager.Print("Gave all weapons");
                break;

            case "ammo":
                _consoleManager.Print($"Gave {GiveAllAmmo(amount ?? int.MaxValue)} ammo");
                break;

            default:
                GiveItem(args[0], amount ?? 1);
                break;
        }

        DrawKeys();
        DrawWeapon();
        DrawAmmo();
    }

    /// <summary>Gives one named actordefs item: a key, some ammo, a weapon or armor.</summary>
    static void GiveItem(string name, int amount)
    {
        // Health and treasure aren't held; they're applied on pickup, hence "give health/points".
        // A WeaponGiver only names the weapon to hand out, so it's never an item itself.
        var item = _inventoryManager.FindClass(name, "Weapon", "Ammo", "Key", "BasicArmor");
        if (item == null || _inventoryManager.FindClass(item, "WeaponGiver") != null)
            throw new ArgumentException($"\"{name}\" is not a weapon, ammo, key or armor");

        if (_inventoryManager.FindClass(item, "BasicArmor") != null)
        {
            TryGiveArmor(item);
            _consoleManager.Print($"Armor is {playerstate.armor} ({playerstate.armorpercent}%)");
        }
        else if (_inventoryManager.FindClass(item, "Weapon") != null)
        {
            // Goes through the pickup path so a better weapon also gets selected.
            TryGiveWeapon(item, 0);
            _consoleManager.Print($"Gave {item}");
        }
        else if (_inventoryManager.FindClass(item, "Ammo") != null)
            _consoleManager.Print($"Gave {GiveAmmo(item, amount)} {item}");
        else
            _consoleManager.Print($"Gave {_inventoryManager.Give(item, amount)} {item}");
    }

    static void GiveAllWeapons()
    {
        foreach (var weapon in AllWeapons())
            TryGiveWeapon(weapon, 0);
    }

    static void GiveAllKeys()
    {
        foreach (var key in _inventoryManager.GetClassesDerivedFrom("Key"))
            _inventoryManager.Give(key, 1);
    }

    private static void Cmd_Map(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("which map? type \"maps\" for a list");

        var maps = _gameEngineManager.GetGameInfo().Maps;
        var map = maps.Keys.FirstOrDefault(k => string.Equals(k, args[0], StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"no map \"{args[0]}\"; type \"maps\" for a list");

        gamestate.mapon = map;
        playstate = playstatetypes.ex_warped;
    }

    private static void Cmd_Teleport(string[] args)
    {
        if (args.Length < 2)
            throw new ArgumentException("usage: tp <tilex> <tiley> [angle]");

        int x = ParseInt(args[0], 0, _mapManager.mapwidth - 1);
        int y = ParseInt(args[1], 0, _mapManager.mapheight - 1);
        int? angle = args.Length > 2 ? ParseInt(args[2], 0, ANGLES - 1) : null;

        if (!TryTeleportPlayer(x, y, angle, out var problem))
            throw new ArgumentException(problem);

        _consoleManager.Print($"Teleported to {x},{y}");
    }

    /// <summary>
    /// Moves the player to a tile, facing <paramref name="angle"/> (degrees, 0 east) when given.
    /// Only onto open floor: a solid tile, a door or a blocking object would trap the player, and
    /// an area number is needed for the sight/sound area bookkeeping.
    /// </summary>
    internal static bool TryTeleportPlayer(int x, int y, int? angle, out string problem)
    {
        problem = "";
        if (x < 0 || y < 0 || x >= _mapManager.mapwidth || y >= _mapManager.mapheight)
        {
            problem = $"tile {x},{y} is off the level";
            return false;
        }
        if (_mapManager.tilemap[x, y] != 0 || _mapManager.actorat[x, y] != null
            || !_mapManager.VALIDAREA(_mapManager.MAPSPOT(x, y, 0)))
        {
            problem = $"tile {x},{y} is not open floor";
            return false;
        }

        player.SetPosition(x, y);
        player.AreaNumber = (byte)(_mapManager.MAPSPOT(x, y, 0) - _mapManager.Floors.AreaTile);
        if (angle is { } degrees)
            player.Angle = (short)(((degrees % ANGLES) + ANGLES) % ANGLES);
        ConnectAreas();
        return true;
    }

    private static void Cmd_Diag(string[] args)
    {
        if (args.Length == 0 || !Enum.TryParse<WallShape>(args[0], ignoreCase: true, out var shape)
            || !Enum.IsDefined(shape))
            throw new ArgumentException("usage: diag <square|solidnw|solidne|solidsw|solidse> [tilex tiley]");

        var (x, y) = TileArg(args, 1);

        // A trigger shares the object plane, so a pushwall can't also be a diagonal.
        if (!_mapManager.IsPlainWall(x, y) || _mapManager.GetTrigger(x, y) != null)
            throw new ArgumentException($"tile {x},{y} is not a plain wall");

        var marker = _mapManager.SetWallShape(x, y, shape)
            ?? throw new ArgumentException($"the mapdefs have no diagonal marker for {shape}");

        _consoleManager.Print($"Tile {x},{y} is now {shape} (plane 1: {marker})");
    }

    private static void Cmd_Summon(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: summon <class> [angle] [tilex tiley]");

        var className = _assetManager.GetActorMetadata().Actors.Keys
            .FirstOrDefault(k => k.Equals(args[0], StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"no actor class named {args[0]}");

        // Facing back at the player by default, so a panel is seen from its front.
        int angle = args.Length > 1 ? ParseInt(args[1], 0, 359) : FacingDir(player.Angle) switch
        {
            controldirs.di_east => 180,
            controldirs.di_north => 270,
            controldirs.di_west => 0,
            _ => 90,
        };
        var (x, y) = TileArg(args, 2);

        if (_mapManager.tilemap[x, y] != 0 || _mapManager.actorat[x, y] != null
            || !_mapManager.VALIDAREA(_mapManager.MAPSPOT(x, y, 0)))
            throw new ArgumentException($"tile {x},{y} is not open floor");

        var before = _mapManager.GetActors().Last;
        _mapManager.SpawnThing(x, y, new MapActorTranslation { Class = className, Angles = angle });
        var actor = _mapManager.GetActors().Last;
        if (actor == before || actor == null)
            throw new ArgumentException($"{className} can't be spawned");

        _consoleManager.Print($"Spawned {className} at {x},{y} facing {angle}{WallSpriteDescription(actor.Value)}");
    }

    private static void Cmd_Factions(string[] args)
    {
        var table = _factionManager.Table;

        // Who's on what side in the level: the living players and enemies, by faction
        var members = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (_mapManager.Player != null)
        {
            foreach (var actor in _mapManager.GetActors())
            {
                bool alive = actor is Entities.Actors.PlayerPawn pawn ? pawn.State.health > 0
                    : actor is Entities.Actors.Monster && actor.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE);
                if (alive && !actor.IsRemoved && actor.Faction != null)
                    members[actor.Faction] = members.GetValueOrDefault(actor.Faction) + 1;
            }
        }

        var names = table.Names.Concat(members.Keys).Append(table.PlayerFaction).Append(table.MonsterFaction)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _consoleManager.Print($"Players are {table.PlayerFaction} and enemies {table.MonsterFaction} unless their class says otherwise"
            + (table.Names.Count == 0 ? " (no factions.yaml)" : ""));
        foreach (var faction in names)
        {
            var relations = names.Where(other => !other.Equals(faction, StringComparison.OrdinalIgnoreCase))
                .Select(other => $"{other} {table.Relation(faction, other).ToString().ToLowerInvariant()}");
            var shown = table.DisplayName(faction);
            _consoleManager.Print($"  {faction}{(shown != faction ? $" ({shown})" : "")}: {members.GetValueOrDefault(faction)} in the level"
                + (relations.Any() ? $"; {string.Join(", ", relations)}" : ""));
        }
    }

    private static void Cmd_SetFaction(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: setfaction <faction> [class]");
        var faction = args[0];
        if (!_factionManager.Table.IsKnown(faction))
            _consoleManager.Print($"{faction} isn't in factions.yaml; it's a side of its own");

        var chosen = ChosenActors(args.Skip(1).FirstOrDefault());
        foreach (var monster in chosen)
            monster.SetFaction(faction);
        _consoleManager.Print(chosen.Count == 1
            ? $"{chosen[0].Name} at {chosen[0].TileX},{chosen[0].TileY} is now {faction}"
            : $"{chosen.Count} {chosen[0].Name}s are now {faction}");
    }

    private static void Cmd_SetTranslation(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: settranslation <translation|none> [class]");
        string? translation = args[0].Equals("none", StringComparison.OrdinalIgnoreCase) ? null : args[0];
        if (translation != null && !_translationManager.Exists(translation))
            throw new ArgumentException($"no translation {translation} in translations.yaml ({string.Join(", ", _translationManager.Names)})");

        var chosen = ChosenActors(args.Skip(1).FirstOrDefault());
        foreach (var monster in chosen)
            monster.Translation = translation;
        _consoleManager.Print($"{(chosen.Count == 1 ? $"{chosen[0].Name} at {chosen[0].TileX},{chosen[0].TileY} is" : $"{chosen.Count} {chosen[0].Name}s are")} "
            + (translation == null ? "back to their own colors" : $"drawn through {translation}"));
    }

    private static void Cmd_PlayerColor(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: playercolor <color|none> [n]");
        string? color = args[0].Equals("none", StringComparison.OrdinalIgnoreCase) ? null : args[0];
        if (color != null && !_translationManager.Exists(color))
            throw new ArgumentException($"no translation {color} in translations.yaml ({string.Join(", ", _translationManager.Names)})");
        int n = args.Length > 1 ? ParseInt(args[1], 1, players.Count) : consoleplayer + 1;

        players[n - 1].Color = color;
        _consoleManager.Print($"Player {n} is {(color == null ? "in their class's own colors" : $"drawn in {color}")}");
    }

    private static void Cmd_KillAll(string[] args)
    {
        // Snapshot first: Kill can drop items, which adds to the actor list.
        var enemies = _mapManager.GetActors().OfType<Entities.Actors.Monster>()
            .Where(a => MapManager.IsEnemy(a) && a.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE))
            .ToList();

        foreach (var enemy in enemies)
            enemy.Kill();

        _consoleManager.Print($"Killed {enemies.Count} enemies");
    }

    private static void Cmd_Overhead(string[] args)
    {
        // BasicOverhead is a blocking screen that waits for a key, so it can't run while the
        // console owns the keyboard. Close it and run the view from the play loop instead.
        _consoleManager.Close();
        _consoleManager.Defer(() =>
        {
            BasicOverhead();
            if (viewsize < 20)
                DrawPlayBorder();
            lasttimecount = (int)GameEngineManager.GetTimeCount();
        });
    }

    /*
    =============================================================================

                            DEBUGGING AIDS AND INFORMATION

    =============================================================================
    */

    private static void Cmd_Hurt(string[] args)
    {
        TakeDamage(args.Length > 0 ? ParseInt(args[0], 1, 1000) : 16, null!);
        _consoleManager.Print($"Health is {playerstate.health}, armor {playerstate.armor} ({playerstate.armorpercent}%)");
    }

    private static void Cmd_Where(string[] args)
    {
        int x = player.TileX, y = player.TileY;

        _consoleManager.Print($"X: {player.X} ({player.X % MapConstants.TILEGLOBAL})  Y: {player.Y} ({player.Y % MapConstants.TILEGLOBAL})  Angle: {player.Angle}");
        _consoleManager.Print($"Tile: {x},{y}  Area: {player.AreaNumber}  Map: {gamestate.mapon}");
        _consoleManager.Print($"tilemap: {_mapManager.tilemap[x, y]}  plane 1: {_mapManager.MAPSPOT(x, y, 1)}  spotvis: {_mapManager.spotvis[x, y]}");
        _consoleManager.Print($"actorat: {_mapManager.actorat[x, y]?.GetType().Name ?? "(nothing)"}");

        var diagonals = new[] { (x + 1, y), (x, y - 1), (x - 1, y), (x, y + 1) }
            .Where(t => _mapManager.wallshape[t.Item1, t.Item2] != WallShape.Square)
            .Select(t => $"{t.Item1},{t.Item2} {_mapManager.wallshape[t.Item1, t.Item2]}");
        if (diagonals.Any())
            _consoleManager.Print($"diagonal walls beside: {string.Join("  ", diagonals)}");

        var (fx, fy) = TileArg([], 0);
        _consoleManager.Print($"wall heights: level {wallstories}  tallest {_mapManager.MaxWallStories}  "
            + $"faced tile {fx},{fy}: {_mapManager.WallStories(fx, fy)} (plane 3: {_mapManager.MAPSPOT(fx, fy, MapManager.HEIGHTPLANE)})");

        _consoleManager.Print($"flats: floor {_mapManager.FlatName(x, y, false)}  ceiling {_mapManager.FlatName(x, y, true)}  "
            + $"(plane 2: {_mapManager.MAPSPOT(x, y, MapManager.FLATPLANE)})");

        _consoleManager.Print($"tags (plane 4): tile {_mapManager.GetTag(x, y)}  faced tile {fx},{fy}: {_mapManager.GetTag(fx, fy)}");
        _consoleManager.Print($"faced tile {fx},{fy}: {DescribePatch(_mapManager.GetPatch(fx, fy))}");

        int zone = _mapManager.GetZone(x, y);
        _consoleManager.Print($"light zone (plane 5): {zone} ({DescribeZone(zone)})");
    }

    private static void Cmd_Count(string[] args)
    {
        var actors = _mapManager.GetActors();
        int enemies = actors.Count(MapManager.IsEnemy);
        int alive = actors.Count(a => MapManager.IsEnemy(a) && a.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE));
        int active = actors.Count(a => a.Active != activetypes.ac_no);

        _consoleManager.Print($"Doors: {doornum}");
        for (int i = 0; i < doornum; i++)
        {
            var door = doorobjlist[i];
            if (!string.IsNullOrEmpty(door.Lock))
                _consoleManager.Print($"  locked ({door.Lock}) at {door.tilex},{door.tiley}");
        }
        _consoleManager.Print($"Actors: {actors.Count}  active: {active}");
        _consoleManager.Print($"Enemies: {enemies}  alive: {alive}");
        _consoleManager.Print($"Kills: {gamestate.killcount}/{gamestate.killtotal}  Secrets: {gamestate.secretcount}/{gamestate.secrettotal}  Treasure: {gamestate.treasurecount}/{gamestate.treasuretotal}");
    }

    private static void Cmd_Actors(string[] args)
    {
        string filter = args.Length > 0 ? args[0] : "";
        int shown = 0;

        foreach (var actor in _mapManager.GetActors())
        {
            if (!actor.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            var line = new StringBuilder($"{actor.Name} at {actor.TileX},{actor.TileY}");
            if (actor.CurrentState != null)
                line.Append($"  state {actor.CurrentState.StateName}");
            if (MapManager.IsEnemy(actor))
                line.Append($"  hp {actor.Hitpoints}");
            if (actor.Active != activetypes.ac_no)
                line.Append("  active");
            if (actor.Tag != 0)
                line.Append($"  tag {actor.Tag}");
            line.Append(WallSpriteDescription(actor));

            _consoleManager.Print(line.ToString());
            shown++;
        }

        _consoleManager.Print($"{shown} actor(s)");
    }

    private static void Cmd_Maps(string[] args)
    {
        // Ten to a row (an episode's worth in Wolf3D) so a whole game fits in the console.
        var names = _gameEngineManager.GetGameInfo().Maps.Keys
            .Select(name => name == gamestate?.mapon ? $"[{name}]" : name)
            .ToList();

        for (int i = 0; i < names.Count; i += 10)
            _consoleManager.Print(string.Join(' ', names.Skip(i).Take(10)));

        if (_mapManager.Player != null)
            _consoleManager.Print($"Current map is {gamestate.mapon}");
    }


    private static void Cmd_Fps(string[] args)
    {
        fpscounter = Toggle(args, fpscounter);
        _consoleManager.Print(fpscounter ? "FPS counter ON" : "FPS counter OFF");
    }

    private static void Cmd_Fade(string[] args)
    {
        if (args.Length == 0 || !Enum.TryParse<FadeStyle>(args[0], ignoreCase: true, out var style))
            throw new ArgumentException("usage: fade <palette|fizzle|melt|mosaic> [tics]");

        // Without a length, as long as a screen fade
        int? tics = args.Length > 1 ? ParseInt(args[1], 1, 700) : _videoManager.FadeTics;

        // Fading blocks, so run it from the play loop with the console out of the picture.
        _consoleManager.Close();
        _consoleManager.Defer(() =>
        {
            ThreeDRefresh();
            _videoManager.FadeOut(style, new Color { Alpha = 255 }, 30, tics);
            _videoManager.FadeIn(style, 30, tics);
            lasttimecount = (int)GameEngineManager.GetTimeCount();
        });
    }

    private static void Cmd_SlowMo(string[] args)
    {
        if (args.Length > 0)
            singlestep = (byte)ParseInt(args[0], 0, 50);
        _consoleManager.Print($"slowmo is {singlestep}");
    }

    private static void Cmd_Vbls(string[] args)
    {
        if (args.Length > 0)
            extravbls = ParseInt(args[0], 0, 8);
        _consoleManager.Print($"vbls is {extravbls}");
    }
}
