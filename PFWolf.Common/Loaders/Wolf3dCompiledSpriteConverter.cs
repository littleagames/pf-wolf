using PFWolf.Assets;

namespace PFWolf.Loaders;

internal struct CompiledShape
{
    public ushort LeftPix, RightPix;
    public ushort[] DataOfs;
    // table data after DataOfs[RightPix-LeftPix+1]

    public CompiledShape()
    {
        DataOfs = new ushort[64];
    }

    public CompiledShape(byte[] data)
    {
        LeftPix = BitConverter.ToUInt16(data, 0);
        RightPix = BitConverter.ToUInt16(data, 2);

        // The table holds one entry per drawn column, not a fixed 64: the
        // column commands start right after it, so a narrow sprite can be
        // shorter than 4 + 64 * 2 bytes (e.g. a 45-byte, 16-column one)
        int columns = Math.Max(0, RightPix - LeftPix + 1);
        DataOfs = new ushort[columns];
        for (int i = 0; i < columns; i++)
            DataOfs[i] = BitConverter.ToUInt16(data, 4 + i * sizeof(ushort));
    }
}

/// <summary>
/// Converts a raw VSWAP "compiled shape" sprite (a per-column offset table
/// pointing at run-length opaque pixel spans, id Software's classic format)
/// into the universal indexed-bitmap SpriteAsset the renderer now expects.
/// </summary>
public static class Wolf3dCompiledSpriteConverter
{
    // A VSWAP sprite is 64x64, as the game's textures are (GameHost.TextureSize)
    private const int SpriteSize = 64;

    internal static SpriteAsset Convert(byte[] rawData)
    {
        var shape = new CompiledShape(rawData);
        int size = SpriteSize;

        var indices = new byte[size * size];
        var mask = new byte[size * size];

        for (int column = shape.LeftPix; column <= shape.RightPix; column++)
        {
            int cmdIndex = shape.DataOfs[column - shape.LeftPix];

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
