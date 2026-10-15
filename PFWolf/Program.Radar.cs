namespace PFWolf;

internal partial class Program
{
    // The status bar's radar (Ui/Radar.cs)
    static Radar radar = null!;     // made in InitServices

    static Radar MakeRadar() =>
        new(_mapManager, _inventoryManager, _inputManager, _hudMessageManager, _videoManager, _graphicManager,
            statusBarDrawing, doors, session, () => viewsize == 21 && ingame, () => godmode);

    internal static void UpdateRadar() => radar.UpdateRadar();
    static void DrawRadar(Assets.StatusBarElement element) => radar.DrawRadar(element);
    static string RadarTileColor(int x, int y, string unmapped, string floor, string door, string locked) =>
        radar.RadarTileColor(x, y, unmapped, floor, door, locked);
}
