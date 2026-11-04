using System.Text.RegularExpressions;
using PFWolf.Assets;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>A stat's value: done of total, or text (a time, a level's name)</summary>
internal readonly record struct StatValue(int Done, int Total, string? Text = null)
{
    /// <summary>What a line's text is filled with: the text, else done and total</summary>
    public object[] Args => Text != null ? [Text] : [Done, Total];
}

/// <summary>What the stats are read from</summary>
/// <param name="Game">The game: the level's kills, secrets and treasure, its time and cluster</param>
/// <param name="LevelObjectives">The level's objectives as they stand (game-info objectives)</param>
/// <param name="FinishedLevels">This game's finished levels, the latest run of each (GameSession.LevelRatios)</param>
/// <param name="Maps">Game-info maps, for each level's cluster</param>
/// <param name="Item">An inventory item the player holds, of its inventory.maxamount; null for no such item</param>
/// <param name="Records">Every map's best from game to game (records.cfg)</param>
/// <param name="MapDisplayName">A map's name as the game shows it</param>
/// <param name="FormatTime">A time in tics as the game shows it</param>
internal sealed record StatSources(
    Func<GameState> Game,
    Func<IReadOnlyDictionary<string, ObjectiveCount>> LevelObjectives,
    Func<IReadOnlyDictionary<string, LevelRatio>> FinishedLevels,
    Func<IReadOnlyDictionary<string, MapInfo>> Maps,
    Func<string, (int Held, int Max)?> Item,
    Func<IEnumerable<LevelResult>> Records,
    Func<string, string> MapDisplayName,
    Func<int, string> FormatTime);

/*
=============================================================================

                                STAT VALUES

The stats a game's screens and scripts name (game-info automap-stats, Lua game.stat), each done
of a total unless it's text:
    kill, secret, treasure   the level's kills, secrets and treasure (items counted: COUNTITEM)
    objective.NAME           a level objective (game-info objectives)
    time                     the time on the level, as text
    level                    the level's name, as text
    item.NAME                an inventory item held, of its inventory.maxamount
Each of the first four can be put in a scope in front:
    level.    the level as it stands (the same as none)
    episode.  this game's finished levels in the level's cluster (the latest run of each), and
              this level as it stands: what the victory screen adds up
    game.     every level this game has finished, and this level as it stands
    ever.     every map's best from game to game (records.cfg); this level only once finished
A name the game doesn't know is nothing (null).

=============================================================================
*/

internal sealed partial class StatValues(StatSources sources)
{
    enum Scope { Level, Episode, Game, Ever }

    [GeneratedRegex(@"\{(\d+)\}|%[ds]")]
    private static partial Regex Placeholder();

    /// <summary>A stat by name (see STAT VALUES), or null for one the game doesn't know</summary>
    public StatValue? Get(string name)
    {
        name = name.Trim();
        var scope = Scope.Level;
        foreach (var (prefix, s) in Scopes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                (scope, name) = (s, name[prefix.Length..]);
                break;
            }
        }

        if (name.Equals("level", StringComparison.OrdinalIgnoreCase))
            return new StatValue(0, 0, sources.MapDisplayName(sources.Game().MapOn ?? ""));
        if (name.StartsWith("item.", StringComparison.OrdinalIgnoreCase))
            return sources.Item(name["item.".Length..]) is { } held ? new StatValue(held.Held, held.Max) : null;

        if (name.Equals("time", StringComparison.OrdinalIgnoreCase))
        {
            int tics = scope switch
            {
                Scope.Ever => sources.Records().Sum(r => r.Time),
                Scope.Level => sources.Game().TimeCount,
                _ => Finished(scope).Sum(r => r.Time * 70) + sources.Game().TimeCount,
            };
            return new StatValue(tics, 0, sources.FormatTime(tics));
        }

        if (!IsCount(name))
            return null;
        return scope switch
        {
            Scope.Ever => Sum(sources.Records().Select(r => OfRecord(r, name))),
            Scope.Level => Live(name),
            _ => Sum(Finished(scope).Select(r => OfRatio(r, name)).Append(Live(name))),
        };
    }

    /// <summary>
    /// A line's text (text or a $language key, already looked up) with its values filled in: {0},
    /// {1}... by place, or each %d and %s in turn, from each value's done and total (or its text)
    /// in order. A value the game doesn't know fills in 0 and 0.
    /// </summary>
    public string Format(string text, IEnumerable<string> values)
    {
        var args = values.SelectMany(v => (Get(v) ?? new StatValue(0, 0)).Args).ToArray();
        int next = 0;
        return Placeholder().Replace(text, match =>
        {
            int i = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : next++;
            return i < args.Length ? args[i].ToString() ?? "" : "";
        });
    }

    static readonly (string Prefix, Scope Scope)[] Scopes =
    [
        ("level.", Scope.Level), ("episode.", Scope.Episode), ("game.", Scope.Game), ("ever.", Scope.Ever),
    ];

    static bool IsCount(string name) =>
        name.Equals("kill", StringComparison.OrdinalIgnoreCase) || name.Equals("secret", StringComparison.OrdinalIgnoreCase)
        || name.Equals("treasure", StringComparison.OrdinalIgnoreCase)
        || (name.StartsWith("objective.", StringComparison.OrdinalIgnoreCase) && name.Length > "objective.".Length);

    // This game's finished levels in the scope, but not the one being played: it counts as it stands
    IEnumerable<LevelRatio> Finished(Scope scope)
    {
        var game = sources.Game();
        var maps = sources.Maps();
        return sources.FinishedLevels()
            .Where(level => !level.Key.Equals(game.MapOn, StringComparison.OrdinalIgnoreCase))
            .Where(level => scope != Scope.Episode || (maps.TryGetValue(level.Key, out var map) && map.Cluster == game.Cluster))
            .Select(level => level.Value);
    }

    StatValue Live(string name)
    {
        var game = sources.Game();
        return name.ToLowerInvariant() switch
        {
            "kill" => new(game.KillCount, game.KillTotal),
            "secret" => new(game.SecretCount, game.SecretTotal),
            "treasure" => new(game.TreasureCount, game.TreasureTotal),
            _ => sources.LevelObjectives().TryGetValue(name["objective.".Length..], out var count) ? new(count.Done, count.Total) : new(0, 0),
        };
    }

    static StatValue OfRatio(LevelRatio ratio, string name) => name.ToLowerInvariant() switch
    {
        "kill" => new(ratio.KillCount, ratio.KillTotal),
        "secret" => new(ratio.SecretCount, ratio.SecretTotal),
        "treasure" => new(ratio.TreasureCount, ratio.TreasureTotal),
        _ => ratio.Objectives.TryGetValue(name["objective.".Length..], out var o) ? new(o.Done, o.Total) : new(0, 0),
    };

    static StatValue OfRecord(LevelResult record, string name) => name.ToLowerInvariant() switch
    {
        "kill" => new(record.Kills, record.KillTotal),
        "secret" => new(record.Secrets, record.SecretTotal),
        "treasure" => new(record.Items, record.ItemTotal),
        _ => record.Objectives.TryGetValue(name["objective.".Length..], out var o) ? new(o.Done, o.Total) : new(0, 0),
    };

    static StatValue Sum(IEnumerable<StatValue> values)
    {
        int done = 0, total = 0;
        foreach (var value in values)
            (done, total) = (done + value.Done, total + value.Total);
        return new(done, total);
    }
}
