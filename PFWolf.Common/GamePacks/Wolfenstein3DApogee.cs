using PFWolf.Common.Loaders;

namespace PFWolf.Common.GamePacks;

public class Wolfenstein3DApogee : RawDataFilePack
{
    public const string AudioHed = "audiohed.wl6";
    public const string AudioT = "audiot.wl6";
    public const string GameMaps = "gamemaps.wl6";
    public const string MapHead = "maphead.wl6";
    public const string VgaDict = "vgadict.wl6";
    public const string VgaGraph = "vgagraph.wl6";
    public const string VgaHead = "vgahead.wl6";
    public const string Vswap = "vswap.wl6";

    public override string PackName => "wolf3d-apogee";
    public override string PackDescription => "Wolfenstein 3D v1.4 Apogee";

    protected override List<DataPackFile> Files => [
        new(AudioHed, "a41af25a2f193e7d4afbcc4301b3d1ce"),
        new(AudioT, "2385b488b18f8721633e5b2bdf054853"),
        new(GameMaps, "a4e73706e100dc0cadfb02d23de46481"),
        new(MapHead, "b8d2a78bc7c50da7ec9ab1d94f7975e1"),
        new(VgaDict, "adb10b0d6fdddba9fcc3d1a7c16937e7"),
        new(VgaGraph, "4e96d7b4e89a5b3a4beeebf5d7d87eb7"),
        new(VgaHead, "a08905e2b0d299b3fad259f90c0efb1a"),
        new(Vswap, "a6d901dfb455dfac96db5e4705837cdb")
    ];

    protected override List<DataPackFileLoader> FileLoaders =>
    [
        new(typeof(Wolf3DAudioFileLoader), AudioHed, AudioT),
        new(typeof(Wolf3DMapFileLoader), MapHead, GameMaps),
        new(typeof(Wolf3DVgaFileLoader), VgaDict, VgaHead, VgaGraph),
        new(typeof(Wolf3DVswapFileLoader), Vswap),
    ];
}