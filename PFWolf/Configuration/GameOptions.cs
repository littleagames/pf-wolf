using PFWolf.Assets;

namespace PFWolf.Configuration;

/// <summary>The Game Options menu's settings, in the order config.cfg saves them</summary>
internal enum GameOption : byte
{
    Look,
    Messages,
    AutomapStats,
    Autosave,
    WeaponBob,
    AlwaysRun,
    Crosshair,
    Lighting,           // levels shaded (fog, night, light zones); off draws everything at full light
    AttackInfo,         // who's attacking shown as a message (game-info attack-message: Blake Stone's REBA)
    ShowCeilings,       // ceiling flats drawn; off draws the plain ceiling color
    ShowFloors,         // floor flats drawn; off draws the plain floor color
    Count,
}

/// <summary>
/// The Game Options menu's on/off settings. Each is the player's choice once they make one (saved
/// in config.cfg), and until then the game pack's (game-info setting-defaults), else the engine's.
/// </summary>
internal sealed class GameOptions
{
    private readonly bool?[] choices = new bool?[(int)GameOption.Count];
    private readonly bool?[] gameDefaults = new bool?[(int)GameOption.Count];

    /// <summary>The engine's own defaults, with no choice and nothing in game-info</summary>
    internal static bool EngineDefault(GameOption option) => option switch
    {
        GameOption.Look => true,
        GameOption.AutomapStats => true,
        GameOption.Autosave => true,
        GameOption.Lighting => true,
        GameOption.AttackInfo => true,
        GameOption.ShowCeilings => true,
        GameOption.ShowFloors => true,
        _ => false,
    };

    /// <summary>The console command each setting has</summary>
    internal static string CommandName(GameOption option) => option switch
    {
        GameOption.Look => "freelook",
        GameOption.Messages => "msg_enabled",
        GameOption.AutomapStats => "am_stats",
        GameOption.Autosave => "autosave",
        GameOption.WeaponBob => "weaponbob",
        GameOption.AlwaysRun => "alwaysrun",
        GameOption.Crosshair => "crosshair",
        GameOption.Lighting => "r_lighting",
        GameOption.AttackInfo => "msg_attackinfo",
        GameOption.ShowCeilings => "r_ceilings",
        GameOption.ShowFloors => "r_floors",
        _ => throw new ArgumentOutOfRangeException(nameof(option)),
    };

    /// <summary>The setting now: the player's choice, else the game's default, else the engine's</summary>
    internal bool this[GameOption option] => choices[(int)option] ?? GameDefault(option);

    /// <summary>What the setting is without a choice of the player's</summary>
    internal bool GameDefault(GameOption option) => gameDefaults[(int)option] ?? EngineDefault(option);

    /// <summary>The player's choice; null goes back to the game's default</summary>
    internal bool? Choice(GameOption option) => choices[(int)option];

    internal void SetChoice(GameOption option, bool? value) => choices[(int)option] = value;

    /// <summary>Turns a setting over, which makes it the player's choice from then on</summary>
    internal void Toggle(GameOption option) => choices[(int)option] = !this[option];

    /// <summary>Takes the game pack's defaults from game-info setting-defaults</summary>
    internal void UseGameDefaults(GameInfoAsset gameInfo)
    {
        var d = gameInfo.SettingDefaults ?? new SettingDefaultsInfo();
        gameDefaults[(int)GameOption.Look] = d.Look;
        gameDefaults[(int)GameOption.Messages] = d.Messages;
        gameDefaults[(int)GameOption.AutomapStats] = d.AutomapStats;
        gameDefaults[(int)GameOption.Autosave] = d.Autosave;
        gameDefaults[(int)GameOption.WeaponBob] = d.WeaponBob;
        gameDefaults[(int)GameOption.AlwaysRun] = d.AlwaysRun;
        gameDefaults[(int)GameOption.Crosshair] = d.Crosshair;
        gameDefaults[(int)GameOption.Lighting] = d.Lighting;
        gameDefaults[(int)GameOption.AttackInfo] = d.AttackInfo;
        gameDefaults[(int)GameOption.ShowCeilings] = d.ShowCeilings;
        gameDefaults[(int)GameOption.ShowFloors] = d.ShowFloors;
    }

    // Shorthands for the code that asks
    internal bool Look => this[GameOption.Look];
    internal bool Messages => this[GameOption.Messages];
    internal bool AutomapStats => this[GameOption.AutomapStats];
    internal bool Autosave => this[GameOption.Autosave];
    internal bool WeaponBob => this[GameOption.WeaponBob];
    internal bool AlwaysRun => this[GameOption.AlwaysRun];
    internal bool Crosshair => this[GameOption.Crosshair];
    internal bool Lighting => this[GameOption.Lighting];
    internal bool AttackInfo => this[GameOption.AttackInfo];
    internal bool ShowCeilings => this[GameOption.ShowCeilings];
    internal bool ShowFloors => this[GameOption.ShowFloors];
}
