using System.Diagnostics.CodeAnalysis;

namespace Wolf3D.Entities.Actors;

/// <summary>
/// The player. Lives in <c>MapManager._actors</c> (at the head, so it thinks before everything
/// else) and is driven by the same <c>MapManager.DoActor</c> loop as every other actor.
/// </summary>
/// <remarks>
/// The states are built here rather than in actordefs YAML: they carry no sprites and exist only
/// to pick which engine handler runs each tic (T_Player, registered in
/// Program.RegisterActorActions), plus the death-cam marker DrawPlayerWeapon looks for.
/// Attacks are the weapon's own states (Program.PlayerWeapon.cs), not the player's; a save from
/// before that, taken mid-attack, names an "Attack" state and loads back on Spawn.
/// Both hold forever (TicTime -1), so the handler runs every tic and Next is never consulted.
/// </remarks>
internal record PlayerPawn : Actor
{
    internal const string SpawnState = "Spawn";
    internal const string DeathCamState = "DeathCam";

    [SetsRequiredMembers]
    public PlayerPawn()
    {
        Name = "Player";

        var spawn = new ActorStateFrame { StateName = SpawnState, Sprite = "", FrameLetter = "", TicTime = -1, Think = "T_Player" };
        var deathCam = new ActorStateFrame { StateName = DeathCamState, Sprite = "DCAM", FrameLetter = "A", TicTime = -1 };

        ResolvedStates = new()
        {
            [SpawnState] = spawn,
            [DeathCamState] = deathCam,
        };
        CurrentState = spawn;
    }
}
