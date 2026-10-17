using System;
using System.Collections.Generic;
using System.Text;

namespace PFWolf.Assets;

/// <summary>
/// A sprite as a universal indexed bitmap: RawData holds row-major palette
/// indices (Width * Height bytes), and OpacityMask marks which of those
/// pixels are actually drawn (0 = transparent, non-zero = opaque). Both
/// Wolf3D's compiled VSWAP shapes and PK3 PNGs are converted into this
/// shape at load time so the renderer only has to know one format.
/// </summary>
public record SpriteAsset : Asset
{
    public short Width { get; init; }
    public short Height { get; init; }

    /// <summary>
    /// The sprite's own offset, from a PNG's grAb chunk (as ZDoom and SLADE write it): the pixel,
    /// counted from its top-left corner, that sits on the actor's spot on the floor. Only meant
    /// when <see cref="HasOffset"/>; see <see cref="Origin"/>.
    /// </summary>
    public Point Offset { get; init; } = Point.Zero;

    /// <summary>Whether the sprite gives an offset (a PNG with a grAb chunk)</summary>
    public bool HasOffset { get; init; }

    public byte[] OpacityMask { get; init; } = [];

    /// <summary>
    /// Where one without an offset stands: a Wolf3D sprite's middle of the bottom of 64 × 64, so
    /// it's drawn 32 texels either side of the actor, its top 64 above the floor (any size)
    /// </summary>
    public const int DefaultOriginX = 32, DefaultOriginY = 64;

    /// <summary>The pixel (from the top-left corner) that sits on the actor's spot on the floor</summary>
    public (int X, int Y) Origin => HasOffset ? (Offset.X, Offset.Y) : (DefaultOriginX, DefaultOriginY);

    // Each opacity mask's first drawn row, worked out once (by the mask, so a copy made `with` a
    // new one gets its own)
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<byte[], object> TopRows = new();

    /// <summary>
    /// Its top: the first row with anything drawn (0 for a blank sprite), which translations'
    /// "below N" counts down from (SpriteTranslation)
    /// </summary>
    public int TopRow => (int)TopRows.GetValue(OpacityMask, mask =>
    {
        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i] != 0)
                return Width > 0 ? i / Width : 0;
        }
        return 0;
    });
}
