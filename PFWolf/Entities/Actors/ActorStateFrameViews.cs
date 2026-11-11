namespace PFWolf.Entities.Actors;

/// <summary>The game's way of asking a frame (Common's <see cref="ActorStateFrame"/>) for a picture: by direction</summary>
internal static class ActorStateFrameViews
{
    /// <summary>The frame's picture for a direction: NoDir its single view (0), otherwise the direction's number as the view</summary>
    public static string GetShapeName(this ActorStateFrame frame, ObjDir dir) =>
        frame.ShapeName(dir == ObjDir.NoDir ? 0 : (int)dir);
}
