using System.Diagnostics.CodeAnalysis;

namespace Wolf3D.Entities.Actors;

/// <summary>
/// The player. Lives in <c>MapManager._actors</c> (at the head, so it thinks before everything
/// else) and is driven by the same <c>MapManager.DoActor</c> loop as every other actor.
/// </summary>
/// <remarks>
/// The states are built here rather than in actordefs YAML: they carry no sprites and exist only
/// to pick which engine handler runs each tic (T_Player, registered in
/// Program.RegisterActorActions): T_Player normally, and T_DeathCam during the death cam, which
/// animates the banner DrawPlayerWeapon draws (the DeathCam class in actordefs/deathcam.yaml).
/// Attacks are the weapon's own states (Program.PlayerWeapon.cs), not the player's.
/// Both hold forever (TicTime -1), so the handler runs every tic and Next is never consulted.
/// </remarks>
internal record PlayerPawn : Actor
{
    internal const string SpawnState = "Spawn";
    internal const string DeathCamState = "DeathCam";

    /// <summary>This player's controls as the game reads them each frame (fed by PlayLoop)</summary>
    internal PlayerInput Input { get; } = new();

    [SetsRequiredMembers]
    public PlayerPawn()
    {
        Name = "Player";

        var spawn = new ActorStateFrame { StateName = SpawnState, Sprite = "", FrameLetter = "", TicTime = -1, Think = "T_Player" };
        var deathCam = new ActorStateFrame { StateName = DeathCamState, Sprite = "", FrameLetter = "", TicTime = -1, Think = "T_DeathCam" };

        ResolvedStates = new()
        {
            [SpawnState] = spawn,
            [DeathCamState] = deathCam,
        };
        CurrentState = spawn;
    }
}
