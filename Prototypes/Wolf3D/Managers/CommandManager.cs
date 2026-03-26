using Wolf3D;
using static Wolf3D.Program;

namespace Wolf3D.Managers;

internal class CommandManager
{
    private readonly List<(string Command, string Response)> _log = [];
    private readonly Dictionary<string, Func<string[], string>> _commands;

    public CommandManager()
    {
        _commands = new Dictionary<string, Func<string[], string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["give"]      = CmdGive,
            ["drop"]      = CmdDrop,
            ["music"]     = CmdMusic,
            ["killall"]   = CmdKillAll,
            ["pushwalls"] = CmdPushWalls,
        };
    }

    /// <summary>Chronological log of every executed command and its response.</summary>
    public IReadOnlyList<(string Command, string Response)> Log => _log;

    /// <summary>Parses and executes a command string, logs it, and returns the response.</summary>
    public string Execute(string input)
    {
        string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return string.Empty;

        string[] args = parts.Length > 1 ? parts[1..] : [];

        string response = _commands.TryGetValue(parts[0], out var handler)
            ? handler(args)
            : "Invalid command";

        _log.Add((input, response));
        return response;
    }

    // -------------------------------------------------------------------------
    //  give <item> [value]
    // -------------------------------------------------------------------------

    private static string CmdGive(string[] args)
    {
        if (args.Length == 0)
            return "Usage: give <health|ammo|weapon|key|points|all> [value]";

        switch (args[0].ToLower())
        {
            case "health":
            {
                int amount = args.Length > 1 && int.TryParse(args[1], out int v) ? v : 25;
                Program.HealSelf(amount);
                return $"Gave {amount} health";
            }
            case "ammo":
            {
                int amount = args.Length > 1 && int.TryParse(args[1], out int v) ? v : 25;
                Program.GiveAmmo(amount);
                return $"Gave {amount} ammo";
            }
            case "weapon":
            {
                if (args.Length < 2)
                    return "Usage: give weapon <knife|pistol|machinegun|chaingun>";

                weapontypes wt = args[1].ToLower() switch
                {
                    "knife"      => weapontypes.wp_knife,
                    "pistol"     => weapontypes.wp_pistol,
                    "machinegun" => weapontypes.wp_machinegun,
                    "chaingun"   => weapontypes.wp_chaingun,
                    _            => (weapontypes)(-2)
                };

                if ((int)wt == -2)
                    return $"Unknown weapon '{args[1]}'";

                Program.GiveWeapon(wt);
                return $"Gave {args[1]}";
            }
            case "key":
            {
                if (args.Length < 2 || !int.TryParse(args[1], out int k) || k < 1 || k > 4)
                    return "Usage: give key <1-4>";

                Program.GiveKey(k - 1);
                return $"Gave key {k}";
            }
            case "points":
            {
                if (args.Length < 2 || !int.TryParse(args[1], out int pts) || pts < 0)
                    return "Usage: give points <amount>";

                Program.GivePoints(pts);
                return $"Gave {pts} points";
            }
            case "all":
                Program.HealSelf(99);
                Program.GiveAmmo(99);
                Program.GiveWeapon(weapontypes.wp_chaingun);
                Program.GiveKey(0);
                Program.GiveKey(1);
                Program.GivePoints(10000);
                return "Gave all items";
            default:
                return $"Unknown item '{args[0]}'";
        }
    }

    // -------------------------------------------------------------------------
    //  drop <item> [value]
    // -------------------------------------------------------------------------

    private static string CmdDrop(string[] args)
    {
        if (args.Length == 0)
            return "Usage: drop <health|ammo|weapon|key> [value]";

        switch (args[0].ToLower())
        {
            case "health":
            {
                int amount = args.Length > 1 && int.TryParse(args[1], out int v) ? v : 25;
                Program.TakeDamage(amount, null);
                return $"Dropped {amount} health";
            }
            case "ammo":
            {
                int amount = args.Length > 1 && int.TryParse(args[1], out int v) ? v : 25;
                Program.gamestate.ammo = (short)Math.Max(0, Program.gamestate.ammo - amount);
                Program.DrawAmmo();
                return $"Dropped {amount} ammo";
            }
            case "weapon":
            {
                if (Program.gamestate.bestweapon <= weapontypes.wp_knife)
                    return "Already at minimum weapon";

                Program.gamestate.bestweapon--;
                Program.gamestate.weapon = Program.gamestate.bestweapon;
                Program.gamestate.chosenweapon = Program.gamestate.bestweapon;
                Program.DrawWeapon();
                return $"Dropped to {Program.gamestate.weapon}";
            }
            case "key":
            {
                if (args.Length < 2 || !int.TryParse(args[1], out int k) || k < 1 || k > 4)
                    return "Usage: drop key <1-4>";

                Program.gamestate.keys &= (short)~(1 << (k - 1));
                Program.DrawKeys();
                return $"Dropped key {k}";
            }
            default:
                return $"Unknown item '{args[0]}'";
        }
    }

    // -------------------------------------------------------------------------
    //  music <track_name>
    // -------------------------------------------------------------------------

    private static string CmdMusic(string[] args)
    {
        if (args.Length == 0)
            return "Usage: music <track_name>  (e.g. CORNER_MUS, DUNGEON_MUS, WARMARCH_MUS)";

        if (!Enum.TryParse<musicnames>(args[0], ignoreCase: true, out var music) || music == musicnames.LASTMUSIC)
            return $"Unknown music '{args[0]}'";

        Program.StartCPMusic(music);
        return $"Music changed to {music}";
    }

    // -------------------------------------------------------------------------
    //  killall
    // -------------------------------------------------------------------------

    private static string CmdKillAll(string[] args)
    {
        var targets = Program.objlist2
            .Where(ob => ob.flags.HasFlag(objflags.FL_SHOOTABLE))
            .ToList();

        foreach (var ob in targets)
            Program.KillActor(ob);

        return targets.Count == 0 ? "No actors to kill" : $"Killed {targets.Count} actor(s)";
    }

    // -------------------------------------------------------------------------
    //  pushwalls
    // -------------------------------------------------------------------------

    private static string CmdPushWalls(string[] args)
    {
        var map = Program._mapManager;
        if (map is null)
            return "Map not loaded";

        int count = 0;
        for (int y = 0; y < map.mapheight; y++)
        {
            for (int x = 0; x < map.mapwidth; x++)
            {
                if (map.MAPSPOT(x, y, 1) == MapConstants.PUSHABLETILE)
                {
                    map.tilemap[x, y] = 0;
                    map.actorat[x, y] = null;
                    map.SetMapSpot(x, y, 1, 0);
                    Program.gamestate.secretcount++;
                    count++;
                }
            }
        }

        return count == 0 ? "No pushwalls found" : $"Opened {count} pushwall(s)";
    }
}

