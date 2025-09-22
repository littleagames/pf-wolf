using PFWolf.Common.Assets;

namespace PFWolf.Common.Loaders;

public class PngGraphicDataLoader
{
    public static Graphic Load(byte[] rawData)
    {
        // PNG to graphic data
        return new Graphic
        {
            // Offset
            Dimensions = new Vector2
            {
                X = 0, // Width
                Y = 0 // Height
            },
            Data = []
        };
    }
}
