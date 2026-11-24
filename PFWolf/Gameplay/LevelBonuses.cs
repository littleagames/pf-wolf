using PFWolf.Assets;
using PFWolf.Entities;

namespace PFWolf.Gameplay;

/*
=============================================================================

                                LEVEL BONUSES

Bonuses for getting all of something on a level, once each (Blake Stone's "pinball bonuses",
bstone's CheckPinballBonus/DisplayPinballBonus: ALL ENEMY DESTROYED!, ALL POINTS COLLECTED!, ALL
INFORMANTS ALIVE!). Game-info level-bonuses lists them, in the order they're checked.

As bstone does, they're checked as the score goes up (here at the end of the tic it went up in,
once the kill or pickup is counted), so a level with none of something gives that bonus with the
first points scored there. A bonus with `after` waits for those bonuses to have been given.

A bonus's points are given at once, and its message (with the sound) waits its turn: one is shown
at a time, the next once the last one's time is up. Its style's priority keeps lesser messages
from replacing it meanwhile (HudMessageManager).

What a level has given isn't saved: a level played already (a kept hub level, a saved game)
counts whatever it has all of as given when it's taken up again, so nothing is given twice. A
message still waiting to be shown then is lost (its points were given).

=============================================================================
*/

/// <param name="Settings">Game-info level-bonuses</param>
/// <param name="Stat">A `when`'s (got, total) on the level in play; null for an unknown one</param>
/// <param name="GivePoints">Gives a player points</param>
/// <param name="ShowMessage">Shows every player a message (text or a $language key) in a style</param>
/// <param name="PlaySound">Plays a sound to the player</param>
/// <param name="StyleDuration">How long a message in a style stays up, in tics</param>
internal sealed record LevelBonusHooks(
    Func<LevelBonusesInfo> Settings,
    Func<string, (int Got, int Total)?> Stat,
    Action<PlayerState, int> GivePoints,
    Action<string, string?> ShowMessage,
    Action<string> PlaySound,
    Func<string?, int> StyleDuration);

internal sealed class LevelBonuses(GameSession session, LevelBonusHooks hooks)
{
    readonly HashSet<int> given = [];
    readonly Queue<int> waiting = new();
    int[] lastScores = [];
    int ticsToNext;

    /// <summary>The bonuses (their places in level-bonuses) given on the level in play</summary>
    internal IReadOnlyCollection<int> Given => given;

    /// <summary>
    /// The level's play starts (PlayLoop): a level just begun has given nothing yet; one taken up
    /// again (TimeCount past 0) has given whatever it has all of
    /// </summary>
    internal void Start()
    {
        given.Clear();
        waiting.Clear();
        ticsToNext = 0;
        lastScores = session.Players.Select(p => p.Score).ToArray();

        var bonuses = hooks.Settings().Bonuses;
        if (session.GameState.TimeCount <= 0)
            return;
        for (int i = 0; i < bonuses.Count; i++)
        {
            if (Earned(bonuses, i))
                given.Add(i);
        }
    }

    /// <summary>After everyone has thought (LevelLoop): the score gone up checks the bonuses, and the next waiting message is shown once the last one's done</summary>
    internal void Tick(int tics)
    {
        var settings = hooks.Settings();
        if (settings.Bonuses.Count == 0)
            return;

        if (ScoredBy() is { } scorer)
            Check(settings, scorer);

        if (ticsToNext > 0)
            ticsToNext -= tics;
        if (ticsToNext <= 0 && waiting.TryDequeue(out var next))
        {
            var bonus = settings.Bonuses[next];
            var style = string.IsNullOrWhiteSpace(bonus.Style) ? settings.Style : bonus.Style;
            if (!string.IsNullOrWhiteSpace(bonus.Message))
                hooks.ShowMessage(bonus.Message, style);
            var sound = string.IsNullOrWhiteSpace(bonus.Sound) ? settings.Sound : bonus.Sound;
            if (!string.IsNullOrWhiteSpace(sound))
                hooks.PlaySound(sound);
            ticsToNext = hooks.StyleDuration(style);
        }
    }

    // The first player whose score has gone up since the last tic (who gets the bonuses); null if none's has
    PlayerState? ScoredBy()
    {
        PlayerState? scorer = null;
        if (lastScores.Length != session.Players.Count)
            lastScores = new int[session.Players.Count];
        for (int i = 0; i < session.Players.Count; i++)
        {
            int score = session.Players[i].Score;
            if (score > lastScores[i])
                scorer ??= session.Players[i];
            lastScores[i] = score;
        }
        return scorer;
    }

    void Check(LevelBonusesInfo settings, PlayerState scorer)
    {
        for (int i = 0; i < settings.Bonuses.Count; i++)
        {
            if (given.Contains(i) || !Earned(settings.Bonuses, i))
                continue;
            given.Add(i);
            waiting.Enqueue(i);
            if (settings.Bonuses[i].Points != 0)
                hooks.GivePoints(scorer, settings.Bonuses[i].Points);
        }

        // (its own points aren't news to check again)
        for (int i = 0; i < session.Players.Count && i < lastScores.Length; i++)
            lastScores[i] = session.Players[i].Score;
    }

    // Whether the level has all of the bonus's `when`, and the bonuses it comes after have been given
    bool Earned(List<LevelBonusInfo> bonuses, int index)
    {
        var bonus = bonuses[index];
        if (hooks.Stat(bonus.When) is not { } stat)
            return false;
        var (got, total) = stat;
        if (got < total || (bonus.NeedsSome && total <= 0))
            return false;
        foreach (var after in bonus.After)
        {
            int other = bonuses.FindIndex(b => b.When.Equals(after, StringComparison.OrdinalIgnoreCase));
            if (other < 0 || !given.Contains(other))
                return false;
        }
        return true;
    }
}
