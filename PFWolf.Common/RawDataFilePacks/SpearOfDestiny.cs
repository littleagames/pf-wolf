using PFWolf.Common.FileLoaders;

namespace PFWolf.Common.RawDataFilePacks;

public class SpearOfDestiny : RawDataFilePack
{
    public const string AudioHed = "audiohed.sod";
    public const string AudioT = "audiot.sod";
    public const string GameMaps = "gamemaps.sod";
    public const string MapHead = "maphead.sod";
    public const string VgaDict = "vgadict.sod";
    public const string VgaGraph = "vgagraph.sod";
    public const string VgaHead = "vgahead.sod";
    public const string Vswap = "vswap.sod";

    public override string PackName => "spear";
    public override string PackDescription => "Spear of Destiny v1.0";

    protected override List<DataPackFile> Files => [
        new (AudioHed, "6e914d15335125872737718470061ad8"),
        new (AudioT, "10020fce0f04d21bd07b1b5b951c360a"),
        new (GameMaps, "04f16534235b4b57fc379d5709f88f4a"),
        new (MapHead, "276c79a4a6419db6b23e7699e41cb9fa"),
        new (VgaDict, "30b11372b9ec6bc06289eb3e9b2ef0b9"),
        new (VgaGraph, "3b85f170098fb48d91d8bedd0cac4e0d"),
        new (VgaHead, "fb75007a1167bba05c4acadf90bc30d8"),
        new (Vswap,  "b1dac0a8786c7cdbb09331a4eba00652")
    ];
    protected override List<DataPackFileLoader> FileLoaders =>
    [
        new(typeof(Wolf3DAudioFileLoader), AudioHed, AudioT),
        new(typeof(Wolf3DMapFileLoader), MapHead, GameMaps),
        new(typeof(Wolf3DVgaFileLoader), VgaDict, VgaHead, VgaGraph),
        new(typeof(Wolf3DVswapFileLoader), Vswap),
    ];
}