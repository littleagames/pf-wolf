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
= The player's own look up/down and eye height are playerpitch and PlayerEyeZ; the
= renderer's ViewPitch and ViewEyeZ are this frame's, taken from the camera.
=
= Chasing: where the match mode allows it (a Prop Hunt prop, to see what it looks like) and
= the chasecam setting is on, the view of the player is from ChaseDistance behind them and
= ChaseRise higher, pulled in short of any wall or shut door behind, with their body in view
= and no weapon in hand.
=
====================
*/

internal sealed class Camera(GameSession session)
{
    const double TurnSpeed = 8;     // degrees a tic the view turns toward the way a watched actor goes

    Entities.Actors.Actor? target;  // null: the player, whichever pawn that is now
    bool isfixed;
    int fixedx, fixedy;
    short fixedangle;
    double fixedpitch;
    int fixedeyez = GameConstants.EyeDefault;
    double smoothangle;             // a watched actor's view angle, turning after its 8-way walk

    /// <summary>
    /// The actor looked through, or null when the camera sits at a fixed point (or there's
    /// no player yet). One removed from the map gives the view back to the player; a new
    /// level always does (SetupGameLevel).
    /// </summary>
    internal Entities.Actors.Actor? Target =>
        isfixed ? null : target is { IsRemoved: false } ? target : session.LocalPlayer.Pawn;

    /// <summary>Looks through the player: the default, set each level</summary>
    internal void FollowPlayer() => (target, isfixed) = (null, false);

    /// <summary>Looks through an actor, from its position and the way it faces</summary>
    internal void Follow(Entities.Actors.Actor actor)
    {
        (target, isfixed) = (actor, false);
        smoothangle = ActorViewAngle(actor);
    }

    /// <summary>Sits at a fixed point (global fixed-point x, y), facing angle</summary>
    internal void SetFixed(int x, int y, short angle, double pitch = 0, int eyez = GameConstants.EyeDefault)
    {
        (target, isfixed) = (null, true);
        (fixedx, fixedy, fixedangle, fixedpitch, fixedeyez) = (x, y, angle, pitch, eyez);
    }

    /// <summary>Whether the view is the player's own, with their weapon and look</summary>
    internal bool OnPlayer => Target is { } t && t == session.LocalPlayer.Pawn;

    /// <summary>Whether the view of the player should be from behind them now (the mode allows it and chasecam is on); unset, never</summary>
    internal Func<bool>? WantsChase { get; set; }

    /// <summary>Whether a tile is open for the chasing view to stand in (no wall or shut door); unset, none is</summary>
    internal Func<int, int, bool>? IsOpenTile { get; set; }

    /// <summary>How far behind and how much higher than the player's eyes the chasing view is (global units, texels)</summary>
    internal const int ChaseDistance = (int)MapConstants.TileGlobal * 3 / 2, ChaseRise = 16;

    /// <summary>Whether the view is of the player from behind (Chasing): their body shows, their weapon doesn't</summary>
    internal bool Chasing => OnPlayer && WantsChase?.Invoke() == true;

    // Where the chasing view stands: back along the player's facing from them, in steps, as far as
    // ChaseDistance or until a step would come within a quarter tile of a closed tile
    (int X, int Y) ChaseSpot(Entities.Actors.Actor pawn)
    {
        double radians = pawn.Angle * Math.PI / 180;
        double backx = -Math.Cos(radians), backy = Math.Sin(radians);     // y grows southward
        const int Step = (int)MapConstants.TileGlobal / 16, Clearance = (int)MapConstants.TileGlobal / 4;
        int x = pawn.X, y = pawn.Y;
        for (int along = Step; along <= ChaseDistance; along += Step)
        {
            int nx = pawn.X + (int)(backx * along), ny = pawn.Y + (int)(backy * along);
            bool clear = true;
            foreach (var (cx, cy) in new[] { (nx + Clearance, ny), (nx - Clearance, ny), (nx, ny + Clearance), (nx, ny - Clearance) })
                clear &= IsOpenTile?.Invoke(cx >> (int)MapConstants.TileShift, cy >> (int)MapConstants.TileShift) == true;
            if (!clear)
                break;
            (x, y) = (nx, ny);
        }
        return (x, y);
    }

    /// <summary>Whether the view is through an actor other than the player</summary>
    internal bool Spectating => Target is { } t && t != session.LocalPlayer.Pawn;

    /// <summary>Turns a watched actor's view toward the way it now goes, so it doesn't snap 45 degrees at a time</summary>
    internal void Tick(uint tics)
    {
        if (!Spectating)
            return;

        double delta = ActorViewAngle(Target!) - smoothangle;
        delta = ((delta % GameConstants.Angles) + GameConstants.Angles * 1.5) % GameConstants.Angles - GameConstants.Angles / 2.0;    // the shorter way round
        double step = TurnSpeed * tics;
        smoothangle += Math.Clamp(delta, -step, step);
        smoothangle = ((smoothangle % GameConstants.Angles) + GameConstants.Angles) % GameConstants.Angles;
    }

    internal int X => Chasing ? ChaseSpot(Target!).X : Target?.X ?? fixedx;
    internal int Y => Chasing ? ChaseSpot(Target!).Y : Target?.Y ?? fixedy;
    internal int TileX => X >> (int)MapConstants.TileShift;
    internal int TileY => Y >> (int)MapConstants.TileShift;

    internal short Angle => Target switch
    {
        null => fixedangle,
        PlayerPawn => Target.Angle,
        _ => (short)((int)Math.Round(smoothangle) % GameConstants.Angles),
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
        PlayerPawn pawn => Chasing ? Math.Min(pawn.State.EyeZ + ChaseRise, GameConstants.MaxEye) : pawn.State.EyeZ,
        _ => GameConstants.EyeDefault,
    };

    /// <summary>
    /// The way an actor other than the player looks: a projectile along its heading, anything
    /// else the way it walks, or its Angle when it stands still.
    /// </summary>
    static short ActorViewAngle(Entities.Actors.Actor actor) =>
        actor.Dir == ObjDir.NoDir || actor.Flags.Contains("PROJECTILE", StringComparer.OrdinalIgnoreCase)
            ? actor.Angle
            : (short)GameConstants.DirAngle[(byte)actor.Dir];
}
