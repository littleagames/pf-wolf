using PFWolf.Assets;
using PFWolf.Entities;

namespace PFWolf.Gameplay;

/*
=============================================================================

                                LEVEL BONUSES

Bonuses a level gives (Blake Stone's "pinball bonuses", bstone's CheckPinballBonus and
DisplayPinballBonus). Game-info level-bonuses lists them, in the order they're checked and shown:
for getting all of something on the level (ALL ENEMY DESTROYED!, ALL POINTS COLLECTED!, ALL
INFORMANTS ALIVE!), for the score reaching a mark (GREAT SCORE!, the score rolling over), for an
extra life earned by score, and for killing one of some actors (GUARDIAN ALIEN DESTROYED!).

As bstone does, they're checked as the score goes up (here at the end of the tic it went up in,
once the kill or pickup is counted), so a level with none of something gives that bonus with the
first points scored there; an extra life or a listed actor's death is checked then too. A bonus
with `after` waits for those bonuses to have been given. Each is given once a level, unless it
`recurs`.

A bonus's points, health and items are given at once, and its message (with the sound) waits its
turn: one is shown at a time, the next once the last one's time is up. Its style's priority keeps
lesser messages from replacing it meanwhile (HudMessageManager). A bonus's score-text then shows
in the score box for a while.

What a level has given isn't saved: a level played already (a kept hub level, a saved game)
counts whatever it has all of as given when it's taken up again, so nothing is given twice. A
message still waiting to be shown then is lost (what it gave was given).

=============================================================================
*/

/// <param name="Settings">Game-info level-bonuses</param>
/// <param name="Stat">A `when`'s (got, total) on the level in play; null for one that isn't a count</param>
/// <param name="GivePoints">Gives a player points</param>
/// <param name="Heal">Heals a player some health</param>
/// <param name="GiveItem">Gives a player some of an item</param>
/// <param name="ShowMessage">Shows every player a message (text or a $language key) in a style</param>
/// <param name="PlaySound">Plays a sound to the player</param>
/// <param name="StyleDuration">How long a message in a style stays up, in tics</param>
/// <param name="RedrawScore">The score box drawn again, once a score-text is done</param>
internal sealed record LevelBonusHooks(
    Func<LevelBonusesInfo> Settings,
    Func<string, (int Got, int Total)?> Stat,
    Action<PlayerState, int> GivePoints,
    Action<PlayerState, int> Heal,
    Action<PlayerState, string, int> GiveItem,
    Action<string, string?> ShowMessage,
    Action<string> PlaySound,
    Func<string?, int> StyleDuration,
    Action RedrawScore);

internal sealed class LevelBonuses(GameSession session, LevelBonusHooks hooks)
{
    readonly HashSet<int> given = [];
    readonly Queue<int> waiting = new();
    readonly HashSet<string> killed = new(StringComparer.OrdinalIgnoreCase);
    int[] lastScores = [];
    int ticsToNext;
    PlayerState? extraLife;
    int scoreTextTics;

    /// <summary>The bonuses (their places in level-bonuses) given on the level in play</summary>
    internal IReadOnlyCollection<int> Given => given;

    /// <summary>What the score box shows in place of the score now (a bonus's score-text), or null</summary>
    internal string? ScoreText { get; private set; }

    /// <summary>
    /// The level's play starts (PlayLoop): a level just begun has given nothing yet; one taken up
    /// again (TimeCount past 0) has given whatever it has all of
    /// </summary>
    internal void Start()
    {
        given.Clear();
        waiting.Clear();
        killed.Clear();
        extraLife = null;
        ticsToNext = 0;
        ScoreText = null;
        scoreTextTics = 0;
        lastScores = session.Players.Select(p => p.Score).ToArray();

        var bonuses = hooks.Settings().Bonuses;
        if (session.GameState.TimeCount <= 0)
            return;
        for (int i = 0; i < bonuses.Count; i++)
        {
            if (hooks.Stat(bonuses[i].When) != null && Earned(bonuses, i, 0, 0))
                given.Add(i);
        }
    }

    /// <summary>A player got an extra life for their score (PlayerStats.GivePoints)</summary>
    internal void ExtraLife(PlayerState player) => extraLife ??= player;

