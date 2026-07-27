using System;
using System.Collections.Generic;
using System.Text;
using Wolf3D.Assets;
using Wolf3D.Assets.Sounds;
using Wolf3D.Managers;
using Wolf3D.Mappers;
using YamlDotNet.Core.Tokens;

namespace Wolf3D.Loaders;

internal class Wolf3dAudioFileLoader
{
    internal int[] audiostarts;
    //private Sound[] audiosegs;// = new Sound[NUMSNDCHUNKS];
    private Dictionary<string, Asset> assets = new Dictionary<string, Asset>();

    public Wolf3dAudioFileLoader(
        string dataFileName,
        string dataExtension,
        string headerFileName,
        string headerExtension)
    {
        string fname = $"{dataFileName}.{dataExtension}";
        string hname = $"{headerFileName}.{headerExtension}";

        if (!File.Exists(fname))
            throw new FileNotFoundException("File not found", fname);

        if (!File.Exists(hname))
            throw new FileNotFoundException("File not found", hname);

        using (FileStream fs = File.OpenRead(hname))
        using (BinaryReader br = new BinaryReader(fs))
        {
            var count = fs.Length / sizeof(int);
            audiostarts = new int[count];
            for (int i = 0; i < count; i++)
            {
                audiostarts[i] = br.ReadInt32();
            }
        }

        using (FileStream fs = File.OpenRead(fname))
        using (BinaryReader br = new BinaryReader(fs))
        {
            for (int i = 0, assetIndex = 0; i < audiostarts.Length-1; i++)
            {
                var currentType = i / AudioMappings.SoundMappingKeys.Count;
                int pos = audiostarts[i];
                int size = audiostarts[i + 1] - pos;
                fs.Seek(pos, SeekOrigin.Begin);

                if (size == 0)
                {
                    assetIndex++;
                    continue;
                }

                var data = br.ReadBytes(size);
                const string tag = "!ID!";
                var key = AudioMappings.AudioTKeys[assetIndex].ToLowerInvariant();
                var tagBytes = Encoding.ASCII.GetBytes(tag);
                if (size == tagBytes.Length && data.SequenceEqual(tagBytes))
                {
                    assetIndex++;
                    continue;
                }

                if (string.IsNullOrEmpty(key))
                {
                    assetIndex++;
                    continue;
                }

                switch (currentType)
                {
                    case 0:
                        assets.Add(key, new PcSound(data));
                        break;
                    case 1:
                        assets.Add(key, new AdLibSound { RawData = data });
                        break;
                    case 2:
                        assets.Add(key, new Wolf3dDigitizedAudio { RawData = data });
                        break;
                    case 3:
                        assets.Add(key, new Wolf3dImfAudio { RawData = data });
                        break;
                }

                assetIndex++;
            }
        }
    }

    public Dictionary<string, Asset> GetAssets()
    {
        // TODO: Return list of Sound objects created from the audio data read in the constructor
        return assets;
    }
}
