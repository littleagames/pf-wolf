using System.Globalization;

namespace PFWolf.Gameplay;

/*
=============================================================================

LEVEL RECORDS

The best the player has done on each map, kept from game to game in records.cfg in the game's
config folder (beside progress.cfg: Gameplay/Unlocks.cs), for a game's scripts to unlock things
by (Lua game.record(map), and game.onlevelend's result for the level just finished).

A map's record is made when it's finished (its exit, a secret exit or the game won; not dying or
leaving the game), and each finish only betters it, field by field: the most kills, secrets,
items and stealth kills (with their totals), the quickest time, under par ever, unseen to the end
ever, and the most done of each level objective (game-info objectives). A demo playing back
records nothing.

=============================================================================
*/

/// <summary>What one finish of a level came to, or the best of a map's finishes</summary>
internal sealed record LevelResult
{
    public int Kills { get; init; }
    public int KillTotal { get; init; }
    public int Secrets { get; init; }
    public int SecretTotal { get; init; }
    public int Items { get; init; }
    public int ItemTotal { get; init; }

    /// <summary>The level's time, in tics (70 a second)</summary>
    public int Time { get; init; }

    /// <summary>The map's par time in seconds (game-info par-time), 0 for none</summary>
    public int Par { get; init; }

    /// <summary>Finished in less than the par time (never, for a map with none)</summary>
    public bool UnderPar { get; init; }

    /// <summary>Finished with no enemy having noticed the player (Gameplay/Stealth.cs)</summary>
    public bool Undetected { get; init; }

    public int StealthKills { get; init; }

    /// <summary>Each level objective's done and total</summary>
    public Dictionary<string, (int Done, int Total)> Objectives { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The better of two, field by field</summary>
    public LevelResult Best(LevelResult other)
    {
        var objectives = new Dictionary<string, (int Done, int Total)>(Objectives, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, count) in other.Objectives)
            objectives[name] = objectives.TryGetValue(name, out var mine) ? Better(mine, count) : count;

        var kills = Better((Kills, KillTotal), (other.Kills, other.KillTotal));
        var secrets = Better((Secrets, SecretTotal), (other.Secrets, other.SecretTotal));
        var items = Better((Items, ItemTotal), (other.Items, other.ItemTotal));
        return new LevelResult
        {
            Kills = kills.Done, KillTotal = kills.Total,
            Secrets = secrets.Done, SecretTotal = secrets.Total,
            Items = items.Done, ItemTotal = items.Total,
            Time = Time <= 0 ? other.Time : other.Time <= 0 ? Time : Math.Min(Time, other.Time),
            Par = other.Par > 0 ? other.Par : Par,
            UnderPar = UnderPar || other.UnderPar,
            Undetected = Undetected || other.Undetected,
            StealthKills = Math.Max(StealthKills, other.StealthKills),
            Objectives = objectives,
        };
    }

    // The higher share done (a total of none is all done)
    static (int Done, int Total) Better((int Done, int Total) a, (int Done, int Total) b) =>
        Share(b) > Share(a) || (Share(b) == Share(a) && b.Done > a.Done) ? b : a;

    static double Share((int Done, int Total) count) => count.Total <= 0 ? 1 : count.Done / (double)count.Total;

    /// <summary>As records.cfg has it: "kills=10/12 secrets=2/3 ... objective.intel=2/3"</summary>
    public string ToLine()
    {
        var parts = new List<string>
        {
            $"kills={Kills}/{KillTotal}", $"secrets={Secrets}/{SecretTotal}", $"items={Items}/{ItemTotal}",
            $"time={Time}", $"par={Par}", $"underpar={(UnderPar ? 1 : 0)}", $"undetected={(Undetected ? 1 : 0)}",
            $"stealthkills={StealthKills}",
        };
        parts.AddRange(Objectives.Select(o => $"objective.{o.Key}={o.Value.Done}/{o.Value.Total}"));
        return string.Join(' ', parts);
    }

    public static LevelResult Parse(IEnumerable<string> fields)
    {
        int kills = 0, killTotal = 0, secrets = 0, secretTotal = 0, items = 0, itemTotal = 0, time = 0, par = 0, stealthKills = 0;
        bool underPar = false, undetected = false;
        var objectives = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            int eq = field.IndexOf('=');
            if (eq <= 0)
                continue;
            var key = field[..eq];
            var value = field[(eq + 1)..];
            switch (key.ToLowerInvariant())
            {
                case "kills": (kills, killTotal) = Pair(value); break;
                case "secrets": (secrets, secretTotal) = Pair(value); break;
                case "items": (items, itemTotal) = Pair(value); break;
                case "time": time = Int(value); break;
                case "par": par = Int(value); break;
                case "underpar": underPar = Int(value) != 0; break;
                case "undetected": undetected = Int(value) != 0; break;
                case "stealthkills": stealthKills = Int(value); break;
                default:
                    if (key.StartsWith("objective.", StringComparison.OrdinalIgnoreCase) && key.Length > 10)
                        objectives[key[10..]] = Pair(value);
                    break;
            }
        }
        return new LevelResult
        {
            Kills = kills, KillTotal = killTotal, Secrets = secrets, SecretTotal = secretTotal, Items = items, ItemTotal = itemTotal,
            Time = time, Par = par, UnderPar = underPar, Undetected = undetected, StealthKills = stealthKills, Objectives = objectives,
        };
    }

    static int Int(string text) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

    static (int, int) Pair(string text)
    {
        int slash = text.IndexOf('/');
        return slash < 0 ? (Int(text), 0) : (Int(text[..slash]), Int(text[(slash + 1)..]));
    }
}

internal sealed class LevelRecords(Func<string> path)
{
    public const string FileName = "records.cfg";

    readonly Dictionary<string, LevelResult> records = new(StringComparer.OrdinalIgnoreCase);
    bool loaded;

    /// <summary>A map's best, or null if it's never been finished</summary>
    public LevelResult? Get(string map)
    {
        Load();
        return records.GetValueOrDefault(map);
    }

    /// <summary>The maps finished so far</summary>
    public IReadOnlyCollection<string> Maps
    {
        get
        {
            Load();
            return records.Keys;
        }
    }

    /// <summary>A finish of a map, bettering its record</summary>
    /// <returns>The map's record now</returns>
    public LevelResult Record(string map, LevelResult result)
    {
        Load();
        var best = records.TryGetValue(map, out var old) ? old.Best(result) : result;
        records[map] = best;
        Save();
        return best;
    }

    /// <summary>Forgets a map's record, or every one</summary>
    public bool Clear(string? map)
    {
        Load();
        bool any = map == null ? records.Count > 0 : records.Remove(map);
        if (map == null)
            records.Clear();
        if (any)
            Save();
        return any;
    }

    void Load()
    {
        if (loaded)
            return;
        loaded = true;

        var file = path();
        if (!File.Exists(file))
            return;
        try
        {
            foreach (var line in File.ReadAllLines(file).Select(l => l.Trim()))
            {
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;
                var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                records[fields[0]] = LevelResult.Parse(fields.Skip(1));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Couldn't read {file} ({e.Message}); no level records.");
        }
    }

    void Save()
    {
        var file = path();
        try
        {
            File.WriteAllLines(file,
            [
                "# The best done on each map finished in this game: its kills, secrets and items (of their",
                "# totals), quickest time in tics, par in seconds, under par and unseen ever, stealth kills,",
                "# and each level objective. The records console command shows or clears them.",
                .. records.Select(r => $"{r.Key} {r.Value.ToLine()}"),
            ]);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Couldn't write {file}: {e.Message}");
        }
    }
}
