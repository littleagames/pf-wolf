using PFWolf.Assets.Sounds;
using Command = PFWolf.Assets.Sounds.Wolf3dImfAudio.WolfensteinMusicCommand;

namespace PFWolf.Editor.Audio.Opl;

/// <summary>
/// Reads IMF commands into an <see cref="ImfSong"/>, a tick at a time (the commands up to one
/// with a delay all land in one tick):
/// <list type="bullet">
/// <item>A channel keyed on in a tick (from off, or off and on again) starts a note, its pitch
/// what A and B hold at the tick's end; one sounding is ended then.</item>
/// <item>Keyed off ends the note sounding: the B write that does it is the note's end when it
/// keeps the note's pitch and is the channel's last B write in the tick.</item>
/// <item>Operator or C0 writes on a channel are a change of instrument (to what the channel has
/// at the tick's end); a carrier level written alone is the note's volume when one starts, else
/// a volume change.</item>
/// <item>Anything else is kept as a write: other registers, and A/B writes that don't start or
/// end a note (a pitch set ahead of time, a bend, a tick that keys on and off again).</item>
/// </list>
/// </summary>
public static class ImfSongReader
{
    public static ImfSong Read(Wolf3dImfAudio audio) => Read(audio.Commands);

    public static ImfSong Read(IReadOnlyList<Command> commands)
    {
        var song = new ImfSong();
        var state = new ReadState(song);
        var tick = 0;
        var index = 0;
        while (index < commands.Count)
        {
            var last = index;
            while (last < commands.Count - 1 && commands[last].Delay == 0)
                last++;
            state.Tick(tick, commands, index, last);
            tick += commands[last].Delay;
            index = last + 1;
        }
        song.Length = tick;
        return song;
    }

    private enum KeyChange
    {
        /// <summary>The key isn't touched (A/B writes there may be are kept as writes)</summary>
        None,
        /// <summary>Keyed off</summary>
        Off,
        /// <summary>Keyed on (from off, or off and on again) and left on: a note starts</summary>
        Strike,
        /// <summary>Anything stranger (on and off again in the tick): the A/B writes are kept as they are</summary>
        Raw,
    }

    private sealed class ReadState(ImfSong song)
    {
        private readonly byte[] _registers = new byte[256];
        private readonly bool[] _keyOn = new bool[OplRegisters.Channels];
        // The note sounding on each channel (its index in song.Notes), when it's one read as a note
        private readonly int?[] _sounding = new int?[OplRegisters.Channels];

        public void Tick(int tick, IReadOnlyList<Command> commands, int first, int last)
        {
            var changes = new KeyChange[OplRegisters.Channels];
            var endWrite = new int?[OplRegisters.Channels];   // the B write that ends the note sounding
            var wasOn = (bool[])_keyOn.Clone();

            // How each channel's key changes over the tick
            for (var channel = 0; channel < OplRegisters.Channels; channel++)
            {
                var key = _keyOn[channel];
                bool struck = false, offAfterStrike = false, turnedOff = false;
                int? lastB = null, offWrite = null;
                byte finalB = _registers[0xB0 + channel];
                for (var i = first; i <= last; i++)
                {
                    if (commands[i].Register != 0xB0 + channel)
                        continue;
                    var on = (commands[i].Value & OplRegisters.KeyOn) != 0;
                    if (!key && on)
                        struck = true;
                    if (key && !on)
                    {
                        turnedOff = true;
                        offWrite ??= i;
                        if (struck)
                            offAfterStrike = true;
                    }
                    key = on;
                    lastB = i;
                    finalB = commands[i].Value;
                }

                if (struck)
                    changes[channel] = key && !offAfterStrike && (finalB & 0xC0) == 0 ? KeyChange.Strike : KeyChange.Raw;
                else if (turnedOff)
                {
                    changes[channel] = KeyChange.Off;
                    // The note's end when it keeps the note's pitch and nothing keys it again after
                    if (_sounding[channel] is { } sounding && offWrite is { } off && off == lastB
                        && commands[off].Value ==(song.Notes[sounding].KeyOnValue & ~OplRegisters.KeyOn))
                        endWrite[channel] = offWrite;
                }
                _keyOn[channel] = key;
            }

            // The writes, in order: what isn't read as notes, instruments or volumes is kept
            var patched = new bool[OplRegisters.Channels];
            var leveled = new bool[OplRegisters.Channels];
            for (var i = first; i <= last; i++)
            {
                var (register, value, _) = commands[i];
                _registers[register] = value;

                if ((OplRegisters.ChannelOf(register, 0xA0) ?? OplRegisters.ChannelOf(register, 0xB0)) is { } pitchChannel)
                {
                    var absorbed = changes[pitchChannel] == KeyChange.Strike || endWrite[pitchChannel] == i;
                    if (!absorbed)
                        song.Writes.Add(new ImfWrite(tick, register, value));
                }
                else if (OplRegisters.ChannelOf(register, 0xC0) is { } feedbackChannel)
                    patched[feedbackChannel] = true;
                else if (OplRegisters.OperatorOf(register) is { } op)
                {
                    if (register == OplRegisters.CarrierLevel(op.Channel))
                        leveled[op.Channel] = true;
                    else
                        patched[op.Channel] = true;
                }
                else
                    song.Writes.Add(new ImfWrite(tick, register, value));
            }

            for (var channel = 0; channel < OplRegisters.Channels; channel++)
            {
                // A note sounding ends when the key goes off (keyed on again counts)
                if (wasOn[channel] && changes[channel] != KeyChange.None && _sounding[channel] is { } sounding)
                {
                    var note = song.Notes[sounding];
                    song.Notes[sounding] = note with { Length = tick - note.Start };
                    _sounding[channel] = null;
                }

                byte? volume = null;
                if (patched[channel])
                    song.PatchChanges.Add(new ImfPatchChange(tick, channel, song.InstrumentIndex(OplInstrument.FromRegisters(_registers, channel))));
                else if (leveled[channel])
                {
                    var level = _registers[OplRegisters.CarrierLevel(channel)];
                    if (changes[channel] == KeyChange.Strike)
                        volume = level;
                    else
                        song.VolumeChanges.Add(new ImfVolumeChange(tick, channel, level));
                }

                if (changes[channel] == KeyChange.Strike)
                {
                    var b = _registers[0xB0 + channel];
                    song.Notes.Add(new ImfNote(channel, tick, null, _registers[0xA0 + channel] | ((b & 3) << 8), (b >> 2) & 7, volume));
                    _sounding[channel] = song.Notes.Count - 1;
                }
            }
        }
    }
}
