namespace PFWolf.Configuration;

/// <summary>The Particles setting (Options → Video, r_particles): how many particles there can be, and how many emitters throw out</summary>
internal enum ParticleDetail : byte
{
    Off = 0,
    Low = 1,
    Medium = 2,
    High = 3,
}

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

    /// <summary>The high score table played for: config.cfg's, or with mods loaded their own (modscores.dat)</summary>
    internal HighScore[] Scores = HighScore.WolfDefaults();

    /// <summary>The loaded mods' key in modscores.dat (ModScores.KeyFor); empty with no mods, when Scores is config.cfg's</summary>
    internal string ModScoresKey = "";

    /// <summary>
    /// With mods loaded, config.cfg's own table as it was read, written back untouched; null
    /// when there was none to read
    /// </summary>
    internal HighScore[]? ConfigScores;

    /// <summary>How big the view is, 4 to 21 (21: the whole screen, no status bar)</summary>
    internal int ViewSize;

    /// <summary>The Game Options menu's settings (Configuration/GameOptions.cs)</summary>
    internal readonly GameOptions Options = new();

    /// <summary>The Particles setting (Rendering/Particles.cs): the player's own, never a game's or a host's</summary>
    internal ParticleDetail Particles = ParticleDetail.High;

    /// <summary>Whether the game saves itself at each new level (autosave.dat)</summary>
    internal bool AutosaveEnabled { get => Options.Autosave; set => Options.SetChoice(GameOption.Autosave, value); }
}
