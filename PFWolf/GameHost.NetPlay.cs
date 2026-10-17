using PFWolf.Networking;

namespace PFWolf;

internal static partial class GameHost
{
    // Playing with others: the lockstep game (Networking/NetPlay.cs), and what this machine shows
    // and does over it (Ui/NetOverlay.cs)
    static NetPlay netPlay = null!;         // made in InitServices
    static NetOverlay netOverlay = null!;

    static NetPlay MakeNetPlay() =>
        new(_mapManager, _videoManager, _inputManager, _hudMessageManager, doors, pushWalls, session,
            new NetPlayHooks(() => localcmd, WriteNetState, ThreeDRefresh,
                () => lasttimecount = (int)GameEngineManager.GetTimeCount(), AddNetPlayer, ConnectAreas));

    static NetOverlay MakeNetOverlay() =>
        new(_inputManager, _videoManager, _mapManager, ui, menuDrawing, scoreboard, netPlay, session,
            () => netgame, state => playstate = state);

    /// <summary>Tics a frame plays, with others: always the same, on every machine</summary>
    internal const uint NETTICS = NetPlay.NETTICS;

    static bool netLeavePrompt => netPlay.netLeavePrompt;
    static (int Index, int Step)? pendingjoinstate { get => netPlay.pendingjoinstate; set => netPlay.pendingjoinstate = value; }

    /// <summary>Whether the game with others is over for this machine: it left, or lost the host</summary>

    static bool netChatting => netOverlay.netChatting;
    static void NetChatKeys() => netOverlay.NetChatKeys();
    static void NetCheckKeys(ScanCodes scan) => netOverlay.NetCheckKeys(scan);
    static void DrawNetOverlay() => netOverlay.DrawNetOverlay();

    // net_find, net_portcheck, net_status and scoreboard (Commands/NetCommands.cs)
    static NetCommands netCommands = null!;     // made in InitServices

    /// <summary>A random number for something only this machine shows (a quit message), not the game's own</summary>
    internal static int UiRandom() => netgame ? Random.Shared.Next(256) : US_RndT();

    // Which of a sound's variants plays (Gameplay/GameSession.cs)
    internal static int SoundRandom() => session.SoundRandom();
}
