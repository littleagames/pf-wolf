using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Ui;

/// <summary>
/// The scoreboard, playing with others: over the view (the `scoreboard` command toggles it,
/// and it's up while the player's dead), and full screen when a match ends.
/// </summary>
/// <param name="clearMemory">Stops what's playing before the final scoreboard (ClearMemory)</param>
internal sealed class Scoreboard(
    VideoManager video,
    InputManager input,
    MenuDrawing draw,
    UiText ui,
    GameSession session,
    Action clearMemory)
{
    /// <summary>Whether the scoreboard is up over the view (the `scoreboard` command toggles it)</summary>
    internal bool showscoreboard;

    /// <summary>The match's team scores and objective (Gameplay/Deathmatch.cs); unset, none</summary>
    internal Deathmatch? Match { get; set; }

    /// <summary>
    /// The players, best first: by frags in a deathmatch; in a team game by team (red first),
    /// then what each did for it (frags in team deathmatch); else by score
    /// </summary>
    internal List<Entities.PlayerState> RankedPlayers()
    {
        var mode = session.gamemode;
        var present = session.players.Where(p => !p.Gone);
        var ranked = mode.IsTeamGame()
            ? present.OrderBy(p => p.Team < 0 ? 2 : p.Team).ThenByDescending(p => PlayerScore(p))
            : present.OrderByDescending(p => PlayerScore(p));
        return ranked.ThenBy(p => p.Number).ToList();
    }

    // The scoreboard's number for a player: frags, what they did for the team, or score
    int PlayerScore(Entities.PlayerState p) => session.gamemode switch
    {
        GameMode.Deathmatch or GameMode.TeamDeathmatch => p.Frags,
        _ when session.gamemode.IsTeamGame() => p.Objective,
        _ => p.score,
    };

    /// <summary>
    /// The scoreboard in a box at (x, y): each team's score in a team game, then each player's
    /// name and their frags (deathmatch), what they did for their team (and frags), or score
    /// and kills (co-op, Horde), this machine's player highlighted
    /// </summary>
    internal void DrawScoreboard(int y, string? title = null)
    {
        var ranked = RankedPlayers();
        var mode = session.gamemode;
        bool teams = mode.IsTeamGame() && Match != null;
        const int X = 40, W = 240, Row = 10;
        int h = 14 + Row * ranked.Count + (title != null ? Row : 0) + (teams ? Row : 0);
        video.Bar(X, y, W, h, "BKGDCOLOR");
        draw.DrawOutline(X, y, W, h, "DEACTIVE", "HIGHLIGHT");

        int ty = y + 4;
        if (title != null)
        {
            ui.CenteredText(X, W, ty, new Fonts.TextStyle(UiFonts.SmallFont, "READHCOLOR")).CPrint(title);
            ty += Row;
        }
        if (teams)
        {
            ui.CenteredText(X, W, ty, new Fonts.TextStyle(UiFonts.SmallFont, "READHCOLOR"))
                .CPrint($"Red {Match!.teamscores[0]}   -   {Match.teamscores[1]} Blue");
            ty += Row;
        }

        // Columns: a team game shows its own score and frags; co-op and Horde, score and kills
        string first = mode == GameMode.Deathmatch || mode == GameMode.TeamDeathmatch ? "Frags"
            : mode.IsTeamGame() ? mode.ScoreName() : "Score";
        string? second = mode == GameMode.Deathmatch || mode == GameMode.TeamDeathmatch ? null
            : mode.IsTeamGame() ? "Frags" : "Kills";
        var heading = new Fonts.TextStyle(UiFonts.SmallFont, "READCOLOR");
        ui.TextAt(X + 6, ty, heading).Print("Player");
        ui.TextAt(X + 150, ty, heading).Print(first);
        if (second != null)
            ui.TextAt(X + 200, ty, heading).Print(second);
        ty += Row;

        foreach (var p in ranked)
        {
            var style = new Fonts.TextStyle(UiFonts.SmallFont, ReferenceEquals(p, session.localplayer) ? "HIGHLIGHT" : "TEXTCOLOR");
            var name = p.Name ?? $"Player {p.Number + 1}";
            if (teams && p.Team is 0 or 1)
                name = $"{GameModes.TeamLabel(p.Team)}: {name}";
            if (p.health <= 0)
                name += " (dead)";
            ui.TextAt(X + 6, ty, style).Print(ui.FitText(name, 140, UiFonts.SmallFont));
            ui.TextAt(X + 150, ty, style).Print(PlayerScore(p).ToString());
            if (second != null)
                ui.TextAt(X + 200, ty, style).Print((second == "Frags" ? p.Frags : p.Kills).ToString());
            ty += Row;
        }
    }

    /// <summary>Who won, for the final scoreboard: a player, a team, or how long the players held out</summary>
    internal string WinnerTitle()
    {
        if (Match?.Objective?.FinalTitle() is { } objective)
            return objective;
        if (session.gamemode.IsTeamGame() && Match != null)
            return Match.LeadingTeam < 0 ? "It's a draw!" : $"{GameModes.TeamLabel(Match.LeadingTeam)} team wins!";
        var winner = RankedPlayers().FirstOrDefault();
        return winner == null ? "Match over" : $"{winner.Name ?? $"Player {winner.Number + 1}"} wins!";
    }

    /// <summary>The end of a match: the final scoreboard, until a key</summary>
    internal void ShowFinalScoreboard()
    {
        var title = WinnerTitle();
        clearMemory();
        video.FadeOut();
        video.ClearScreen(0);
        DrawScoreboard(40, title);
        video.Update();
        video.FadeIn();
        input.ClearKeysDown();
        input.UserInput(Timing.TickBase * 15);
        video.FadeOut();
    }

    /// <summary>`scoreboard`: shows or hides the scoreboard over the view (bind it to a key)</summary>
    internal void ScoreboardCommand(string[] args)
    {
        showscoreboard = args.Length > 0 ? args[0] == "1" : !showscoreboard;
    }
}
