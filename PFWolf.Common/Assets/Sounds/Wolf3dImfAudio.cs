namespace PFWolf.Assets.Sounds;

public record Wolf3dImfAudio : Asset
{
    public IReadOnlyList<WolfensteinMusicCommand> Commands { get; }

    /// <summary>An AUDIOT music chunk: a 16-bit byte length, then the commands</summary>
    public Wolf3dImfAudio(byte[] data)
    {
        RawData = data;

        using var dataReader = new BinaryReader(new MemoryStream(data));
        var byteLength = dataReader.ReadUInt16();
        if (byteLength % 4 != 0 || byteLength > data.Length - sizeof(ushort))
            throw new InvalidDataException($"Music chunk has an invalid IMF length.");
        Commands = ReadCommands(dataReader, byteLength / 4);
    }

    private Wolf3dImfAudio(byte[] data, IReadOnlyList<WolfensteinMusicCommand> commands)
    {
        RawData = data;
        Commands = commands;
    }

    /// <summary>
    /// A .imf (or .wlf) file, played at Wolf3D's 700 Hz: type 1, which starts with its byte length
    /// like an AUDIOT chunk (and may have a tag after the commands), or type 0, all commands
    /// </summary>
    public static Wolf3dImfAudio FromFile(byte[] data)
    {
        using var dataReader = new BinaryReader(new MemoryStream(data));
        var byteLength = data.Length >= sizeof(ushort) ? dataReader.ReadUInt16() : 0;
        // A type 0 file starts with a command, almost always register 0 set to 0
        var isType1 = byteLength > 0 && byteLength % 4 == 0 && byteLength <= data.Length - sizeof(ushort);
        if (!isType1)
            dataReader.BaseStream.Position = 0;
        var count = isType1 ? byteLength / 4 : data.Length / 4;
        if (count == 0)
            throw new InvalidDataException("not an IMF file: it has no commands");
        var commands = ReadCommands(dataReader, count);
        // Found at startup rather than when it comes to play
        if (commands.All(command => command.Delay == 0))
            throw new InvalidDataException("not an IMF file: its commands take no time");
        return new Wolf3dImfAudio(data, commands);
    }

    /// <summary>The most commands a type 1 file (or an AUDIOT chunk) holds: its length is 16-bit</summary>
    public const int MaxType1Commands = ushort.MaxValue / 4;

    /// <summary>
    /// A .imf file of these commands: type 1 (a 16-bit byte length first, as in AUDIOT) with a
    /// tag after the commands (0x1A, then title, composer and remarks each ending in a 0, then
    /// the program's name in 9 bytes) when it has a title; type 0 (all commands) when there are
    /// more than a type 1 file holds
    /// </summary>
    public static byte[] ToFile(IReadOnlyList<WolfensteinMusicCommand> commands, string? title = null, string? composer = null, string? remarks = null)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var isType1 = commands.Count <= MaxType1Commands;
        if (isType1)
            writer.Write((ushort)(commands.Count * 4));
        foreach (var command in commands)
        {
            writer.Write(command.Register);
            writer.Write(command.Value);
            writer.Write(command.Delay);
        }

        if (isType1 && (title ?? composer ?? remarks) != null)
        {
            writer.Write((byte)0x1A);
            foreach (var text in new[] { title, composer, remarks })
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes(text ?? ""));
                writer.Write((byte)0);
            }
            var program = new byte[9];
            System.Text.Encoding.ASCII.GetBytes("PFWolf").CopyTo(program, 0);
            writer.Write(program);
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static WolfensteinMusicCommand[] ReadCommands(BinaryReader dataReader, int count)
    {
        var commands = new WolfensteinMusicCommand[count];
        for (var index = 0; index < commands.Length; index++)
        {
            commands[index] = new WolfensteinMusicCommand(
                dataReader.ReadByte(),
                dataReader.ReadByte(),
                dataReader.ReadUInt16());
        }
        return commands;
    }

    public readonly record struct WolfensteinMusicCommand(byte Register, byte Value, ushort Delay);
}
