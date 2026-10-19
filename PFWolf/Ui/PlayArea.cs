namespace PFWolf.Ui;

/// <summary>
/// Where the play area is on the screen: between the status bar across the top (if any;
/// statusbar.yaml top, Blake Stone's) and the one along the bottom (statusbar.yaml background)
/// </summary>
internal sealed class PlayArea(VideoManager video, StatusBarDrawing statusBar)
{
    // The lines the status bar takes from the play area, in 320x200 lines (statusbar.yaml
    // background's height): 0 with none, or with one drawn over the view (overlay). The bar's own
    // layout, not the full-screen one (statusbar-fullscreen.yaml), which is always drawn over the view
    internal int StatusLines => statusBar.BarLayout.Get("background") is { Overlay: false } background ? background.Height ?? 0 : 0;

    /// <summary>Whether the status bar is drawn over the view (statusbar.yaml background overlay)</summary>
    internal bool OverlayBar => statusBar.BarLayout.Get("background")?.Overlay == true;
    // Lines a status bar across the top of the screen takes (statusbar.yaml top; Blake Stone's)
    internal int TopLines => statusBar.BarLayout.Get("top")?.Height ?? 0;

    /// <summary>The screen lines between the top status bar (if any) and the bottom one, in screen pixels</summary>
    internal int PlayAreaHeight => video.ScreenYAboveBottom(StatusLines) - PlayAreaTop;

    /// <summary>The play area's height and the bottom status bar's first line, in screen pixels</summary>
    internal int PlayAreaAndStatusLine => video.ScreenYAboveBottom(StatusLines - 1) - PlayAreaTop;

    /// <summary>Where the play area (view and border) starts down the screen, in screen pixels: below any top status bar</summary>
    internal int PlayAreaTop => video.ToScreenLength(TopLines);
}
