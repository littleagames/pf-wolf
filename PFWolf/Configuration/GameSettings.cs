namespace PFWolf.Configuration;

/// <summary>
/// The player's settings and high scores that config.cfg and controls.cfg keep (GameEngineManager
/// reads and writes them), besides what the managers hold themselves
/// </summary>
internal sealed class GameSettings
{
    /// <summary>The keys, mouse buttons and joystick buttons bound to each button and automap key (saved to controls.cfg).</summary>
    internal readonly ControlBindings Controls = new();

    // The mouse look and controller settings (Configuration/ControlSettings.cs)
    internal readonly ControlSettings ControlSettings = new();

    internal HighScore[] Scores = HighScore.WolfDefaults();

    /// <summary>How big the view is, 4 to 21 (21: the whole screen, no status bar)</summary>
    internal int ViewSize;

    /// <summary>The Game Options menu's settings (Configuration/GameOptions.cs)</summary>
    internal readonly GameOptions Options = new();

    /// <summary>Whether the game saves itself at each new level (autosave.dat)</summary>
    internal bool AutosaveEnabled { get => Options.Autosave; set => Options.SetChoice(GameOption.Autosave, value); }
}
