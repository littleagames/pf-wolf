using PFWolf.Assets.Sounds;
using PFWolf.Editor.Audio.Opl;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Tests;

/// <summary>The AdLib sound editor: drawing tones, its tools, undo, saving</summary>
public class AdLibEditorTests
{
    private static readonly OplInstrument Voice = OplBank.BuiltIn.Melodic(112).Instrument;

    private static AdLibEditorViewModel Editor(params byte[] tones) => new("TEST", new AdLibDraft(40, Voice, 4, tones), isNew: false);

    [Test]
    public void Tones_Are_Pitches_In_The_Block()
    {
        var draft = new AdLibDraft(0, Voice, 4, []);
        Assert.That(draft.Frequency(100), Is.EqualTo(100 * 49716.0 / 65536).Within(1e-9));
        // Block 4 goes up to tone 255, 193 Hz: A3 (220 Hz) would be tone 290; A2 fits
        Assert.That(draft.ToneOf(57), Is.Null);
        Assert.That(draft.Frequency(draft.ToneOf(45)!.Value), Is.EqualTo(110).Within(1));
    }

    [Test]
    public void A_Stroke_Is_One_Step_And_Grows_The_Sound()
    {
        using var editor = Editor(0, 0, 0);
        editor.SnapToNotes = false;
        editor.BeginStroke();
        editor.SetTones(1, 1, _ => 100);
        editor.SetTones(1, 5, tick => 100 + tick);
        editor.EndStroke("Drew tones");

        Assert.That(editor.Draft.Tones, Is.EqualTo(new byte[] { 0, 101, 102, 103, 104, 105 }));
        Assert.That(editor.IsDirty, Is.True);
        editor.UndoCommand.Execute(null);
        Assert.That(editor.Draft.Tones, Is.EqualTo(new byte[] { 0, 0, 0 }));
        Assert.That(editor.IsDirty, Is.False);
        editor.RedoCommand.Execute(null);
        Assert.That(editor.Draft.Tones, Has.Length.EqualTo(6));
    }

    [Test]
    public void Snapped_Tones_Are_Semitones()
    {
        using var editor = Editor(0);
        editor.BeginStroke();
        editor.SetTones(0, 0, _ => 150);
        editor.EndStroke("Drew");
        var note = OplPitch.MidiNote(editor.Draft.Frequency(editor.Draft.Tones[0]));
        Assert.That(note, Is.EqualTo(Math.Round(note)).Within(0.05));
    }

    [Test]
    public void Tools_Work_On_The_Ticks_Picked_Or_All()
    {
        using var editor = Editor(100, 100, 0, 200, 200);
        editor.Transpose(12);
        Assert.That(editor.Draft.Tones, Is.EqualTo(new byte[] { 200, 200, 0, 255, 255 }), "an octave up, clamped at 255");

        editor.Range = (0, 2);
        editor.Transpose(-12);
        Assert.That(editor.Draft.Tones, Is.EqualTo(new byte[] { 100, 100, 0, 255, 255 }));

        editor.Stretch(2);
        Assert.That(editor.Draft.Tones, Is.EqualTo(new byte[] { 100, 100, 100, 100, 0, 255, 255 }));
        Assert.That(editor.Range, Is.EqualTo((0, 4)));

        editor.SilenceCommand.Execute(null);
        Assert.That(editor.Draft.Tones, Is.EqualTo(new byte[] { 0, 0, 0, 0, 0, 255, 255 }));
        editor.DeleteRangeCommand.Execute(null);
        Assert.That(editor.Draft.Tones, Is.EqualTo(new byte[] { 0, 255, 255 }));
        Assert.That(editor.Range, Is.Null);
    }

    [Test]
    public void A_Block_Down_With_The_Tones_Doubled_Sounds_The_Same()
    {
        using var editor = Editor(60, 0, 100);
        editor.BlockDownCommand.Execute(null);
        Assert.That(editor.Draft.Block, Is.EqualTo(3));
        Assert.That(editor.Draft.Tones, Is.EqualTo(new byte[] { 120, 0, 200 }));
        editor.BlockUpCommand.Execute(null);
        Assert.That((editor.Draft.Block, editor.Draft.Tones), Is.EqualTo((4, new byte[] { 60, 0, 100 })));
    }

    [Test]
    public void Settings_And_The_Instrument_Change_As_Steps()
    {
        using var editor = Editor(100);
        editor.Length = 140;
        Assert.That(editor.Draft.Tones, Has.Length.EqualTo(140));
        editor.Priority = 90;
        editor.Block = 6;
        Assert.That((editor.Draft.Priority, editor.Draft.Block), Is.EqualTo(((ushort)90, (byte)6)));

        editor.InstrumentEditor.Carrier.Level = 20;
        editor.InstrumentEditor.Modulator.Multiplier = 3;
        Assert.That(editor.Draft.Instrument.Carrier.Level & 0x3F, Is.EqualTo(20));
        editor.UndoCommand.Execute(null);
        Assert.That(editor.Draft.Instrument, Is.EqualTo(Voice), "two instrument changes close together are one step");
        Assert.That(editor.InstrumentEditor.IgnoresFeedback, Is.True);
    }

    [Test]
    public void Saved_It_Is_An_Adl_File_That_Plays()
    {
        using var editor = new AdLibEditorViewModel("NEWSOUND", new AdLibDraft(50, Voice, 4, new byte[140]), isNew: true);
        Assert.That(editor.IsDirty, Is.False, "a new silent sound has nothing to lose");
        editor.BeginStroke();
        editor.SetTones(0, 69, _ => 120);
        editor.EndStroke("Drew");

        byte[]? saved = null;
        editor.SaveToMod = (name, adl) => { saved = adl; return Task.FromResult<string?>("BOOP"); };
        editor.SaveCommand.Execute(null);
        var sound = AdLibSound.FromFile(saved!);
        Assert.That((sound.Priority, sound.Block, sound.Tones.Length), Is.EqualTo(((ushort)50, (byte)4, 140)));
        Assert.That(sound.ToPcm16().Take(44100 / 2).Any(sample => sample != 0), Is.True);
        Assert.That(editor.Title, Is.EqualTo("AdLib sound - BOOP"));
    }
}
