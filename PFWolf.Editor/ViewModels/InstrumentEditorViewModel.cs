using CommunityToolkit.Mvvm.ComponentModel;
using PFWolf.Assets.Sounds;

namespace PFWolf.Editor.ViewModels;

/// <summary>
/// One OPL2 operator's settings by name: what its registers' bits are. A change raises
/// <see cref="Changed"/>; <see cref="Set"/> shows an operator without raising it.
/// </summary>
public sealed partial class OperatorEditorViewModel(string title) : ObservableObject
{
    private bool _setting;

    public string Title { get; } = title;

    public event EventHandler? Changed;

    [ObservableProperty] private int _attack;          // 0x60 bits 4-7: higher is faster
    [ObservableProperty] private int _decay;           // 0x60 bits 0-3
    [ObservableProperty] private int _sustain;         // 0x80 bits 4-7: higher is quieter
    [ObservableProperty] private int _release;         // 0x80 bits 0-3
    [ObservableProperty] private int _multiplier;      // 0x20 bits 0-3
    [ObservableProperty] private int _level;           // 0x40 bits 0-5: total level, higher is quieter
    [ObservableProperty] private int _keyScaleLevel;   // 0x40 bits 6-7
    [ObservableProperty] private int _waveform;        // 0xE0 bits 0-1
    [ObservableProperty] private bool _tremolo;        // 0x20 bit 7
    [ObservableProperty] private bool _vibrato;        // 0x20 bit 6
    [ObservableProperty] private bool _sustaining;     // 0x20 bit 5: holds at the sustain level while the key is on
    [ObservableProperty] private bool _keyScaleRate;   // 0x20 bit 4

    public static IReadOnlyList<string> Waveforms { get; } = ["Sine", "Half sine", "Absolute sine", "Quarter sine"];

    public void Set(OplOperator op)
    {
        _setting = true;
        Attack = op.AttackDecay >> 4;
        Decay = op.AttackDecay & 15;
        Sustain = op.SustainRelease >> 4;
        Release = op.SustainRelease & 15;
        Multiplier = op.Characteristic & 15;
        Tremolo = (op.Characteristic & 0x80) != 0;
        Vibrato = (op.Characteristic & 0x40) != 0;
        Sustaining = (op.Characteristic & 0x20) != 0;
        KeyScaleRate = (op.Characteristic & 0x10) != 0;
        Level = op.Level & 0x3F;
        KeyScaleLevel = op.Level >> 6;
        Waveform = op.Waveform & 3;
        _setting = false;
    }

    public OplOperator ToOperator() => new(
        (byte)((Tremolo ? 0x80 : 0) | (Vibrato ? 0x40 : 0) | (Sustaining ? 0x20 : 0) | (KeyScaleRate ? 0x10 : 0) | (Multiplier & 15)),
        (byte)(((KeyScaleLevel & 3) << 6) | (Level & 0x3F)),
        (byte)(((Attack & 15) << 4) | (Decay & 15)),
        (byte)(((Sustain & 15) << 4) | (Release & 15)),
        (byte)(Waveform & 3));

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!_setting)
            Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>An instrument's two operators, feedback and connection, edited by name</summary>
public sealed partial class InstrumentEditorViewModel : ObservableObject
{
    private bool _setting;

    public OperatorEditorViewModel Modulator { get; } = new("Modulator");
    public OperatorEditorViewModel Carrier { get; } = new("Carrier");

    [ObservableProperty] private int _feedback;        // 0xC0 bits 1-3
    [ObservableProperty] private bool _additive;       // 0xC0 bit 0: both operators sound (else the modulator shapes the carrier)

    /// <summary>Feedback and connection aren't heard (an AdLib sound: Wolf3D writes 0xC0 = 0 for them)</summary>
    [ObservableProperty] private bool _ignoresFeedback;

    /// <summary>The instrument changed from here</summary>
    public event EventHandler<OplInstrument>? Changed;

    public InstrumentEditorViewModel()
    {
        Modulator.Changed += (_, _) => Raise();
        Carrier.Changed += (_, _) => Raise();
    }

    public void Set(OplInstrument instrument)
    {
        _setting = true;
        Modulator.Set(instrument.Modulator);
        Carrier.Set(instrument.Carrier);
        Feedback = (instrument.FeedbackConnection >> 1) & 7;
        Additive = (instrument.FeedbackConnection & 1) != 0;
        _setting = false;
    }

    public OplInstrument ToInstrument() => new(Modulator.ToOperator(), Carrier.ToOperator(), (byte)(((Feedback & 7) << 1) | (Additive ? 1 : 0)));

    public InstrumentEditorViewModel(bool ignoresFeedback) : this() => _ignoresFeedback = ignoresFeedback;

    partial void OnFeedbackChanged(int value) => Raise();
    partial void OnAdditiveChanged(bool value) => Raise();

    private void Raise()
    {
        if (!_setting)
            Changed?.Invoke(this, ToInstrument());
    }
}
