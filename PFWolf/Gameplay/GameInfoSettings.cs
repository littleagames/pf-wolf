using PFWolf.Assets;

namespace PFWolf.Gameplay;

/// <summary>
/// How the game looks doing things, from game-info: fade styles and lengths (tics), the death
/// screen, and the weapon in hand. Bad values are reported (to stdout, which the signon shows)
/// and replaced with the defaults.
/// </summary>
internal sealed class GameInfoSettings
{
    // Ordinary fades (screen-fade-style, screen-fade-tics), which VideoManager does
    internal FadeStyle ScreenFadeStyle = FadeStyle.Palette;
    internal int? ScreenFadeTics;

    internal FadeStyle MenuFadeStyle = FadeStyle.Palette, DeathFadeStyle = FadeStyle.Fizzle, LevelFadeStyle = FadeStyle.Fizzle;
    internal int MenuFadeTics = 20, DeathFadeTics = 70, LevelFadeTics = 20;
    internal string DeathFadeColor = "Maroon";
    // The death screen's turn to face the killer (degrees a tic) and hold on the color (tics)
    internal int DeathTurnSpeed = 2, DeathHoldTics = 100;
    // The dead view's drop toward the floor: the eye height it ends at (null: no drop) and texels a tic
    internal int? DeathDropHeight;
    internal int DeathDropSpeed = 1;
    internal double WeaponScale = 1;    // game-info weapon-scale
    internal bool MenuGun = true;       // game-info menu-cursor (none: no gun)

    internal static GameInfoSettings Read(GameInfoAsset gameInfo)
    {
        var settings = new GameInfoSettings();
        settings.ScreenFadeStyle = ParseFadeStyle(gameInfo.ScreenFadeStyle, "screen-fade-style", FadeStyle.Palette);
        settings.ScreenFadeTics = ParseFadeTics(gameInfo.ScreenFadeTics, "screen-fade-tics");
        settings.MenuFadeStyle = ParseFadeStyle(gameInfo.MenuFadeStyle, "menu-fade-style", settings.ScreenFadeStyle);
        settings.MenuFadeTics = ParseFadeTics(gameInfo.MenuFadeTics, "menu-fade-tics") ?? 20;
        settings.DeathFadeStyle = ParseFadeStyle(gameInfo.DeathFadeStyle, "death-fade-style", FadeStyle.Fizzle);
        settings.DeathFadeTics = ParseFadeTics(gameInfo.DeathFadeTics, "death-fade-tics") ?? 70;
        settings.DeathFadeColor = string.IsNullOrWhiteSpace(gameInfo.DeathFadeColor) ? "Maroon" : gameInfo.DeathFadeColor;
        settings.DeathTurnSpeed = Math.Max(gameInfo.DeathTurnSpeed ?? 2, 1);    // 0 would never finish turning
        settings.DeathHoldTics = Math.Max(gameInfo.DeathHoldTics ?? 100, 0);
        settings.DeathDropHeight = gameInfo.DeathDropHeight is { } drop ? Math.Clamp(drop, GameConstants.MinEye, GameConstants.MaxEye) : null;
        settings.DeathDropSpeed = Math.Max(gameInfo.DeathDropSpeed ?? 1, 1);    // 0 would never finish dropping
        settings.WeaponScale = Math.Clamp(gameInfo.WeaponScale ?? 1, 0.1, 2);
        settings.LevelFadeStyle = ParseFadeStyle(gameInfo.LevelFadeStyle, "level-fade-style", FadeStyle.Fizzle);
        settings.LevelFadeTics = ParseFadeTics(gameInfo.LevelFadeTics, "level-fade-tics") ?? 20;
        settings.MenuGun = gameInfo.MenuCursor?.Trim().ToLowerInvariant() switch
        {
            null or "" or "gun" => true,
            "none" => false,
            var other => Warn(other),
        };
        return settings;

        static bool Warn(string value)
        {
            Console.WriteLine($"Unknown menu-cursor '{value}' in game-info (gun or none); using the gun");
            return true;
        }
    }

    private static int? ParseFadeTics(int? value, string key)
    {
        if (value is null or > 0)
            return value;

        Console.WriteLine($"{key} must be more than 0 tics in game-info, not {value}; using the default");
        return null;
    }

    private static FadeStyle ParseFadeStyle(string? value, string key, FadeStyle fallback)
    {
        if (string.IsNullOrEmpty(value))
            return fallback;

        // By name only: Enum.TryParse would also take numbers
        if (Enum.GetNames<FadeStyle>().Contains(value, StringComparer.OrdinalIgnoreCase))
            return Enum.Parse<FadeStyle>(value, ignoreCase: true);

        Console.WriteLine($"Unknown {key} '{value}' in game-info, using {fallback.ToString().ToLowerInvariant()} instead");
        return fallback;
    }
}
