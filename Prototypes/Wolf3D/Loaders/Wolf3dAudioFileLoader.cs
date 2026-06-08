using System;
using System.Collections.Generic;
using System.Text;

namespace Wolf3D.Loaders;

internal class Wolf3dAudioFileLoader
{
    internal int[] audiostarts;

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
            }
        }
    }
}
