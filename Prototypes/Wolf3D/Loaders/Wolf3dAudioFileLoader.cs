using System;
using System.Collections.Generic;
using System.Text;
using Wolf3D.Assets;
using Wolf3D.Assets.Sounds;
using Wolf3D.Managers;

namespace Wolf3D.Loaders;

internal class Wolf3dAudioFileLoader
{
    internal int[] audiostarts;
    private Sound[] audiosegs;// = new Sound[NUMSNDCHUNKS];

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
            for (int i = 0; i < audiostarts.Length-1; i++)
            {
                int pos = audiostarts[i];
                int size = audiostarts[i + 1] - pos;
                fs.Seek(pos, SeekOrigin.Begin);
                var data = br.ReadBytes(size);
                // TODO: Store
            }
        }
    }

    public Dictionary<string, Asset> GetAssets()
    {
        // TODO: Return list of Sound objects created from the audio data read in the constructor
        return [];
    }
}
