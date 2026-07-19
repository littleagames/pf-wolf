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

        var types = new List<Type> { typeof(PcSound), typeof(AdLibSound), typeof(Wolf3dDigitizedAudio), typeof(Wolf3dImfAudio) };
        int currentType = 0;

        using (FileStream fs = File.OpenRead(fname))
        using (BinaryReader br = new BinaryReader(fs))
        {
            for (int i = 0, offset = 0; i < audiostarts.Length-1; i++, offset++)
            {
                int pos = audiostarts[i];
                int size = audiostarts[i + 1] - pos;
                fs.Seek(pos, SeekOrigin.Begin);
                var data = br.ReadBytes(size);
                const string tag = "!ID!";
                switch (currentType)
                {
                    case 0:
                        assets.Add($"PC:{AudioMappings.SoundKeys[offset]}".ToLowerInvariant(), new PcSound(data));
                        break;
                    case 1:
                        assets.Add($"ADLIB:{AudioMappings.SoundKeys[offset]}".ToLowerInvariant(), new AdLibSound { RawData = data });
                        break;
                    case 2:
                        assets.Add($"DIGI:{AudioMappings.SoundKeys[offset]}".ToLowerInvariant(), new Wolf3dDigitizedAudio { RawData = data });
                        break;
                    case 3:
                        assets.Add(AudioMappings.MusicKeys[offset].ToLowerInvariant(), new Wolf3dImfAudio { RawData = data });
                        break;
                }

                if (offset >= AudioMappings.SoundKeys.Count - 1)
                {
                    currentType++;
                    offset = -1;
                }
                // TODO: Store
                // ahould be stored pc, adlib, digi, imf
                // or do i need to validate the data?
            }
        }
    }

    public Dictionary<string, Asset> GetAssets()
    {
        // TODO: Return list of Sound objects created from the audio data read in the constructor
        return assets;
    }
}
