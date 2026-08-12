
using Wolf3D.Assets;

namespace Wolf3D.Loaders;

internal class PaletteDataLoader
{
    public static Palette Load(MemoryStream stream)
    {
        var paletteColors = new PaletteColor[256];
        {
            var data = stream.ToArray();
            var colors = new PaletteColor[256];
            if (data.Length != 768) // 256*3 bytes
            {
                throw new ArgumentException("Invalid SDL palette data");
            }

            for (int i = 0; i < 256; i++)
            {
                paletteColors[i] = new PaletteColor
                {
                    Red = data[(i * 3)],
                    Green = data[(i * 3) + 1],
                    Blue = data[(i * 3) + 2],
                    Alpha = 255
                };
            }
        }

        return new Palette
        {
            Colors = paletteColors
        };
    }
}
