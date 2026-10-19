using PFWolf.Constants;

namespace PFWolf.Rendering;

/// <summary>Where the view is drawn from: the camera's place, angle (degrees, maybe between whole ones), look and eye height</summary>
internal readonly record struct ViewPose(int X, int Y, double Angle, double Pitch, int EyeZ);

/*
====================
=
= FrameInterpolation
=
= With an uncapped frame rate (VideoSettings.Uncapped), frames are drawn between tics, so the
= view and everything moving are drawn between where they were on the last two: before each
= batch of tics thinks, BeginTics keeps where the camera and every actor are, and a frame drawn
= Fraction of the way through the next batch places them that far from there to where they are
= now. The view is up to a tic behind, as in other ports that do this.
=
= Only the drawing reads these; the game goes on from the real positions. Anything that went
= more than a tile in one batch (a teleport, a respawn) is drawn where it is, as is anything
= that appeared during the batch.
=
====================
*/

internal sealed class FrameInterpolation(MapManager map, GameSession session)
{
    /// <summary>Whether this frame is drawn between tics; off, everything is drawn where it is</summary>
    internal bool Active;

    /// <summary>How far the frame is from where things were before the last batch of tics (0) to where they are (1)</summary>
    internal double Fraction = 1;

    // Counts batches: an actor last kept in an earlier one appeared since, so it's drawn where it is
    int batch;

    ViewPose camera;
    Entities.Actors.Actor? cameraTarget;
    bool cameraKept;

    const int JumpDistance = (int)MapConstants.TileGlobal;

    /// <summary>Keeps where the camera and every actor are, before a batch of tics thinks</summary>
    internal void BeginTics()
    {
        batch++;
        var view = session.Camera;
        camera = new ViewPose(view.X, view.Y, view.Angle, view.Pitch, view.EyeZ);
        cameraTarget = view.Target;
        cameraKept = true;

        foreach (var actor in map.GetActors())
        {
            actor.PrevX = actor.X;
            actor.PrevY = actor.Y;
            actor.PrevBatch = batch;
        }
    }

    /// <summary>A new level, life or loaded game: nothing is slid from where it was before</summary>
    internal void Forget()
    {
        batch++;
        cameraKept = false;
    }

    /// <summary>Where to draw an actor this frame (global units)</summary>
    internal (int X, int Y) PositionOf(Entities.Actors.Actor actor)
    {
        if (!Active || actor.PrevBatch != batch
            || Math.Abs(actor.X - actor.PrevX) > JumpDistance || Math.Abs(actor.Y - actor.PrevY) > JumpDistance)
            return (actor.X, actor.Y);
        return (Lerp(actor.PrevX, actor.X), Lerp(actor.PrevY, actor.Y));
    }

    /// <summary>Where to draw the view from this frame</summary>
    internal ViewPose CameraPose()
    {
        var view = session.Camera;
        var now = new ViewPose(view.X, view.Y, view.Angle, view.Pitch, view.EyeZ);
        if (!Active || !cameraKept || !ReferenceEquals(view.Target, cameraTarget)
            || Math.Abs(now.X - camera.X) > JumpDistance || Math.Abs(now.Y - camera.Y) > JumpDistance)
            return now;

        // the shorter way round, so turning past east doesn't spin the other way
        double turn = ((now.Angle - camera.Angle) % GameConstants.Angles + GameConstants.Angles * 1.5) % GameConstants.Angles - GameConstants.Angles / 2.0;
        if (Math.Abs(turn) > 90)
            return now;             // a snap round (the death cam, a respawn): not a turn

        double angle = camera.Angle + turn * Fraction;
        angle = (angle % GameConstants.Angles + GameConstants.Angles) % GameConstants.Angles;
        return new ViewPose(Lerp(camera.X, now.X), Lerp(camera.Y, now.Y), angle,
            camera.Pitch + (now.Pitch - camera.Pitch) * Fraction, Lerp(camera.EyeZ, now.EyeZ));
    }

    int Lerp(int from, int to) => from + (int)Math.Round((to - from) * Fraction);
}
