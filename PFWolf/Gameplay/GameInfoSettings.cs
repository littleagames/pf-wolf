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
    internal FadeStyle screenFadeStyle = FadeStyle.Palette;
    internal int? screenFadeTics;

    internal FadeStyle menuFadeStyle = FadeStyle.Palette, deathFadeStyle = FadeStyle.Fizzle, levelFadeStyle = FadeStyle.Fizzle;
    internal int menuFadeTics = 20, deathFadeTics = 70, levelFadeTics = 20;
    internal string deathFadeColor = "Maroon";
    // The death screen's turn to face the killer (degrees a tic) and hold on the color (tics)
    internal int deathTurnSpeed = 2, deathHoldTics = 100;
    // The dead view's drop toward the floor: the eye height it ends at (null: no drop) and texels a tic
    internal int? deathDropHeight;
    internal int deathDropSpeed = 1;
    internal double weaponScale = 1;    // game-info weapon-scale
    internal bool weaponBob;            // game-info weapon-bob

    internal static GameInfoSettings Read(GameInfoAsset gameInfo)
    {
        var settings = new GameInfoSettings();
        settings.screenFadeStyle = ParseFadeStyle(gameInfo.ScreenFadeStyle, "screen-fade-style", FadeStyle.Palette);
        settings.screenFadeTics = ParseFadeTics(gameInfo.ScreenFadeTics, "screen-fade-tics");
        settings.menuFadeStyle = ParseFadeStyle(gameInfo.MenuFadeStyle, "menu-fade-style", settings.screenFadeStyle);
        settings.menuFadeTics = ParseFadeTics(gameInfo.MenuFadeTics, "menu-fade-tics") ?? 20;
        settings.deathFadeStyle = ParseFadeStyle(gameInfo.DeathFadeStyle, "death-fade-style", FadeStyle.Fizzle);
        settings.deathFadeTics = ParseFadeTics(gameInfo.DeathFadeTics, "death-fade-tics") ?? 70;
        settings.deathFadeColor = string.IsNullOrWhiteSpace(gameInfo.DeathFadeColor) ? "Maroon" : gameInfo.DeathFadeColor;
        settings.deathTurnSpeed = Math.Max(gameInfo.DeathTurnSpeed ?? 2, 1);    // 0 would never finish turning
        settings.deathHoldTics = Math.Max(gameInfo.DeathHoldTics ?? 100, 0);
        settings.deathDropHeight = gameInfo.DeathDropHeight is { } drop ? Math.Clamp(drop, GameConstants.MinEye, GameConstants.MaxEye) : null;
        settings.deathDropSpeed = Math.Max(gameInfo.DeathDropSpeed ?? 1, 1);    // 0 would never finish dropping
        settings.weaponScale = Math.Clamp(gameInfo.WeaponScale ?? 1, 0.1, 2);
        settings.weaponBob = gameInfo.WeaponBob ?? false;
        settings.levelFadeStyle = ParseFadeStyle(gameInfo.LevelFadeStyle, "level-fade-style", FadeStyle.Fizzle);
        settings.levelFadeTics = ParseFadeTics(gameInfo.LevelFadeTics, "level-fade-tics") ?? 20;
        return settings;
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
