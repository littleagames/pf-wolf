namespace PFWolf;

internal partial class Program
{
    /*
    =============================================================================

                                    STATUS BAR

    The layout (statusbar.yaml) and its parts drawn from the player's state are in
    Ui/StatusBarDrawing.cs; the weapon's charge light and ammo gauge, which go with the
    weapon code, are here.

    =============================================================================
    */

    static StatusBarDrawing statusBarDrawing = null!;     // made in InitServices

    static StatusBarDrawing MakeStatusBarDrawing() =>
        new(_gameEngineManager, _assetManager, _videoManager, _graphicManager, _inventoryManager, _audioManager,
            _hudMessageManager, ui, session, new StatusBarHooks(() => viewsize == 21 && ingame, GetMapDisplayName, WrapText));

    /// <summary>The game pack's status bar layout (statusbar.yaml); empty if it has none</summary>
    internal static Assets.StatusBarAsset StatusBar => statusBarDrawing?.StatusBar ?? StatusBarDrawing.NoStatusBar;

    static bool StatusBarHidden => statusBarDrawing.StatusBarHidden;
    static void StatusDrawFace(string picName) => statusBarDrawing.StatusDrawFace(picName);
    static void LatchNumber(string part, int number, bool hide = false) => statusBarDrawing.LatchNumber(part, number, hide);
    static int StatusBarTop(Assets.StatusBarElement element) => statusBarDrawing.StatusBarTop(element);
    static Managers.VideoManager.UiOriginScope StatusBarOrigin(Assets.StatusBarElement element) => statusBarDrawing.StatusBarOrigin(element);
    static bool Grinning => statusBarDrawing.Grinning;
    static void DrawFace() => statusBarDrawing.DrawFace();
    static void DrawHealth() => statusBarDrawing.DrawHealth();
    static void DrawArmor() => statusBarDrawing.DrawArmor();
    static void DrawKeys() => statusBarDrawing.DrawKeys();
    static void DrawLevel() => statusBarDrawing.DrawLevel();
    static void DrawLives() => statusBarDrawing.DrawLives();
    static void DrawScore() => statusBarDrawing.DrawScore();
    static void DrawWeaponPic() => statusBarDrawing.DrawWeaponPic();
    static void DrawTopBar() => statusBarDrawing.DrawTopBar();
    static void FillBarSides(Assets.StatusBarElement bar, bool atTop, int height) => statusBarDrawing.FillBarSides(bar, atTop, height);
    static void DrawLocation() => statusBarDrawing.DrawLocation();
    static void DrawRadarGauge() => statusBarDrawing.DrawRadarGauge();
    static bool HasInfoArea => statusBarDrawing.HasInfoArea;
    internal static void DrawInfoArea(bool force) => statusBarDrawing.DrawInfoArea(force);
    internal static void UpdateHeartMonitor() => statusBarDrawing.UpdateHeartMonitor();
    internal static void DrawHeartMonitor(bool force) => statusBarDrawing.DrawHeartMonitor(force);

    /// <summary>
    /// The charge light (statusbar.yaml charge): for a weapon that charges (weapon.chargetics),
    /// its ready picture once charged, else its wait picture
    /// </summary>
    static void DrawCharge()
    {
        if (StatusBarHidden) return;
        if (StatusBar.Get("charge") is not { } element || WeaponChargeTics(playerstate.weapon) <= 0)
            return;
        var pic = weaponcharge > 0 ? element.Wait : element.Ready;
        using var _ = StatusBarOrigin(element);
        if (!string.IsNullOrEmpty(pic))
            _graphicManager.DrawPic(pic, element.X, StatusBarTop(element) + element.Y);
    }

    /// <summary>
    /// The ammo gauge (statusbar.yaml ammo-gauge): a strip of segments, lit from the bottom for
    /// how full the weapon in hand's ammo is (any ammo at all lights one), colored by its levels
    /// for how many are unlit. Not drawn for a weapon that uses no ammo.
    /// </summary>
    static void DrawAmmoGauge()
    {
        if (StatusBarHidden) return;
        if (StatusBar.Get("ammo-gauge") is not { } gauge || WeaponAmmoType(playerstate.weapon) is not { } ammoType)
            return;
        statusBarDrawing.DrawGauge(gauge, ammoType);
    }
}
