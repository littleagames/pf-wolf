using System.Text;
using System.Xml.Linq;
using Wolf3D.Assets;
using Wolf3D.Assets.Sounds;

namespace Wolf3D.Loaders;

internal class Wolf3dAudioFileLoader
{
    internal int[] audiostarts;
    private readonly string audioDataFileName;

    private Dictionary<string, Asset> assets = new Dictionary<string, Asset>();

    public Wolf3dAudioFileLoader(
        string dataFileName,
        string dataExtension,
        string headerFileName,
        string headerExtension)
    {
        audioDataFileName = $"{dataFileName}.{dataExtension}";
        var audioHeaderFileName = $"{headerFileName}.{headerExtension}";

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

    public Dictionary<string, Asset> GetAssets(List<string> audioDataMap, List<string> musicDataMap)
    {

        if (!File.Exists(audioDataFileName))
            throw new FileNotFoundException("File not found", audioDataFileName);

        using (FileStream fs = File.OpenRead(audioDataFileName))
        using (BinaryReader br = new BinaryReader(fs))
        {
            int block = 0;
            for (int i = 0, assetIndex = 0; i < audiostarts.Length - 1; i++, assetIndex++)
            {
                //var currentType = i / AudioMappings.SoundMappingKeys.Count; // Determine this count value based on the file alone
                int pos = audiostarts[i];
                int size = audiostarts[i + 1] - pos;
                fs.Seek(pos, SeekOrigin.Begin);

                if (size == 0)
                {
                    continue;
                }

                var data = br.ReadBytes(size);
                const string tag = "!ID!";
                var tagBytes = Encoding.ASCII.GetBytes(tag);
                if (size == tagBytes.Length && data.SequenceEqual(tagBytes))
                {
                    block++;
                    assetIndex = -1;
                    continue;
                }

                if (block == 0) // audio block
                {
                    var key = audioDataMap[assetIndex].ToLowerInvariant();
                    switch (key.Substring(0, 2).ToLowerInvariant())
                    {
                        case "pc":
                            assets.Add(key, new PcSound(data));
                            break;
                        case "al":
                            assets.Add(key, new AdLibSound(data));
                            break;
                        case "ds":
                            assets.Add(key, new Wolf3dDigitizedAudio { RawData = data });
                            break;
                    }
                }
                else if (block == 1) // Music block
                {
                    var key = musicDataMap[assetIndex].ToLowerInvariant();
                    assets.Add(key, new Wolf3dImfAudio(data));
                }
            }
        }
        return assets;
    }
}
