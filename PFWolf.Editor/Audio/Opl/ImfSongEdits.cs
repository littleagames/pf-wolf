using PFWolf.Assets.Sounds;

namespace PFWolf.Editor.Audio.Opl;

/// <summary>
/// The changes the song editor makes to an <see cref="ImfSong"/>, in place. A channel plays one
/// note at a time, so a note put where another sounds on its channel cuts that one short (or
/// takes it out, if it would start inside the new one). A note's pitch changes go where it goes.
/// </summary>
public static class ImfSongEdits
{
    private static readonly string[] NoteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    /// <summary>A MIDI note's name: 60 is C4, 69 A4</summary>
    public static string NoteName(int midiNote) => $"{NoteNames[((midiNote % 12) + 12) % 12]}{midiNote / 12 - 1}";

    /// <summary>The instrument a channel has at a tick (the last change at or before it); null before any</summary>
    public static int? InstrumentAt(ImfSong song, int channel, int tick)
        => song.PatchChanges.Where(change => change.Channel == channel && change.Tick <= tick).OrderBy(change => change.Tick).LastOrDefault()?.Instrument;

    /// <summary>The note playing on a channel at a tick, if any</summary>
    public static ImfNote? NoteAt(ImfSong song, int channel, int tick)
        => song.Notes.FirstOrDefault(note => note.Channel == channel && note.Start <= tick && tick < (note.End ?? int.MaxValue));

    /// <summary>
    /// Puts a note in, cutting short or taking out what it lands on. A channel with no instrument
    /// by then is given <paramref name="instrument"/> from the start. The song grows to hold it.
    /// </summary>
    public static ImfNote AddNote(ImfSong song, ImfNote note, int instrument)
    {
        note = note with { Start = Math.Max(0, note.Start), Length = Math.Max(1, note.Length ?? 1) };
        ClearFor(song, note);
        if (InstrumentAt(song, note.Channel, note.Start) == null)
            song.PatchChanges.Add(new ImfPatchChange(0, note.Channel, instrument));
        song.Notes.Add(note);
        Grow(song, note);
        return note;
    }

    /// <summary>Takes notes out, with their pitch changes</summary>
    public static void RemoveNotes(ImfSong song, IEnumerable<ImfNote> notes)
    {
        foreach (var note in notes.ToList())
        {
            if (!song.Notes.Remove(note))
                continue;
            var bends = song.PitchChangesOf(note).ToList();
            song.PitchChanges.RemoveAll(bends.Contains);
        }
    }

    /// <summary>
    /// Moves notes in time and pitch (and to another channel, when given), their pitch changes
    /// with them; returns the notes as they are now, in the same order
    /// </summary>
    public static List<ImfNote> MoveNotes(ImfSong song, IReadOnlyList<ImfNote> notes, int ticks, int semitones, int? channel = null)
    {
        var moving = notes.Where(song.Notes.Contains).ToList();
        // Never before the start
        ticks = Math.Max(ticks, moving.Count > 0 ? -moving.Min(note => note.Start) : 0);
        var bends = moving.ToDictionary(note => note, note => song.PitchChangesOf(note).ToList());
        RemoveNotes(song, moving);

        var moved = new List<ImfNote>();
        foreach (var note in moving)
        {
            var (fNumber, block) = Transpose(note.FNumber, note.Block, semitones);
            var target = channel ?? note.Channel;
            var now = note with { Start = note.Start + ticks, FNumber = fNumber, Block = block, Channel = target };
            ClearFor(song, now);
            song.Notes.Add(now);
            foreach (var bend in bends[note])
            {
                var (bendNumber, bendBlock) = Transpose(bend.FNumber, bend.Block, semitones);
                song.PitchChanges.Add(new ImfPitchChange(bend.Tick + ticks, target, bendNumber, bendBlock));
            }
            if (channel is { } to && InstrumentAt(song, to, now.Start) == null && InstrumentAt(song, note.Channel, note.Start) is { } instrument)
                song.PatchChanges.Add(new ImfPatchChange(0, to, instrument));
            Grow(song, now);
            moved.Add(now);
        }
        return moved;
    }

    /// <summary>A note made longer or shorter (at least a tick, never into the next note on its channel)</summary>
    public static ImfNote Resize(ImfSong song, ImfNote note, int length)
    {
        if (!song.Notes.Contains(note))
            return note;
        var next = song.Notes.Where(other => other.Channel == note.Channel && other.Start > note.Start).Select(other => other.Start).DefaultIfEmpty(int.MaxValue).Min();
        length = Math.Clamp(length, 1, next - note.Start);
        var bends = song.PitchChangesOf(note).ToList();
        var resized = note with { Length = length };
        song.Notes[song.Notes.IndexOf(note)] = resized;
        song.PitchChanges.RemoveAll(bend => bends.Contains(bend) && bend.Tick >= resized.End);
        Grow(song, resized);
        return resized;
    }

    /// <summary>Notes given a volume (the carrier's 0x40 register); returns them as they are now</summary>
    public static List<ImfNote> SetVolume(ImfSong song, IReadOnlyList<ImfNote> notes, byte level)
        => Replace(song, notes, note => note with { Volume = level });

