using PFWolf.Common.Assets;

namespace PFWolf.Common.DataLoaders;

public class GraphicDataLoader
{
    public static Graphic Load(MemoryStream stream, Palette sourcePalette)
    {
        // Read the first few bytes to determine the texture type
        byte[] header = new byte[4];
        stream.Seek(0, SeekOrigin.Begin);
        stream.ReadExactly(header, 0, 4);

        // Check the header to determine the texture type
        if (header[0] == 0x42 && header[1] == 0x4D) // BMP
        {
            return BmpGraphicDataLoader.Load(stream);
        }
        else if (header[0] == 0x89 && header[1] == 0x50) // PNG
        {
            return PngGraphicDataLoader.Load(stream, sourcePalette);
        }
        else
        {
            //return WolfRawGraphicDataLoader.Load(stream, picNum, dimensions, huffman);
            throw new NotImplementedException();
        }
    }
}