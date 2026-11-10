using PFWolf.Assets.Sounds;
using Command = PFWolf.Assets.Sounds.Wolf3dImfAudio.WolfensteinMusicCommand;

namespace PFWolf.Editor.Audio.Opl;

/// <summary>
/// Writes an <see cref="ImfSong"/> as IMF commands. In each tick: notes ending are keyed off,
/// then the song's other writes go out in order, then instrument changes (only the registers
/// that differ, except at tick 0, where everything is written so the song starts the same each
/// time it loops), volume changes, and last the notes starting (volume, A, then B keyed on).
/// </summary>
public static class ImfSongWriter
{
    public static List<Command> Write(ImfSong song)
    {
        var byTick = new SortedDictionary<int, TickEvents>();
        TickEvents At(int tick)
        {
            if (!byTick.TryGetValue(tick, out var events))
                byTick[tick] = events = new TickEvents();
            return events;
        }

        foreach (var note in song.Notes)
        {
            At(note.Start).Starts.Add(note);
            if (note.End is { } end && end <= song.Length)
                At(end).Ends.Add(note);
        }
        foreach (var write in song.Writes)
            At(write.Tick).Writes.Add(write);
        foreach (var change in song.PatchChanges)
            At(change.Tick).Patches.Add(change);
        foreach (var change in song.VolumeChanges)
            At(change.Tick).Volumes.Add(change);
        foreach (var change in song.PitchChanges)
            At(change.Tick).Pitches.Add(change);

        var registers = new byte[256];
        var commands = new List<Command>();
        var pending = new List<(byte Register, byte Value)>();
        if (!byTick.ContainsKey(0))
            At(0);

        var ticks = byTick.Keys.ToList();
        for (var t = 0; t < ticks.Count; t++)
        {
            var tick = ticks[t];
            var events = byTick[tick];
            var all = tick == 0;
            pending.Clear();

            void Put(byte register, byte value, bool always = true)
            {
                if (always || registers[register] != value)
                    pending.Add((register, value));
                registers[register] = value;
            }

            // Keyed off at the pitch it's sounding at (a pitch change may have moved it)
            foreach (var note in events.Ends.OrderBy(note => note.Channel))
                Put((byte)(0xB0 + note.Channel), (byte)(registers[0xB0 + note.Channel] & ~OplRegisters.KeyOn));
            foreach (var write in events.Writes)
                Put(write.Register, write.Value);
            foreach (var change in events.Pitches)
            {
                Put((byte)(0xA0 + change.Channel), (byte)(change.FNumber & 0xFF), always: false);
                Put((byte)(0xB0 + change.Channel), (byte)(OplRegisters.KeyOn | ((change.Block & 7) << 2) | ((change.FNumber >> 8) & 3)), always: false);
            }
            foreach (var change in events.Patches)
            {
                if (change.Instrument < 0 || change.Instrument >= song.Instruments.Count)
                    continue;
                foreach (var (register, value) in song.Instruments[change.Instrument].Registers(change.Channel))
                    Put(register, value, always: all);
            }
            foreach (var change in events.Volumes)
                Put(OplRegisters.CarrierLevel(change.Channel), change.Level);
            foreach (var note in events.Starts.OrderBy(note => note.Channel))
            {
                if (note.Volume is { } volume)
                    Put(OplRegisters.CarrierLevel(note.Channel), volume, always: all);
                Put((byte)(0xA0 + note.Channel), note.FNumberLow, always: all);
                Put((byte)(0xB0 + note.Channel), note.KeyOnValue);
            }

            // A tick has to have a write to take its time: register 0 is the chip's test register,
            // which songs set to 0 as they start
            if (pending.Count == 0)
                pending.Add((0x00, registers[0x00]));

            var next = t + 1 < ticks.Count ? ticks[t + 1] : Math.Max(song.Length, tick);
            var delay = next - tick;
            for (var i = 0; i < pending.Count; i++)
            {
                var (register, value) = pending[i];
                var wait = i == pending.Count - 1 ? Math.Min(delay, ushort.MaxValue) : 0;
                commands.Add(new Command(register, value, (ushort)wait));
            }
            // Longer than a delay holds
            for (delay -= ushort.MaxValue; delay > 0; delay -= ushort.MaxValue)
                commands.Add(new Command(0x00, registers[0x00], (ushort)Math.Min(delay, ushort.MaxValue)));
        }
        return commands;
    }

    private sealed class TickEvents
    {
        public List<ImfNote> Starts { get; } = [];
        public List<ImfNote> Ends { get; } = [];
        public List<ImfWrite> Writes { get; } = [];
        public List<ImfPatchChange> Patches { get; } = [];
        public List<ImfVolumeChange> Volumes { get; } = [];
        public List<ImfPitchChange> Pitches { get; } = [];
    }
}
