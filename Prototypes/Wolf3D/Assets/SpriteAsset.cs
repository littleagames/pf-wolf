using System;
using System.Collections.Generic;
using System.Text;

namespace Wolf3D.Assets;

/// <summary>
/// A sprite as a universal indexed bitmap: RawData holds row-major palette
/// indices (Width * Height bytes), and OpacityMask marks which of those
/// pixels are actually drawn (0 = transparent, non-zero = opaque). Both
/// Wolf3D's compiled VSWAP shapes and PK3 PNGs are converted into this
/// shape at load time so the renderer only has to know one format.
/// </summary>
internal record SpriteAsset : Asset
{
    public short Width { get; init; }
    public short Height { get; init; }
    public Point Offset { get; init; } = Point.Zero;
    public byte[] OpacityMask { get; init; } = [];

    public override void Merge(Asset other)
    {
        // For now, do nothing
    }
}
