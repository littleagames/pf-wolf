using PFWolf.Assets.Sounds;
using PFWolf.Editor.Audio.Opl;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Tests;

/// <summary>The song editor: its edits to a song, its playback, its view model</summary>
public class SongEditorTests
{
    private static readonly OplInstrument Piano = OplBank.BuiltIn.Melodic(0).Instrument;
    private static readonly OplInstrument Organ = OplBank.BuiltIn.Melodic(16).Instrument;

    private static ImfSong Song(params ImfNote[] notes)
    {
        var song = new ImfSong { Length = 1000 };
        song.Instruments.Add(Piano);
        song.PatchChanges.Add(new ImfPatchChange(0, 1, 0));
        song.Notes.AddRange(notes);
        return song;
    }

    private static ImfNote Note(int channel, int start, int length, int midi) => ImfSongEdits.NoteOf(channel, start, length, midi);

    [Test]
    public void Note_Names()
    {
        Assert.That(ImfSongEdits.NoteName(60), Is.EqualTo("C4"));
        Assert.That(ImfSongEdits.NoteName(69), Is.EqualTo("A4"));
        Assert.That(ImfSongEdits.NoteName(13), Is.EqualTo("C#0"));
    }

    [Test]
    public void A_Note_Put_On_Others_Cuts_Them_Short_Or_Takes_Them_Out()
    {
        var before = Note(1, 0, 100, 60);
        var inside = Note(1, 120, 20, 62);
        var after = Note(1, 300, 50, 64);
        var otherChannel = Note(2, 50, 200, 67);
        var song = Song(before, inside, after, otherChannel);

        var added = ImfSongEdits.AddNote(song, Note(1, 50, 150, 72), 0);

        Assert.That(song.Notes, Has.Count.EqualTo(4));
        Assert.That(song.Notes, Does.Contain(before with { Length = 50 }));
        Assert.That(song.Notes, Does.Not.Contain(inside));
        Assert.That(song.Notes, Does.Contain(after).And.Contain(otherChannel).And.Contain(added));
    }

    [Test]
    public void A_Channel_Without_An_Instrument_Is_Given_One_And_The_Song_Grows()
    {
        var song = Song();
        ImfSongEdits.AddNote(song, Note(5, 950, 200, 60), 0);
        Assert.That(song.PatchChanges, Does.Contain(new ImfPatchChange(0, 5, 0)));
        Assert.That(song.Length, Is.EqualTo(1150));
    }

    [Test]
    public void Moved_Notes_Take_Their_Pitch_Changes_And_Stay_After_The_Start()
    {
        var note = Note(1, 100, 100, 60);
        var song = Song(note);
        song.PitchChanges.Add(new ImfPitchChange(150, 1, note.FNumber + 20, note.Block));
        song.PitchChanges.Add(new ImfPitchChange(400, 1, 300, 4));    // another note's, after this one

        var moved = ImfSongEdits.MoveNotes(song, [note], 50, 12).Single();
        Assert.That(moved.Start, Is.EqualTo(150));
        Assert.That(moved.Frequency, Is.EqualTo(note.Frequency * 2).Within(2));
        var bend = song.PitchChangesOf(moved).Single();
        Assert.That(bend.Tick, Is.EqualTo(200));
        Assert.That(bend.Frequency, Is.EqualTo(OplPitch.Frequency(note.FNumber + 20, note.Block) * 2).Within(2));
        Assert.That(song.PitchChanges, Does.Contain(new ImfPitchChange(400, 1, 300, 4)));

        Assert.That(ImfSongEdits.MoveNotes(song, [moved], -1000, 0).Single().Start, Is.Zero);
    }

    [Test]
    public void Moved_To_Another_Channel_It_Takes_Its_Instrument()
    {
        var note = Note(1, 0, 50, 60);
        var song = Song(note);
        var moved = ImfSongEdits.MoveNotes(song, [note], 0, 0, channel: 3).Single();
        Assert.That(moved.Channel, Is.EqualTo(3));
        Assert.That(ImfSongEdits.InstrumentAt(song, 3, 0), Is.EqualTo(0));
    }

    [Test]
    public void Resized_Notes_Stop_Short_Of_The_Next_And_Lose_Pitch_Changes_Past_Their_End()
    {
        var first = Note(1, 0, 100, 60);
        var next = Note(1, 150, 50, 62);
        var song = Song(first, next);
        song.PitchChanges.Add(new ImfPitchChange(80, 1, 400, 4));

        Assert.That(ImfSongEdits.Resize(song, first, 500).Length, Is.EqualTo(150));
        var shorter = ImfSongEdits.Resize(song, song.Notes[0], 60);
        Assert.That(shorter.Length, Is.EqualTo(60));
        Assert.That(song.PitchChanges, Is.Empty);
    }

