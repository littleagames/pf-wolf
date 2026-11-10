using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets.Sounds;

namespace PFWolf.Editor.ViewModels;

/// <summary>
/// A PC speaker sound as the editor changes it: its priority and one value a tick (140 a second):
/// the PIT's reload value / 60, so 1193180 / (value × 60) Hz, 0 for silence
/// </summary>
public sealed record PcDraft(ushort Priority, byte[] Tones) : ToneDraft(Priority, Tones)
{
    /// <summary>The PIT counts down at this rate; a value is its reload value / 60</summary>
    public const double PitRate = 1193180, Scale = 60;

    public static PcDraft From(PcSound sound) => new(sound.Priority, sound.Values);

    /// <summary>
    /// An AdLib sound made a PC speaker sound: each tick's frequency (some octaves up or down) as
    /// the nearest value (values run 78 Hz to 19886 Hz, so lower and higher tones are clamped)
    /// </summary>
    public static PcDraft FromAdLib(AdLibDraft adLib, ushort priority, int octaves = 0)
    {
        var pc = new PcDraft(priority, []);
        var factor = Math.Pow(2, octaves);
        return pc with { Tones = [.. adLib.Tones.Select(tone => tone == 0 ? (byte)0 : (byte)pc.ToneOfFrequency(adLib.Frequency(tone) * factor))] };
    }

    public PcSound ToSound() => PcSound.Create(Priority, Tones);

    public override double Frequency(int tone) => PitRate / (Math.Max(tone, 1) * Scale);

    protected override int NearestTone(double frequency) => frequency <= 0 ? int.MaxValue : (int)Math.Round(PitRate / (frequency * Scale));

    // Values under 8 (2486 Hz and up) are big steps the game's sounds don't use (they use 15-160)
    private const int HighestShownValue = 8;

    public override (double Low, double High) Shown => (Frequency(255), Frequency(HighestShownValue));

    public override string ToneWord => "value";

    public override byte[] ToFile() => ToSound().RawData;

    public override short[] ToSamples()
    {
        var mono8 = ToSound().ToMono8();
        var samples = new short[mono8.Length];
        for (var i = 0; i < mono8.Length; i++)
            samples[i] = (short)((mono8[i] - 128) << 8);
        return samples;
    }
}

/// <summary>
/// The PC speaker sound editor: a sound's values drawn on a strip, a tick at a time, with its
/// priority, played as the game plays it, undone and redone, saved into a mod as sounds/NAME.pcs;
/// or made from its AdLib version
/// </summary>
public sealed partial class PcSpeakerEditorViewModel : ToneSoundEditorViewModel
{
    /// <param name="adLibVersion">The AdLib sound it can be made from (the same sound's AdLib version), if there is one</param>
    public PcSpeakerEditorViewModel(string name, PcDraft draft, bool isNew, AdLibDraft? adLibVersion = null, string? adLibName = null)
        : base(name, draft, isNew)
    {
        AdLibVersion = adLibVersion;
        AdLibName = adLibName ?? name;
        Refresh();
    }

    protected override string Kind => "PC speaker sound";

    /// <summary>The sound as it is now</summary>
    public PcDraft Draft => (PcDraft)Current;

    /// <summary>The AdLib sound <see cref="FromAdLibCommand"/> makes this one from, if any</summary>
    public AdLibDraft? AdLibVersion { get; }

    public string AdLibName { get; }

    public bool HasAdLibVersion => AdLibVersion != null;

    public string FromAdLibTip => AdLibVersion != null
        ? $"Makes this sound from the AdLib sound {AdLibName}, tick by tick, each tone as the nearest PC speaker value (keeps the priority)"
        : "There's no AdLib version of this sound to make it from";

    /// <summary>
    /// How many octaves above the AdLib version it's made: the games' own PC versions mostly sit
    /// about one up (the speaker is weak down low)
    /// </summary>
    [ObservableProperty] private decimal? _fromAdLibOctaves = 1;

    protected override string Describe() => $"value 1 is {Draft.Frequency(1):0} Hz, 255 is {Draft.Frequency(255):0} Hz";

    /// <summary>The sound replaced by the AdLib version's tones, each as the nearest value</summary>
    [RelayCommand(CanExecute = nameof(HasAdLibVersion))]
    private void FromAdLib()
    {
        if (AdLibVersion is not { } adLib)
            return;
        var octaves = (int)Math.Clamp(FromAdLibOctaves ?? 0, -3, 3);
        Edit($"Made from the AdLib sound {AdLibName}" + (octaves != 0 ? $", {Math.Abs(octaves)} octave{(Math.Abs(octaves) == 1 ? "" : "s")} {(octaves > 0 ? "up" : "down")}" : ""),
            draft => PcDraft.FromAdLib(adLib, draft.Priority, octaves));
        Range = null;
    }

    public byte[] ToPcs() => ToFile();
}
