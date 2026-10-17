using PFWolf.Configuration;
using PFWolf.Entities.Actors;
using PFWolf.Extensions;
using PFWolf.Fonts;
using PFWolf.Networking;
using System.Net;
using System.Text;

namespace PFWolf;

// GameHost's multiplayer wiring: the lobby, network play, joining a game being played, deathmatch
internal static partial class GameHost
{
    // ==== Multiplayer ============================================================================

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
    internal static bool NetGame { get => Session.NetGame; set => Session.NetGame = value; }

    // The multiplayer screens (Ui/MultiplayerMenu.cs) and the command line's --host and --join
    // (Networking/NetStartRequest.cs), made in InitServices
    static MultiplayerMenu multiplayerMenu = null!;
    static readonly NetStartRequest netStart = new();

    internal static int RunMultiplayerMenu(int _) => multiplayerMenu.Run();
    static ControlPanelItem[] BuildJoinRows() => multiplayerMenu.BuildJoinRows();

    // Starting and ending games with others (Networking/NetGames.cs)
    static NetGames netGames = null!;       // made in InitServices

    static NetGames MakeNetGames() => new(GameEngineManager, AssetManager, AudioManager, TranslationManager, menuDrawing, multiplayerMenu,
        netStart, gameSaves, playerClassRules, playersManager, deathmatch, scoreboard, netPlay, Session,
        new NetGamesHooks((skill, episode, map, playerClass) => NewGame(skill, episode, map, playerClass), () => StartGame, start => StartGame = start, () => MenuSong));

    /// <summary>The command line's --host, --join, --port and --name</summary>
    internal static void SetNetParams(GameParams gameParams) => netGames.SetNetParams(gameParams);
    static NetIdentity LocalIdentity() => netGames.LocalIdentity();
    static void BeginNetGame(NetSession session, StartGameInfo start) => netGames.BeginNetGame(session, start);
    internal static string? PlayerColor(string color) => netGames.PlayerColor(color);

    /// <summary>What the multiplayer screens need from the game</summary>
    static MultiplayerHooks MakeMultiplayerHooks() => new(
        PlayerClass: () => NewGamePlayerClass ?? DefaultPlayerClass,
        ClassChosen: playerClass => NewGamePlayerClass = playerClass,
        PlayerClasses: PlayerClasses,
        Identity: LocalIdentity,
        BeginNetGame: BeginNetGame,
        BeginJoinedGame: BeginJoinedGame,
        Started: () => StartGame != 0,
        EpisodeOpen: episode => Unlocks.IsOpen(episode));

    // ==== NetPlay ================================================================================

    // Playing with others: the lockstep game (Networking/NetPlay.cs), and what this machine shows
    // and does over it (Ui/NetOverlay.cs)
    static NetPlay netPlay = null!;         // made in InitServices
    static NetOverlay netOverlay = null!;

    static NetPlay MakeNetPlay() =>
        new(MapManager, _videoManager, _inputManager, HudMessageManager, doors, pushWalls, Session,
            new NetPlayHooks(() => LocalCmd, WriteNetState, ThreeDRefresh,
                () => LastTimeCount = (int)GameEngineManager.GetTimeCount(), AddNetPlayer, ConnectAreas));

    static NetOverlay MakeNetOverlay() =>
        new(_inputManager, _videoManager, MapManager, ui, menuDrawing, scoreboard, netPlay, Session);

    static bool NetLeavePrompt => netPlay.NetLeavePrompt;

    static bool NetChatting => netOverlay.NetChatting;
    static void NetChatKeys() => netOverlay.NetChatKeys();
    static void NetCheckKeys(ScanCodes scan) => netOverlay.NetCheckKeys(scan);
    static void DrawNetOverlay() => netOverlay.DrawNetOverlay();

    // net_find, net_portcheck, net_status and scoreboard (Commands/NetCommands.cs)
    static NetCommands netCommands = null!;     // made in InitServices

    /// <summary>A random number for something only this machine shows (a quit message), not the game's own</summary>
    internal static int UiRandom() => NetGame ? Random.Shared.Next(256) : NextRandom();

