using PFWolf.Common.Assets;

namespace PFWolf.Common;

public abstract record Asset
{
    public string Name { get; init; } = null!;
    public AssetType Type { get; init; } = AssetType.Unknown;
}