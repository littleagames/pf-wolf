namespace PFWolf;

internal static partial class GameHost
{
    // The status bar's radar (Ui/Radar.cs)
    static Radar radar = null!;     // made in InitServices

    static Radar MakeRadar() =>
        new(_mapManager, _inventoryManager, _inputManager, _hudMessageManager, _videoManager, _graphicManager,
            statusBarDrawing, doors, session, () => viewsize == 21 && ingame, () => godmode);

    internal static void UpdateRadar() => radar.UpdateRadar();
    static string RadarTileColor(int x, int y, string unmapped, string floor, string door, string locked) =>
        radar.RadarTileColor(x, y, unmapped, floor, door, locked);
}
