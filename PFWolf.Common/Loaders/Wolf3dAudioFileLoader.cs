using System.Text;
using System.Xml.Linq;
using PFWolf.Assets;
using PFWolf.Assets.Sounds;

namespace PFWolf.Loaders;

public class Wolf3dAudioFileLoader
{
    internal int[] audiostarts;
    private readonly string audioDataFileName;

    private Dictionary<string, Asset> assets = new Dictionary<string, Asset>();

    public Wolf3dAudioFileLoader(string dataFileName, string headerFileName)
    {
        audioDataFileName = dataFileName;
        var audioHeaderFileName = headerFileName;

        if (!File.Exists(audioDataFileName))
            throw new FileNotFoundException("File not found", audioDataFileName);

        if (!File.Exists(audioHeaderFileName))
            throw new FileNotFoundException("File not found", audioHeaderFileName);

        using (FileStream fs = File.OpenRead(audioHeaderFileName))
        using (BinaryReader br = new BinaryReader(fs))
        {
            var count = fs.Length / sizeof(int);
            audiostarts = new int[count];
            for (int i = 0; i < count; i++)
            {
                audiostarts[i] = br.ReadInt32();
            }
        }
    }

    /// <param name="warn">Told when the file has a different number of sounds or songs than the names</param>
    public Dictionary<string, Asset> GetAssets(List<string> audioDataMap, List<string> musicDataMap, Action<string>? warn = null)
    {

        if (!File.Exists(audioDataFileName))
            throw new FileNotFoundException("File not found", audioDataFileName);

        using (FileStream fs = File.OpenRead(audioDataFileName))
        using (BinaryReader br = new BinaryReader(fs))
        {
            const string tag = "!ID!";
            var tagBytes = Encoding.ASCII.GetBytes(tag);
            bool IsTag(int chunk, out byte[] data)
            {
                int pos = audiostarts[chunk];
                int size = audiostarts[chunk + 1] - pos;
                data = [];
                if (size <= 0 || pos < 0 || pos + size > fs.Length)
                    return false;
                fs.Seek(pos, SeekOrigin.Begin);
                data = br.ReadBytes(size);
                return size == tagBytes.Length && data.SequenceEqual(tagBytes);
            }

            // How many chunks each block has, to name the ones past the map's names
            var blockCounts = new List<int> { 0 };
            for (int i = 0; i < audiostarts.Length - 1; i++)
            {
                if (IsTag(i, out _))
                    blockCounts.Add(0);
                else
                    blockCounts[^1]++;
            }

            var soundNames = new SoundNames(audioDataMap, blockCounts[0]);
            var musicNames = new DataMapNames(musicDataMap, "songs", "MUSIC");

            int block = 0;
            for (int i = 0, assetIndex = 0; i < audiostarts.Length - 1; i++, assetIndex++)
            {
                //var currentType = i / AudioMappings.SoundMappingKeys.Count; // Determine this count value based on the file alone
                if (IsTag(i, out var data))
                {
                    block++;
                    assetIndex = -1;
                    continue;
                }

                if (data.Length == 0)
                {
                    continue;
                }

                if (block == 0) // audio block
                {
                    var key = soundNames.NameAt(assetIndex).ToLowerInvariant();
                    switch (key.Length >= 2 ? key[..2] : key)
                    {
                        case "pc":
                            assets[key] = new PcSound(data);
                            break;
                        case "al":
                            assets[key] = new AdLibSound(data);
                            break;
                        case "ds":
                            assets[key] = new Wolf3dDigitizedAudio { RawData = data };
                            break;
                    }
                }
                else if (block == 1) // Music block
                {
                    // A song the release leaves out (the shareware's) is a stub whose IMF length is 0,
                    // followed by the song's name: it gets no asset
                    if (data.Length >= 2 && BitConverter.ToUInt16(data, 0) == 0)
                        continue;

                    var key = musicNames.NameAt(assetIndex).ToLowerInvariant();
                    assets[key] = new Wolf3dImfAudio(data);
                }
            }

            soundNames.Warn(warn);
            musicNames.Warn(warn);
        }
        return assets;
    }

    /// <summary>
    /// Names for the sound block's chunks. It holds every sound three times over, PC speaker,
    /// AdLib, then digitized (the last of those slots being the !ID! tag), so a file with sounds
    /// added has more in each third than the map names. Each third goes by the map's names of
    /// its kind (PC*, AL*, DS*), and those past them are PCSND####, ALSND####, DSSND#### (by number
    /// within the third), so the names still say which kind they are.
    /// </summary>
    private sealed class SoundNames
    {
        private readonly (string Prefix, DataMapNames Names)[] _kinds;
        private readonly int _perKind;

        public SoundNames(List<string> names, int inFile)
        {
            _perKind = Math.Max(1, (inFile + 2) / 3);
            _kinds = [Kind("PC"), Kind("AL"), Kind("DS")];

            (string, DataMapNames) Kind(string prefix)
                => (prefix + "SND", new DataMapNames(
                    names.Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList(),
                    $"{prefix} sounds", prefix + "SND"));
        }

        public string NameAt(int index)
        {
            var kind = Math.Min(index / _perKind, 2);
            return _kinds[kind].Names.NameAt(index - kind * _perKind);
        }

        public void Warn(Action<string>? warn)
        {
            foreach (var (_, names) in _kinds)
                names.Warn(warn);
        }
    }
}
