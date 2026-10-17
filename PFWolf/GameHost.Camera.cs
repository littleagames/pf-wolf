using PFWolf.Entities.Actors;

namespace PFWolf;

internal static partial class GameHost
{
    internal static void SetEyeHeight(int z) => playereyez = Math.Clamp(z, MINEYE, MAXEYE);

    // The `spectate` command (Gameplay/Spectating.cs)
    static Spectating spectating = null!;       // made in InitServices

    static IEnumerable<Entities.Actors.Actor> SpectateCandidates() => spectating.SpectateCandidates();
    private static void Cmd_Spectate(string[] args) => spectating.Cmd_Spectate(args);

    // What others see of each player (Gameplay/PlayerBodies.cs)
    static PlayerBodies playerBodies = null!;       // made in InitServices

    internal static void TickPlayerBodies(uint tics) => playerBodies.TickPlayerBodies(tics);
    static Entities.Actors.Actor? VisiblePlayerBody(PlayerPawn pawn) => playerBodies.VisiblePlayerBody(pawn);
    static bool IsPlayerBody(Entities.Actors.Actor actor) => playerBodies.IsPlayerBody(actor);
    internal static string DrawnSprite(Entities.Actors.Actor actor) => playerBodies.DrawnSprite(actor);
    static bool SpriteExists(string name) => playerBodies.SpriteExists(name);
}
