using PFWolf.Configuration;
using PFWolf.Extensions;

namespace PFWolf.Ui;

/// <summary>
/// The Video menu (menudefs/video): FullScreen, VSync and aspect checkboxes, and the window
/// size, resolution, UI scale and filter stepped through with left/right (or Enter). A change
/// that could leave the screen unreadable asks to be kept, and goes back if it isn't.
/// </summary>
internal sealed class VideoMenu(AssetManager assets, InputManager input, AudioManager audio, VideoManager video,
    MenuDrawing draw, MenuLoop loop, MenuInput menuInput, MenuDefs defs)
{
    // Loaded from menudefs/ in CheckForEpisodes
    internal ControlPanelItem[] Items = [];
    internal ControlPanelMenuInfo Info = null!;
    internal int Run()
    {
        int which;

        DrawVideoMenu();
        draw.MenuFadeIn();
        menuInput.WaitKeyUp();

        do
        {
            which = loop.HandleMenu(Info, Items, null, (w, delta) => StepVideoChoice(w, delta, wrap: false));

            var settings = video.Settings;
            switch (MenuDefs.SelectedId(Items, which))
            {
                case "fullscreen":
                    loop.ShootSnd();
                    ChangeVideo(settings with { Fullscreen = !settings.Fullscreen });
                    break;

                case "vsync":
                    loop.ShootSnd();
                    ChangeVideo(settings with { VSync = !settings.VSync });
                    break;

                case "aspect":
                    loop.ShootSnd();
                    ChangeVideo(WithAspect(settings, !settings.AspectCorrect));
                    break;

                case "window-size":
                case "render-scale":
                case "ui-scale":
                case "filter":
                    StepVideoChoice(which, 1, wrap: true);
                    break;
            }
        }
        while (which >= 0);

        draw.MenuFadeOut();

        return 0;
    }

    internal void DrawVideoMenu(bool update = true)
    {
        var settings = video.Settings;
        var language = assets.GetText("en-us");

        defs.DrawMenuComponents("video");

        // The window's size means nothing while FullScreen
        MenuDefs.FindMenuItem(Items, "window-size")?.Active = (short)(settings.Fullscreen ? 0 : 1);
        if (Items[Info.CurPos].Active == 0)
            Info.CurPos = (short)Math.Max(0, Array.FindIndex(Items, item => item.Active != 0));

        draw.DrawMenu(Info, Items);

        draw.DrawMenuCheckbox(Info, Items, "fullscreen", settings.Fullscreen);
        draw.DrawMenuCheckbox(Info, Items, "vsync", settings.VSync);
        draw.DrawMenuCheckbox(Info, Items, "aspect", settings.AspectCorrect);
        draw.DrawMenuChoice(Info, Items, "window-size", $"{settings.WindowWidth}x{settings.WindowHeight}");
        draw.DrawMenuChoice(Info, Items, "render-scale", settings.MatchWindow
            ? "$STR_AUTO".ToLanguageText(language)
            : $"{settings.RenderWidth}x{settings.RenderHeight}");
        var uiScale = VideoSettings.FormatScale(settings.EffectiveUiScale);
        draw.DrawMenuChoice(Info, Items, "ui-scale", settings.UiScale <= 0
            ? $"{"$STR_AUTO".ToLanguageText(language)} ({uiScale}x)"
            : $"{uiScale}x");
        draw.DrawMenuChoice(Info, Items, "filter", FilterName(settings.Filter).ToLanguageText(language));

        draw.DrawMenuGun(Info);
        if (update)
            video.Update();
    }

    internal static string FilterName(ScaleFilter filter) => filter switch
    {
        ScaleFilter.Linear => "$STR_LINEAR",
        _ => "$STR_NEAREST",
    };

    // Moves a Video menu choice row to its next value, and switches to it
    private void StepVideoChoice(int which, int delta, bool wrap)
    {
        var id = MenuDefs.SelectedId(Items, which);
        if (id == null)
            return;

        var settings = video.Settings;
        var choices = VideoChoices(id, settings);
        if (choices.Count == 0)
            return;

        int current = Math.Max(0, choices.IndexOf(settings));
        int next = MenuDrawing.StepMenuChoice(current, choices.Count, delta, wrap);
        if (next == current)
            return;

        // Left/right arrive while HandleMenu is still running, before it records where the cursor is
        Info.CurPos = (short)which;
        audio.Play("menu/move1");
        ChangeVideo(choices[next]);
    }

    /// <summary>
    /// The video modes a choice row steps through, in order, each differing from the current
    /// one only in that row's setting. The current mode is always one of them.
    /// </summary>
    private List<VideoSettings> VideoChoices(string id, VideoSettings settings) => id switch
    {
        "window-size" => WindowSizes(settings)
            .Select(size => settings with { WindowWidth = size.Width, WindowHeight = size.Height })
            .ToList(),
        // Auto first, then the whole multiples of 320x200
        "render-scale" => RenderScales(settings)
            .Select(scale => settings with { RenderScale = scale, RenderSize = null, MatchWindow = false })
            .Prepend(settings.MatchWindow ? settings : settings with { MatchWindow = true })
            .ToList(),
        "ui-scale" => UiScales(settings)
            .Select(scale => settings with { UiScale = scale })
            .ToList(),
        "filter" => Enum.GetValues<ScaleFilter>()
            .Select(filter => settings with { Filter = filter })
            .ToList(),
        _ => [],
    };

    // The window's height for each 320 across: 200 with square pixels, 240 at 4:3
    internal static int WindowBaseHeight(bool aspectCorrect) => aspectCorrect ? 240 : 200;

    // Common monitor sizes offered as window sizes, for an Auto resolution to fill: 4:3, 16:10, 16:9 and 21:9
    private static readonly (int Width, int Height)[] CommonWindowSizes =
    [
        (800, 600), (1024, 768), (1280, 960), (1600, 1200),
        (1280, 800), (1440, 900), (1680, 1050), (1920, 1200), (2560, 1600),
        (1280, 720), (1366, 768), (1600, 900), (1920, 1080), (2560, 1440), (3200, 1800), (3840, 2160),
        (2560, 1080), (3440, 1440),
    ];

    /// <summary>
    /// Whole multiples of 320x200 (320x240 at 4:3, so the picture fills the window) and, with
    /// an Auto resolution (which fills a window of any shape), the common monitor sizes, that
    /// fit on the desktop, plus the current size if it's something else, as vid_window can set.
    /// </summary>
    private List<(int Width, int Height)> WindowSizes(VideoSettings settings) => WindowSizes(settings, video.GetLargestWindowSize());

    /// <param name="largest">The largest window the desktop holds</param>
    internal static List<(int Width, int Height)> WindowSizes(VideoSettings settings, (int Width, int Height) largest)
    {
        var (maxWidth, maxHeight) = largest;
        int baseHeight = WindowBaseHeight(settings.AspectCorrect);

        var sizes = new List<(int Width, int Height)> { (VideoSettings.BaseWidth, baseHeight) };
        for (int k = 2; VideoSettings.BaseWidth * k <= maxWidth && baseHeight * k <= maxHeight; k++)
            sizes.Add((VideoSettings.BaseWidth * k, baseHeight * k));

        if (settings.MatchWindow)
            sizes.AddRange(CommonWindowSizes.Where(size => size.Width <= maxWidth && size.Height <= maxHeight));

        if (!sizes.Contains((settings.WindowWidth, settings.WindowHeight)))
            sizes.Add((settings.WindowWidth, settings.WindowHeight));

        return sizes.Distinct().OrderBy(size => size.Width).ThenBy(size => size.Height).ToList();
    }

    private const double UiScaleStep = 0.25;

    /// <summary>
    /// Auto (0), then quarter steps from 1 up to the most that fits, then that most itself (to
    /// the hundredth below, as config.cfg keeps it), which fills the screen's height or width.
    /// A scale set in the console that's none of these is put in its place.
    /// </summary>
    internal static List<double> UiScales(VideoSettings settings)
    {
        double max = Math.Floor(settings.MaxUiScale * 100) / 100;
        var scales = new List<double> { 0 };
        for (double scale = 1; scale <= max + 1e-9; scale += UiScaleStep)
            scales.Add(scale);
        scales.Add(max);
        if (settings.UiScale > 0)
            scales.Add(settings.UiScale);

        return scales.Distinct().Order().ToList();
    }

    private const int MaxRenderScale = 8;

    // Render scales up to the display's size, since drawing more than it can show is wasted
    private List<int> RenderScales(VideoSettings settings) => RenderScales(settings, video.GetDisplaySize());

    /// <param name="display">The display's size</param>
    internal static List<int> RenderScales(VideoSettings settings, (int Width, int Height) display)
    {
        var (displayWidth, displayHeight) = display;
        int max = Math.Min(MaxRenderScale, Math.Min(displayWidth / VideoSettings.BaseWidth, displayHeight / VideoSettings.BaseHeight));
        max = Math.Max(max, Math.Max(1, settings.RenderScale));

        return Enumerable.Range(1, max).ToList();
    }

    /// <summary>
    /// Aspect correction on or off. A window of a standard size is reshaped to match, so the
    /// picture still fills it: 640x400 becomes 640x480, made smaller if that won't fit.
    /// </summary>
    private VideoSettings WithAspect(VideoSettings settings, bool aspectCorrect) => WithAspect(settings, aspectCorrect, video.GetLargestWindowSize());

    /// <param name="largest">The largest window the desktop holds</param>
    internal static VideoSettings WithAspect(VideoSettings settings, bool aspectCorrect, (int Width, int Height) largest)
    {
        var result = settings with { AspectCorrect = aspectCorrect };

        // An Auto resolution reshapes itself to fill the window instead
        if (settings.MatchWindow)
            return result;

        int k = settings.WindowWidth / VideoSettings.BaseWidth;
        bool standardSize = settings.WindowWidth == VideoSettings.BaseWidth * k
            && settings.WindowHeight == WindowBaseHeight(settings.AspectCorrect) * k;
        if (!standardSize)
            return result;

        var (maxWidth, maxHeight) = largest;
        int baseHeight = WindowBaseHeight(aspectCorrect);
        while (k > 1 && (VideoSettings.BaseWidth * k > maxWidth || baseHeight * k > maxHeight))
            k--;

        return result with { WindowWidth = VideoSettings.BaseWidth * k, WindowHeight = baseHeight * k };
    }

    /// <summary>
    /// Switches to a video mode and redraws the menu in it. A change that could leave the
    /// screen unreadable (FullScreen, the window's size, the resolution, VSync) then asks to be
    /// kept, and goes back if it isn't.
    /// </summary>
    private void ChangeVideo(VideoSettings next)
    {
        var language = assets.GetText("en-us");
        var previous = video.Settings;

        if (!video.ApplyVideoSettings(next))
        {
            DrawVideoMenu(update: false);
            audio.Play("player/usefail");
            draw.Message("$STR_VIDEOFAILED".ToLanguageText(language));
            input.ClearKeysDown();
            input.Ack();
        }
        // Against the mode as set, with an Auto resolution's size worked out
        else if (NeedsVideoConfirm(previous, video.Settings) && !ConfirmVideoMode())
        {
            video.ApplyVideoSettings(previous);
        }

        DrawVideoMenu();
    }

    internal static bool NeedsVideoConfirm(VideoSettings previous, VideoSettings next)
        => next.Fullscreen != previous.Fullscreen
           || next.RenderWidth != previous.RenderWidth || next.RenderHeight != previous.RenderHeight
           || next.VSync != previous.VSync
           || (!next.Fullscreen && (next.WindowWidth != previous.WindowWidth || next.WindowHeight != previous.WindowHeight));

    private const int VideoConfirmSeconds = 10;

    /// <summary>
    /// Asks whether to keep the video mode just switched to, counting down on screen. Only
    /// the yes key keeps it: no, escape, or letting the time run out goes back.
    /// </summary>
    private bool ConfirmVideoMode()
    {
        var language = assets.GetText("en-us");
        uint start = GameEngineManager.GetTimeCount();
        int shown = -1;
        bool keep = false;
        ControlInfo ci;

        input.ClearKeysDown();

        while (true)
        {
            int left = VideoConfirmSeconds - (int)((GameEngineManager.GetTimeCount() - start) / Timing.TickBase);
            if (left <= 0)
                break;

            // The box is redrawn over a fresh menu each second, since its width changes with the number
            if (left != shown)
            {
                shown = left;
                DrawVideoMenu(update: false);
                draw.Message("$STR_KEEPVIDEO".ToLanguageText(language).Replace("{SECONDS}", left.ToString()));
            }

            menuInput.ReadAnyControl(out ci);
            if (input.IsKeyDown(ScanCodes.Y) || ci.Button0)
            {
                keep = true;
                break;
            }

            if (input.IsKeyDown(ScanCodes.N) || input.IsKeyDown(ScanCodes.Escape) || ci.Button1)
                break;

            GameEngineManager.DelayMs(5);
        }

        input.ClearKeysDown();
        audio.Play(keep ? "menu/activate" : "menu/escape");
        return keep;
    }
}
