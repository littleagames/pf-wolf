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
        render.viewwidth = (int)(width & ~15);                  // must be divisable by 16
        render.viewheight = (int)(height & ~1);                 // must be even
        render.centerx = (short)(render.viewwidth / 2 - 1);
        render.centery = render.basecentery = (short)(render.viewheight / 2);
        render.projectionwidth = ProjectionWidth(render.viewwidth, render.viewheight);
        render.shootdelta = render.projectionwidth / 10;               // the same angle either side, however wide the view
        if (render.viewheight == video.screenHeight)
            render.viewscreenx = render.viewscreeny = (int)(render.screenofs = 0);
        else
        {
            render.viewscreenx = (video.screenWidth - render.viewwidth) / 2;
            render.viewscreeny = screenArea.PlayAreaTop + (screenArea.PlayAreaHeight - render.viewheight) / 2;
            render.screenofs = (uint)(render.viewscreeny * video.screenWidth + render.viewscreenx);
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
    = Uses focallength
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
        render.focallength = (int)GameConstants.FocalLength;
        facedist = focal + GameConstants.MinDist;
        halfview = render.viewwidth / 2;                                 // half view in pixels

        //
        // calculate scale value for vertical height calculations
        // and sprite x calculations: from the projection width, so a view wider than the
        // classic shape sees further to the sides rather than having its walls made taller
        //
        render.scale = (int) (render.projectionwidth / 2 * facedist / (GameConstants.ViewGlobal / 2));

        //
        // divide heightnumerator by a posts distance to get the posts height for
        // the heightbuffer.  The pixel height is height>>2
        //
        render.heightnumerator = (int)((MapConstants.TileGlobal * render.scale) >> 6);

        //
        // calculate the angle offset from view angle of each pixel's ray
        //

        for (i = 0; i < halfview; i++)
        {
            // start 1/2 pixel over, so viewangle bisects two middle pixels
            tang = (int)i * GameConstants.ViewGlobal / render.projectionwidth / facedist;
            angle = (float)Math.Atan(tang);
            intang = (int)(angle * RadToInt);
            render.pixelangle[halfview - 1 - i] = (short)intang;
            render.pixelangle[halfview + i] = (short)-intang;
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

        oldwidth = render.viewwidth;
        oldheight = render.viewheight;
        width = AllowedViewSize(width);

        if (width == 21)
        {
            render.viewwidth = video.screenWidth;
            render.viewheight = video.screenHeight;
            video.BarScaledCoord(0, 0, video.screenWidth, video.screenHeight, "Black");
        }
        else if (width == 20)
        {
            render.viewwidth = video.screenWidth;
            render.viewheight = screenArea.PlayAreaHeight;
            playScreen.DrawPlayBorder();
        }
        else
        {
            render.viewwidth = width * 16 * video.screenWidth / 320;
            render.viewheight = BorderedViewHeight(width);
            playScreen.DrawPlayBorder();
        }

        render.viewwidth = oldwidth;
        render.viewheight = oldheight;
    }
}
