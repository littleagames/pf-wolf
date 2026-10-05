using System.Diagnostics.CodeAnalysis;

namespace Wolf3D.Entities.Actors;

/// <summary>
/// The player. Lives in <c>MapManager._actors</c> (at the head, so it thinks before everything
/// else) and is driven by the same <c>MapManager.DoActor</c> loop as every other actor.
/// </summary>
/// <remarks>
/// The states are built here rather than in actordefs YAML: they carry no sprites and exist only
/// to pick which engine handler runs each tic (T_Player, registered in
/// Program.RegisterActorActions): T_Player normally, T_DeathCam during the death cam, which
/// animates the banner DrawPlayerWeapon draws (the DeathCam class in actordefs/deathcam.yaml), and
/// T_PlayerDead while dead in a game with others (Program.Players.cs), until they come back.
/// Attacks are the weapon's own states (Program.PlayerWeapon.cs), not the player's.
/// Each holds forever (TicTime -1), so the handler runs every tic and Next is never consulted.
/// </remarks>
internal record PlayerPawn : Actor
{
    internal const string SpawnState = "Spawn";
    internal const string DeathCamState = "DeathCam";
    internal const string DeadState = "Dead";

    /// <summary>The player this is the pawn of (MapManager.CreatePlayer)</summary>
    internal PlayerState State { get; init; } = null!;

    /// <summary>This player's controls as the game reads them each frame (fed by PlayLoop)</summary>
    internal PlayerInput Input => State.Input;

    [SetsRequiredMembers]
    public PlayerPawn()
    {
        Name = "Player";

        var spawn = new ActorStateFrame { StateName = SpawnState, Sprite = "", FrameLetter = "", TicTime = -1, Think = "T_Player" };
        var deathCam = new ActorStateFrame { StateName = DeathCamState, Sprite = "", FrameLetter = "", TicTime = -1, Think = "T_DeathCam" };
        var dead = new ActorStateFrame { StateName = DeadState, Sprite = "", FrameLetter = "", TicTime = -1, Think = "T_PlayerDead" };

        ResolvedStates = new()
        {
            [SpawnState] = spawn,
            [DeathCamState] = deathCam,
            [DeadState] = dead,
        };
        CurrentState = spawn;
    }
}
