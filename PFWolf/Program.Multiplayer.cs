using System.Net;
using PFWolf.Configuration;
using PFWolf.Extensions;
using PFWolf.Fonts;
using PFWolf.Networking;

namespace PFWolf;

internal partial class Program
{
    /*
    =============================================================================

                                    MULTIPLAYER

    The main menu's Multiplayer screen: host a game, join one (found on the local network,
    or at an address typed in), and wait in its lobby until the host starts it. Then every
    machine starts the same game, with everyone in it (Program.players, in slot order) and
    this machine's player as the local one.

    =============================================================================
    */

    /// <summary>Whether the game running is one with others over the network</summary>
    internal static bool netgame;

    // The random number table's starting place for each level of a network game (the host's pick)
    static byte netseed;

    // The multiplayer screens (Ui/MultiplayerMenu.cs) and the command line's --host and --join
    // (Networking/NetStartRequest.cs), made in InitServices
    static MultiplayerMenu multiplayerMenu = null!;
    static readonly NetStartRequest netStart = new();
    static MultiplayerConfig MpConfig => multiplayerMenu.MpConfig;
    static string MpConfigPath => multiplayerMenu.MpConfigPath;

    internal static int CP_Multiplayer(int _) => multiplayerMenu.Run();
    static void Cmd_NetPortCheck(string[] args) => multiplayerMenu.NetPortCheck(args);
    static void Cmd_NetFind(string[] args) => multiplayerMenu.NetFind(args);
    static CP_itemtype[] BuildJoinRows() => multiplayerMenu.BuildJoinRows();
    static void ShowNetMessage(string text) => multiplayerMenu.ShowNetMessage(text);

    /// <summary>The command line's --host, --join, --port and --name</summary>
    internal static void SetNetParams(GameParams gameParams)
    {
        netStart.SetParams(gameParams);
        if (!string.IsNullOrWhiteSpace(gameParams.Name))
        {
            MpConfig.Name = NetProtocol.CleanName(gameParams.Name);
            MpConfig.Write(MpConfigPath);
        }
    }

    /// <summary>Whether the command line asked to host or join</summary>
    internal static bool HasPendingNetStart => netStart.HasPending;

    /// <summary>
    /// Hosts or joins as the command line asked (once): true if a game was started from the
    /// lobby, for the demo loop to play
    /// </summary>
    internal static bool RunPendingNetStart()
    {
        if (!HasPendingNetStart)
            return false;

        var (host, join) = netStart.Take();

        StartCPMusic(MENUSONG);
        StartGame = 0;
        if (host)
            multiplayerMenu.HostGame();
        else if (join != null && NetAddresses.ParseAddress(join) is { } at)
            multiplayerMenu.ConnectTo(at.Host, at.Port);

        MenuFadeOut();
        return startgame || loadedgame;     // (joining a game being played takes it up as a loaded one)
    }

    /*
    ===============
    = What this machine runs
    ===============
    */

    static NetIdentity? localIdentity;

    /// <summary>
    /// What this machine runs, for the host to check a joining player against: the engine (its
    /// version and commit), the game pack and its data files, and the mods, by name and by
    /// content (Networking/ContentHash.cs)
    /// </summary>
    static NetIdentity LocalIdentity() => localIdentity ??= new NetIdentity(GameEngineManager.EngineVersion, _gameEngineManager.GamePackId,
        _gameEngineManager.GameReleaseId ?? "", string.Join(", ", CurrentSavedMods()),
        ContentHash.Of(AssetManager.BasePk3FileName, _assetManager.LoadedMods.Select(mod => mod.FullPath)));

    /// <summary>What the multiplayer screens need from the game</summary>
    static MultiplayerHooks MakeMultiplayerHooks() => new(
        PlayerClass: () => newGamePlayerClass ?? DefaultPlayerClass,
        ClassChosen: playerClass => newGamePlayerClass = playerClass,
        PlayerClasses: PlayerClasses,
        Identity: LocalIdentity,
        BeginNetGame: BeginNetGame,
        BeginJoinedGame: BeginJoinedGame,
        Started: () => StartGame != 0);
    /*
    ===============
    = Starting the game
    ===============
    */

    /// <summary>
    /// Starts the game the host started, here: its episode and skill, everyone in it in slot
    /// order (each with their class), this machine's player the local one, and the levels'
    /// random numbers starting where the host said, so every machine begins alike.
    /// </summary>
    static void BeginNetGame(NetSession session, StartGameInfo start)
    {
        var gameInfo = _gameEngineManager.GetGameInfo();
        var episode = gameInfo.Episodes.Values.ElementAtOrDefault(start.Settings.Episode) ?? gameInfo.Episodes.Values.First();
        var mapInfo = gameInfo.Maps[episode.StartMap];

        string ClassOrDefault(string playerClass) => FindPlayerClass(playerClass) ?? DefaultPlayerClass;

        // The game as one player's, then everyone in it
        NewGame((short)Math.Clamp(start.Settings.Skill, 0, gameInfo.Skills.Count - 1), episode, mapInfo,
            ClassOrDefault(start.Players[0].PlayerClass));

        // A deathmatch on an arena starts there, not on the episode's first floor
        if (start.Settings.Mode == GameMode.Deathmatch
            && gameInfo.Maps.TryGetValue(start.Settings.Map, out var arena) && arena.Deathmatch)
        {
            gamestate.mapon = start.Settings.Map;
            gamestate.cluster = arena.Cluster;
        }

        players.Clear();
        for (int i = 0; i < start.Players.Count; i++)
        {
            var p = new Entities.PlayerState { Number = i, Name = start.Players[i].Name, Color = PlayerColor(start.Players[i].Color) };
            players.Add(p);
            using (ActAs(p))
                StartPlayer(ClassOrDefault(start.Players[i].PlayerClass));
        }
        consoleplayer = Math.Max(0, start.Players.FindIndex(p => p.Slot == session.LocalSlot));
        SetActing(localplayer);
        SetGameMode(start.Settings.Mode);

        godmode = noclip = ammocheat = 0;     // no cheats carried in from playing alone
        netgame = true;
        netseed = start.Seed;
        var rules = start.Settings;
        netrules = new NetRules(rules.Monsters || rules.Mode == GameMode.Coop, rules.FragLimit, rules.TimeLimit, rules.ItemRespawn);
        matchover = false;
        showscoreboard = false;
        netlevel = 0;
        netLeft = false;
        session.BeginPlaying();
        StartGame = 1;
    }

    /// <summary>A player's color as it came over the network, if it's one this game has; else null (their class's own)</summary>
    internal static string? PlayerColor(string color) =>
        !string.IsNullOrEmpty(color) && _translationManager.Exists(color) ? color : null;

    /// <summary>A game with others has ended (or never started): back to playing alone</summary>
    internal static void EndNetGame()
    {
        var error = NetSession.Current?.Error;
        netgame = false;
        netLeft = false;
        NetSession.Current?.Dispose();
        if (error != null)
            ShowNetMessage(WrapForMessage(error));     // the host left, or the connection was lost
    }
}
