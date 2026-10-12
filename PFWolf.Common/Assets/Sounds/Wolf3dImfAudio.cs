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
