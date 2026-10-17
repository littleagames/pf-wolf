using PFWolf.Assets;
using PFWolf.Configuration;
using PFWolf.SaveGames;

namespace PFWolf.Networking;

/// <summary>What starting and ending games with others needs from the game</summary>
/// <param name="NewGame">Starts a game on a skill, from a map, as a player class (GameHost.NewGame)</param>
/// <param name="StartGame">The menu's "start the game" (0: not)</param>
/// <param name="SetStartGame">Sets it</param>
/// <param name="MenuSong">The menus' music</param>
internal sealed record NetGamesHooks(
    Action<short, EpisodeInfo, MapInfo, string> NewGame,
    Func<int> StartGame,
    Action<int> SetStartGame,
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
        return (game.GameChosen || game.LoadedGame);     // (joining a game being played takes it up as a loaded one)
    }

    /*
    ===============
    = What this machine runs
    ===============
    */

    internal NetIdentity? CachedLocalIdentity;

    /// <summary>
    /// What this machine runs, for the host to check a joining player against: the engine (its
    /// version and commit), the game pack and its data files, and the mods, by name and by
    /// content (Networking/ContentHash.cs)
    /// </summary>
    internal NetIdentity LocalIdentity() => CachedLocalIdentity ??= new NetIdentity(GameEngineManager.EngineVersion, engine.GamePackId,
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

        // A match on an arena starts there, not on the episode's first floor
        var mode = start.Settings.Mode;
        if (mode != GameMode.Coop
            && gameInfo.Maps.TryGetValue(start.Settings.Map, out var arena) && arena.IsArenaFor(mode.Id()))
        {
            game.GameState.MapOn = start.Settings.Map;
            game.GameState.Cluster = arena.Cluster;
        }

        game.Players.Clear();
        for (int i = 0; i < start.Players.Count; i++)
        {
            // In a team game, each is seen in their team's color
            int team = mode.IsTeamGame() ? start.Players[i].Team & 1 : -1;
            var color = team >= 0 ? PlayerColor(GameModes.TeamNames[team]) : PlayerColor(start.Players[i].Color);
            var p = new Entities.PlayerState { Number = i, Name = start.Players[i].Name, Color = color, Team = team };
            game.Players.Add(p);
            using (game.ActAs(p))
                playersManager.StartPlayer(ClassOrDefault(start.Players[i].PlayerClass));
        }
        game.ConsolePlayer = Math.Max(0, start.Players.FindIndex(p => p.Slot == session.LocalSlot));
        game.SetActing(game.LocalPlayer);
        game.SetGameMode(start.Settings.Mode);

        game.GodMode = game.NoClip = game.AmmoCheat = 0;     // no cheats carried in from playing alone
        game.NetGame = true;
        game.NetSeed = start.Seed;
        var rules = start.Settings;
        deathmatch.NetRules = new NetRules(rules.Monsters || rules.Mode == GameMode.Coop, rules.ScoreLimit, rules.TimeLimit, rules.ItemRespawn,
            rules.HideSeconds, rules.HuntMinutes);
        deathmatch.NewMatch();
        scoreboard.ShowScoreboard = false;
        netPlay.NetLevel = 0;
        netPlay.NetLeft = false;
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
        game.NetGame = false;
        netPlay.NetLeft = false;
        NetSession.Current?.Dispose();
        if (error != null)
            multiplayerMenu.ShowNetMessage(MenuDrawing.WrapForMessage(error));     // the host left, or the connection was lost
    }
}
