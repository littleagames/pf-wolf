using PFWolf.Assets;

namespace PFWolf.Loaders;

internal struct compshape_t
{
    public ushort leftpix, rightpix;
    public ushort[] dataofs;
    // table data after dataofs[rightpix-leftpix+1]

    public compshape_t()
    {
        dataofs = new ushort[64];
    }

    public compshape_t(byte[] data)
    {
        var offset = 0;
        leftpix = BitConverter.ToUInt16(data);
        offset += sizeof(ushort);

        rightpix = BitConverter.ToUInt16(data.Skip(offset).ToArray());
        offset += sizeof(ushort);

        dataofs = new ushort[64];
        for (int i = 0; i < 64; i++)
        {
            dataofs[i] = BitConverter.ToUInt16(data.Skip(offset).ToArray());
            offset += sizeof(ushort);
        }
    }
}

/// <summary>
/// Converts a raw VSWAP "compiled shape" sprite (a per-column offset table
/// pointing at run-length opaque pixel spans, id Software's classic format)
/// into the universal indexed-bitmap SpriteAsset the renderer now expects.
/// </summary>
public static class Wolf3dCompiledSpriteConverter
{
    // A VSWAP sprite is 64x64, as the game's textures are (Program.TEXTURESIZE)
    private const int SpriteSize = 64;

    internal static SpriteAsset Convert(byte[] rawData)
    {
        var shape = new compshape_t(rawData);
        int size = SpriteSize;

        var indices = new byte[size * size];
        var mask = new byte[size * size];

        for (int column = shape.leftpix; column <= shape.rightpix; column++)
        {
            int cmdIndex = shape.dataofs[column - shape.leftpix];

            int end;
            while ((end = BitConverter.ToInt16(rawData, cmdIndex) >> 1) != 0)
            {
                int top = BitConverter.ToInt16(rawData, cmdIndex + 2);
                int start = BitConverter.ToInt16(rawData, cmdIndex + 4) >> 1;

                for (int row = start; row < end; row++)
                {
                    int destIndex = row * size + column;
                    indices[destIndex] = rawData[top + row];
                    mask[destIndex] = 1;
                }

                cmdIndex += 6;
            }
        }

        return new SpriteAsset
        {
            RawData = indices,
            OpacityMask = mask,
            Width = (short)size,
            Height = (short)size,
        };
    }
}
