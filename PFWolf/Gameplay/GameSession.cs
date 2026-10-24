using PFWolf.Entities;
using PFWolf.Entities.Actors;

namespace PFWolf.Gameplay;

/// <summary>
/// How the players play together: Single (just one), Coop (together against the level, keys
/// shared), Deathmatch (everyone against everyone), or one of the team and objective modes
/// (Gameplay/GameModes.cs). Sent over the network as a byte: new ones go at the end.
/// </summary>
internal enum GameMode { Single, Coop, Deathmatch, TeamDeathmatch, CaptureTheFlag, Heist, Domination, KingOfTheHill, Horde, PropHunt }

/*
=============================================================================

                                GAME SESSION

    The state nearly all of the game reads: who's playing and who's acting
    now, how they play together, this frame's tics, the game's progress
    (GameState) and where the view is drawn from (camera).

    The game's player code is written for one player: it reads player,
    PlayerState and PlayerInput, which are always the acting player's.
    MapManager.DoActors makes each pawn the acting player while it thinks,
    ActAs does it for anything else done to a particular player (hurting
    them, say), and the rest of the time it's the local player: the one
    whose view, status bar, flashes and sounds this machine shows.

    GameHost forwards its old globals of the same names to one of these.

=============================================================================
*/

internal sealed class GameSession
{
    public GameSession()
    {
        PlayerState = Players[0];
        Camera = new Camera(this);
    }

    /// <summary>Everyone in the game, in the order they think; never empty once a game has started</summary>
    internal readonly List<PlayerState> Players = [new()];

    /// <summary>Which of players is this machine's: what the screen shows and the controls drive</summary>
    internal int ConsolePlayer;

    /// <summary>This machine's player</summary>
    internal PlayerState LocalPlayer => Players[ConsolePlayer];

    /// <summary>The player acting now: see ActAs</summary>
    internal PlayerState PlayerState { get; private set; }

    /// <summary>Whether the acting player is this machine's, so their screen, status bar and sounds are shown</summary>
    internal bool ActingIsLocal => ReferenceEquals(PlayerState, LocalPlayer);

    /// <summary>Told each time the acting player changes (the inventory reads the acting player's items)</summary>
    internal Action<PlayerState>? ActingChanged;

    /// <summary>
    /// Makes <paramref name="p"/> the acting player until the returned scope is disposed: the
    /// game's player code (player, PlayerState, the inventory...) then reads and changes theirs.
    /// </summary>
    internal ActingScope ActAs(PlayerState p)
    {
        var scope = new ActingScope(this, PlayerState);
        SetActing(p);
        return scope;
    }

    internal void SetActing(PlayerState p)
    {
        PlayerState = p;
        ActingChanged?.Invoke(p);
    }

    internal readonly struct ActingScope(GameSession session, PlayerState previous) : IDisposable
    {
        public void Dispose() => session.SetActing(previous);
    }

    // The acting player's pawn, which lives in MapManager._actors like every other actor; this
    // is just a shortcut to it.
    internal PlayerPawn Player =>
        PlayerState.Pawn ?? throw new InvalidOperationException("The player has not been spawned for this level yet.");

    // The acting player's controls this frame: the game reads these, never _inputManager's buttons
    internal PlayerInput PlayerInput => PlayerState.Input;

    internal GameMode GameMode { get; private set; } = GameMode.Single;

    /// <summary>Told each time the game mode changes (co-op shares keys)</summary>
    internal Action<GameMode>? GameModeChanged;

    internal void SetGameMode(GameMode mode)
    {
        GameMode = mode;
        GameModeChanged?.Invoke(mode);
    }

    /// <summary>Players fight each other (deathmatch and every team mode)</summary>
    internal bool IsVersus => GameMode.IsVersus();

    /// <summary>Players are on two teams (Entities.PlayerState.Team)</summary>
    internal bool IsTeamGame => GameMode.IsTeamGame();

