using Wolf3D.Mappers;

namespace Wolf3D;

internal partial class Program
{
    internal const int STARTPCSOUNDS = 0;
    internal static int STARTADLIBSOUNDS = AudioMappings.SoundKeys.Count;
    internal static int STARTDIGISOUNDS = (2 * AudioMappings.SoundKeys.Count);
    internal static int STARTMUSIC = (3 * AudioMappings.SoundKeys.Count);

    internal static int NUMSOUNDS = AudioMappings.SoundKeys.Count;
    internal static int NUMSNDCHUNKS = (STARTMUSIC + AudioMappings.MusicKeys.Count);
}
