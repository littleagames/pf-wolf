using PFWolf.Enums;
using PFWolf.Managers;
using PFWolf.Networking;

namespace PFWolf.Ui;

/// <summary>
/// What this machine shows and does with others, over the game: the keys (Esc asks whether to
/// leave, T starts a line to say, and while dead, turning watches another player), the line
/// being typed, and the overlay -- the scoreboard while dead, who the game is waiting for, and
/// whether to leave.
/// </summary>
internal sealed class NetOverlay(
    InputManager input,
    VideoManager video,
    MapManager map,
    UiText ui,
    MenuDrawing draw,
    Scoreboard scoreboard,
    NetPlay netPlay,
    GameSession game)
{
    const int MaxY = GameConstants.MaxY;

    // Typing a line to say to the others (T), and what's typed so far
    internal bool netChatting;
    internal string netChatText = "";

    /// <summary>While typing a chat line: letters go on it, Backspace takes one off, Enter says it, Esc drops it</summary>
    internal void NetChatKeys()
    {
        foreach (var c in input.GetTextInput())
        {
            if (c == 0)
                break;
            if (c >= ' ' && c < 127 && netChatText.Length < NetProtocol.MaxChatLength)
                netChatText += c;
        }
        input.ClearTextInput();

        while (input.TryTakePressedKey(out var key))
        {
            switch (key)
            {
                case ScanCodes.Enter:
                    NetSession.Current?.Say(netChatText);
                    goto case ScanCodes.Escape;
                case ScanCodes.Escape:
                    netChatting = false;
                    netChatText = "";
                    input.ClearKeysDown();
                    return;
                case ScanCodes.BackSpace when netChatText.Length > 0:
                    netChatText = netChatText[..^1];
                    break;
            }
        }
    }

    /// <summary>Dead, with others: watches the next (or previous) living player, until coming back</summary>
    internal void WatchNextPlayer(int step)
    {
        var camera = game.Camera;
        var living = map.Players.Where(pawn => pawn.State != game.LocalPlayer && pawn.State.Health > 0).ToList();
        if (living.Count == 0)
        {
            camera.FollowPlayer();
            return;
        }
        int at = camera.Target is Entities.Actors.PlayerPawn watched ? living.IndexOf(watched) : -1;
        camera.Follow(living[((at < 0 ? (step > 0 ? -1 : 0) : at) + step + living.Count) % living.Count]);
    }

    /// <summary>
    /// The keys, with others: Esc asks whether to leave (then Y leaves, N or Esc plays on), T
    /// starts a line to say, and while dead, turning left or right watches another player
    /// </summary>
    internal void NetCheckKeys(ScanCodes scan)
    {
        if (!netPlay.netLeavePrompt && scan == ScanCodes.T)
        {
            netChatting = true;
            netChatText = "";
            input.ClearTextInput();
            input.ClearKeysDown();
            return;
        }

        var localplayer = game.LocalPlayer;
        if (localplayer.Health <= 0 && localplayer.Pawn != null)
        {
            if (input.IsButtonPressed(ButtonType.TurnRight) && !input.IsButtonHeld(ButtonType.TurnRight))
                WatchNextPlayer(1);
            else if (input.IsButtonPressed(ButtonType.TurnLeft) && !input.IsButtonHeld(ButtonType.TurnLeft))
                WatchNextPlayer(-1);
        }

        if (netPlay.netLeavePrompt)
        {
            if (scan == ScanCodes.Y)
            {
                netPlay.netLeft = true;
                game.PlayState = PlayState.Abort;
            }
            if (scan is ScanCodes.Y or ScanCodes.N or ScanCodes.Escape)
            {
                netPlay.netLeavePrompt = false;
                input.ClearKeysDown();
            }
            return;
        }

        if (scan == ScanCodes.Escape || input.IsButtonPressed(ButtonType.Esc))
        {
            netPlay.netLeavePrompt = true;
            input.ClearKeysDown();
        }
    }

    /// <summary>The match being played (Gameplay/Deathmatch.cs), for the line across the top; unset, none</summary>
    internal Deathmatch? Match { get; set; }

    /// <summary>The top of the 3D view, in 320x200 pixels; unset, the screen's top</summary>
    internal Func<int>? ViewTop { get; set; }

    /// <summary>How much of the view's top the match's line takes (0 with no match on), for the messages to go under</summary>
    internal int MatchLineHeight() =>
        Match == null || game.GameMode is GameMode.Single or GameMode.Coop ? 0
        : Match.Objective?.StatusLine() != null && game.GameMode != GameMode.Horde ? 20 : 11;

    /// <summary>
    /// A match's line across the top of the view: the teams' scores (Heist's tug of war as a
    /// rope pulled one way or the other), the time left, and what the objective has to say
    /// </summary>
    internal void DrawMatchLine()
    {
        if (MatchLineHeight() == 0)
            return;
        var style = new Fonts.TextStyle(UiFonts.SmallFont, "HIGHLIGHT");
        var (score, news) = Match!.MatchStatus();
        int y = (ViewTop?.Invoke() ?? 0) + 2;
        if (game.GameMode == GameMode.Heist)
        {
            // The rope: its middle, and how far it's been pulled toward the team ahead
            const int RopeX = 110, RopeW = 100;
            int limit = Math.Max(1, Match.NetRules.ScoreLimit > 0 ? Match.NetRules.ScoreLimit : 100);
            int lead = Math.Clamp(Match.TeamScores[0] - Match.TeamScores[1], -limit, limit);
            int pulled = lead * (RopeW / 2) / limit;
            video.Bar(RopeX - 1, y - 1, RopeW + 2, 7, "BKGDCOLOR");
            video.Bar(RopeX, y, RopeW, 5, "#404040");
            if (pulled > 0)
                video.Bar(RopeX + RopeW / 2 - pulled, y, pulled, 5, "#FC0000");
            else if (pulled < 0)
                video.Bar(RopeX + RopeW / 2, y, -pulled, 5, "#0000FC");
            video.Bar(RopeX + RopeW / 2, y - 1, 1, 7, "#FCFCFC");
            ui.TextAt(RopeX - 40, y - 1, style).Print($"Red {Match.TeamScores[0]}");
            ui.TextAt(RopeX + RopeW + 6, y - 1, style).Print($"{Match.TeamScores[1]} Blue");
            y += 9;
        }
        else if (score.Length > 0)      // (Horde has none: just its wave)
        {
            ui.CenteredText(0, 320, y, style).CPrint(ui.FitText(score, 300, UiFonts.SmallFont));
            y += 9;
        }
        if (news != null)
            ui.CenteredText(0, 320, y, new Fonts.TextStyle(UiFonts.SmallFont, "READCOLOR")).CPrint(ui.FitText(news, 300, UiFonts.SmallFont));
    }

    /// <summary>Over the view, with others: who the game is waiting for, or whether to leave</summary>
    internal void DrawNetOverlay()
    {
        DrawMatchLine();
        if (!game.NetGame)
            return;

        // Dead: everyone's score, who's being watched, and how to come back
        var localplayer = game.LocalPlayer;
        bool dead = localplayer.Health <= 0 && localplayer.Pawn != null;
        if (scoreboard.showscoreboard || dead)
            scoreboard.DrawScoreboard(12);
        if (dead)
        {
            var watching = game.Camera.Target is Entities.Actors.PlayerPawn pawn && pawn != localplayer.Pawn
                ? $"Watching {pawn.State.Name ?? $"Player {pawn.State.Number + 1}"} - " : "";
            var hint = $"{watching}use or fire to come back, left/right to watch others";
            ui.CenteredText(0, 320, MaxY - 12, new Fonts.TextStyle(UiFonts.SmallFont, "HIGHLIGHT")).CPrint(ui.FitText(hint, 300, UiFonts.SmallFont));
        }

        // The line being typed to the others
        if (netChatting)
        {
            video.Bar(16, MaxY - 24, 288, 11, "BKGDCOLOR");
            ui.TextAt(20, MaxY - 22, new Fonts.TextStyle(UiFonts.SmallFont, "HIGHLIGHT"))
                .Print(ui.FitText($"Say: {netChatText}_", 280, UiFonts.SmallFont));
        }

        var text = netPlay.netLeavePrompt ? "Leave this game?\n(Y or N)" : netPlay.netWaiting;
        if (text != null)
            draw.Message(text, MaxY);
    }
}
