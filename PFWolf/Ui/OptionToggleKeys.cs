using PFWolf.Configuration;

namespace PFWolf.Ui;

/// <summary>
/// Blake Stone's option keys during play (bstone CheckKeys): each turns a setting over the moment
/// its control goes down, and says so with a message ($STR_TOGGLE_*_ON/OFF; an empty one says
/// nothing, as bstone's lighting, ceiling and flooring keys). Unbound unless the player binds them.
/// Only this machine's settings change, so they work in a game with others too.
/// </summary>
/// <param name="hasHeartbeat">Whether the status bar's heart monitor has a beat to hear (Planet Strike's has no monitor)</param>
/// <param name="say">Shows a message (a $NAME language key)</param>
internal sealed class OptionToggleKeys(
    InputManager input,
    AudioManager audio,
    GameOptions options,
    Func<bool> hasHeartbeat,
    Action<string> say)
{
    // The sound devices on when the sound key turned them all off, to turn back on
    (bool Pc, bool AdLib, bool Digitized)? soundWas;

    internal void CheckKeys()
    {
        if (Pressed(ButtonType.ToggleAttackInfo))
            Toggle(GameOption.AttackInfo, "ATTACKINFO");
        if (Pressed(ButtonType.ToggleLighting))
            Toggle(GameOption.Lighting, "LIGHTING");
        if (Pressed(ButtonType.ToggleCeilings))
            Toggle(GameOption.ShowCeilings, "CEILINGS");
        if (Pressed(ButtonType.ToggleFloors))
            Toggle(GameOption.ShowFloors, "FLOORS");
        if (Pressed(ButtonType.ToggleHeartbeat) && hasHeartbeat())
            Toggle(GameOption.HeartbeatSound, "HEARTBEAT");

        if (Pressed(ButtonType.ToggleSound))
            Say("SOUND", ToggleSound());

        if (Pressed(ButtonType.ToggleMusic))
        {
            audio.MusicEnabled ^= true;
            Say("MUSIC", audio.MusicEnabled);
        }
    }

    // Down this frame and not the last
    bool Pressed(ButtonType button) => input.IsButtonPressed(button) && !input.IsButtonHeld(button);

    void Toggle(GameOption option, string name)
    {
        options.Toggle(option);
        Say(name, options[option]);
    }

    void Say(string name, bool on) => say($"$STR_TOGGLE_{name}_{(on ? "ON" : "OFF")}");

    /// <summary>
    /// Sound effects off (every device), or back on as they were before; all of them if they
    /// were all off to begin with. True if they're on now.
    /// </summary>
    internal bool ToggleSound()
    {
        bool anyOn = audio.PcSoundEnabled || audio.AdLibSoundEnabled || audio.DigitizedSoundEnabled;
        if (anyOn)
        {
            soundWas = (audio.PcSoundEnabled, audio.AdLibSoundEnabled, audio.DigitizedSoundEnabled);
            audio.StopAll();
            audio.PcSoundEnabled = audio.AdLibSoundEnabled = audio.DigitizedSoundEnabled = false;
            return false;
        }

        var (pc, adLib, digitized) = soundWas ?? (true, true, true);
        audio.PcSoundEnabled = pc;
        audio.AdLibSoundEnabled = adLib;
        audio.DigitizedSoundEnabled = digitized;
        soundWas = null;
        return true;
    }
}
