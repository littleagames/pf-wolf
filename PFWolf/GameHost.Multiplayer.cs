using System.Net;
using PFWolf.Configuration;
using PFWolf.Extensions;
using PFWolf.Fonts;
using PFWolf.Networking;

namespace PFWolf;

internal static partial class GameHost
{
    /*
    =============================================================================

                                    MULTIPLAYER

    The main menu's Multiplayer screen: host a game, join one (found on the local network,
    or at an address typed in), and wait in its lobby until the host starts it. Then every
    machine starts the same game, with everyone in it (GameHost.players, in slot order) and
    this machine's player as the local one.

    =============================================================================
    */

    // Whether the game running is one with others over the network, and its levels' random seed (Gameplay/GameSession.cs)
    internal static bool netgame { get => session.netgame; set => session.netgame = value; }
    static byte netseed { get => session.netseed; set => session.netseed = value; }

    // The multiplayer screens (Ui/MultiplayerMenu.cs) and the command line's --host and --join
    // (Networking/NetStartRequest.cs), made in InitServices
    static MultiplayerMenu multiplayerMenu = null!;
    static readonly NetStartRequest netStart = new();

    internal static int CP_Multiplayer(int _) => multiplayerMenu.Run();
    static CP_itemtype[] BuildJoinRows() => multiplayerMenu.BuildJoinRows();

    // Starting and ending games with others (Networking/NetGames.cs)
    static NetGames netGames = null!;       // made in InitServices

    static NetGames MakeNetGames() => new(_gameEngineManager, _assetManager, _audioManager, _translationManager, menuDrawing, multiplayerMenu,
        netStart, gameSaves, playerClassRules, playersManager, deathmatch, scoreboard, netPlay, session,
        new NetGamesHooks((skill, episode, map, playerClass) => NewGame(skill, episode, map, playerClass), on => netgame = on,
            seed => netseed = seed, () => godmode = noclip = ammocheat = 0, () => StartGame, start => StartGame = start,
            () => startgame || loadedgame, () => MENUSONG));

    /// <summary>The command line's --host, --join, --port and --name</summary>
    internal static void SetNetParams(GameParams gameParams) => netGames.SetNetParams(gameParams);
    static NetIdentity LocalIdentity() => netGames.LocalIdentity();
    static void BeginNetGame(NetSession session, StartGameInfo start) => netGames.BeginNetGame(session, start);
    internal static string? PlayerColor(string color) => netGames.PlayerColor(color);

    /// <summary>What the multiplayer screens need from the game</summary>
    static MultiplayerHooks MakeMultiplayerHooks() => new(
        PlayerClass: () => newGamePlayerClass ?? DefaultPlayerClass,
        ClassChosen: playerClass => newGamePlayerClass = playerClass,
        PlayerClasses: PlayerClasses,
        Identity: LocalIdentity,
        BeginNetGame: BeginNetGame,
        BeginJoinedGame: BeginJoinedGame,
        Started: () => StartGame != 0,
        EpisodeOpen: episode => unlocks.IsOpen(episode));
}
