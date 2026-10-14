using PFWolf.Assets;
using PFWolf.Assets.Sounds;
using PFWolf.Fonts;

namespace PFWolf.Ui;

/// <summary>
/// The signon screen's startup info, printed in the open area of its pic (game-info signon
/// text-area): what's running, how it's set up, and whether startup printed any warnings
/// </summary>
/// <param name="mouseEnabled">Whether the mouse is on (the Control menu's setting)</param>
/// <param name="scriptCount">How many Lua scripts loaded</param>
internal sealed class SignonPanel(GameEngineManager engine, AssetManager assets, VideoManager video, InputManager input,
    AudioManager audio, GraphicManager graphics, FontManager fonts, UiText ui, Func<bool> mouseEnabled, Func<int> scriptCount)
{    // The next line's top in the text area; lines past its bottom are left out
    private int signonY;

    private static readonly string[] SignonLabels = ["Engine:", "Game:", "Mods:", "Video:", "Input:", "Sound:", "Content:", "Warnings:"];

    private SignonTextArea? SignonArea => engine.GetGameInfo().Signon.TextArea;
    /// <summary>The lines known as soon as the signon is drawn: what's running and how it's shown</summary>
    internal void PrintSignonInfo()
    {
        if (SignonArea is not { } area)
            return;

        signonY = area.Y;
        SignonLine(area, "Engine:", $"PFWolf v{GameEngineManager.EngineVersion}");
        SignonLine(area, "Game:", assets.GetGameDescription() ?? engine.GamePackId);
        SignonLine(area, "Mods:", ModsSummary(area));
        SignonLine(area, "Video:", VideoSummary());
    }

    /// <summary>The lines that need the config read, then the warnings startup printed</summary>
    internal void FinishSignonInfo(List<string> startupLines)
    {
        if (SignonArea is not { } area)
            return;

        SignonLine(area, "Input:", InputSummary());
        SignonLine(area, "Sound:", SoundSummary());
        PrintContentSummary(area);
        PrintSignonWarnings(area, startupLines);
    }

    private string ModsSummary(SignonTextArea area)
    {
        var names = assets.LoadedMods.Select(mod => mod.DisplayName).ToList();
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
    private string VideoSummary()
    {
        var settings = video.Settings;
        var shownIn = settings.Fullscreen ? "Fullscreen" : $"Window {settings.WindowWidth}x{settings.WindowHeight}";
        var drawnAt = $"{video.screenWidth}x{video.screenHeight}";
        return settings.Fullscreen || drawnAt != $"{settings.WindowWidth}x{settings.WindowHeight}"
            ? $"{shownIn}, drawn {drawnAt}"
            : shownIn;
    }

    // The keyboard always works, so only the mouse and controller are named
    private string InputSummary()
        => $"{(mouseEnabled() ? "Mouse" : "No mouse")}, {input.ControllerName ?? "no controller"}";

    private string SoundSummary()
    {
        if (!audio.IsAvailable)
            return "Off, OpenAL failed (see warnings)";

        var effects = new List<string>();
        if (audio.DigitizedSoundEnabled)
            effects.Add("Digi");
        if (audio.AdLibSoundEnabled)
            effects.Add("AdLib");
        if (audio.PcSoundEnabled)
            effects.Add("PC");

        var sound = effects.Count == 0 ? "No effects" : string.Join(", ", effects);
        return $"{sound}; music {(audio.MusicEnabled ? "on" : "off")}";
    }

    private void PrintContentSummary(SignonTextArea area)
    {
        int maps = assets.CountNames(typeof(MapAsset));
        int walls = assets.CountNames(typeof(TextureAsset));
        int sprites = assets.CountNames(typeof(SpriteAsset));
        // Every sound has an AdLib version (named alNAME, as its PC one is pcNAME), and some a digitized one
        int sounds = assets.CountNames(typeof(AdLibSound));
        int digitized = assets.CountNames(typeof(Wolf3dDigitizedAudio), typeof(SoundFileAsset));
        int songs = assets.CountNames(typeof(Wolf3dImfAudio), typeof(MusicFileAsset));
        SignonLine(area, "Content:", $"{maps} maps, {walls} walls, {sprites} sprites");
        int scripts = scriptCount();
        SignonLine(area, null, $"{sounds} sounds ({digitized} digitized), {songs} songs{(scripts > 0 ? $", {scripts} {(scripts == 1 ? "script" : "scripts")}" : "")}");
    }

    /// <summary>How many lines startup printed, then as many of them as there's room for</summary>
    private void PrintSignonWarnings(SignonTextArea area, List<string> startupLines)
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
    private void SignonLine(SignonTextArea area, string? label, string value, string? color = null)
    {
        ui.MeasureText(value, area.Font, out _, out int height);
        if (height <= 0 || signonY + height > area.Y + area.Height)
            return;

        var style = new TextStyle(area.Font, area.LabelColor, area.Background);
        if (label != null)
            graphics.DrawText(area.X, signonY, label, style);

        int valueX = SignonValueX(area);
        graphics.DrawText(valueX, signonY, FitSignonText(area, value, area.X + area.Width - valueX),
            style with { Color = color ?? area.Color });
        signonY += height;
    }

    /// <summary>Where values start: past the longest name</summary>
    private int SignonValueX(SignonTextArea area)
        => area.X + SignonLabels.Max(label => ui.TextWidth(label, area.Font)) + 4;

    private int SignonLinesLeft(SignonTextArea area)
    {
        ui.MeasureText("A", area.Font, out _, out int height);
        return height <= 0 ? 0 : (area.Y + area.Height - signonY) / height;
    }

    private bool SignonTextFits(SignonTextArea area, string text)
        => ui.TextWidth(text, area.Font) <= area.X + area.Width - SignonValueX(area);

    private string FitSignonText(SignonTextArea area, string text, int width)
    {
        if (ui.TextWidth(text, area.Font) <= width)
            return text;

        const string ellipsis = "...";
        var font = fonts.Find(area.Font);
        if (font == null)
            return text;

        return text[..font.Fit(text, Math.Max(width - font.Measure(ellipsis), 0))].TrimEnd() + ellipsis;
    }
}
