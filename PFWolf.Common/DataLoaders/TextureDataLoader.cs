using PFWolf.Common.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PFWolf.Common.DataLoaders;

public class TextureDataLoader
{
    public static Texture Load(Stream stream)
    {
        // Read the first few bytes to determine the texture type
        byte[] header = new byte[4];
        stream.ReadExactly(header, 0, 4);

        // Check the header to determine the texture type
        if (header[0] == 0x42 && header[1] == 0x4D) // BMP
        {
            throw new NotImplementedException();
            //return new BmpTextureDataLoader(stream);
        }
        else if (header[0] == 0x89 && header[1] == 0x50) // PNG
        {
            return PngTextureDataLoader.Load(stream);
        }
        else
        {
            //return new WolfRawTextureDataLoader(stream);
            throw new NotImplementedException();
        }
    }
}