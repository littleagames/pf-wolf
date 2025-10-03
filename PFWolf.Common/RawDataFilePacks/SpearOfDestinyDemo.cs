using PFWolf.Common.FileLoaders;

namespace PFWolf.Common.RawDataFilePacks;

public class SpearOfDestinyDemo : RawDataFilePack
{
    public const string AudioHed = "audiohed.sdm";
    public const string AudioT = "audiot.sdm";
    public const string GameMaps = "gamemaps.sdm";
    public const string MapHead = "maphead.sdm";
    public const string VgaDict = "vgadict.sdm";
    public const string VgaGraph = "vgagraph.sdm";
    public const string VgaHead = "vgahead.sdm";
    public const string Vswap = "vswap.sdm";

    public override string PackName => "spear-demo";
    public override string PackDescription => "Spear of Destiny Demo";

    protected override List<DataPackFile> Files => [
        new (AudioHed, "f0022742f86c214872bd72f03aaf1529"),
        new (AudioT, "fcde1333c941229f4dd6ca099fcfe616"),
        new (GameMaps, "4eb2f538aab6e4061dadbc3b73837762"),
        new (MapHead, "40fa03caf7a1a4dbd22da4321c6e10d4"),
        new (VgaDict, "2f85b6763a582df19e6a35dd9634c736"),
        new (VgaGraph, "1cc5ceb8e43c0c0030cf552fc8ae9d0d"),
        new (VgaHead, "18c03cb401ed274bc0b659e951140e64"),
        new (Vswap,  "35afda760bea840b547d686a930322dc")
    ];
    protected override List<DataPackFileLoader> FileLoaders =>
    [
        new(typeof(Wolf3DAudioFileLoader), AudioHed, AudioT),
        new(typeof(Wolf3DMapFileLoader), MapHead, GameMaps),
        new(typeof(Wolf3DVgaFileLoader), VgaDict, VgaHead, VgaGraph),
        new(typeof(Wolf3DVswapFileLoader), Vswap),
    ];
}