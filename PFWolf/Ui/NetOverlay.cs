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
/// <param name="netGame">Whether this is a game with others</param>
/// <param name="setPlayState">Ends the level the way it says (leaving)</param>
internal sealed class NetOverlay(
    InputManager input,
    VideoManager video,
    MapManager map,
    UiText ui,
    MenuDrawing draw,
    Scoreboard scoreboard,
    NetPlay netPlay,
    GameSession game,
    Func<bool> netGame,
    Action<playstatetypes> setPlayState)
{
    const int MAXY = GameConstants.MAXY;

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
                case ScanCodes.sc_Enter:
                    NetSession.Current?.Say(netChatText);
                    goto case ScanCodes.sc_Escape;
                case ScanCodes.sc_Escape:
                    netChatting = false;
                    netChatText = "";
                    input.ClearKeysDown();
                    return;
                case ScanCodes.sc_BackSpace when netChatText.Length > 0:
                    netChatText = netChatText[..^1];
                    break;
            }
        }
    }

    /// <summary>Dead, with others: watches the next (or previous) living player, until coming back</summary>
    internal void WatchNextPlayer(int step)
    {
        var camera = game.camera;
        var living = map.Players.Where(pawn => pawn.State != game.localplayer && pawn.State.health > 0).ToList();
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
        if (!netPlay.netLeavePrompt && scan == ScanCodes.sc_T)
        {
            netChatting = true;
            netChatText = "";
            input.ClearTextInput();
            input.ClearKeysDown();
            return;
        }

        var localplayer = game.localplayer;
        if (localplayer.health <= 0 && localplayer.Pawn != null)
        {
            if (input.IsButtonPressed(buttontypes.bt_turnright) && !input.IsButtonHeld(buttontypes.bt_turnright))
                WatchNextPlayer(1);
            else if (input.IsButtonPressed(buttontypes.bt_turnleft) && !input.IsButtonHeld(buttontypes.bt_turnleft))
                WatchNextPlayer(-1);
        }

        if (netPlay.netLeavePrompt)
        {
            if (scan == ScanCodes.sc_Y)
            {
                netPlay.netLeft = true;
                setPlayState(playstatetypes.ex_abort);
            }
            if (scan is ScanCodes.sc_Y or ScanCodes.sc_N or ScanCodes.sc_Escape)
            {
                netPlay.netLeavePrompt = false;
                input.ClearKeysDown();
            }
            return;
        }

        if (scan == ScanCodes.sc_Escape || input.IsButtonPressed(buttontypes.bt_esc))
        {
            netPlay.netLeavePrompt = true;
            input.ClearKeysDown();
        }
    }

    /// <summary>Over the view, with others: who the game is waiting for, or whether to leave</summary>
    internal void DrawNetOverlay()
    {
        if (!netGame())
            return;

        // Dead: everyone's score, who's being watched, and how to come back
        var localplayer = game.localplayer;
        bool dead = localplayer.health <= 0 && localplayer.Pawn != null;
        if (scoreboard.showscoreboard || dead)
            scoreboard.DrawScoreboard(12);
        if (dead)
        {
            var watching = game.camera.Target is Entities.Actors.PlayerPawn pawn && pawn != localplayer.Pawn
                ? $"Watching {pawn.State.Name ?? $"Player {pawn.State.Number + 1}"} - " : "";
            var hint = $"{watching}use or fire to come back, left/right to watch others";
            ui.CenteredText(0, 320, MAXY - 12, new Fonts.TextStyle(UiFonts.SMALL_FONT, "HIGHLIGHT")).CPrint(ui.FitText(hint, 300, UiFonts.SMALL_FONT));
        }

        // The line being typed to the others
        if (netChatting)
        {
            video.Bar(16, MAXY - 24, 288, 11, "BKGDCOLOR");
            ui.TextAt(20, MAXY - 22, new Fonts.TextStyle(UiFonts.SMALL_FONT, "HIGHLIGHT"))
                .Print(ui.FitText($"Say: {netChatText}_", 280, UiFonts.SMALL_FONT));
        }

        var text = netPlay.netLeavePrompt ? "Leave this game?\n(Y or N)" : netPlay.netWaiting;
        if (text != null)
            draw.Message(text, MAXY);
    }
}
