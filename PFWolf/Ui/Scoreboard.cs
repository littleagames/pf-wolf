using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Ui;

/// <summary>
/// The scoreboard, playing with others: over the view (the `scoreboard` command toggles it,
/// and it's up while the player's dead), and full screen when a deathmatch ends.
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

    /// <summary>The players, best first: by frags in a deathmatch, else by score</summary>
    internal List<Entities.PlayerState> RankedPlayers() =>
        session.players.Where(p => !p.Gone)
            .OrderByDescending(p => session.gamemode == GameMode.Deathmatch ? p.Frags : p.score)
            .ThenBy(p => p.Number)
            .ToList();

    /// <summary>
    /// The scoreboard in a box at (x, y): each player's name, and their frags (deathmatch) or
    /// score and kills (co-op), this machine's player highlighted
    /// </summary>
    internal void DrawScoreboard(int y, string? title = null)
    {
        var ranked = RankedPlayers();
        const int X = 40, W = 240, Row = 10;
        int h = 14 + Row * ranked.Count + (title != null ? Row : 0);
        video.Bar(X, y, W, h, "BKGDCOLOR");
        draw.DrawOutline(X, y, W, h, "DEACTIVE", "HIGHLIGHT");

        int ty = y + 4;
        if (title != null)
        {
            ui.CenteredText(X, W, ty, new Fonts.TextStyle(UiFonts.SMALL_FONT, "READHCOLOR")).CPrint(title);
            ty += Row;
        }
        bool dm = session.gamemode == GameMode.Deathmatch;
        ui.TextAt(X + 6, ty, new Fonts.TextStyle(UiFonts.SMALL_FONT, "READCOLOR")).Print(dm ? "Player" : "Player");
        ui.TextAt(X + 150, ty, new Fonts.TextStyle(UiFonts.SMALL_FONT, "READCOLOR")).Print(dm ? "Frags" : "Score");
        if (!dm)
            ui.TextAt(X + 200, ty, new Fonts.TextStyle(UiFonts.SMALL_FONT, "READCOLOR")).Print("Kills");
        ty += Row;

        foreach (var p in ranked)
        {
            var style = new Fonts.TextStyle(UiFonts.SMALL_FONT, ReferenceEquals(p, session.localplayer) ? "HIGHLIGHT" : "TEXTCOLOR");
            var name = p.Name ?? $"Player {p.Number + 1}";
            if (p.health <= 0)
                name += " (dead)";
            ui.TextAt(X + 6, ty, style).Print(ui.FitText(name, 140, UiFonts.SMALL_FONT));
            ui.TextAt(X + 150, ty, style).Print((dm ? p.Frags : p.score).ToString());
            if (!dm)
                ui.TextAt(X + 200, ty, style).Print(p.Kills.ToString());
            ty += Row;
        }
    }

    /// <summary>The end of a deathmatch: the final scoreboard, until a key</summary>
    internal void ShowFinalScoreboard()
    {
        var winner = RankedPlayers().FirstOrDefault();
        clearMemory();
        video.FadeOut();
        video.ClearScreen(0);
        DrawScoreboard(40, winner == null ? "Match over" : $"{winner.Name ?? $"Player {winner.Number + 1}"} wins!");
        video.Update();
        video.FadeIn();
        input.ClearKeysDown();
        input.UserInput(Timing.TickBase * 15);
        video.FadeOut();
    }

    /// <summary>`scoreboard`: shows or hides the scoreboard over the view (bind it to a key)</summary>
    internal void Cmd_Scoreboard(string[] args)
    {
        showscoreboard = args.Length > 0 ? args[0] == "1" : !showscoreboard;
    }
}
