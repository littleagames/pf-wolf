using System.Text;
using Wolf3D.Assets;
using Wolf3D.Assets.Sounds;
using Wolf3D.Fonts;
using Wolf3D.Managers;

namespace Wolf3D;

/// <summary>
/// The signon screen's startup info, printed in the open area of its pic (game-info signon
/// text-area): what's running, how it's set up, and whether startup printed any warnings
/// </summary>
internal partial class Program
{
    // Everything written to stdout from the start until the signon screen is done
    private static StartupOutput? startupOutput;

    // The next line's top in the text area; lines past its bottom are left out
    private static int signonY;

    private static readonly string[] SignonLabels = ["Engine:", "Game:", "Mods:", "Video:", "Input:", "Sound:", "Content:", "Warnings:"];

    private static SignonTextArea? SignonArea => _gameEngineManager.GetGameInfo().Signon.TextArea;

    private static void CaptureStartupOutput() => startupOutput = StartupOutput.Capture();

    /// <summary>
    /// Stops keeping stdout and returns what startup wrote to it, which the console's
    /// scrollback starts with
    /// </summary>
    private static List<string> StopCapturingStartupOutput()
    {
        var lines = startupOutput?.Stop() ?? [];
        startupOutput = null;
        _consoleManager.AddToScrollback(lines);
        return lines;
    }

    /// <summary>The lines known as soon as the signon is drawn: what's running and how it's shown</summary>
    private static void PrintSignonInfo()
    {
        if (SignonArea is not { } area)
            return;

        signonY = area.Y;
        SignonLine(area, "Engine:", $"PFWolf v{GameEngineManager.EngineVersion}");
        SignonLine(area, "Game:", _assetManager.GetGameDescription() ?? _gameEngineManager.GamePackId);
        SignonLine(area, "Mods:", ModsSummary(area));
        SignonLine(area, "Video:", VideoSummary());
    }

    /// <summary>The lines that need the config read, then the warnings startup printed</summary>
    private static void FinishSignonInfo(List<string> startupLines)
    {
        if (SignonArea is not { } area)
            return;

        SignonLine(area, "Input:", InputSummary());
        SignonLine(area, "Sound:", SoundSummary());
        PrintContentSummary(area);
        PrintSignonWarnings(area, startupLines);
    }

    private static string ModsSummary(SignonTextArea area)
    {
        var names = _assetManager.LoadedMods.Select(mod => mod.DisplayName).ToList();
        if (names.Count == 0)
            return "none";

        // As many names as fit, and how many more there are
        for (int shown = names.Count; shown > 1; shown--)
        {
            var text = string.Join(", ", names.Take(shown)) + (shown < names.Count ? $", +{names.Count - shown} more" : "");
            if (SignonTextFits(area, text))
                return text;
        }

        return names.Count == 1 ? names[0] : $"{names[0]}, +{names.Count - 1} more";
    }

    // The window's size, and the size the game draws at when that's different
    private static string VideoSummary()
    {
        var settings = _videoManager.Settings;
        var shownIn = settings.Fullscreen ? "Fullscreen" : $"Window {settings.WindowWidth}x{settings.WindowHeight}";
        var drawnAt = $"{_videoManager.screenWidth}x{_videoManager.screenHeight}";
        return settings.Fullscreen || drawnAt != $"{settings.WindowWidth}x{settings.WindowHeight}"
            ? $"{shownIn}, drawn {drawnAt}"
            : shownIn;
    }

    // The keyboard always works, so only the mouse and controller are named
    private static string InputSummary()
        => $"{(mouseenabled ? "Mouse" : "No mouse")}, {_inputManager.ControllerName ?? "no controller"}";

    private static string SoundSummary()
    {
        if (!_audioManager.IsAvailable)
            return "Off, OpenAL failed (see warnings)";

        var effects = new List<string>();
        if (_audioManager.DigitizedSoundEnabled)
            effects.Add("Digi");
        if (_audioManager.AdLibSoundEnabled)
            effects.Add("AdLib");
        if (_audioManager.PcSoundEnabled)
            effects.Add("PC");

        var sound = effects.Count == 0 ? "No effects" : string.Join(", ", effects);
        return $"{sound}; music {(_audioManager.MusicEnabled ? "on" : "off")}";
    }

