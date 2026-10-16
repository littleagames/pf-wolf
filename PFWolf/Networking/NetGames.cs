using PFWolf.Assets;
using PFWolf.Configuration;
using PFWolf.SaveGames;

namespace PFWolf.Networking;

/// <summary>What starting and ending games with others needs from the game</summary>
/// <param name="NewGame">Starts a game on a skill, from a map, as a player class (GameHost.NewGame)</param>
/// <param name="SetNetGame">This is (or stops being) a game with others</param>
/// <param name="SetNetSeed">Where the levels' random numbers start, as the host said</param>
/// <param name="ClearCheats">No cheats carried in from playing alone</param>
/// <param name="StartGame">The menu's "start the game" (0: not)</param>
/// <param name="SetStartGame">Sets it</param>
/// <param name="StartedOrLoaded">Whether a game was started, or taken up as a loaded one</param>
/// <param name="MenuSong">The menus' music</param>
internal sealed record NetGamesHooks(
    Action<short, EpisodeInfo, MapInfo, string> NewGame,
    Action<bool> SetNetGame,
    Action<byte> SetNetSeed,
    Action ClearCheats,
    Func<int> StartGame,
    Action<int> SetStartGame,
    Func<bool> StartedOrLoaded,
    Func<string> MenuSong);

/// <summary>
/// Games with others, from this machine's side: hosting or joining as the command line asks
/// (SetNetParams, RunPendingNetStart), what this machine runs for the host to check
/// (LocalIdentity), every machine starting the game the host started alike (BeginNetGame), and
/// back to playing alone when it ends (EndNetGame). The screens are Ui/MultiplayerMenu.cs.
/// </summary>
internal sealed class NetGames(
    GameEngineManager engine,
    AssetManager assets,
    AudioManager audio,
    TranslationManager translations,
    MenuDrawing draw,
    MultiplayerMenu multiplayerMenu,
    NetStartRequest netStart,
    GameSaves gameSaves,
    PlayerClassRules classes,
    Players playersManager,
    Deathmatch deathmatch,
    Scoreboard scoreboard,
    NetPlay netPlay,
    GameSession game,
    NetGamesHooks hooks)
{
    /// <summary>The command line's --host, --join, --port and --name</summary>
    internal void SetNetParams(GameParams gameParams)
    {
        netStart.SetParams(gameParams);
        if (!string.IsNullOrWhiteSpace(gameParams.Name))
        {
            multiplayerMenu.MpConfig.Name = NetProtocol.CleanName(gameParams.Name);
            multiplayerMenu.MpConfig.Write(multiplayerMenu.MpConfigPath);
        }
    }

    /// <summary>Whether the command line asked to host or join</summary>
    internal bool HasPendingNetStart => netStart.HasPending;

    /// <summary>
    /// Hosts or joins as the command line asked (once): true if a game was started from the
    /// lobby, for the demo loop to play
    /// </summary>
    internal bool RunPendingNetStart()
    {
        if (!HasPendingNetStart)
            return false;

        var (host, join) = netStart.Take();

        audio.PlayMusic(hooks.MenuSong());
        hooks.SetStartGame(0);
        if (host)
            multiplayerMenu.HostGame();
        else if (join != null && NetAddresses.ParseAddress(join) is { } at)
            multiplayerMenu.ConnectTo(at.Host, at.Port);

        draw.MenuFadeOut();
        return hooks.StartedOrLoaded();     // (joining a game being played takes it up as a loaded one)
    }

    /*
    ===============
    = What this machine runs
    ===============
    */

    internal NetIdentity? localIdentity;

    /// <summary>
    /// What this machine runs, for the host to check a joining player against: the engine (its
    /// version and commit), the game pack and its data files, and the mods, by name and by
    /// content (Networking/ContentHash.cs)
    /// </summary>
    internal NetIdentity LocalIdentity() => localIdentity ??= new NetIdentity(GameEngineManager.EngineVersion, engine.GamePackId,
        engine.GameReleaseId ?? "", string.Join(", ", gameSaves.CurrentSavedMods()),
        ContentHash.Of(AssetManager.BasePk3FileName, assets.LoadedMods.Select(mod => mod.FullPath)));


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
    internal void BeginNetGame(NetSession session, StartGameInfo start)
    {
        var gameInfo = engine.GetGameInfo();
        var episode = gameInfo.Episodes.Values.ElementAtOrDefault(start.Settings.Episode) ?? gameInfo.Episodes.Values.First();
        var mapInfo = gameInfo.Maps[episode.StartMap];

        string ClassOrDefault(string playerClass) => classes.FindPlayerClass(playerClass) ?? classes.DefaultPlayerClass;

        // The game as one player's, then everyone in it
        hooks.NewGame((short)Math.Clamp(start.Settings.Skill, 0, gameInfo.Skills.Count - 1), episode, mapInfo,
            ClassOrDefault(start.Players[0].PlayerClass));

        // A deathmatch on an arena starts there, not on the episode's first floor
        if (start.Settings.Mode == GameMode.Deathmatch
            && gameInfo.Maps.TryGetValue(start.Settings.Map, out var arena) && arena.Deathmatch)
        {
            game.gamestate.mapon = start.Settings.Map;
            game.gamestate.cluster = arena.Cluster;
        }

        game.players.Clear();
        for (int i = 0; i < start.Players.Count; i++)
        {
            var p = new Entities.PlayerState { Number = i, Name = start.Players[i].Name, Color = PlayerColor(start.Players[i].Color) };
            game.players.Add(p);
            using (game.ActAs(p))
                playersManager.StartPlayer(ClassOrDefault(start.Players[i].PlayerClass));
        }
        game.consoleplayer = Math.Max(0, start.Players.FindIndex(p => p.Slot == session.LocalSlot));
        game.SetActing(game.localplayer);
        game.SetGameMode(start.Settings.Mode);

        hooks.ClearCheats();     // no cheats carried in from playing alone
        hooks.SetNetGame(true);
        hooks.SetNetSeed(start.Seed);
        var rules = start.Settings;
        deathmatch.netrules = new NetRules(rules.Monsters || rules.Mode == GameMode.Coop, rules.FragLimit, rules.TimeLimit, rules.ItemRespawn);
        deathmatch.matchover = false;
        scoreboard.showscoreboard = false;
        netPlay.netlevel = 0;
        netPlay.netLeft = false;
        session.BeginPlaying();
        hooks.SetStartGame(1);
    }

    /// <summary>A player's color as it came over the network, if it's one this game has; else null (their class's own)</summary>
    internal string? PlayerColor(string color) =>
        !string.IsNullOrEmpty(color) && translations.Exists(color) ? color : null;

    /// <summary>A game with others has ended (or never started): back to playing alone</summary>
    internal void EndNetGame()
    {
        var error = NetSession.Current?.Error;
        hooks.SetNetGame(false);
        netPlay.netLeft = false;
        NetSession.Current?.Dispose();
        if (error != null)
            multiplayerMenu.ShowNetMessage(MenuDrawing.WrapForMessage(error));     // the host left, or the connection was lost
    }
}
