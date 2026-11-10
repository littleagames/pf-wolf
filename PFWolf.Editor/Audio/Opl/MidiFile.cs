namespace PFWolf.Editor.Audio.Opl;

/// <summary>A note in a MIDI file, from its note-on to its note-off, in seconds, with what its channel was set to as it started</summary>
public sealed record MidiNote(int Channel, int Key, int Velocity, double Start, double End, int Program, int Volume, int Expression, double Bend)
{
    public bool IsDrum => Channel == MidiFile.DrumChannel;
}

/// <summary>
/// A Standard MIDI File (type 0 or 1; type 2's tracks are read one after another as if
/// together): its notes with their times in seconds by its tempo changes, and how long it is.
/// Program changes, channel volume (CC 7), expression (CC 11) and pitch bend are kept as each
/// note starts; other controllers, aftertouch, SysEx and meta events are skipped.
/// </summary>
public sealed class MidiFile
{
    /// <summary>MIDI channel 10, where General MIDI's drums are</summary>
    public const int DrumChannel = 9;

    public int Format { get; private init; }

    /// <summary>Ticks a quarter note; null for a file timed in SMPTE frames</summary>
    public int? TicksPerQuarter { get; private init; }

    public List<MidiNote> Notes { get; } = [];

    /// <summary>Where the last track ends, in seconds (at least as late as the last note)</summary>
    public double Length { get; private set; }

    /// <summary>Pitch bends after a note started, which aren't kept</summary>
    public int BendsSkipped { get; private set; }

    public string? Title { get; private set; }

    /// <summary>
    /// Reads a MIDI file, its notes snapped to a grid of 1/<paramref name="quantizeDivisor"/>
    /// notes (16: sixteenths; 0: as they are); throws InvalidDataException with what's wrong
    /// </summary>
    public static MidiFile Read(byte[] data, int quantizeDivisor = 0)
    {
        var reader = new ChunkReader(data);
        if (reader.Tag() != "MThd")
            throw new InvalidDataException("not a MIDI file: it doesn't start with MThd");
        var headerLength = reader.UInt32();
        if (headerLength < 6)
            throw new InvalidDataException("not a MIDI file: its header is too short");
        var format = reader.UInt16();
        var trackCount = reader.UInt16();
        var division = reader.UInt16();
        reader.Skip((int)headerLength - 6);

        // SMPTE: frames a second (negative, in the high byte) by ticks a frame
        double? ticksPerSecond = (division & 0x8000) != 0 ? -(sbyte)(division >> 8) * (division & 0xFF) : null;
        int? ticksPerQuarter = ticksPerSecond == null ? division : null;
        if (ticksPerQuarter == 0 || ticksPerSecond == 0)
            throw new InvalidDataException("the MIDI file's timing (division) is 0");

        var events = new List<TimedEvent>();
        var trackOffset = 0L;
        var lastEnd = 0L;
        for (var track = 0; track < trackCount && !reader.AtEnd; track++)
        {
            var tag = reader.Tag();
            var length = (int)reader.UInt32();
            if (tag != "MTrk")
            {
                reader.Skip(length);
                track--;
                continue;
            }
            var end = ReadTrack(reader.Slice(length), track, format == 2 ? trackOffset : 0, events);
            lastEnd = Math.Max(lastEnd, end);
            if (format == 2)
                trackOffset = end;
        }

        var file = new MidiFile { Format = format, TicksPerQuarter = ticksPerQuarter };
        file.Build(events, lastEnd, ticksPerQuarter, ticksPerSecond, quantizeDivisor);
        return file;
    }

    private readonly record struct TimedEvent(long Tick, int Track, int Order, byte Status, byte Data1, byte Data2, int Tempo = 0, string? Text = null);

