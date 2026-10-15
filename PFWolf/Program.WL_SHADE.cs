using PFWolf.Assets;

namespace PFWolf;

/// <summary>A level's shading, with every value filled in (see ShadingInfo)</summary>
internal record struct ShadingSettings(string FadeColor, double FadeStart, double FadeEnd, int MaxFade, int Light, bool InverseFade = false);

internal partial class Program
{
    // The level's shading, light grid and light zones (Rendering/Lighting.cs)
    static Lighting lighting = null!;       // made in InitServices

    static Lighting MakeLighting() =>
        new(_gameEngineManager, _videoManager, _mapManager,
            new LightingHooks(() => heightnumerator, () => colortints.Clear(), TickActorLights));

    internal const int LIGHTLEVELS = Lighting.LIGHTLEVELS;
    internal const int FADESTEPS = Lighting.FADESTEPS;
    internal const int LIGHTCELLSHIFT = Lighting.LIGHTCELLSHIFT;
    internal const int LIGHTCELLS = Lighting.LIGHTCELLS;
    const int CELLGLOBALSHIFT = Lighting.CELLGLOBALSHIFT;
    const int MAXLIGHTLEVEL = Lighting.MAXLIGHTLEVEL;
    internal const int GRIDSHIFT = Lighting.GRIDSHIFT;
    internal const int GRIDSIZE = Lighting.GRIDSIZE;

    static byte[] noshade => Lighting.noshade;
    static (byte, byte, byte) NoTint => Lighting.NoTint;
    static Random zonerandom => Lighting.zonerandom;
    internal static ShadingSettings? levelshading => lighting.levelshading;
    internal static Dictionary<int, ZoneState> levelzones => lighting.levelzones;
    static bool shading => lighting.shading;
    static byte[] lightrow => lighting.lightrow;
    static bool haszones => lighting.haszones;
    static byte[] celladd => lighting.celladd;
    static byte[] cellstrong => lighting.cellstrong;
    static byte[] celltint => lighting.celltint;
    static List<(byte R, byte G, byte B)> tints => lighting.tints;

    internal static void SetShading(ShadingSettings? settings) => lighting.SetShading(settings);
    static (byte R, byte G, byte B) ParseRgb(string? color, (byte, byte, byte) fallback, string what) => Lighting.ParseRgb(color, fallback, what);
    static int ShadeOffset(long nx) => lighting.ShadeOffset(nx);
    static int ShadeOffsetForHeight(int height) => lighting.ShadeOffsetForHeight(height);
    internal static bool IsBright(Entities.Actors.Actor actor) => Lighting.IsBright(actor);
    internal static void InitLevelShading(MapInfo? mapInfo) => lighting.InitLevelShading(mapInfo);
    static byte TintIndex((byte R, byte G, byte B) tint) => lighting.TintIndex(tint);
    static void ResetLightRows() => lighting.ResetLightRows();
    static int TileIndex(int x, int y) => Lighting.TileIndex(x, y);
    static byte[] LightAt(int tile, long gx, long gy) => lighting.LightAt(tile, gx, gy);
    static (int X, int Y) TileCenter(int x, int y) => Lighting.TileCenter(x, y);
    static void UpdateLightGrid() => lighting.UpdateLightGrid();

    internal static bool TryParseZoneEffect(string name, out ZoneEffect effect) => Lighting.TryParseZoneEffect(name, out effect);
    static void TickZoneLights(uint tics) => lighting.TickZoneLights(tics);
    internal static void FadeZoneLight(int id, int light, int tics) => lighting.FadeZoneLight(id, light, tics);
    internal static void SetZoneColor(int id, string? color) => lighting.SetZoneColor(id, color);
    internal static void SetZoneEffect(int id, ZoneEffect effect) => lighting.SetZoneEffect(id, effect);
    internal static void RemoveZone(int id) => lighting.RemoveZone(id);
    static ZoneState ZoneFor(int id) => lighting.ZoneFor(id);
    private static void RegisterZoneLightActions() => (lighting ??= MakeLighting()).RegisterZoneLightActions();
    internal static bool IsRgb(string color) => Lighting.IsRgb(color);
    static void WriteZoneLights(BinaryWriter bw) => lighting.WriteZoneLights(bw);
    static Dictionary<int, ZoneState> ReadZoneLights(BinaryReader br) => lighting.ReadZoneLights(br);
    static void RestoreZoneLights(Dictionary<int, ZoneState> zones) => lighting.RestoreZoneLights(zones);
}
