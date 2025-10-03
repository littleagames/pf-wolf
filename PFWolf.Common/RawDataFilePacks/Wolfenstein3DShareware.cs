using PFWolf.Common.FileLoaders;

namespace PFWolf.Common.RawDataFilePacks;

public class Wolfenstein3DShareware : RawDataFilePack
{
    public const string AudioHed = "audiohed.wl1";
    public const string AudioT = "audiot.wl1";
    public const string GameMaps = "gamemaps.wl1";
    public const string MapHead = "maphead.wl1";
    public const string VgaDict = "vgadict.wl1";
    public const string VgaGraph = "vgagraph.wl1";
    public const string VgaHead = "vgahead.wl1";
    public const string Vswap = "vswap.wl1";

    public override string PackName => "wolf3d-shareware";
    public override string PackDescription => "Wolfenstein 3D v1.4 Shareware";

    protected override List<DataPackFile> Files => [
        new (AudioHed, "58aa1b9892d5adfa725fab343d9446f8"),
        new (AudioT, "4b6109e957b584e4ad7f376961f3887e"),
        new (GameMaps, "30fecd7cce6bc70402651ec922d2da3d"),
        new (MapHead, "7b6dd4e55c33c33a41d1600be5df3228"),
        new (VgaDict, "76a6128f3c0dd9b77939ce8313992746"),
        new (VgaGraph, "74decb641b1a4faed173e10ab744bff0"),
        new (VgaHead, "61bf1616e78367853c91f2c04e2c1cb7"),
        new (Vswap,  "6efa079414b817c97db779cecfb081c9")
    ];
    protected override List<DataPackFileLoader> FileLoaders =>
    [
        new(typeof(Wolf3DAudioFileLoader), AudioHed, AudioT),
        new(typeof(Wolf3DMapFileLoader), MapHead, GameMaps),
        new(typeof(Wolf3DVgaFileLoader), VgaDict, VgaHead, VgaGraph),
        new(typeof(Wolf3DVswapFileLoader), Vswap),
    ];
}