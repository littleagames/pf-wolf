using PFWolf.Assets;

namespace PFWolf.Gameplay;

/*
=============================================================================

                                ANNOUNCER

Cues for a multiplayer match, from game-info `announcer:`: a sound and a message as a player's
kill streak (rivals killed since they last died: Deathmatch.CountFrag) reaches a number under
`streaks`, and `match-won`, `match-lost` or `match-draw` for each player as the match ends.

Every machine runs the game alike, so each comes here for every player's streak and every
match's end, and gives a cue only when this machine's player is one it's for: a streak's to the
player on it (`to: self`), everyone else (`others`) or everyone (`all`); a match's by how it went
for them (Deathmatch.Outcome). The sound's heard unplaced; the message is shown even with
messages off, like the kill feed.

=============================================================================
*/

/// <param name="Settings">Game-info announcer</param>
/// <param name="PlaySound">Plays a sound on this machine, unplaced</param>
/// <param name="ShowMessage">Shows a message on this machine (text or a $language key), in a style, with %x placeholders</param>
/// <param name="PlayerName">A player's name for messages ("Player 2" without one)</param>
internal sealed record AnnouncerHooks(
    Func<AnnouncerInfo> Settings,
    Action<string> PlaySound,
    Action<string, string, Dictionary<char, string>> ShowMessage,
    Func<Entities.PlayerState, string> PlayerName);

internal sealed class Announcer(GameSession session, AnnouncerHooks hooks)
{
    /// <summary>A player's kill streak has grown (PlayerState.Streak): its cue, if one's set for that length</summary>
    internal void Streaked(Entities.PlayerState player)
    {
        if (!hooks.Settings().Streaks.TryGetValue(player.Streak, out var cue))
            return;
        bool isFor = cue.To.Trim().ToLowerInvariant() switch
        {
            "all" => true,
            "others" => !ReferenceEquals(session.LocalPlayer, player),
            _ => ReferenceEquals(session.LocalPlayer, player),
        };
        if (isFor)
            Give(cue, new Dictionary<char, string> { ['p'] = hooks.PlayerName(player), ['n'] = player.Streak.ToString() });
    }

    /// <summary>The match is over: this machine's player's cue for how it went for them</summary>
    internal void MatchOver(Func<Entities.PlayerState, MatchOutcome> outcome)
    {
        var settings = hooks.Settings();
        var cue = outcome(session.LocalPlayer) switch
        {
            MatchOutcome.Won => settings.MatchWon,
            MatchOutcome.Lost => settings.MatchLost,
            _ => settings.MatchDraw,
        };
        if (cue != null)
            Give(cue, new Dictionary<char, string> { ['p'] = hooks.PlayerName(session.LocalPlayer) });
    }

    void Give(AnnouncerCue cue, Dictionary<char, string> placeholders)
    {
        if (!string.IsNullOrWhiteSpace(cue.Sound))
            hooks.PlaySound(cue.Sound.Trim());
        if (!string.IsNullOrWhiteSpace(cue.Message))
            hooks.ShowMessage(cue.Message, cue.Style, placeholders);
    }
}
