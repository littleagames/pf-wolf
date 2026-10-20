using PFWolf.Managers;

namespace PFWolf.Ui;

/// <summary>
/// statusbar.yaml radar (Planet Strike's, bstone's DrawRadar and ShowOverhead): a small map
/// of the tiles around the player, turned so the way they face is up, of what they've seen
/// (the automap's seen tiles). It always shows at 1x; zooming in (2x, 4x: radar zoom in and
/// out) uses up its item, the radar's power, and shows enemies. When the power runs out it
/// goes back to 1x. (bstone also shows MovingWalls at 4x; not done.)
///
/// Midnight Assault's corner map (ma_automap.c) is one too: north up (rotate: false), enemies
/// shown at 1x (enemies: true), and only on the maps that ask for it (maps-only, game-info map
/// radar: the Stealth Challenge).
/// </summary>
/// <param name="currentMap">The level's game-info: a maps-only radar shows where it says (map radar)</param>
internal sealed class Radar(
    MapManager map,
    InventoryManager inventory,
    InputManager input,
    HudMessageManager hudMessages,
    VideoManager video,
    GraphicManager graphics,
    StatusBarDrawing statusBar,
    Doors doors,
    GameSession session,
    Configuration.GameSettings settings,
    CurrentMapInfo? currentMap = null)
{
    /// <summary>Whether the radar shows now: not with the view full screen, and a maps-only one only on a map that turns it on</summary>
    internal bool Shows(Assets.StatusBarElement radar) =>
        !(settings.ViewSize == 21 && session.InGame) && (!radar.MapsOnly || currentMap?.Map?.Radar == true);

    /// <summary>The radar's zoom: 0 for 1x, 1 for 2x, 2 for 4x</summary>
    internal int RadarZoom;

    // Counts the frames, so its power goes every other one as bstone's does
    int radarframe;

    /// <summary>Each tic: the radar's zoom keys, its power going while it's zoomed in, and the radar drawn</summary>
    internal void UpdateRadar()
    {
        if (statusBar.StatusBar.Get("radar") is not { } radar || !Shows(radar))
            return;

        int power = string.IsNullOrEmpty(radar.Item) ? 0 : inventory.GetCount(radar.Item);
        int maxzoom = Math.Max(radar.ZoomPics.Count - 1, 0);

        if (input.IsButtonPressed(ButtonType.RadarZoomIn) && !input.IsButtonHeld(ButtonType.RadarZoomIn))
        {
            input.SetButtonHeld(ButtonType.RadarZoomIn, true);
            if (power > 0 && RadarZoom < maxzoom)
                RadarZoom++;
        }
        if (input.IsButtonPressed(ButtonType.RadarZoomOut) && !input.IsButtonHeld(ButtonType.RadarZoomOut))
        {
            input.SetButtonHeld(ButtonType.RadarZoomOut, true);
            if (RadarZoom > 0)
                RadarZoom--;
        }

        // Zoomed in, it uses tics << zoom of its power every other frame
        radarframe++;
        if (power > 0 && RadarZoom > 0 && session.GodMode == 0 && (radarframe & 1) != 0)
        {
            int left = Math.Max(power - ((int)session.Tics << RadarZoom), 0);
            inventory.Take(radar.Item!, power - left);
            if (left == 0)
            {
                RadarZoom = 0;
                if (!string.IsNullOrEmpty(radar.EmptyMessage))
                    hudMessages.Show(HudMessageKind.Other, radar.EmptyMessage);
            }
        }
        else if (power <= 0)
            RadarZoom = 0;

        statusBar.DrawRadarGauge();
        DrawRadar(radar);
    }

    internal void DrawRadar(Assets.StatusBarElement radar)
    {
        if (map.Player == null)     // the status bar is drawn before a level is loaded too
            return;

        using var _ = statusBar.StatusBarOrigin(radar);
        int top = statusBar.StatusBarTop(radar);
        int zoompic = Math.Min(RadarZoom, radar.ZoomPics.Count - 1);
        if (zoompic >= 0)
            graphics.DrawPic(radar.ZoomPics[zoompic], radar.ZoomX, top + radar.ZoomY);

        string Color(string name, string fallback) => radar.Colors.GetValueOrDefault(name) ?? fallback;
        string unmapped = Color("unmapped", "82"), floor = Color("floor", "85"), door = Color("door", "88"),
            locked = Color("locked", "24"), playerColor = Color("player", "240"), key = Color("key", "243"),
            enemy = Color("enemy", "24");

        int scale = 1 << RadarZoom;
        double radius = radar.Tiles / 2.0 / scale;
        int diameter = (int)(radius * 2);

        // Keys lying about, and (zoomed in, or always with enemies) living enemies, by tile, in
        // their class's own color if the radar gives one
        var keys = new HashSet<int>();
        var enemies = new Dictionary<int, string>();
        foreach (var thing in map.GetActors())
        {
            if (thing.IsRemoved || thing == session.Player)
                continue;
            if (inventory.FindClass(thing.Name, "Key") != null)
                keys.Add(thing.TileX << 8 | thing.TileY);
            else if ((scale > 1 || radar.Enemies) && thing.Hitpoints > 0 && thing.RuntimeFlags.Has(ObjFlags.Shootable) && MapManager.IsEnemy(thing))
                enemies[thing.TileX << 8 | thing.TileY] = radar.Colors.GetValueOrDefault(thing.Name) ?? enemy;
        }

        // What a tile shows: the player, else what's been seen of it, else unmapped
        string TileColor(int mx, int my, ref bool drawnplayer)
        {
            if (!map.InMap(mx, my))
                return unmapped;
            if (!drawnplayer && mx == session.Player.TileX && my == session.Player.TileY)
            {
                drawnplayer = true;
                return playerColor;
            }
            if (map.Seen[mx, my] == SeenFlags.None)
                return unmapped;
            string color = RadarTileColor(mx, my, unmapped, floor, door, locked);
            if (keys.Contains(mx << 8 | my))
                color = key;
            return enemies.GetValueOrDefault(mx << 8 | my) ?? color;
        }

        if (!radar.Rotate)
        {
            // North up, the player's tile in the middle (Midnight Assault's DrawAutomap)
            bool drawn = false;
            int half = diameter / 2;
            for (int x = 0; x < diameter; x++)
                for (int y = 0; y < diameter; y++)
                    video.Bar(radar.X + x * scale, top + radar.Y + y * scale, scale, scale,
                        TileColor(session.Player.TileX - half + x, session.Player.TileY - half + y, ref drawn));
            return;
        }

        // The map turned so the player's facing is up, as bstone walks it
        double px = session.Player.X / 65536.0, py = session.Player.Y / 65536.0;
        double angle = session.Player.Angle * Math.PI / 180;
        // The sine with the sign that keeps the way the player faces at the top (checked against
        // the automap; bstone's own sign put it at the bottom here)
        double psin = -Math.Sin(angle), pcos = Math.Cos(angle);
        double baselmx = px + (radius * pcos - radius * psin);
        double baselmy = py - (radius * psin + radius * pcos);
        double xinc = -pcos, yinc = psin;
        bool drawnplayer = false;

        for (int x = 0; x < diameter; x++)
        {
            double lmx = baselmx, lmy = baselmy;
            for (int y = 0; y < diameter; y++)
            {
                string color = TileColor((int)Math.Floor(lmx), (int)Math.Floor(lmy), ref drawnplayer);
                video.Bar(radar.X + x * scale, top + radar.Y + y * scale, scale, scale, color);
                lmx += xinc;
                lmy += yinc;
            }
            baselmx += yinc;
            baselmy -= xinc;
        }
    }

    // What a seen tile shows as: open floor, a door (closed, or locked), or a wall (as unmapped)
    internal string RadarTileColor(int x, int y, string unmapped, string floor, string door, string locked)
    {
        int tile = map.TileMap[x, y];
        string color;
        if ((tile & GameConstants.BitDoor) != 0)
        {
            var d = doors.DoorObjList[tile & ~GameConstants.BitDoor];
            // As bstone: a door with a lock (none once a key has unlocked it) is locked, open or shut
            color = d.IsLocked ? locked
                : d.Action == DoorAction.Closed ? door
                : floor;
        }
        else if (tile != 0)
            color = unmapped;
        else
            color = floor;
        return color;
    }
}
