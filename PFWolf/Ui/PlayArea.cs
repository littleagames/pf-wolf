namespace PFWolf.Ui;

/// <summary>
/// Where the play area is on the screen: between the status bar across the top (if any;
/// statusbar.yaml top, Blake Stone's) and the one along the bottom (statusbar.yaml background)
/// </summary>
internal sealed class PlayArea(VideoManager video, StatusBarDrawing statusBar)
{
    // The status bar's height in 320x200 lines (statusbar.yaml background), 0 with none
    internal int StatusLines => statusBar.StatusBar.Get("background")?.Height ?? 0;
    // Lines a status bar across the top of the screen takes (statusbar.yaml top; Blake Stone's)
    internal int TopLines => statusBar.StatusBar.Get("top")?.Height ?? 0;

    /// <summary>The screen lines between the top status bar (if any) and the bottom one, in screen pixels</summary>
    internal int PlayAreaHeight => video.ScreenYAboveBottom(StatusLines) - PlayAreaTop;

    /// <summary>The play area's height and the bottom status bar's first line, in screen pixels</summary>
    internal int PlayAreaAndStatusLine => video.ScreenYAboveBottom(StatusLines - 1) - PlayAreaTop;

    /// <summary>Where the play area (view and border) starts down the screen, in screen pixels: below any top status bar</summary>
    internal int PlayAreaTop => video.ToScreenLength(TopLines);
}
