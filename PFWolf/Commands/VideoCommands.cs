using PFWolf.Configuration;
using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Commands;

/// <summary>The video mode: vid_mode and the vid_* settings</summary>
/// <param name="redrawScreen">Draws again what doesn't redraw itself every frame (the play screen round the view), after a mode change blanks it</param>
internal sealed class VideoCommands(ConsoleManager console, VideoManager video, Action redrawScreen)
{
    internal void Register()
    {
        console.Register("vid_mode", "Shows the video mode.", "vid_mode", _ => PrintVideoMode());
        console.Register("vid_fullscreen", "Borderless fullscreen (1) or a window (0).", "vid_fullscreen [0|1]",
            args => ApplyVideo(s => s with { Fullscreen = Toggle(args, s.Fullscreen) }), complete: Values("0", "1"));
        console.Register("vid_scale", "Draws the game at 320x200 times this: sharper, not bigger.", "vid_scale <1-8>",
            args => ApplyVideo(s => s with { RenderScale = args.Length > 0 ? ParseInt(args[0], 1, 8) : throw new ArgumentException("usage: vid_scale <1-8>"), RenderSize = null, MatchWindow = false }),
            complete: Values("1", "2", "3", "4", "5", "6"));
        console.Register("vid_render", "Draws the game at any size and shape in place of 320x200 times vid_scale: auto follows the window (or the desktop when fullscreen); default goes back to vid_scale.",
            "vid_render <width> <height> | auto | default", VidRenderCommand, complete: Values("auto", "default"));
        console.Register("vid_uiscale", "How big the menus, text and status bar are: screen pixels to each of their 320x200 pixels (2.5 is fine), or auto for the most whole number that fits.",
            "vid_uiscale <n|auto>", VidUiScaleCommand, complete: Values("auto", "1", "1.5", "2", "2.5", "3", "4"));
        console.Register("vid_window", "The window's size when it isn't fullscreen.", "vid_window <width> <height>", VidWindowCommand);
        console.Register("vid_vsync", "Waits for the display's refresh before showing each frame.", "vid_vsync [0|1]",
            args => ApplyVideo(s => s with { VSync = Toggle(args, s.VSync) }), complete: Values("0", "1"));
        console.Register("vid_aspect", "Shows the picture at 4:3, the shape it had on a CRT (1), or with square pixels (0).",
            "vid_aspect [0|1]", args => ApplyVideo(s => s with { AspectCorrect = Toggle(args, s.AspectCorrect) }),
            complete: Values("0", "1"));
        console.Register("vid_filter", "How the picture is smoothed when scaled up to the window.", "vid_filter [nearest|linear]",
            args => ApplyVideo(s => s with { Filter = args.Length > 0 ? ParseFilter(args[0]) : s.Filter == ScaleFilter.Nearest ? ScaleFilter.Linear : ScaleFilter.Nearest }),
            complete: Values("nearest", "linear"));
    }

    /// <summary>Switches to the video mode <paramref name="change"/> makes of the current one, then shows it.</summary>
    void ApplyVideo(Func<VideoSettings, VideoSettings> change)
    {
        var next = change(video.Settings);
        if (!video.ApplyVideoSettings(next))
            console.Print("Couldn't switch to that mode; kept the old one");

        // A new screen buffer starts blank; the view redraws itself every frame, the rest doesn't
        redrawScreen();

        PrintVideoMode();
    }

    void PrintVideoMode()
    {
        var s = video.Settings;
        var shown = s.Fullscreen ? "fullscreen" : $"{s.WindowWidth}x{s.WindowHeight} window";
        var size = s.MatchWindow ? "auto" : s.RenderSize != null ? "custom size" : $"scale {s.RenderScale}";
        var effective = VideoSettings.FormatScale(s.EffectiveUiScale);
        var ui = s.UiScale <= 0 ? $"auto ({effective})" : effective;
        console.Print($"{s.RenderWidth}x{s.RenderHeight} ({size}) in a {shown}, ui scale {ui}");
        console.Print($"vsync {(s.VSync ? 1 : 0)}  aspect {(s.AspectCorrect ? 1 : 0)}  filter {s.Filter.ToString().ToLowerInvariant()}");
    }

    private void VidWindowCommand(string[] args)
    {
        if (args.Length < 2)
            throw new ArgumentException("usage: vid_window <width> <height>");

        int width = ParseInt(args[0], VideoSettings.BaseWidth, 16384);
        int height = ParseInt(args[1], VideoSettings.BaseHeight, 16384);
        ApplyVideo(s => s with { WindowWidth = width, WindowHeight = height });
    }

    private void VidRenderCommand(string[] args)
    {
        if (args.Length == 1 && args[0].Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            ApplyVideo(s => s with { RenderSize = null, MatchWindow = false });
            return;
        }
        if (args.Length == 1 && args[0].Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            ApplyVideo(s => s with { MatchWindow = true });
            return;
        }
        if (args.Length < 2)
            throw new ArgumentException("usage: vid_render <width> <height> | auto | default");

        int width = ParseInt(args[0], VideoSettings.BaseWidth, VideoSettings.MaxRenderWidth);
        int height = ParseInt(args[1], VideoSettings.BaseHeight, VideoSettings.MaxRenderHeight);
        ApplyVideo(s => s with { RenderSize = (width, height), MatchWindow = false });
    }

    private void VidUiScaleCommand(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: vid_uiscale <n|auto>");

        double scale = 0;
        if (!args[0].Equals("auto", StringComparison.OrdinalIgnoreCase)
            && !(double.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out scale)
                 && scale >= 1 && scale <= 32))
            throw new ArgumentException($"expected auto or a scale from 1 to 32 (2.5, say), got \"{args[0]}\"");

        // Kept to hundredths, as config.cfg saves it
        ApplyVideo(s => s with { UiScale = Math.Round(scale, 2) });
    }
}
