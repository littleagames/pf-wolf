using PFWolf.Configuration;
using PFWolf.Constants;

namespace PFWolf.Rendering;

/// <summary>
/// The view's size and place on the screen for a view size setting (NewViewSize, SetViewSize),
/// the play area round it, the projection that follows from it (CalcProjection: scale, height
/// numerator, each column's ray angle), and the Change View outline (ShowViewSize).
/// </summary>
internal sealed class ViewSetup(
    VideoManager video,
    PlayScreen playScreen,
    RenderState render,
    PlayArea screenArea,
    Configuration.GameSettings settings)
{
    // Radians to fine angles (FineAngles a turn)
    const float RadToInt = GameConstants.FineAngles / 2 / GameConstants.Pi;

    /// <summary>
    /// The view sizes there are: with a status bar drawn over the view (overlay), none smaller
    /// than the play area (20), as Midnight Assault's view always filled the screen
    /// </summary>
    internal int AllowedViewSize(int width) => screenArea.OverlayBar ? Math.Max(width, 20) : width;

    internal void NewViewSize(int width)
    {
        width = AllowedViewSize(width);
        settings.viewsize = width;
        if (settings.viewsize == 21)
            SetViewSize((uint)video.screenWidth, (uint)video.screenHeight);
        else if (settings.viewsize == 20)
            SetViewSize((uint)video.screenWidth, (uint)screenArea.PlayAreaHeight);
        else
            SetViewSize((uint)(width * 16 * video.screenWidth / 320), (uint)BorderedViewHeight(width));
    }

    /// <summary>
    /// A bordered view's height at view size <paramref name="width"/>. The sizes are laid out for
    /// Wolf3D's 40 line status bar: with a taller one (Blake Stone's), the view stops short of it,
    /// leaving room for the border lines round it.
    /// </summary>
    internal int BorderedViewHeight(int width)
    {
        int height = (int)(width * 16 * GameConstants.HeightRatio * video.screenHeight / 200);
        int room = screenArea.PlayAreaHeight - video.ToScreenLength(2);
        return Math.Min(height, room);
    }

    internal bool SetViewSize(uint width, uint height)
    {
        render.ViewWidth = (int)(width & ~15);                  // must be divisable by 16
        render.ViewHeight = (int)(height & ~1);                 // must be even
        render.CenterX = (short)(render.ViewWidth / 2 - 1);
        render.CenterY = render.BaseCenterY = (short)(render.ViewHeight / 2);
        render.ProjectionWidth = ProjectionWidth(render.ViewWidth, render.ViewHeight);
        render.ShootDelta = render.ProjectionWidth / 10;               // the same angle either side, however wide the view
        if (render.ViewHeight == video.screenHeight)
            render.ViewScreenX = render.ViewScreenY = (int)(render.ScreenOfs = 0);
        else
        {
            render.ViewScreenX = (video.screenWidth - render.ViewWidth) / 2;
            render.ViewScreenY = screenArea.PlayAreaTop + (screenArea.PlayAreaHeight - render.ViewHeight) / 2;
            render.ScreenOfs = (uint)(render.ViewScreenY * video.screenWidth + render.ViewScreenX);
        }

        //
        // calculate trace angles and projection constants
        //
        CalcProjection(GameConstants.FocalLength + focalExcursion);

        return true;
    }

    // How far the focal length is pushed from the usual now (a quake's pulse: Rendering/ViewQuake.cs)
    long focalExcursion;

    /// <summary>Pushes the focal length that far from the usual, working the projection out again if that's a change</summary>
    internal void SetFocalExcursion(long excursion)
    {
        if (excursion == focalExcursion)
            return;
        focalExcursion = excursion;
        CalcProjection(GameConstants.FocalLength + excursion);
    }

    /*
    ====================
    =
    = CalcProjection
    =
    = Uses FocalLength
    =
    ====================
    */

    internal void CalcProjection(long focal)
    {
        int i;
        int intang;
        float angle;
        double tang;
        int halfview;
        double facedist;

        // The view stays where it always is (the usual focal length behind the camera); a longer
        // focal length (a scope's zoom, a quake's pulse) only narrows what it sees, magnifying it.
        // Midnight Assault moved the view back with it, which put a zoomed view behind walls.
        render.FocalLength = (int)GameConstants.FocalLength;
        facedist = focal + GameConstants.MinDist;
        halfview = render.ViewWidth / 2;                                 // half view in pixels

        //
        // calculate scale value for vertical height calculations
        // and sprite x calculations: from the projection width, so a view wider than the
        // classic shape sees further to the sides rather than having its walls made taller
        //
        render.Scale = (int) (render.ProjectionWidth / 2 * facedist / (GameConstants.ViewGlobal / 2));

        //
        // divide HeightNumerator by a posts distance to get the posts height for
        // the heightbuffer.  The pixel height is height>>2
        //
        render.HeightNumerator = (int)((MapConstants.TileGlobal * render.Scale) >> 6);

        //
        // calculate the angle offset from view angle of each pixel's ray
        //

        for (i = 0; i < halfview; i++)
        {
            // start 1/2 pixel over, so ViewAngle bisects two middle pixels
            tang = (int)i * GameConstants.ViewGlobal / render.ProjectionWidth / facedist;
            angle = (float)Math.Atan(tang);
            intang = (int)(angle * RadToInt);
            render.PixelAngle[halfview - 1 - i] = (short)intang;
            render.PixelAngle[halfview + i] = (short)-intang;
        }
    }

    /// <summary>
    /// The width a view's field of view is worked out for. Up to the shape the view size has on
    /// a 320x200 screen, that's the view's own width, as it always was: a narrower screen (4:3)
    /// shows the same across and more above and below. A wider view is worked out from its
    /// height instead, so it shows the same above and below and more to the sides (Hor+).
    /// </summary>
    internal int ProjectionWidth(int width, int height)
    {
        // SetViewSize rounds the height down to even, so allow for the line that may lose:
        // otherwise a classic layout could come out a hair too wide and change
        int classicWidth = (int)Math.Ceiling((height + 1) * ClassicViewAspect(settings.viewsize) - 1e-9);
        return Math.Max(1, Math.Min(width, classicWidth));
    }

    /// <summary>A view size's width over its height on a 320x200 screen, as NewViewSize lays it out there.</summary>
    internal double ClassicViewAspect(int size)
    {
        int playArea = VideoSettings.BaseHeight - screenArea.StatusLines - screenArea.TopLines;
        if (size >= 21)
            return (double)VideoSettings.BaseWidth / VideoSettings.BaseHeight;
        if (size == 20)
            return (double)VideoSettings.BaseWidth / Math.Max(playArea, 1);

        double height = Math.Min(size * 16 * GameConstants.HeightRatio, playArea - 2);
        return size * 16 / Math.Max(height, 1);
    }

    internal void ShowViewSize(int width)
    {
        int oldwidth, oldheight;

        oldwidth = render.ViewWidth;
        oldheight = render.ViewHeight;
        width = AllowedViewSize(width);

        if (width == 21)
        {
            render.ViewWidth = video.screenWidth;
            render.ViewHeight = video.screenHeight;
            video.BarScaledCoord(0, 0, video.screenWidth, video.screenHeight, "Black");
        }
        else if (width == 20)
        {
            render.ViewWidth = video.screenWidth;
            render.ViewHeight = screenArea.PlayAreaHeight;
            playScreen.DrawPlayBorder();
        }
        else
        {
            render.ViewWidth = width * 16 * video.screenWidth / 320;
            render.ViewHeight = BorderedViewHeight(width);
            playScreen.DrawPlayBorder();
        }

        render.ViewWidth = oldwidth;
        render.ViewHeight = oldheight;
    }
}
