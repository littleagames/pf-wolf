using PFWolf.Assets.Sounds;
using PFWolf.Editor.Audio.Opl;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Tests;

/// <summary>The PC speaker sound editor: values as pitches, its tools, made from an AdLib sound, saving</summary>
public class PcSpeakerEditorTests
{
    private static PcSpeakerEditorViewModel Editor(params byte[] values) => new("TEST", new PcDraft(30, values), isNew: false);

    [Test]
    public void Values_Are_Pit_Reload_Values_Over_60()
    {
        var draft = new PcDraft(0, []);
        Assert.That(draft.Frequency(100), Is.EqualTo(1193180.0 / 6000).Within(1e-9));
        Assert.That(draft.Shown.Low, Is.EqualTo(draft.Frequency(255)));
        Assert.That(draft.Shown.High, Is.EqualTo(draft.Frequency(8)));
        // A4 (440 Hz) is value 45; A1 (55 Hz) is lower than 255 goes
        Assert.That(draft.ToneOf(69), Is.EqualTo(45));
        Assert.That(draft.ToneOf(33), Is.Null);
        Assert.That(draft.ToneOfFrequency(10), Is.EqualTo(255), "clamped");
        Assert.That(draft.ToneOfFrequency(1e6), Is.EqualTo(1), "clamped");
    }

    [Test]
    public void Up_Is_A_Smaller_Value()
    {
        using var editor = Editor(200, 0, 100);
        editor.Transpose(12);
        Assert.That(editor.Draft.Tones, Is.EqualTo(new byte[] { 100, 0, 50 }));
        editor.Transpose(-24);
        Assert.That(editor.Draft.Tones, Is.EqualTo(new byte[] { 255, 0, 200 }), "two octaves down, clamped at 255");
        editor.UndoCommand.Execute(null);
        Assert.That(editor.Draft.Tones, Is.EqualTo(new byte[] { 100, 0, 50 }));
    }

    [Test]
    public void Drawn_Values_Snap_To_Semitones_And_Strokes_Are_Steps()
    {
        using var editor = Editor(0, 0);
        editor.BeginStroke();
        editor.SetTones(0, 3, _ => 46);
        editor.EndStroke("Drew");
        Assert.That(editor.Draft.Tones, Is.EqualTo(new byte[] { 45, 45, 45, 45 }), "46 is nearest A4, value 45");
        Assert.That(editor.IsDirty, Is.True);
        editor.UndoCommand.Execute(null);
        Assert.That((editor.Draft.Tones.Length, editor.IsDirty), Is.EqualTo((2, false)));
    }

    [Test]
    public void Made_From_The_AdLib_Version_Tick_By_Tick()
    {
        var voice = OplBank.BuiltIn.Melodic(112).Instrument;
        var adLib = new AdLibDraft(60, voice, 6, [0, 0, 0, 0]);
        // Block 6 holds A4 (440 Hz) as tone 145
        adLib = adLib with { Tones = [(byte)adLib.ToneOf(69)!.Value, 0, (byte)adLib.ToneOf(76)!.Value, (byte)adLib.ToneOf(57)!.Value] };

        var made = PcDraft.FromAdLib(adLib, 30);
        Assert.That(made.Priority, Is.EqualTo(30));
        Assert.That(made.Tones[1], Is.Zero, "silence stays silence");
        Assert.That(made.Tones[0], Is.EqualTo(45), "A4");
        Assert.That(made.Frequency(made.Tones[2]), Is.EqualTo(659).Within(15), "E5");
        Assert.That(made.Frequency(made.Tones[3]), Is.EqualTo(220).Within(3), "A3");

        using var editor = new PcSpeakerEditorViewModel("ALHIT", new PcDraft(30, new byte[10]), isNew: false, adLib);
        Assert.That(editor.FromAdLibCommand.CanExecute(null), Is.True);
        Assert.That(editor.FromAdLibOctaves, Is.EqualTo(1), "an octave up, as the games' own PC versions mostly are");
        editor.FromAdLibCommand.Execute(null);
        Assert.That(editor.Draft.Tones[0], Is.EqualTo(23), "A5: 1193180 / (880 × 60) is 22.6");
        Assert.That(editor.Status, Is.EqualTo("Made from the AdLib sound ALHIT, 1 octave up"));
        editor.FromAdLibOctaves = 0;
        editor.FromAdLibCommand.Execute(null);
        Assert.That(editor.Draft.Tones, Is.EqualTo(made.Tones));
        editor.UndoCommand.Execute(null);
        editor.UndoCommand.Execute(null);
        Assert.That(editor.Draft.Tones, Has.Length.EqualTo(10));

        using var without = Editor(1, 2);
        Assert.That(without.FromAdLibCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void Saved_It_Is_A_Pcs_File_That_Plays()
    {
        using var editor = new PcSpeakerEditorViewModel("NEWSOUND", new PcDraft(50, new byte[140]), isNew: true);
        Assert.That(editor.IsDirty, Is.False, "a new silent sound has nothing to lose");
        Assert.That(editor.Title, Is.EqualTo("PC speaker sound - NEWSOUND"));
        editor.BeginStroke();
        editor.SetTones(0, 69, _ => 45);
        editor.EndStroke("Drew");
        editor.Priority = 70;

        byte[]? saved = null;
        editor.SaveToMod = (name, pcs) => { saved = pcs; return Task.FromResult<string?>("BEEP"); };
        editor.SaveCommand.Execute(null);
        var sound = PcSound.FromFile(saved!);
        Assert.That((sound.Priority, sound.Values.Length, sound.Values[0], sound.Values[70]), Is.EqualTo(((ushort)70, 140, (byte)45, (byte)0)));
        Assert.That(editor.ToSamples().Take(44100 / 2).Any(sample => sample != 0), Is.True);
        Assert.That(editor.ToSamples().Skip(44100 / 2 + 400).Take(1000).All(sample => sample == 0), Is.True);
        Assert.That((editor.Title, editor.IsDirty), Is.EqualTo(("PC speaker sound - BEEP", false)));
    }
}