    // Reads a track's events with their ticks from its start (plus offset); returns the tick it ends on
    private static long ReadTrack(ChunkReader reader, int track, long offset, List<TimedEvent> events)
    {
        var tick = offset;
        byte running = 0;
        var order = 0;
        while (!reader.AtEnd)
        {
            tick += reader.VariableLength();
            var status = reader.Byte();
            byte data1;
            if (status < 0x80)
            {
                // Running status: this byte is the first data byte of the last status's event
                if (running == 0)
                    throw new InvalidDataException($"track {track + 1} has data before any status byte");
                data1 = status;
                status = running;
            }
            else if (status < 0xF0)
            {
                running = status;
                data1 = reader.Byte();
            }
            else
            {
                if (status == 0xFF)
                {
                    var type = reader.Byte();
                    var length = (int)reader.VariableLength();
                    var body = reader.Bytes(length);
                    if (type == 0x51 && length == 3)
                        events.Add(new TimedEvent(tick, track, order++, 0xFF, 0x51, 0, (body[0] << 16) | (body[1] << 8) | body[2]));
                    else if (type == 0x03 && track == 0)
                        events.Add(new TimedEvent(tick, track, order++, 0xFF, 0x03, 0, Text: System.Text.Encoding.Latin1.GetString(body).Trim()));
                    else if (type == 0x2F)
                        return tick;
                }
                else if (status is 0xF0 or 0xF7)
                    reader.Skip((int)reader.VariableLength());
                else
                    throw new InvalidDataException($"track {track + 1} has a system event 0x{status:X2} a file can't hold");
                // Meta and SysEx events cancel running status
                running = 0;
                continue;
            }

            var kind = status & 0xF0;
            var data2 = kind is 0xC0 or 0xD0 ? (byte)0 : reader.Byte();
            events.Add(new TimedEvent(tick, track, order++, status, (byte)(data1 & 0x7F), (byte)(data2 & 0x7F)));
        }
        return tick;
    }

    private void Build(List<TimedEvent> events, long tracksEnd, int? ticksPerQuarter, double? ticksPerSecond, int quantizeDivisor)
    {
        // In time order; at one tick, as the tracks had them (note-offs ahead of note-ons, so a
        // note repeated on the same tick sounds again)
        events.Sort((a, b) => a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick)
            : IsNoteOff(a) != IsNoteOff(b) ? (IsNoteOff(a) ? -1 : 1)
            : a.Track != b.Track ? a.Track.CompareTo(b.Track) : a.Order.CompareTo(b.Order));

        // Seconds at a tick, by the tempo changes up to it (120 bpm until the first)
        var tempoTicks = new List<long> { 0 };
        var tempoSeconds = new List<double> { 0 };
        var tempos = new List<int> { 500000 };
        foreach (var e in events.Where(e => e.Status == 0xFF && e.Data1 == 0x51))
        {
            var seconds = SecondsAt(e.Tick);
            tempoTicks.Add(e.Tick);
            tempoSeconds.Add(seconds);
            tempos.Add(e.Tempo);
        }

        double SecondsAt(long tick)
        {
            if (ticksPerSecond is { } rate)
                return tick / rate;
            var index = tempoTicks.Count - 1;
            while (index > 0 && tempoTicks[index] > tick)
                index--;
            return tempoSeconds[index] + (tick - tempoTicks[index]) * tempos[index] / 1_000_000.0 / ticksPerQuarter!.Value;
        }

        // Snapped to a grid of a quarter note / divisor (16 for sixteenths); note ends too
        long Snap(long tick)
        {
            if (quantizeDivisor <= 0 || ticksPerQuarter is not { } quarter)
                return tick;
            var grid = Math.Max(1.0, quarter / (double)quantizeDivisor * 4);
            return (long)(Math.Round(tick / grid) * grid);
        }

        var program = new int[16];
        var volume = Enumerable.Repeat(100, 16).ToArray();
        var expression = Enumerable.Repeat(127, 16).ToArray();
        var bend = new double[16];
        var bendRange = Enumerable.Repeat(2.0, 16).ToArray();
        var rpn = Enumerable.Repeat(0x3FFF, 16).ToArray();
        // Notes sounding, by channel and key: their starts (the same key again stacks)
        var sounding = new Dictionary<(int, int), Stack<(long Tick, int Velocity, int Program, int Volume, int Expression, double Bend)>>();
        var lastTick = tracksEnd;