    [Test]
    public void A_Notes_Instrument_Changes_Back_For_The_Next_Note()
    {
        var a = Note(1, 0, 50, 60);
        var b = Note(1, 100, 50, 62);
        var c = Note(1, 200, 50, 64);
        var song = Song(a, b, c);
        song.Instruments.Add(Organ);

        ImfSongEdits.SetInstrument(song, b, 1);
        Assert.That(ImfSongEdits.InstrumentAt(song, 1, a.Start), Is.EqualTo(0));
        Assert.That(ImfSongEdits.InstrumentAt(song, 1, b.Start), Is.EqualTo(1));
        Assert.That(ImfSongEdits.InstrumentAt(song, 1, c.Start), Is.EqualTo(0));
    }

    [Test]
    public void The_Grid_Fits_Notes_That_Arent_On_Whole_Ticks()
    {
        // Rows of 87.6 ticks, each note a tick or two late on some channels
        var song = new ImfSong { Length = 50000 };
        var random = new Random(3);
        for (var row = 0; row < 300; row++)
        {
            if (row % 3 != 1)
                song.Notes.Add(Note(1 + row % 2, (int)Math.Round(row * 87.6) + random.Next(0, 2), 40, 60));
        }
        Assert.That(ImfSongEdits.GuessTicksPerRow(song), Is.EqualTo(87.6).Within(0.3));
        Assert.That(ImfSongEdits.GuessTicksPerRow(new ImfSong()), Is.EqualTo(17.5));
        Assert.That(ImfSongEdits.Snap(180, 87.6), Is.EqualTo(175));
        Assert.That(ImfSongEdits.Snap(260, 87.6, floor: true), Is.EqualTo(175));
    }

    [Test]
    public void An_Edited_Song_Written_And_Read_Again_Has_The_Notes_It_Was_Given()
    {
        var song = Song(Note(1, 0, 100, 60), Note(1, 200, 100, 64));
        var moved = ImfSongEdits.MoveNotes(song, [song.Notes[1]], 30, -5).Single();
        ImfSongEdits.AddNote(song, Note(2, 50, 400, 48), 0);
        ImfSongEdits.SetVolume(song, [moved], 0x10);

        var read = ImfSongReader.Read(ImfSongWriter.Write(song));
        Assert.That(read.Notes.OrderBy(note => note.Start), Is.EqualTo(song.Notes.OrderBy(note => note.Start)));
        Assert.That(read.Length, Is.EqualTo(song.Length));
    }

    [Test]
    public void Playing_From_The_Middle_Sounds_The_Note_Already_Keyed_And_Loops()
    {
        var song = Song(Note(1, 0, 700, 69));
        var commands = ImfSongWriter.Write(song);
        var source = new ImfRangeSource(commands, 350, 700);
        var stereo = new short[44100 * 2];
        // Half a second of the note, then it loops back to the middle of it
        Assert.That(source.Read(stereo, loop: true), Is.EqualTo(44100));
        Assert.That(stereo.Take(4410 * 2).Any(sample => sample != 0), Is.True, "the note started before the range sounds in it");

        var mono = ImfRangeSource.RenderMono(commands, 0, 1000);
        Assert.That(mono, Has.Length.EqualTo(1000 * 63));
        Assert.That(OplAudition.Note(Piano, 0x28B, 4, null).Any(sample => sample != 0), Is.True);
    }

    [Test]
    public void The_Editor_Undoes_And_Redoes_And_Knows_When_It_Matches_What_Was_Saved()
    {
        using var editor = new SongEditorViewModel("TEST", Song(Note(1, 0, 100, 60)), isNew: false);
        Assert.That(editor.IsDirty, Is.False);
        Assert.That(editor.TicksPerRow, Is.EqualTo(17.5));

        var added = editor.AddNote(200, 64, 50);
        Assert.That(editor.Song.Notes, Has.Count.EqualTo(2));
        Assert.That(editor.Selection, Is.EqualTo(new[] { added }));
        Assert.That(editor.IsDirty, Is.True);
        Assert.That(editor.Title, Does.EndWith("*"));

        editor.UndoCommand.Execute(null);
        Assert.That(editor.Song.Notes, Has.Count.EqualTo(1));
        Assert.That(editor.IsDirty, Is.False, "back as it was opened");
        Assert.That(editor.Selection, Is.Empty);

        editor.RedoCommand.Execute(null);
        Assert.That(editor.Song.Notes, Has.Count.EqualTo(2));
        Assert.That(editor.IsDirty, Is.True);
    }

