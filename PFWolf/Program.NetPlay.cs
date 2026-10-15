using PFWolf.Networking;

namespace PFWolf;

internal partial class Program
{
    // Playing with others: the lockstep game (Networking/NetPlay.cs), and what this machine shows
    // and does over it (Ui/NetOverlay.cs)
    static NetPlay netPlay = null!;         // made in InitServices
    static NetOverlay netOverlay = null!;

    static NetPlay MakeNetPlay() =>
        new(_mapManager, _videoManager, _inputManager, _hudMessageManager, doors, pushWalls, session,
            new NetPlayHooks(() => localcmd, WriteNetState, ThreeDRefresh,
                () => lasttimecount = (int)GameEngineManager.GetTimeCount(), AddNetPlayer, ConnectAreas, () => netgame));

    static NetOverlay MakeNetOverlay() =>
        new(_inputManager, _videoManager, _mapManager, ui, menuDrawing, scoreboard, netPlay, session,
            () => netgame, state => playstate = state);

    /// <summary>Tics a frame plays, with others: always the same, on every machine</summary>
    internal const uint NETTICS = NetPlay.NETTICS;

    static int netlevel { get => netPlay.netlevel; set => netPlay.netlevel = value; }
    static int netstep { get => netPlay.netstep; set => netPlay.netstep = value; }
    static bool netLeavePrompt => netPlay.netLeavePrompt;
    static bool netLeft { get => netPlay.netLeft; set => netPlay.netLeft = value; }
    static (int Index, int Step)? pendingjoinstate { get => netPlay.pendingjoinstate; set => netPlay.pendingjoinstate = value; }
    static bool netJoinResume { get => netPlay.netJoinResume; set => netPlay.netJoinResume = value; }

    static uint GameChecksum() => netPlay.GameChecksum();

    /// <summary>Whether the game with others is over for this machine: it left, or lost the host</summary>
    internal static bool NetGameOver => netPlay.NetGameOver;

    static bool netChatting => netOverlay.netChatting;
    static void NetChatKeys() => netOverlay.NetChatKeys();
    static void NetCheckKeys(ScanCodes scan) => netOverlay.NetCheckKeys(scan);
    static void DrawNetOverlay() => netOverlay.DrawNetOverlay();

    /// <summary>`net_status`: the game with others as this machine has it</summary>
    static void Cmd_NetStatus(string[] args)
    {
        if (!netgame || NetSession.Current is not { } session)
        {
            _consoleManager.Print("Not playing with others");
            return;
        }

        var lines = new List<string> { $"{(session.IsHost ? "Hosting" : "Joined")}, level {netlevel}, frame {netstep}, game sum {GameChecksum():x8}" };
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            lines.Add($"  {i + 1}. {p.Name}{(i == consoleplayer ? " (you)" : "")}: "
                + (p.Pawn is { } pawn ? $"{pawn.X},{pawn.Y} facing {pawn.Angle}, health {p.health}" : p.Gone ? "left" : "-"));
        }
        if (session.IsHost)
            lines.Add($"  {session.SumsMatched} of {session.SumsCompared} checks matched");

        foreach (var line in lines)
        {
            _consoleManager.Print(line);
            Console.WriteLine($"net_status: {line}");
        }
    }

    /// <summary>A random number for something only this machine shows (a quit message), not the game's own</summary>
    internal static int UiRandom() => netgame ? Random.Shared.Next(256) : US_RndT();

    /// <summary>
    /// A random number for which of a sound's variants plays: the game's own alone, as it always
    /// was, but not with others (one machine may have its sound off, and play none)
    /// </summary>
    internal static int SoundRandom() => gamemode == GameMode.Single ? US_RndT() : Random.Shared.Next(256);
}
