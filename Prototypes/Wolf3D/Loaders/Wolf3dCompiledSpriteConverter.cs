using Wolf3D.Assets;

namespace Wolf3D.Loaders;

/// <summary>
/// Converts a raw VSWAP "compiled shape" sprite (a per-column offset table
/// pointing at run-length opaque pixel spans, id Software's classic format)
/// into the universal indexed-bitmap SpriteAsset the renderer now expects.
/// </summary>
internal static class Wolf3dCompiledSpriteConverter
{
    internal static SpriteAsset Convert(byte[] rawData)
    {
        var shape = new compshape_t(rawData);
        int size = Program.TEXTURESIZE;

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
