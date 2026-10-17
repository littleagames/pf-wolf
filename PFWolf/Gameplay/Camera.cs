using PFWolf.Entities.Actors;

namespace PFWolf.Gameplay;

/*
====================
=
= Camera
=
= Where the view is drawn from. It follows an actor (the player, normally) or sits at a
= fixed point (the death cam). The renderer reads only the camera, never the player, so it
= can look through another actor (spectate) while the player plays on. What the player
= touches and sees doesn't move with it (Rendering/PlayerView.cs).
=
= The player's own look up/down and eye height are playerpitch and playereyez; the
= renderer's viewpitch and vieweyez are this frame's, taken from the camera.
=
====================
*/

internal sealed class Camera(GameSession session)
{
    const double TURNSPEED = 8;     // degrees a tic the view turns toward the way a watched actor goes

    Entities.Actors.Actor? target;  // null: the player, whichever pawn that is now
    bool isfixed;
    int fixedx, fixedy;
    short fixedangle;
    double fixedpitch;
    int fixedeyez = GameConstants.EYEDEFAULT;
    double smoothangle;             // a watched actor's view angle, turning after its 8-way walk

    /// <summary>
    /// The actor looked through, or null when the camera sits at a fixed point (or there's
    /// no player yet). One removed from the map gives the view back to the player; a new
    /// level always does (SetupGameLevel).
    /// </summary>
    internal Entities.Actors.Actor? Target =>
        isfixed ? null : target is { IsRemoved: false } ? target : session.localplayer.Pawn;

    /// <summary>Looks through the player: the default, set each level</summary>
    internal void FollowPlayer() => (target, isfixed) = (null, false);

    /// <summary>Looks through an actor, from its position and the way it faces</summary>
    internal void Follow(Entities.Actors.Actor actor)
    {
        (target, isfixed) = (actor, false);
        smoothangle = ActorViewAngle(actor);
    }

    /// <summary>Sits at a fixed point (global fixed-point x, y), facing angle</summary>
    internal void SetFixed(int x, int y, short angle, double pitch = 0, int eyez = GameConstants.EYEDEFAULT)
    {
        (target, isfixed) = (null, true);
        (fixedx, fixedy, fixedangle, fixedpitch, fixedeyez) = (x, y, angle, pitch, eyez);
    }

    /// <summary>Whether the view is the player's own, with their weapon and look</summary>
    internal bool OnPlayer => Target is { } t && t == session.localplayer.Pawn;

    /// <summary>Whether the view is through an actor other than the player</summary>
    internal bool Spectating => Target is { } t && t != session.localplayer.Pawn;

    /// <summary>Turns a watched actor's view toward the way it now goes, so it doesn't snap 45 degrees at a time</summary>
    internal void Tick(uint tics)
    {
        if (!Spectating)
            return;

        double delta = ActorViewAngle(Target!) - smoothangle;
        delta = ((delta % GameConstants.ANGLES) + GameConstants.ANGLES * 1.5) % GameConstants.ANGLES - GameConstants.ANGLES / 2.0;    // the shorter way round
        double step = TURNSPEED * tics;
        smoothangle += Math.Clamp(delta, -step, step);
        smoothangle = ((smoothangle % GameConstants.ANGLES) + GameConstants.ANGLES) % GameConstants.ANGLES;
    }

    internal int X => Target?.X ?? fixedx;
    internal int Y => Target?.Y ?? fixedy;
    internal int TileX => X >> (int)MapConstants.TILESHIFT;
    internal int TileY => Y >> (int)MapConstants.TILESHIFT;

    internal short Angle => Target switch
    {
        null => fixedangle,
        PlayerPawn => Target.Angle,
        _ => (short)((int)Math.Round(smoothangle) % GameConstants.ANGLES),
    };

    internal double Pitch => Target switch
    {
        null => fixedpitch,
        PlayerPawn pawn => pawn.State.Pitch,
        _ => 0,
    };

    internal int EyeZ => Target switch
    {
        null => fixedeyez,
        PlayerPawn pawn => pawn.State.EyeZ,
        _ => GameConstants.EYEDEFAULT,
    };

    /// <summary>
    /// The way an actor other than the player looks: a projectile along its heading, anything
    /// else the way it walks, or its Angle when it stands still.
    /// </summary>
    static short ActorViewAngle(Entities.Actors.Actor actor) =>
        actor.Dir == objdirtypes.nodir || actor.Flags.Contains("PROJECTILE", StringComparer.OrdinalIgnoreCase)
            ? actor.Angle
            : (short)GameConstants.dirangle[(byte)actor.Dir];
}
