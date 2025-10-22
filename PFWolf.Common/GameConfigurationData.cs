using PFWolf.Common.Assets;

namespace PFWolf.Common;

public record GameConfigurationData
{
    public Dimension ScreenResolution { get; set; }
    public Palette DefaultPalette { get; set; } = null!;
}
