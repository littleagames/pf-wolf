using PFWolf.Entities;
using PFWolf.Entities.Actors;

namespace PFWolf.Gameplay;

/// <summary>
/// How the players play together: Single (just one), Coop (together against the level, keys
/// shared) or Deathmatch (against each other)
/// </summary>
internal enum GameMode { Single, Coop, Deathmatch }

/*
=============================================================================

                                GAME SESSION

    The state nearly all of the game reads: who's playing and who's acting
    now, how they play together, this frame's tics, the game's progress
    (gamestate) and where the view is drawn from (camera).

    The game's player code is written for one player: it reads player,
    playerstate and playerinput, which are always the acting player's.
    MapManager.DoActors makes each pawn the acting player while it thinks,
    ActAs does it for anything else done to a particular player (hurting
    them, say), and the rest of the time it's the local player: the one
    whose view, status bar, flashes and sounds this machine shows.

    Program forwards its old globals of the same names to one of these.

=============================================================================
*/

internal sealed class GameSession
{
    public GameSession()
    {
        playerstate = players[0];
        camera = new Camera(this);
    }

    /// <summary>Everyone in the game, in the order they think; never empty once a game has started</summary>
    internal readonly List<PlayerState> players = [new()];

    /// <summary>Which of players is this machine's: what the screen shows and the controls drive</summary>
    internal int consoleplayer;

    /// <summary>This machine's player</summary>
    internal PlayerState localplayer => players[consoleplayer];

    /// <summary>The player acting now: see ActAs</summary>
    internal PlayerState playerstate { get; private set; }

    /// <summary>Whether the acting player is this machine's, so their screen, status bar and sounds are shown</summary>
    internal bool ActingIsLocal => ReferenceEquals(playerstate, localplayer);

    /// <summary>Told each time the acting player changes (the inventory reads the acting player's items)</summary>
    internal Action<PlayerState>? ActingChanged;

    /// <summary>
    /// Makes <paramref name="p"/> the acting player until the returned scope is disposed: the
    /// game's player code (player, playerstate, the inventory...) then reads and changes theirs.
    /// </summary>
    internal ActingScope ActAs(PlayerState p)
    {
        var scope = new ActingScope(this, playerstate);
        SetActing(p);
        return scope;
    }

    internal void SetActing(PlayerState p)
    {
        playerstate = p;
        ActingChanged?.Invoke(p);
    }

    internal readonly struct ActingScope(GameSession session, PlayerState previous) : IDisposable
    {
        public void Dispose() => session.SetActing(previous);
    }

    // The acting player's pawn, which lives in MapManager._actors like every other actor; this
    // is just a shortcut to it.
    internal PlayerPawn player =>
        playerstate.Pawn ?? throw new InvalidOperationException("The player has not been spawned for this level yet.");

    // The acting player's controls this frame: the game reads these, never _inputManager's buttons
    internal PlayerInput playerinput => playerstate.Input;

    internal GameMode gamemode { get; private set; } = GameMode.Single;

    /// <summary>Told each time the game mode changes (co-op shares keys)</summary>
    internal Action<GameMode>? GameModeChanged;

    internal void SetGameMode(GameMode mode)
    {
        gamemode = mode;
        GameModeChanged?.Invoke(mode);
    }

    /// <summary>The tics this frame moves the game on by</summary>
    internal uint tics;

    internal gametype gamestate = new gametype();

    internal readonly Camera camera;
}
