using System.Diagnostics.CodeAnalysis;

namespace Wolf3D.Entities.Actors;

/// <summary>
/// The player. Lives in <c>MapManager._actors</c> (at the head, so it thinks before everything
/// else, as it did when it held the first slot of objlist2) and is driven by the same
/// <c>MapManager.DoActor</c> loop as every other actor.
/// </summary>
/// <remarks>
/// The states are built here rather than in actordefs YAML: they carry no sprites and exist only
/// to pick which engine handler runs each tic (T_Player / T_Attack, registered in
/// Program.RegisterActorActions), plus the death-cam marker DrawPlayerWeapon looks for.
/// Both think states have TicTime 0, so the handler runs every tic and Next is never consulted.
/// </remarks>
internal record PlayerPawn : Actor
{
    internal const string SpawnState = "Spawn";
    internal const string AttackState = "Attack";
    internal const string DeathCamState = "DeathCam";

    [SetsRequiredMembers]
    public PlayerPawn()
    {
        Name = "Player";

        var spawn = new ActorStateFrame { StateName = SpawnState, Sprite = "", FrameLetter = "", Think = "T_Player" };
        var attack = new ActorStateFrame { StateName = AttackState, Sprite = "", FrameLetter = "", Think = "T_Attack" };
        var deathCam = new ActorStateFrame { StateName = DeathCamState, Sprite = "DCAM", FrameLetter = "A" };

        ResolvedStates = new()
        {
            [SpawnState] = spawn,
            [AttackState] = attack,
            [DeathCamState] = deathCam,
        };
        CurrentState = spawn;
    }
}