    /// <summary>
    /// A note played with an instrument: the channel changes to it as the note starts, and back to
    /// what it had for the next note on the channel (unless that one has a change of its own)
    /// </summary>
    public static void SetInstrument(ImfSong song, ImfNote note, int instrument)
    {
        if (!song.Notes.Contains(note) || InstrumentAt(song, note.Channel, note.Start) == instrument)
            return;
        var next = song.Notes.Where(other => other.Channel == note.Channel && other.Start > note.Start).OrderBy(other => other.Start).FirstOrDefault();
        if (next != null && !song.PatchChanges.Any(change => change.Channel == next.Channel && change.Tick > note.Start && change.Tick <= next.Start)
            && InstrumentAt(song, next.Channel, next.Start) is { } before)
            song.PatchChanges.Add(new ImfPatchChange(next.Start, next.Channel, before));
        song.PatchChanges.RemoveAll(change => change.Channel == note.Channel && change.Tick == note.Start);
        song.PatchChanges.Add(new ImfPatchChange(note.Start, note.Channel, instrument));
        song.PatchChanges.Sort((a, b) => a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : a.Channel.CompareTo(b.Channel));
    }

    /// <summary>An instrument of the song changed, wherever it's used</summary>
    public static void ReplaceInstrument(ImfSong song, int index, OplInstrument instrument)
    {
        if (index >= 0 && index < song.Instruments.Count)
            song.Instruments[index] = instrument;
    }

    /// <summary>The F-number and block a pitch moved by some semitones is, kept in the chip's range</summary>
    public static (int FNumber, int Block) Transpose(int fNumber, int block, int semitones)
    {
        if (semitones == 0)
            return (fNumber, block);
        var frequency = OplPitch.Frequency(fNumber, block) * Math.Pow(2, semitones / 12.0);
        return OplPitch.FromFrequency(frequency) ?? (semitones > 0 ? (1023, 7) : (1, 0));
    }

    /// <summary>A note of a MIDI note number, with the chip's nearest pitch to it</summary>
    public static ImfNote NoteOf(int channel, int start, int length, int midiNote, byte? volume = null)
    {
        var (fNumber, block) = OplPitch.FromMidiNote(midiNote) ?? (1023, 7);
        return new ImfNote(channel, start, length, fNumber, block, volume);
    }

    /// <summary>
    /// A grid that fits the song, in ticks a row (not a whole number of ticks, as Muse's songs
    /// weren't timed in them): the gap most often between note starts (a channel's notes start a
    /// tick or two apart, so gaps under 10 are left out, and gaps a tick or two apart are one),
    /// then fitted to every start near the grid. 17.5 (a sixteenth at 150 bpm) for a song with
    /// none.
    /// </summary>
    public static double GuessTicksPerRow(ImfSong song)
    {
        const double fallback = 17.5;
        var starts = song.Notes.Select(note => note.Start).Distinct().Order().ToList();
        var gaps = starts.Zip(starts.Skip(1), (a, b) => b - a).Where(gap => gap >= 10).ToList();
        if (gaps.Count < 4)
            return fallback;

        // The most common gap, with its neighbours a tick or two either side
        var counts = gaps.GroupBy(gap => gap).ToDictionary(group => group.Key, group => group.Count());
        int Around(double gap) => counts.Where(entry => Math.Abs(entry.Key - gap) <= 2).Sum(entry => entry.Value);
        var best = counts.Keys.OrderByDescending(gap => Around(gap)).ThenBy(gap => gap).First();
        // A half, third or quarter of it, when that's common too, is the grid's step
        double step = best;
        foreach (var divisor in new[] { 2, 3, 4 })
        {
            if (best / (double)divisor >= 10 && Around(best / (double)divisor) >= gaps.Count * 0.2)
                step = best / (double)divisor;
        }
        var near = gaps.Where(gap => Math.Abs(gap - step) <= 2).ToList();
        var period = near.Count > 0 ? near.Average() : step;

        // Fitted to the starts on (or near) the grid it makes: a least squares fit through 0
        for (var pass = 0; pass < 3; pass++)
        {
            double sk = 0, kk = 0;
            foreach (var start in starts)
            {
                var k = Math.Round(start / period);
                if (k == 0 || Math.Abs(start - k * period) > Math.Max(2, period * 0.1))
                    continue;
                sk += start * k;
                kk += k * k;
            }
            if (kk > 0)
                period = sk / kk;
        }
        return Math.Round(period, 2);
    }

    /// <summary>A tick snapped to the nearest point (or the one before, with <paramref name="floor"/>) of a grid</summary>
    public static int Snap(double tick, double ticksPerRow, bool floor = false)
    {
        var row = Math.Max(1, ticksPerRow);
        var steps = floor ? Math.Floor(tick / row + 1e-6) : Math.Round(tick / row);
        return Math.Max(0, (int)Math.Round(steps * row));
    }

    // Cuts short what sounds where a note goes on its channel, takes out what starts inside it
    private static void ClearFor(ImfSong song, ImfNote note)
    {
        var end = note.End ?? int.MaxValue;
        foreach (var other in song.Notes.Where(other => other.Channel == note.Channel).ToList())
        {
            var otherEnd = other.End ?? int.MaxValue;
            if (other.Start >= note.Start && other.Start < end)
                RemoveNotes(song, [other]);
            else if (other.Start < note.Start && otherEnd > note.Start)
                Resize(song, other, note.Start - other.Start);
        }
    }

    private static void Grow(ImfSong song, ImfNote note)
    {
        if (note.End is { } end && end > song.Length)
            song.Length = end;
    }

    private static List<ImfNote> Replace(ImfSong song, IReadOnlyList<ImfNote> notes, Func<ImfNote, ImfNote> change)
    {
        var result = new List<ImfNote>();
        foreach (var note in notes)
        {
            var index = song.Notes.IndexOf(note);
            if (index < 0)
                continue;
            song.Notes[index] = change(note);
            result.Add(song.Notes[index]);
        }
        return result;
    }
}
