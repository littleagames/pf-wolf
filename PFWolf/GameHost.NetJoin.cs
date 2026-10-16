using System.Text;
using PFWolf.Networking;

namespace PFWolf;

internal static partial class GameHost
{
    /*
    =============================================================================

                                JOINING MID-GAME

    Someone joining a game already being played comes in at a frame of the host's choosing
    (the next one to go out: TicBundle.Join). Every machine adds them at that frame alike
    (AddNetPlayer), at the start or (deathmatch) somewhere random. Once the frame has been
    played, the host sends them the game as it stands (WriteNetState: what a save holds, for
    every player, and everything else that decides how it goes on), and they play on from the
    next frame, the controls for it already on their way (BeginJoinedGame): Networking/MidGameJoin.cs.

    =============================================================================
    */

    // Joining mid-game (Networking/MidGameJoin.cs)
    static MidGameJoin midGameJoin = null!;     // made in InitServices

    static MidGameJoin MakeMidGameJoin() => new(_mapManager, _inventoryManager, levelBodies, levelSetup, pushWalls, playersManager, deathmatch,
        scoreboard, netPlay, session,
        new MidGameJoinHooks(() => netseed, seed => netseed = seed, on => loadedgame = on, on => netgame = on,
            () => godmode = noclip = ammocheat = 0, PlayerColor, () => StartGame = 1));

    static byte[] WriteNetState() => midGameJoin.WriteNetState();
    static string? BeginJoinedGame(NetSession session, JoinStateInfo join) => midGameJoin.BeginJoinedGame(session, join);
}