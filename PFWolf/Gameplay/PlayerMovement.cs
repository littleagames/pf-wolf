using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>What the player's movement needs from the game</summary>
/// <param name="PlayerFactor">A factor from the player class (player.forwardmove, player.sidemove), 1 when left out</param>
/// <param name="MaxPitch">How far up or down the view can look, in degrees</param>
/// <param name="PlayPlayerSound">A sound the acting player makes (bumping a wall)</param>
/// <param name="WallSpriteHit">The wall sprite panel the player's box at x, y (size either way) runs into among the tiles xl-xh, yl-yh, if any</param>
/// <param name="WallSpriteMoveFrom">Where a move starts, so a panel the player already overlaps there doesn't block it</param>
/// <param name="ActivateTrigger">Runs a mapdefs trigger (a walk-over one, stepped onto)</param>
internal sealed record PlayerMovementHooks(
    Func<string, double> PlayerFactor,
    Func<double> MaxPitch,
    Action<string> PlayPlayerSound,
    Func<int, int, int, int, int, int, long, WallSpriteSpan?> WallSpriteHit,
    Action<int, int> WallSpriteMoveFrom,
    Action<MapTriggerTranslation, int, int, controldirs> ActivateTrigger);

/// <summary>
/// The acting player's movement: turning, looking up and down, walking and strafing from their
/// input, and the move itself -- blocked by walls, pushwalls, diagonals, wall sprites and
/// actors, sliding along what it can -- then the walk-over and floor triggers it steps onto.
/// </summary>
internal sealed class PlayerMovement(
    MapManager map,
    AudioManager audio,
    PushWalls pushWalls,
    GameSession session,
    PlayerMovementHooks hooks)
{
    // The diagonal shape the player last ran into in TryMove, for ClipMove to slide along
    WallShape diagonalblock;

    // The panel the player last ran into in TryMove, for ClipMove to slide along
    WallSpriteSpan? wallspriteblock;

    /// <summary>The cardinal direction the player faces, as Cmd_Use reckons it</summary>
    internal static controldirs FacingDir(int angle) =>
        angle < GameConstants.ANGLES / 8 || angle > 7 * GameConstants.ANGLES / 8 ? controldirs.di_east
        : angle < 3 * GameConstants.ANGLES / 8 ? controldirs.di_north
        : angle < 5 * GameConstants.ANGLES / 8 ? controldirs.di_west
        : controldirs.di_south;

    internal void ControlMovement(Entities.Actors.Actor ob)
    {
        const int ANGLES = GameConstants.ANGLES;
        var playerinput = session.playerinput;
        var playerstate = session.playerstate;
        var tics = session.tics;
        int angle;
        int angleunits;

        playerstate.ThrustSpeed = 0;

        // Manning a mounted gun, they turn but don't move (Gameplay/MountedWeapons.cs); with
        // something latched on, they move that many times slower (Gameplay/Latching.cs)
        bool mounted = playerstate.Mount != null;
        int slowdown = Math.Max(playerstate.MoveSlowdown, 1);
        void Move(int moveAngle, int speed)
        {
            if (!mounted)
                Thrust(moveAngle, speed / slowdown);
        }

        // The player class's speeds: player.forwardmove forwards and back, player.sidemove strafing
        double forward = hooks.PlayerFactor("player.forwardmove"), side = hooks.PlayerFactor("player.sidemove");

        //
        // looking up and down: only the view, which SetupPitch keeps within what it can show
        //
        if (playerinput.CenterView)
            playerstate.Pitch = 0;
        else if (playerinput.Pitch != 0)
            playerstate.Pitch = Math.Clamp(playerstate.Pitch + playerinput.Pitch, -hooks.MaxPitch(), hooks.MaxPitch());

        if (playerinput.IsPressed(buttontypes.bt_strafeleft))
        {
            angle = ob.Angle + ANGLES / 4;
            if (angle >= ANGLES)
                angle -= ANGLES;
            if (playerinput.IsPressed(buttontypes.bt_run))
                Move(angle, (int)(GameConstants.RUNMOVE * GameConstants.MOVESCALE * tics * side));
            else
                Move(angle, (int)(GameConstants.BASEMOVE * GameConstants.MOVESCALE * tics * side));
        }

        if (playerinput.IsPressed(buttontypes.bt_straferight))
        {
            angle = ob.Angle - ANGLES / 4;
            if (angle < 0)
                angle += ANGLES;
            if (playerinput.IsPressed(buttontypes.bt_run))
                Move(angle, (int)(GameConstants.RUNMOVE * GameConstants.MOVESCALE * tics * side));
            else
                Move(angle, (int)(GameConstants.BASEMOVE * GameConstants.MOVESCALE * tics * side));
        }

        //
        // a controller stick's strafe, as far as it's pushed
        //
        if (playerinput.ControlStrafe != 0)
        {
            angle = ob.Angle + (playerinput.ControlStrafe > 0 ? -ANGLES / 4 : ANGLES / 4);
            if (angle < 0)
                angle += ANGLES;
            else if (angle >= ANGLES)
                angle -= ANGLES;
            Move(angle, (int)(Math.Abs(playerinput.ControlStrafe) * GameConstants.MOVESCALE * side));
        }

        //
        // side to side move
        //
        if (playerinput.IsPressed(buttontypes.bt_strafe))
        {
            //
            // strafing
            //
            //
            if (playerinput.ControlX > 0)
            {
                angle = ob.Angle - ANGLES / 4;
                if (angle < 0)
                    angle += ANGLES;
                Move(angle, (int)(playerinput.ControlX * GameConstants.MOVESCALE * side));      // move to left
            }
            else if (playerinput.ControlX < 0)
            {
                angle = ob.Angle + ANGLES / 4;
                if (angle >= ANGLES)
                    angle -= ANGLES;
                Move(angle, (int)(-playerinput.ControlX * GameConstants.MOVESCALE * side));     // move to right
            }
        }
        else
        {
            //
            // not strafing
            //
            // a zoomed scope turns slower (Gameplay/WeaponZoom.cs)
            int angleScale = GameConstants.ANGLESCALE * Math.Max(playerstate.TurnSlowdown, 1);
            playerstate.AngleFrac += (short)playerinput.ControlX;
            angleunits = playerstate.AngleFrac / angleScale;
            playerstate.AngleFrac -= (short)(angleunits * angleScale);
            ob.Angle -= (short)angleunits;

            if (ob.Angle >= ANGLES)
                ob.Angle -= ANGLES;
            if (ob.Angle < 0)
                ob.Angle += ANGLES;
        }

        //
        // forward/backwards move
        //
        if (playerinput.ControlY < 0)
        {
            Move(ob.Angle, (int)(-playerinput.ControlY * GameConstants.MOVESCALE * forward)); // move forwards
        }
        else if (playerinput.ControlY > 0)
        {
            angle = ob.Angle + ANGLES / 2;
            if (angle >= ANGLES)
                angle -= ANGLES;
            Move(angle, (int)(playerinput.ControlY * GameConstants.BACKMOVESCALE * forward));          // move backwards
        }
    }

    internal void Thrust(int angle, int speed)
    {
        var player = session.player;
        int xmove, ymove;
        //
        // ZERO FUNNY COUNTER IF MOVED!
        //
        session.playerstate.ThrustSpeed += speed;
        //
        // moving bounds speed
        //
        if (speed >= GameConstants.MINDIST * 2)
            speed = (int)(GameConstants.MINDIST * 2 - 1);

        // demos move the player exactly as v1.4 did, or they drift from what was recorded
        if ((session.demorecord || session.demoplayback))
        {
            xmove = MathUtils.FixedByFracOrig(speed, TrigTables.costable[angle]);
            ymove = -MathUtils.FixedByFracOrig(speed, TrigTables.sintable[angle]);
        }
        else
        {
            xmove = MathUtils.FixedMul(speed, TrigTables.costable[angle]);
            ymove = -MathUtils.FixedMul(speed, TrigTables.sintable[angle]);
        }

        ClipMove(player, xmove, ymove);

        var (oldtilex, oldtiley) = (player.TileX, player.TileY);
        player.TileX = (byte)(player.X >> (int)MapConstants.TILESHIFT);                // scale to tile values
        player.TileY = (byte)(player.Y >> (int)MapConstants.TILESHIFT);

        // A diagonal wall tile's open half has no area number (plane 0 holds the wall), so keep
        // the one the player walked in from.
        var areatile = map.MAPSPOT(player.TileX, player.TileY, 0);
        if (map.VALIDAREA(areatile))
            player.AreaNumber = (byte)(areatile - map.Floors.AreaTile);

        //
        // mapdefs walk-over trigger (the end-of-castle exit) on the tile just stepped onto
        //
        if ((player.TileX != oldtilex || player.TileY != oldtiley)
            && map.GetTrigger(player.TileX, player.TileY) is { IsWalkOver: true } trigger)
            hooks.ActivateTrigger(trigger, player.TileX, player.TileY, FacingDir(player.Angle));

        // mapdefs floors trigger: a floor code that does something when stepped onto (Blake Stone's way out)
        if ((player.TileX != oldtilex || player.TileY != oldtiley)
            && map.Floors.Triggers.TryGetValue(areatile, out var floorAction))
        {
            Entities.MapTriggerRegistry.Invoke(floorAction, new Entities.TriggerActivation(player.TileX, player.TileY,
                FacingDir(player.Angle), player, map.GetTag(player.TileX, player.TileY)));
        }
    }

    internal bool TryMove(Entities.Actors.Actor ob)
    {
        const long PLAYERSIZE = GameConstants.PLAYERSIZE;
        uint xl, yl, xh, yh, x, y;
        Actor? check;

        xl = (uint)((ob.X - PLAYERSIZE) >> (int)MapConstants.TILESHIFT);
        yl = (uint)((ob.Y - PLAYERSIZE) >> (int)MapConstants.TILESHIFT);

        xh = (uint)((ob.X + PLAYERSIZE) >> (int)MapConstants.TILESHIFT);
        yh = (uint)((ob.Y + PLAYERSIZE) >> (int)MapConstants.TILESHIFT);

        const long PUSHWALLMINDIST = PLAYERSIZE;
        bool nearwallsprite = false;

        //
        // check for solid walls
        //
        for (y = yl; y <= yh; y++)
        {
            for (x = xl; x <= xh; x++)
            {
                check = map.actorat[x, y];
                if (check != null && check is not ActorMark)       // enemies are checked below
                {
                    if (check is WallSpriteBlocker)
                        nearwallsprite = true;          // only its panel blocks: checked below
                    else if (map.tilemap[x, y] == GameConstants.BIT_WALL
                        && pushWalls.pushwalls.Find(p => p.X == x && p.Y == y) is { } pw)    // back of a moving pushwall?
                    {
                        switch (pw.Dir)
                        {
                            case controldirs.di_north:
                                if (ob.Y - PUSHWALLMINDIST <= (pw.Y << (int)MapConstants.TILESHIFT) + ((63 - pw.Pos) << 10))
                                    return false;
                                break;
                            case controldirs.di_west:
                                if (ob.X - PUSHWALLMINDIST <= (pw.X << (int)MapConstants.TILESHIFT) + ((63 - pw.Pos) << 10))
                                    return false;
                                break;
                            case controldirs.di_east:
                                if (ob.X + PUSHWALLMINDIST >= (pw.X << (int)MapConstants.TILESHIFT) + (pw.Pos << 10))
                                    return false;
                                break;
                            case controldirs.di_south:
                                if (ob.Y + PUSHWALLMINDIST >= (pw.Y << (int)MapConstants.TILESHIFT) + (pw.Pos << 10))
                                    return false;
                                break;
                        }
                    }
                    else if (map.wallshape[x, y] is var shape and not WallShape.Square)
                    {
                        //
                        // a diagonal only blocks on its solid side of the face
                        //
                        if (Diagonals.BoxHitsDiagonal(shape, (int)x, (int)y, ob.X, ob.Y, PLAYERSIZE))
                        {
                            diagonalblock = shape;
                            return false;
                        }
                    }
                    else return false;
                }
            }
        }

        //
        // a wall sprite only blocks along its panel (GameHost.WallSprites.cs)
        //
        if (nearwallsprite
            && hooks.WallSpriteHit((int)xl, (int)yl, (int)xh, (int)yh, ob.X, ob.Y, PLAYERSIZE) is { } panel)
        {
            wallspriteblock = panel;
            return false;
        }

        //
        // check for actors: a living actor marked on a tile near the player
        // (MapManager.MarkActorTile), and within MINACTORDIST of it, blocks the move
        //
        if (yl > 0)
            yl--;
        if (yh < MapManager.MAPSIZE - 1)
            yh++;
        if (xl > 0)
            xl--;
        if (xh < MapManager.MAPSIZE - 1)
            xh++;

        for (y = yl; y <= yh; y++)
        {
            for (x = xl; x <= xh; x++)
            {
                if (map.ActorMarkAt((int)x, (int)y) is not { } actor || !MapManager.IsSolidActor(actor))
                    continue;

                var deltax = ob.X - actor.X;
                if (deltax < -GameConstants.MINACTORDIST || deltax > GameConstants.MINACTORDIST)
                    continue;
                var deltay = ob.Y - actor.Y;
                if (deltay < -GameConstants.MINACTORDIST || deltay > GameConstants.MINACTORDIST)
                    continue;

                // An ally following the player gets out of their way
                if (ob is Entities.Actors.PlayerPawn pawn)
                    actor.ShovedBy(pawn);
                return false;
            }
        }

        return true;
    }

    internal void ClipMove(Entities.Actors.Actor ob, int xmove, int ymove)
    {
        int basex, basey;

        basex = ob.X;
        basey = ob.Y;

        ob.X = basex + xmove;
        ob.Y = basey + ymove;
        diagonalblock = WallShape.Square;
        wallspriteblock = null;
        hooks.WallSpriteMoveFrom(basex, basey);
        if (TryMove(ob))
            return;

        if ((session.noclip != 0) && ob.X > 2 * MapConstants.TILEGLOBAL && ob.Y > 2 * MapConstants.TILEGLOBAL
            && ob.X < (((int)(map.mapwidth - 1)) << (int)MapConstants.TILESHIFT)
            && ob.Y < (((int)(map.mapheight - 1)) << (int)MapConstants.TILESHIFT))
            return;         // walk through walls

        if (!audio.IsAnySoundPlaying())
             hooks.PlayPlayerSound("world/hitwall");

        //
        // ran into a diagonal face: slide along it (the move's part in the face's direction),
        // since neither axis on its own gets past a 45 degree wall
        //
        if (diagonalblock != WallShape.Square)
        {
            bool nwToSe = diagonalblock is WallShape.SolidNE or WallShape.SolidSW;
            int along = nwToSe ? (xmove + ymove) / 2 : (xmove - ymove) / 2;
            ob.X = basex + along;
            ob.Y = basey + (nwToSe ? along : -along);
            if (TryMove(ob))
                return;
        }

        //
        // ran into a wall sprite's panel: slide along it, which for a diagonal one neither
        // axis on its own would
        //
        if (wallspriteblock is { } panel)
        {
            double ux = (panel.X2 - panel.X1) / panel.Length, uy = (panel.Y2 - panel.Y1) / panel.Length;
            double along = xmove * ux + ymove * uy;
            ob.X = basex + (int)(along * ux);
            ob.Y = basey + (int)(along * uy);
            if (TryMove(ob))
                return;
        }

        ob.X = basex + xmove;
        ob.Y = basey;
        if (TryMove(ob))
            return;

        ob.X = basex;
        ob.Y = basey + ymove;
        if (TryMove(ob))
            return;

        ob.X = basex;
        ob.Y = basey;
    }
}
