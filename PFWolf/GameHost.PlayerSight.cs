using PFWolf.Entities.Actors;

namespace PFWolf;

internal static partial class GameHost
{
    // What the player touches and sees, kept apart from what the camera draws (Rendering/PlayerView.cs)
    static PlayerView playerView = null!;       // made in InitServices

    static PlayerView MakePlayerView() =>
        new(_mapManager, lineOfSight, session, new PlayerViewHooks(
            () => focallength, () => viewwidth, TransformActorFrom, IsWallSprite, GetBonus));

    static (int x, int y, int sin, int cos) PlayerViewOrigin() => playerView.PlayerViewOrigin();
    internal static bool PlayerTileInView(PlayerPawn pawn) => playerView.PlayerTileInView(pawn);
    internal static bool InViewOf(PlayerPawn pawn, Entities.Actors.Actor actor) => playerView.InViewOf(pawn, actor);
    internal static bool SeenByAPlayer(Entities.Actors.Actor actor) => playerView.SeenByAPlayer(actor);
}
