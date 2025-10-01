using PFWolf.Common.Assets;

namespace PFWolf.Common;

public abstract record Asset
{
    [Obsolete("This should be set based on the Asset type itself, and not manually set.")]
    public AssetType Type { get; init; } = AssetType.Unknown;
}