        foreach (var e in events)
        {
            lastTick = Math.Max(lastTick, e.Tick);
            if (e.Status == 0xFF)
            {
                if (e.Data1 == 0x03 && Title == null && e.Text is { Length: > 0 } title)
                    Title = title;
                continue;
            }

            var channel = e.Status & 0x0F;
            switch (e.Status & 0xF0)
            {
                case 0x90 when e.Data2 > 0:
                    if (!sounding.TryGetValue((channel, e.Data1), out var stack))
                        sounding[(channel, e.Data1)] = stack = new();
                    stack.Push((e.Tick, e.Data2, program[channel], volume[channel], expression[channel], bend[channel]));
                    break;
                case 0x80 or 0x90:
                    if (sounding.TryGetValue((channel, e.Data1), out var started) && started.Count > 0)
                        AddNote(channel, e.Data1, started.Pop(), e.Tick);
                    break;
                case 0xB0:
                    switch (e.Data1)
                    {
                        case 7: volume[channel] = e.Data2; break;
                        case 11: expression[channel] = e.Data2; break;
                        case 101: rpn[channel] = (rpn[channel] & 0x7F) | (e.Data2 << 7); break;
                        case 100: rpn[channel] = (rpn[channel] & 0x3F80) | e.Data2; break;
                        case 6 when rpn[channel] == 0: bendRange[channel] = e.Data2; break;
                        case 121:   // reset all controllers
                            volume[channel] = 100;
                            expression[channel] = 127;
                            bend[channel] = 0;
                            break;
                    }
                    break;
                case 0xC0:
                    program[channel] = e.Data1;
                    break;
                case 0xE0:
                    bend[channel] = ((e.Data2 << 7 | e.Data1) - 8192) / 8192.0 * bendRange[channel];
                    if (sounding.Any(entry => entry.Key.Item1 == channel && entry.Value.Count > 0))
                        BendsSkipped++;
                    break;
            }
        }

        // Notes never let go end where the file does
        foreach (var ((channel, key), stack) in sounding)
        {
            while (stack.Count > 0)
                AddNote(channel, key, stack.Pop(), lastTick);
        }
        Notes.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.Channel != b.Channel ? a.Channel.CompareTo(b.Channel) : a.Key.CompareTo(b.Key));
        Length = Math.Max(SecondsAt(Snap(lastTick)), Notes.Count > 0 ? Notes.Max(note => note.End) : 0);

        void AddNote(int channel, int key, (long Tick, int Velocity, int Program, int Volume, int Expression, double Bend) start, long endTick)
        {
            var (from, to) = (Snap(start.Tick), Snap(endTick));
            if (to <= from)
                to = quantizeDivisor > 0 && ticksPerQuarter is { } quarter ? from + Math.Max(1, (long)(quarter * 4.0 / quantizeDivisor)) : Math.Max(endTick, from + 1);
            Notes.Add(new MidiNote(channel, key, start.Velocity, SecondsAt(from), SecondsAt(to), start.Program, start.Volume, start.Expression, start.Bend));
        }
    }

    private static bool IsNoteOff(TimedEvent e) => (e.Status & 0xF0) == 0x80 || (e.Status & 0xF0) == 0x90 && e.Data2 == 0;

    // Big-endian reading of a MIDI file's chunks, with what went wrong said plainly
    private sealed class ChunkReader(byte[] data, int start = 0, int? end = null)
    {
        private readonly int _end = end ?? data.Length;
        private int _position = start;

        public bool AtEnd => _position >= _end;

        private void Need(int count)
        {
            if (_position + count > _end)
                throw new InvalidDataException("the MIDI file ends in the middle of something");
        }

        public byte Byte()
        {
            Need(1);
            return data[_position++];
        }

        public byte[] Bytes(int count)
        {
            Need(count);
            var bytes = data.AsSpan(_position, count).ToArray();
            _position += count;
            return bytes;
        }

        public void Skip(int count)
        {
            Need(Math.Max(count, 0));
            _position += Math.Max(count, 0);
        }

        public string Tag() => System.Text.Encoding.ASCII.GetString(Bytes(4));
        public ushort UInt16() => (ushort)(Byte() << 8 | Byte());
        public uint UInt32() => (uint)(UInt16() << 16 | UInt16());

        public long VariableLength()
        {
            long value = 0;
            for (var i = 0; i < 4; i++)
            {
                var b = Byte();
                value = value << 7 | (uint)(b & 0x7F);
                if ((b & 0x80) == 0)
                    return value;
            }
            throw new InvalidDataException("the MIDI file has a number longer than 4 bytes");
        }

        // The next count bytes as a reader of their own (a track)
        public ChunkReader Slice(int count)
        {
            Need(count);
            var slice = new ChunkReader(data, _position, _position + count);
            _position += count;
            return slice;
        }
    }
}
