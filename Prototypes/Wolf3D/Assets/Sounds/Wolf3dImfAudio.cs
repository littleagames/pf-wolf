using System.Resources;

namespace Wolf3D.Assets.Sounds;

internal record Wolf3dImfAudio : Asset
{
    public IReadOnlyList<WolfensteinMusicCommand> Commands { get; }

    public Wolf3dImfAudio(byte[] data)
    {
        RawData = data;

        using var dataReader = new BinaryReader(new MemoryStream(data));
        var byteLength = dataReader.ReadUInt16();
        if (byteLength % 4 != 0 || byteLength > data.Length - sizeof(ushort))
            throw new InvalidDataException($"Music chunk has an invalid IMF length.");
        var commands = new WolfensteinMusicCommand[byteLength / 4];
        for (var index = 0; index < commands.Length; index++)
        {
            commands[index] = new WolfensteinMusicCommand(
                dataReader.ReadByte(),
                dataReader.ReadByte(),
                dataReader.ReadUInt16());
        }

        Commands = commands;
    }
    public readonly record struct WolfensteinMusicCommand(byte Register, byte Value, ushort Delay);
}
