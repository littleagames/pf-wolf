using Wolf3D.Entities.Actors;

namespace Wolf3D;

internal partial class Program
{
    /*
    ====================
    =
    = Player sight
    =
    = What the player touches and sees, kept apart from what the camera draws. Vanilla worked
    = both out while drawing: items were picked up when the renderer found them under the view,
    = and an enemy on the screen got FL_VISABLE (the gun's targets, enemy aim, NOTICEWHENSEEN,
    = Blake's AI, the automap's marks). While the camera is on the player, drawing still sets
    = FL_VISABLE; when it's on someone else, PlayerSight sets it from the player's own view.
    =
    ====================
    */

    /// <summary>The player's view point and its sine and cosine: focallength behind them, as Setup3DView puts it</summary>
    static (int x, int y, int sin, int cos) PlayerViewOrigin()
    {
        int sin = sintable[player.Angle], cos = sintable[player.Angle + ANGLES / 4];
        return (player.X - MathUtils.FixedMul(focallength, cos), player.Y + MathUtils.FixedMul(focallength, sin), sin, cos);
    }

    /// <summary>
    /// Picks up the items the player is on, each tic after the actors have moved. An item is
    /// taken when it's within TransformTile's grabbing distance of the player's view point and
    /// in front of it, or in the player's own tile (vanilla's spotvis fix for that tile).
    /// </summary>
    internal static void TouchItems()
    {
        if (_mapManager.Player == null)
            return;

        var (fromx, fromy, fromsin, fromcos) = PlayerViewOrigin();
        List<Inventory>? touched = null;

        foreach (var actor in _mapManager.GetActors())
        {
            // Only the items DrawScaleds would have drawn as statics
            if (actor is not Inventory item || item.IsRemoved || item.CurrentState == null
                || item.ResolvedStates.ContainsKey("Chase") || item.Active == activetypes.ac_yes || IsWallSprite(item))
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
                GetBonus(item);
    }

    /// <summary>
    /// When the camera is on someone else, sets FL_VISABLE and the gun's ViewX/TransX for what
    /// the player would see: an enemy in front of them, on their screen and in their line of
    /// sight (CheckLine). Run after the frame is drawn, so it has the last word.
    /// </summary>
    internal static void PlayerSight()
    {
        if (camera.OnPlayer || _mapManager.Player == null)
            return;

        var (fromx, fromy, fromsin, fromcos) = PlayerViewOrigin();

        foreach (var actor in _mapManager.GetActors())
        {
            if (actor is PlayerPawn || actor.IsRemoved || actor.CurrentState == null
                || !(actor.ResolvedStates.ContainsKey("Chase") || actor.Active == activetypes.ac_yes))
                continue;

            TransformActorFrom(actor, fromx, fromy, fromsin, fromcos);

            bool onscreen = actor.ViewHeight > 0
                && actor.ViewX + actor.ViewHeight >= 0 && actor.ViewX - actor.ViewHeight < viewwidth;
            if (onscreen && actor is Monster && gamemode == GameMode.Single)
                actor.Active = activetypes.ac_yes;      // seen: awake for good, as when drawn
            if (onscreen && CheckLine(actor))
                actor.RuntimeFlags |= objflags.FL_VISABLE;
            else
                actor.RuntimeFlags &= ~objflags.FL_VISABLE;
        }
    }

    /// <summary>
    /// Whether a player's tile is in view: vanilla read spotvis there, which the camera fix
    /// always set. With others (or the camera elsewhere), whether it's open floor or a door,
    /// which is what that comes to, without asking the screen.
    /// </summary>
    internal static bool PlayerTileInView(Entities.Actors.PlayerPawn pawn) =>
        camera.Target == pawn && gamemode == GameMode.Single
            ? _mapManager.spotvis[pawn.TileX, pawn.TileY]
            : _mapManager.tilemap[pawn.TileX, pawn.TileY] == 0 || (_mapManager.tilemap[pawn.TileX, pawn.TileY] & BIT_DOOR) != 0;
}
