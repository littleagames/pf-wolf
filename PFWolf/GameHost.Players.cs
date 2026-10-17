using PFWolf.Entities.Actors;

namespace PFWolf;

internal static partial class GameHost
{
    /*
    ====================
    =
    = Players
    =
    = Everyone in the game, each with their own PlayerState, and who's acting now: the
    = GameSession (Gameplay/GameSession.cs), which these forward to.
    =
    ====================
    */

    internal static List<Entities.PlayerState> players => session.players;
    internal static Entities.PlayerState localplayer => session.localplayer;
    internal static Entities.PlayerState playerstate => session.playerstate;
    internal static GameSession.ActingScope ActAs(Entities.PlayerState p) => session.ActAs(p);
    static void SetActing(Entities.PlayerState p) => session.SetActing(p);
    internal static GameMode gamemode => session.gamemode;

    // Everyone in the game coming and going (Gameplay/Players.cs)
    static Players playersManager = null!;      // made in InitServices

    static Players MakePlayers() =>
        new(_mapManager, _audioManager, _videoManager, _inventoryManager, _hudMessageManager, statusBarDrawing, playerClassRules, playerStats,
            playerWeapons, playerMovement, playerThink, session,
            new PlayersHooks(PlaySoundLocActor, DeathmatchSpot, GiveDeathmatchKeys, CountFrag, MayRespawn, ShowObituary, ConnectAreas,
                () => deathTurnSpeed, () => deathDropHeight, () => deathDropSpeed, PlayerColor));

    internal static void TakeDamage(Entities.Actors.PlayerPawn victim, int points, Entities.Actors.Actor attacker) => playersManager.TakeDamage(victim, points, attacker);
    internal static void PlayPlayerSound(string name) => playersManager.PlayPlayerSound(name);
    internal static (int x, int y) StandInStart() => playersManager.StandInStart();
    static (int x, int y)? FreeTileNear(int tilex, int tiley, List<(int x, int y)> taken) => playersManager.FreeTileNear(tilex, tiley, taken);
    static bool IsFreeFloor(int x, int y) => playersManager.IsFreeFloor(x, y);
    internal static void PlayerDies() => playersManager.PlayerDies();
    internal static void T_PlayerDead(Entities.Actors.Actor ob) => playersManager.T_PlayerDead(ob);
    static void AddNetPlayer(Networking.NetJoin join) => playersManager.AddNetPlayer(join);

    // addplayer, gamemode and controlplayer (Commands/PlayerCommands.cs)
    static PlayerCommands playerCommands = null!;       // made in InitServices

    /// <summary>The most players a game can have</summary>
    internal const int MAXPLAYERS = PlayerCommands.MAXPLAYERS;

    //
    // The acting player's state, under the names the game has always used for it
    //
    static ushort plux { get => playerstate.PlUX; set => playerstate.PlUX = value; }    // player coordinates scaled to unsigned
    static Entities.Actors.Actor? LastAttacker { get => playerstate.LastAttacker; set => playerstate.LastAttacker = value; }

    // The player's own look up/down (degrees, up positive) and eye height (texels above the
    // floor): reset each level, not saved
    internal static int playereyez { get => playerstate.EyeZ; set => playerstate.EyeZ = value; }
}