    private static void PrintContentSummary(SignonTextArea area)
    {
        int maps = _assetManager.CountNames(typeof(MapAsset));
        int walls = _assetManager.CountNames(typeof(TextureAsset));
        int sprites = _assetManager.CountNames(typeof(SpriteAsset));
        // Every sound has an AdLib version (named alNAME, as its PC one is pcNAME), and some a digitized one
        int sounds = _assetManager.CountNames(typeof(AdLibSound));
        int digitized = _assetManager.CountNames(typeof(Wolf3dDigitizedAudio));
        int songs = _assetManager.CountNames(typeof(Wolf3dImfAudio));
        SignonLine(area, "Content:", $"{maps} maps, {walls} walls, {sprites} sprites");
        SignonLine(area, null, $"{sounds} sounds ({digitized} digitized), {songs} songs");
    }

    /// <summary>How many lines startup printed, then as many of them as there's room for</summary>
    private static void PrintSignonWarnings(SignonTextArea area, List<string> startupLines)
    {
        var warnings = startupLines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim()).ToList();
        if (warnings.Count == 0)
        {
            SignonLine(area, "Warnings:", "none");
            return;
        }

        SignonLine(area, "Warnings:", $"{warnings.Count}, see the console", area.WarningColor);
        foreach (var warning in warnings.Take(SignonLinesLeft(area)))
            SignonLine(area, null, warning, area.WarningColor);
    }

    /// <summary>
    /// A line of the text area: its name, and its value beside it (cut short with "..." when it's
    /// too long). Nothing is printed once the area is full.
    /// </summary>
    private static void SignonLine(SignonTextArea area, string? label, string value, string? color = null)
    {
        MeasureText(value, area.Font, out _, out int height);
        if (height <= 0 || signonY + height > area.Y + area.Height)
            return;

        var style = new TextStyle(area.Font, area.LabelColor, area.Background);
        if (label != null)
            _graphicManager.DrawText(area.X, signonY, label, style);

        int valueX = SignonValueX(area);
        _graphicManager.DrawText(valueX, signonY, FitSignonText(area, value, area.X + area.Width - valueX),
            style with { Color = color ?? area.Color });
        signonY += height;
    }

    /// <summary>Where values start: past the longest name</summary>
    private static int SignonValueX(SignonTextArea area)
        => area.X + SignonLabels.Max(label => TextWidth(label, area.Font)) + 4;

    private static int SignonLinesLeft(SignonTextArea area)
    {
        MeasureText("A", area.Font, out _, out int height);
        return height <= 0 ? 0 : (area.Y + area.Height - signonY) / height;
    }

    private static bool SignonTextFits(SignonTextArea area, string text)
        => TextWidth(text, area.Font) <= area.X + area.Width - SignonValueX(area);

    private static string FitSignonText(SignonTextArea area, string text, int width)
    {
        if (TextWidth(text, area.Font) <= width)
            return text;

        const string ellipsis = "...";
        var font = _fontManager.Find(area.Font);
        if (font == null)
            return text;

        return text[..font.Fit(text, Math.Max(width - font.Measure(ellipsis), 0))].TrimEnd() + ellipsis;
    }

    /// <summary>Writes through to stdout, keeping each line written until it's stopped</summary>
    private sealed class StartupOutput : TextWriter
    {
        private readonly TextWriter original;
        private readonly List<string> lines = [];
        private readonly StringBuilder line = new();

        private StartupOutput(TextWriter original) => this.original = original;

        public override Encoding Encoding => original.Encoding;

        /// <summary>Starts keeping what's written to stdout</summary>
        public static StartupOutput Capture()
        {
            var output = new StartupOutput(Console.Out);
            Console.SetOut(output);
            return output;
        }

        /// <summary>Puts stdout back, and returns the lines written to it (with an unfinished last one)</summary>
        public List<string> Stop()
        {
            Console.SetOut(original);
            lock (lines)
            {
                if (line.Length > 0)
                    lines.Add(line.ToString());
                line.Clear();
                return [.. lines];
            }
        }

        public override void Write(char value)
        {
            original.Write(value);
            Keep(value);
        }

        public override void Write(string? value)
        {
            original.Write(value);
            foreach (var ch in value ?? "")
                Keep(ch);
        }

        public override void Flush() => original.Flush();

        private void Keep(char ch)
        {
            lock (lines)
            {
                if (ch == '\n')
                {
                    lines.Add(line.ToString().TrimEnd('\r'));
                    line.Clear();
                }
                else
                    line.Append(ch);
            }
        }
    }
}