    /// <summary>An actor was killed (Monster.Kill)</summary>
    internal void Killed(Entities.Actors.Actor actor) => killed.Add(actor.Name);

    /// <summary>After everyone has thought (LevelLoop): the score gone up checks the bonuses, and the next waiting message is shown once the last one's done</summary>
    internal void Tick(int tics)
    {
        var settings = hooks.Settings();
        if (settings.Bonuses.Count == 0)
            return;

        var scored = Scored();
        var scorer = scored?.Player ?? extraLife ?? (killed.Count > 0 ? session.Players[0] : null);
        if (scorer != null)
            Check(settings, scorer, scored?.Before ?? scorer.Score, scorer.Score);
        killed.Clear();
        extraLife = null;

        if (scoreTextTics > 0 && (scoreTextTics -= tics) <= 0)
        {
            ScoreText = null;
            hooks.RedrawScore();
        }

        if (ticsToNext > 0)
            ticsToNext -= tics;
        if (ticsToNext <= 0 && waiting.TryDequeue(out var next))
            Show(settings, settings.Bonuses[next]);
    }

    void Show(LevelBonusesInfo settings, LevelBonusInfo bonus)
    {
        var style = string.IsNullOrWhiteSpace(bonus.Style) ? settings.Style : bonus.Style;
        var message = bonus.ClusterMessages.GetValueOrDefault(session.GameState.Cluster) ?? bonus.Message;
        if (!string.IsNullOrWhiteSpace(message))
            hooks.ShowMessage(message, style);
        var sound = string.IsNullOrWhiteSpace(bonus.Sound) ? settings.Sound : bonus.Sound;
        if (!string.IsNullOrWhiteSpace(sound))
            hooks.PlaySound(sound);
        ticsToNext = hooks.StyleDuration(style);

        if (!string.IsNullOrEmpty(bonus.ScoreText) && bonus.ScoreTextTics > 0)
        {
            ScoreText = bonus.ScoreText;
            scoreTextTics = bonus.ScoreTextTics;
            hooks.RedrawScore();
        }
    }

    // The first player whose score has gone up since the last tic (who gets the bonuses), with
    // their score before; null if none's has
    (PlayerState Player, int Before)? Scored()
    {
        (PlayerState, int)? scored = null;
        if (lastScores.Length != session.Players.Count)
            lastScores = session.Players.Select(p => p.Score).ToArray();
        for (int i = 0; i < session.Players.Count; i++)
        {
            int score = session.Players[i].Score;
            if (score > lastScores[i])
                scored ??= (session.Players[i], lastScores[i]);
            lastScores[i] = score;
        }
        return scored;
    }

    void Check(LevelBonusesInfo settings, PlayerState scorer, int before, int after)
    {
        for (int i = 0; i < settings.Bonuses.Count; i++)
        {
            var bonus = settings.Bonuses[i];
            if (given.Contains(i) || !Earned(settings.Bonuses, i, before, after))
                continue;
            if (!bonus.Recurs)
                given.Add(i);
            waiting.Enqueue(i);

            // (its points are news for the next tic's check: they may reach a score mark)
            if (bonus.Points != 0)
                hooks.GivePoints(scorer, bonus.Points);
            if (bonus.Health > 0)
                hooks.Heal(scorer, bonus.Health);
            foreach (var (item, amount) in bonus.Give)
                hooks.GiveItem(scorer, item, amount);
        }
    }

    // Whether the bonus's `when` has come about (the score going from before to after), and the
    // bonuses it comes after have been given
    bool Earned(List<LevelBonusInfo> bonuses, int index, int before, int after)
    {
        var bonus = bonuses[index];
        bool earned = bonus.When.Trim().ToLowerInvariant() switch
        {
            "score" => before < bonus.At && after >= bonus.At,
            "extra-life" => extraLife != null,
            "killed" => bonus.Actors.Any(killed.Contains),
            _ => hooks.Stat(bonus.When) is { } stat && stat.Got >= stat.Total && !(bonus.NeedsSome && stat.Total <= 0),
        };
        if (!earned)
            return false;
        foreach (var other in bonus.After)
        {
            int at = bonuses.FindIndex(b => b.When.Equals(other, StringComparison.OrdinalIgnoreCase));
            if (at < 0 || !given.Contains(at))
                return false;
        }
        return true;
    }
}