    /// <summary>
    /// Whether two players fight: in a versus mode, anyone else, but not a teammate (no
    /// friendly fire)
    /// </summary>
    internal bool AreRivals(PlayerState a, PlayerState b) =>
        IsVersus && !ReferenceEquals(a, b) && !(IsTeamGame && a.Team == b.Team);

    /// <summary>The tics this frame moves the game on by</summary>
    internal uint Tics;

    internal GameState GameState = new GameState();

    /// <summary>
    /// Each floor's ratios and time as last completed, by map, for the win tally (saved with
    /// the game); a new game starts it empty
    /// </summary>
    internal Dictionary<string, LevelRatio> LevelRatios = new();

    /// <summary>id's random numbers, which every machine and every demo has to step through alike</summary>
    internal readonly RandomTable Random = new();

    /// <summary>Which of the level's areas are connected, by open doors, to the players' (Gameplay/AreaConnections.cs)</summary>
    internal readonly AreaConnections Areas = new();

    internal readonly Camera Camera;

    /// <summary>Whether the level being set up came from a save (its totals are the save's, not counted afresh)</summary>
    internal bool LoadedGame;

    /// <summary>Whether a demo's being recorded, or played back</summary>
    internal bool DemoRecord, DemoPlayback;

    /// <summary>
    /// Whether a player can be gone after: one alive. A lone player always can (dying ends the
    /// level there, and the rest of that frame plays on as it always has).
    /// </summary>
    internal bool IsTargetable(PlayerPawn pawn) => GameMode == GameMode.Single || pawn.State.Health > 0;

    // ---- The game's flow, its network game, and the cheat and debug switches (GameHost had these as its own statics)

    /// <summary>A game was chosen in the menu (new or loaded): the title loop starts it</summary>
    internal bool GameChosen;

    /// <summary>Whether a game is going (the menu was opened from it), and whether the next frame fades the level in</summary>
    internal bool InGame, FizzleIn;

    /// <summary>The color of the border around the view (Change View, in game)</summary>
    internal string BorderCol = "VIEWCOLOR";

    /// <summary>Where the menu left the level's song, to carry on from</summary>
    internal int LastGameMusicOffset;

    /// <summary>Whether the game running is one with others over the network</summary>
    internal bool NetGame;

    /// <summary>The random number table's starting place for each level of a network game (the host's pick)</summary>
    internal byte NetSeed;

    /// <summary>The debug keys and cheat commands are on (Shift+Alt+Backspace, or --cheats)</summary>
    internal int DebugOk;

    /// <summary>Cheats and debug switches: frame stepping, god mode, walking through walls, endless ammo, the whole map shown</summary>
    internal byte SingleStep, GodMode, NoClip, AmmoCheat, MapReveal;

    /// <summary>Extra frames waited each refresh (the bls debug command)</summary>
    internal int ExtraVbls;

    /// <summary>true when shooting or screaming: enemies in the player's areas hear it this tic</summary>
    internal bool MadeNoise;

    /// <summary>A noise was made last tic (bstone's `alerted` counting down): security lights still hear it</summary>
    internal bool NoiseLastTic;

    /// <summary>
    /// The player's weapon can't fire (A_PlayerFire("off"): Goldfire morphing, bstone's noShots).
    /// Cleared as a level starts.
    /// </summary>
    internal bool NoShots;

    /// <summary>A message shown as the level's play starts, after the last level's are cleared (Blake Stone's "transporting...")</summary>
    internal string? LevelStartHudMessage;

    /// <summary>How the level's going: playing on, died, completed, warped...</summary>
    internal PlayState PlayState;

    /// <summary>
    /// A random number for which of a sound's variants plays: the game's own alone, as it always
    /// was, but not with others (one machine may have its sound off, and play none)
    /// </summary>
    internal int SoundRandom() => GameMode == GameMode.Single ? Random.RndT() : System.Random.Shared.Next(256);
}