    [Test]
    public void The_Editor_Moves_Deletes_And_Sets_Volumes_Of_What_Is_Picked()
    {
        using var editor = new SongEditorViewModel("TEST", Song(Note(1, 0, 100, 60), Note(1, 200, 100, 62)), isNew: false);
        editor.Selection = [editor.Song.Notes[1]];
        Assert.That(editor.SelectionText, Does.StartWith("D4 on channel 1, from tick 200"));

        editor.MoveSelection(0, 2);
        Assert.That(editor.Selection.Single().NearestNote.Note, Is.EqualTo(64));
        editor.SelectionVolume = 40;
        Assert.That(editor.Selection.Single().Volume! & 0x3F, Is.EqualTo(63 - 40));

        // Moved onto the first note's time on its channel: it takes the first one out, and says so
        editor.MoveSelection(-200, 0);
        Assert.That(editor.Song.Notes, Has.Count.EqualTo(1));
        Assert.That(editor.Status, Does.Contain("took out 1 note"));

        editor.DeleteSelectionCommand.Execute(null);
        Assert.That(editor.Song.Notes, Is.Empty);
        Assert.That(editor.Selection, Is.Empty);
    }

    [Test]
    public void Instrument_Edits_Close_Together_Are_One_Undo_Step()
    {
        using var editor = new SongEditorViewModel("TEST", Song(Note(1, 0, 100, 60)), isNew: false);
        editor.InstrumentEditor.Carrier.Attack = 3;
        editor.InstrumentEditor.Carrier.Release = 9;
        editor.InstrumentEditor.Feedback = 5;
        Assert.That(editor.Song.Instruments[0].Carrier.AttackDecay >> 4, Is.EqualTo(3));
        Assert.That(editor.Song.Instruments[0].Carrier.SustainRelease & 15, Is.EqualTo(9));
        Assert.That((editor.Song.Instruments[0].FeedbackConnection >> 1) & 7, Is.EqualTo(5));

        editor.UndoCommand.Execute(null);
        Assert.That(editor.Song.Instruments[0], Is.EqualTo(Piano));
        Assert.That(editor.InstrumentEditor.ToInstrument(), Is.EqualTo(Piano), "the panel shows the instrument as it is again");
    }

    [Test]
    public void Saving_Names_The_Song_And_Clears_The_Changes()
    {
        using var editor = new SongEditorViewModel("NEWSONG", Song(), isNew: true);
        Assert.That(editor.IsDirty, Is.False, "an empty new song has nothing to lose");
        editor.AddNote(0, 60, 70);

        (string Name, byte[] Imf)? saved = null;
        editor.SaveToMod = (name, imf) =>
        {
            saved = (name, imf);
            return Task.FromResult<string?>("MYTUNE");
        };
        editor.SaveCommand.Execute(null);
        Assert.That(saved?.Name, Is.EqualTo("NEWSONG"));
        Assert.That(ImfSongReader.Read(Wolf3dImfAudio.FromFile(saved!.Value.Imf)).Notes, Has.Count.EqualTo(1));
        Assert.That(editor.Name, Is.EqualTo("MYTUNE"));
        Assert.That(editor.IsDirty, Is.False);
        Assert.That(editor.Title, Is.EqualTo("Song editor - MYTUNE"));

        using var imported = new SongEditorViewModel("IMPORTED", Song(Note(1, 0, 100, 60)), isNew: true);
        Assert.That(imported.IsDirty, Is.True, "an imported song isn't in a mod yet");
    }

    [Test]
    public void Muted_Channels_Are_Shown_Dim_And_Solo_Wins()
    {
        using var editor = new SongEditorViewModel("TEST", Song(Note(1, 0, 100, 60), Note(2, 0, 100, 64)), isNew: false);
        editor.Channels[1].IsMuted = true;
        Assert.That((editor.IsShown(1), editor.IsShown(2)), Is.EqualTo((false, true)));
        editor.Channels[1].IsSolo = true;
        Assert.That((editor.IsShown(1), editor.IsShown(2)), Is.EqualTo((true, false)));
        editor.SelectAllCommand.Execute(null);
        Assert.That(editor.Selection.Single().Channel, Is.EqualTo(1));
    }

    [Test]
    public void Raw_View_Names_The_Registers()
    {
        Assert.That(SongEditorViewModel.RegisterName(0x43), Is.EqualTo("channel 0 carrier key scale/level"));
        Assert.That(SongEditorViewModel.RegisterName(0xB4), Is.EqualTo("channel 4 key on/block/F-number (high)"));
        Assert.That(SongEditorViewModel.RegisterName(0xBD), Does.Contain("rhythm"));
        using var editor = new SongEditorViewModel("TEST", Song(Note(1, 0, 100, 60)), isNew: false);
        Assert.That(editor.CommandLines(), Has.Count.EqualTo(ImfSongWriter.Write(editor.Song).Count));
    }
}
