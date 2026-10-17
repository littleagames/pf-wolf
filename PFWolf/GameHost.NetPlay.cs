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
        new(_inputManager, _videoManager, _mapManager, ui, menuDrawing, scoreboard, netPlay, session);

    static bool netLeavePrompt => netPlay.netLeavePrompt;

    static bool netChatting => netOverlay.netChatting;
    static void NetChatKeys() => netOverlay.NetChatKeys();
    static void NetCheckKeys(ScanCodes scan) => netOverlay.NetCheckKeys(scan);
    static void DrawNetOverlay() => netOverlay.DrawNetOverlay();

    // net_find, net_portcheck, net_status and scoreboard (Commands/NetCommands.cs)
    static NetCommands netCommands = null!;     // made in InitServices

    /// <summary>A random number for something only this machine shows (a quit message), not the game's own</summary>
    internal static int UiRandom() => netgame ? Random.Shared.Next(256) : US_RndT();
}
