using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets.Sounds;

namespace PFWolf.Editor.ViewModels;

/// <summary>
/// An AdLib sound as the editor changes it: its priority, instrument, octave block, and one tone a
/// tick (140 a second): an F-number's low byte, 0 for silence
/// </summary>
public sealed record AdLibDraft(ushort Priority, OplInstrument Instrument, byte Block, byte[] Tones) : ToneDraft(Priority, Tones)
{
    // Tones under 8 are coarse steps a block down plays better
    private const int LowestShownTone = 8;

    public static AdLibDraft From(AdLibSound sound) => new(sound.Priority, sound.Instrument, sound.Block, sound.Tones);

    public AdLibSound ToSound() => AdLibSound.Create(Priority, Instrument, Block, Tones);

    /// <summary>A tone's frequency in this block</summary>
    public override double Frequency(int tone) => OplPitch.Frequency(tone, Block);

    protected override int NearestTone(double frequency) => (int)Math.Round(frequency * (1 << (20 - Block)) / OplPitch.ChipRate);

    public override (double Low, double High) Shown => (Frequency(LowestShownTone), Frequency(255));

    public override string ToneWord => "tone";

    public override byte[] ToFile() => ToSound().RawData;

    public override short[] ToSamples() => ToSound().ToPcm16();
}

/// <summary>
/// The AdLib sound editor: a sound's tones drawn on a strip, a tick at a time, with its
/// instrument, block and priority, played as the game plays it, undone and redone, saved into a
/// mod as sounds/NAME.adl
/// </summary>
public sealed partial class AdLibEditorViewModel : ToneSoundEditorViewModel
{
    private const string InstrumentChange = "Changed the instrument";
    private bool _settingInstrument;
    private DateTime _lastInstrumentEdit;

    public AdLibEditorViewModel(string name, AdLibDraft draft, bool isNew, IReadOnlyList<InstrumentSource>? library = null)
        : base(name, draft, isNew)
    {
        Library = library ?? [];
        InstrumentEditor.Set(draft.Instrument);
        InstrumentEditor.Changed += (_, instrument) => ChangeInstrument(instrument);
        Refresh();
    }

    protected override string Kind => "AdLib sound";

    public IReadOnlyList<InstrumentSource> Library { get; }

    /// <summary>The sound as it is now</summary>
    public AdLibDraft Draft => (AdLibDraft)Current;

    public InstrumentEditorViewModel InstrumentEditor { get; } = new(ignoresFeedback: true);

    private void EditAdLib(string what, Func<AdLibDraft, AdLibDraft> change) => Edit(what, draft => change((AdLibDraft)draft));

    protected override string Describe() => $"block {Draft.Block}: tone 1 is {Draft.Frequency(1):0.#} Hz, 255 is {Draft.Frequency(255):0} Hz";

    protected override void Refreshed() => OnPropertyChanged(nameof(Block));

    protected override void Restored()
    {
        _settingInstrument = true;
        InstrumentEditor.Set(Draft.Instrument);
        _settingInstrument = false;
    }

    /// <summary>The octave block the tones play in (0-7); each up is an octave higher</summary>
    public decimal? Block
    {
        get => Draft.Block;
        set
        {
            if (value is { } block)
                EditAdLib($"Block {block}", draft => draft with { Block = (byte)Math.Clamp(block, 0, 7) });
        }
    }

    // Instrument panel edits close together are one step
    private void ChangeInstrument(OplInstrument instrument)
    {
        if (_settingInstrument)
            return;
        if (DateTime.UtcNow - _lastInstrumentEdit >= TimeSpan.FromSeconds(2)
            || !AmendLast(InstrumentChange, draft => (AdLibDraft)draft with { Instrument = instrument }))
            EditAdLib(InstrumentChange, draft => draft with { Instrument = instrument });
        _lastInstrumentEdit = DateTime.UtcNow;
    }

    /// <summary>The instrument becomes a copy of one from a game song, AdLib sound or bank</summary>
    public void CopyInstrument(InstrumentSource source)
    {
        EditAdLib($"Instrument copied from {source.Label}", draft => draft with { Instrument = source.Instrument });
        Restored();
    }

    /// <summary>
    /// The block moved up or down with the tones halved or doubled, so it sounds the same with
    /// finer (down) or coarser (up) steps; tones past 255 or under 1 are clamped
    /// </summary>
    public void ShiftBlock(int by)
    {
        var block = Math.Clamp(Draft.Block + by, 0, 7);
        if (block == Draft.Block)
            return;
        var factor = Math.Pow(2, Draft.Block - block);
        EditAdLib($"Block {block}, same pitch", draft => draft with
        {
            Block = (byte)block,
            Tones = [.. draft.Tones.Select(tone => tone == 0 ? (byte)0 : (byte)Math.Clamp((int)Math.Round(tone * factor), 1, 255))],
        });
    }

    [RelayCommand] private void BlockUp() => ShiftBlock(1);
    [RelayCommand] private void BlockDown() => ShiftBlock(-1);

    public byte[] ToAdl() => ToFile();
}