    // ==== NetJoin ================================================================================

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

    static MidGameJoin MakeMidGameJoin() => new(MapManager, InventoryManager, levelBodies, levelSetup, pushWalls, playersManager, deathmatch,
        scoreboard, netPlay, Session,
        new MidGameJoinHooks(PlayerColor, () => StartGame = 1));

    static byte[] WriteNetState() => midGameJoin.WriteNetState();
    static string? BeginJoinedGame(NetSession session, JoinStateInfo join) => midGameJoin.BeginJoinedGame(session, join);

    // ==== Deathmatch =============================================================================

    // Deathmatch's rules and state (Gameplay/Deathmatch.cs), and the scoreboard (Ui/Scoreboard.cs)
    static Deathmatch deathmatch = null!;       // made in InitServices
    static Scoreboard scoreboard = null!;

    static Deathmatch MakeDeathmatch() =>
        new(MapManager, InventoryManager, statusBarDrawing, Session,
            new DeathmatchHooks(IsFreeFloor, GiveAllKeys));

    // Each mode's objective (Gameplay/MatchModes): the one for the mode played runs
    static void AddObjectives()
    {
        var hooks = new Gameplay.MatchModes.MatchModeHooks(
            (x, y) => playersManager.IsOpenFloor(x, y),
            (x, y) => playersManager.IsFreeFloor(x, y),
            name => TranslationManager.Exists(name) ? name : null,
            name => AudioManager.Play(name),
            Rearm,
            p =>
            {
                using var _ = Session.ActAs(p);
                playersManager.Respawn();
            },
            p =>
            {
                using var _ = Session.ActAs(p);
                statusBarDrawing.DrawHealth();
            });
        deathmatch.MatchObjectives.AddRange(
        [
            new Gameplay.MatchModes.CaptureTheFlag(MapManager, Session, deathmatch, HudMessageManager, hooks),
            new Gameplay.MatchModes.Heist(MapManager, Session, deathmatch, HudMessageManager, hooks),
            new Gameplay.MatchModes.Domination(MapManager, Session, deathmatch, HudMessageManager, hooks),
            new Gameplay.MatchModes.KingOfTheHill(MapManager, Session, deathmatch, HudMessageManager, hooks),
            new Gameplay.MatchModes.Horde(MapManager, Session, deathmatch, HudMessageManager, hooks, () => GameEngineManager.GetGameInfo().Horde),
            new Gameplay.MatchModes.PropHunt(MapManager, Session, deathmatch, HudMessageManager, hooks),
        ]);
    }

    // A player's starting loadout afresh, carrying every key, as a respawn gives them
    static void Rearm(Entities.PlayerState p)
    {
        using var _ = Session.ActAs(p);
        playerStats.GiveStartingInventory();
        p.WeaponSprite = null;
        p.WeaponCharge = 0;
        GiveDeathmatchKeys();
        playerWeapons.DrawWeapon();
        playerWeapons.DrawAmmo();
    }

    static Scoreboard MakeScoreboard() =>
        new(_videoManager, _inputManager, menuDrawing, ui, Session, ClearMemory);

    /// <summary>The rules of the deathmatch being played</summary>
    internal static NetRules NetRules { get => deathmatch.NetRules; set => deathmatch.NetRules = value; }

    static (int x, int y, int angle) DeathmatchSpot(PlayerPawn self) => deathmatch.DeathmatchSpot(self);
    static void GiveDeathmatchKeys() => deathmatch.GiveDeathmatchKeys();
    static bool HasWeaponFrom(Weapon pickup) => deathmatch.HasWeaponFrom(pickup);
    static void QueueItemRespawn(Inventory item) => deathmatch.QueueItemRespawn(item);
    static void CountFrag(Entities.PlayerState victim, Entities.Actors.Actor? killer) => deathmatch.PlayerKilled(victim, killer);
    static bool MayRespawn(Entities.PlayerState p) => deathmatch.Objective?.MayRespawn(p) ?? true;
}
