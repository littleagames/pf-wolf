using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Rendering;

/// <summary>What working out the player's sight needs from the game</summary>
/// <param name="FocalLength">How far behind the player the view point is (Setup3DView's focallength)</param>
/// <param name="ViewWidth">The view's width in pixels</param>
/// <param name="TransformActorFrom">Works out where an actor would be on a view from a point (sets its ViewX, ViewHeight, TransX)</param>
/// <param name="IsWallSprite">Whether an actor is a wall sprite (never picked up)</param>
/// <param name="GetBonus">The acting player picks an item up (Gameplay/Pickups.cs)</param>
internal sealed record PlayerViewHooks(
    Func<int> FocalLength,
    Func<int> ViewWidth,
    Action<Entities.Actors.Actor, int, int, int, int> TransformActorFrom,
    Func<Entities.Actors.Actor, bool> IsWallSprite,
    Action<Inventory> GetBonus);

/// <summary>
/// Player sight: what the player touches and sees, kept apart from what the camera draws.
/// Vanilla worked both out while drawing: items were picked up when the renderer found them
/// under the view, and an enemy on the screen got FL_VISABLE (the gun's targets, enemy aim,
/// NOTICEWHENSEEN, Blake's AI, the automap's marks). While the camera is on the player, drawing
/// still sets FL_VISABLE; when it's on someone else, PlayerSight sets it from the player's own
/// view.
///
/// What players see, with several of them: alone, what the player sees is what the renderer
/// drew: an enemy on the screen is FL_VISABLE (enemies aim worse at a player who can see them)
/// and wakes for good. With others, the screen is just one of them (and on another machine,
/// another one), so it's worked out from each player's own position instead: InViewOf, the
/// same on every machine.
/// </summary>
internal sealed class PlayerView(
    MapManager map,
    LineOfSight lineOfSight,
    GameSession session,
    PlayerViewHooks hooks)
{
    /// <summary>The player's view point and its sine and cosine: focallength behind them, as Setup3DView puts it</summary>
    internal (int x, int y, int sin, int cos) PlayerViewOrigin()
    {
        var player = session.player;
        int focallength = hooks.FocalLength();
        int sin = Program.sintable[player.Angle], cos = Program.sintable[player.Angle + Program.ANGLES / 4];
        return (player.X - MathUtils.FixedMul(focallength, cos), player.Y + MathUtils.FixedMul(focallength, sin), sin, cos);
    }

    /// <summary>
    /// Picks up the items the player is on, each tic after the actors have moved. An item is
    /// taken when it's within TransformTile's grabbing distance of the player's view point and
    /// in front of it, or in the player's own tile (vanilla's spotvis fix for that tile).
    /// </summary>
    internal void TouchItems()
    {
        if (map.Player == null)
            return;

        var player = session.player;
        var (fromx, fromy, fromsin, fromcos) = PlayerViewOrigin();
        List<Inventory>? touched = null;

        foreach (var actor in map.GetActors())
        {
            // Only the items DrawScaleds would have drawn as statics
            if (actor is not Inventory item || item.IsRemoved || item.CurrentState == null
                || item.ResolvedStates.ContainsKey("Chase") || item.Active == activetypes.ac_yes || hooks.IsWallSprite(item))
                continue;

            int tx = (int)item.Position.X, ty = (int)item.Position.Y;
            int gx = (tx << MapConstants.TILESHIFT) + 0x8000 - fromx;
            int gy = (ty << MapConstants.TILESHIFT) + 0x8000 - fromy;
            int nx = MathUtils.FixedMul(gx, fromcos) - MathUtils.FixedMul(gy, fromsin);
            int ny = MathUtils.FixedMul(gx, fromsin) + MathUtils.FixedMul(gy, fromcos);

            bool grab = nx - 0x2000 < MapConstants.TILEGLOBAL && ny > -MapConstants.TILEGLOBAL / 2 && ny < MapConstants.TILEGLOBAL / 2;
            bool seen = nx > 0 || (tx == player.TileX && ty == player.TileY);
            if (grab && seen)
                (touched ??= []).Add(item);
        }

        // Taken after the walk: GetBonus removes them from the actor list
        if (touched != null)
            foreach (var item in touched)
                hooks.GetBonus(item);
    }

    /// <summary>
    /// When the camera is on someone else, sets FL_VISABLE and the gun's ViewX/TransX for what
    /// the player would see: an enemy in front of them, on their screen and in their line of
    /// sight (CheckLine). Run after the frame is drawn, so it has the last word.
    /// </summary>
    internal void PlayerSight()
    {
        if (session.camera.OnPlayer || map.Player == null)
            return;

        var (fromx, fromy, fromsin, fromcos) = PlayerViewOrigin();
        int viewwidth = hooks.ViewWidth();

        foreach (var actor in map.GetActors())
        {
            if (actor is PlayerPawn || actor.IsRemoved || actor.CurrentState == null
                || !(actor.ResolvedStates.ContainsKey("Chase") || actor.Active == activetypes.ac_yes))
                continue;

            hooks.TransformActorFrom(actor, fromx, fromy, fromsin, fromcos);

            bool onscreen = actor.ViewHeight > 0
                && actor.ViewX + actor.ViewHeight >= 0 && actor.ViewX - actor.ViewHeight < viewwidth;
            if (onscreen && actor is Monster && session.gamemode == GameMode.Single)
                actor.Active = activetypes.ac_yes;      // seen: awake for good, as when drawn
            if (onscreen && lineOfSight.CheckLine(actor))
                actor.RuntimeFlags |= Program.objflags.FL_VISABLE;
            else
                actor.RuntimeFlags &= ~Program.objflags.FL_VISABLE;
        }
    }

    /// <summary>
    /// Whether a player's tile is in view: vanilla read spotvis there, which the camera fix
    /// always set. With others (or the camera elsewhere), whether it's open floor or a door,
    /// which is what that comes to, without asking the screen.
    /// </summary>
    internal bool PlayerTileInView(PlayerPawn pawn) =>
        session.camera.Target == pawn && session.gamemode == GameMode.Single
            ? map.spotvis[pawn.TileX, pawn.TileY]
            : map.tilemap[pawn.TileX, pawn.TileY] == 0 || (map.tilemap[pawn.TileX, pawn.TileY] & Program.BIT_DOOR) != 0;

    /// <summary>
    /// Whether a player could see an actor: in front of them, within the view's width either
    /// side (the classic field of view, however wide the screen), with nothing in the way
    /// </summary>
    internal bool InViewOf(PlayerPawn pawn, Entities.Actors.Actor actor)
    {
        int focallength = hooks.FocalLength();
        int sin = Program.sintable[pawn.Angle], cos = Program.sintable[pawn.Angle + Program.ANGLES / 4];
        int fromx = pawn.X - MathUtils.FixedMul(focallength, cos), fromy = pawn.Y + MathUtils.FixedMul(focallength, sin);
        int gx = actor.X - fromx, gy = actor.Y - fromy;
        int nx = MathUtils.FixedMul(gx, cos) - MathUtils.FixedMul(gy, sin) - Program.ACTORSIZE;
        int ny = MathUtils.FixedMul(gy, cos) + MathUtils.FixedMul(gx, sin);
        if (nx < Program.MINDIST)
            return false;

        // |ny / nx| * scale within half the view: the view's width taken out of both
        return Math.Abs((long)ny) * (Program.FOCALLENGTH + Program.MINDIST) < (long)nx * (Program.VIEWGLOBAL / 2)
            && lineOfSight.CheckLine(actor, pawn);
    }

    /// <summary>Whether any living player could see an actor (InViewOf)</summary>
    internal bool SeenByAPlayer(Entities.Actors.Actor actor) =>
        map.Players.Any(pawn => pawn.State.health > 0 && InViewOf(pawn, actor));

    /// <summary>
    /// With several players, each frame: enemies a living player could see wake for good, as
    /// being drawn wakes them alone (MapManager.DoActor lets a sleeping one rest out of reach)
    /// </summary>
    internal void WakeSeenEnemies()
    {
        foreach (var actor in map.GetActors())
        {
            if (actor is Monster && !actor.IsRemoved && actor.Active == activetypes.ac_no && SeenByAPlayer(actor))
                actor.Active = activetypes.ac_yes;
        }
    }
}
