using PFWolf.Managers;

namespace PFWolf;

internal partial class Program
{
    /*
    =============================================================================

                                    RADAR

    statusbar.yaml radar (Planet Strike's, bstone's DrawRadar and ShowOverhead): a small map
    of the tiles around the player, turned so the way they face is up, of what they've seen
    (the automap's seen tiles). It always shows at 1x; zooming in (2x, 4x: radar zoom in and
    out) uses up its item, the radar's power, and shows enemies. When the power runs out it
    goes back to 1x. (bstone also shows pushwalls at 4x; not done.)

    =============================================================================
    */

    /// <summary>The radar's zoom: 0 for 1x, 1 for 2x, 2 for 4x</summary>
    static int radarzoom;

    // Counts the frames, so its power goes every other one as bstone's does
    static int radarframe;

    /// <summary>Each tic: the radar's zoom keys, its power going while it's zoomed in, and the radar drawn</summary>
    internal static void UpdateRadar()
    {
        if (StatusBar.Get("radar") is not { } radar || (viewsize == 21 && ingame))
            return;

        int power = string.IsNullOrEmpty(radar.Item) ? 0 : _inventoryManager.GetCount(radar.Item);
        int maxzoom = Math.Max(radar.ZoomPics.Count - 1, 0);

        if (_inputManager.IsButtonPressed(buttontypes.bt_radarzoomin) && !_inputManager.IsButtonHeld(buttontypes.bt_radarzoomin))
        {
            _inputManager.SetButtonHeld(buttontypes.bt_radarzoomin, true);
            if (power > 0 && radarzoom < maxzoom)
                radarzoom++;
        }
        if (_inputManager.IsButtonPressed(buttontypes.bt_radarzoomout) && !_inputManager.IsButtonHeld(buttontypes.bt_radarzoomout))
        {
            _inputManager.SetButtonHeld(buttontypes.bt_radarzoomout, true);
            if (radarzoom > 0)
                radarzoom--;
        }

        // Zoomed in, it uses tics << zoom of its power every other frame
        radarframe++;
        if (power > 0 && radarzoom > 0 && godmode == 0 && (radarframe & 1) != 0)
        {
            int left = Math.Max(power - ((int)tics << radarzoom), 0);
            _inventoryManager.Take(radar.Item!, power - left);
            if (left == 0)
            {
                radarzoom = 0;
                if (!string.IsNullOrEmpty(radar.EmptyMessage))
                    _hudMessageManager.Show(HudMessageKind.Other, radar.EmptyMessage);
            }
        }
        else if (power <= 0)
            radarzoom = 0;

        DrawRadarGauge();
        DrawRadar(radar);
    }

    static void DrawRadar(Assets.StatusBarElement radar)
    {
        if (_mapManager.Player == null)     // the status bar is drawn before a level is loaded too
            return;

        using var _ = StatusBarOrigin(radar);
        int top = StatusBarTop(radar);
        int zoompic = Math.Min(radarzoom, radar.ZoomPics.Count - 1);
        if (zoompic >= 0)
            _graphicManager.DrawPic(radar.ZoomPics[zoompic], radar.ZoomX, top + radar.ZoomY);

        string Color(string name, string fallback) => radar.Colors.GetValueOrDefault(name) ?? fallback;
        string unmapped = Color("unmapped", "82"), floor = Color("floor", "85"), door = Color("door", "88"),
            locked = Color("locked", "24"), playerColor = Color("player", "240"), key = Color("key", "243"),
            enemy = Color("enemy", "24");

        int scale = 1 << radarzoom;
        double radius = radar.Tiles / 2.0 / scale;
        int diameter = (int)(radius * 2);

        // Keys lying about, and (zoomed in) living enemies, by tile
        var keys = new HashSet<int>();
        var enemies = new HashSet<int>();
        foreach (var thing in _mapManager.GetActors())
        {
            if (thing.IsRemoved || thing == player)
                continue;
            if (_inventoryManager.FindClass(thing.Name, "Key") != null)
                keys.Add(thing.TileX << 8 | thing.TileY);
            else if (scale > 1 && thing.Hitpoints > 0 && MapManager.IsEnemy(thing))
                enemies.Add(thing.TileX << 8 | thing.TileY);
        }

        // The map turned so the player's facing is up, as bstone walks it
        double px = player.X / 65536.0, py = player.Y / 65536.0;
        double angle = player.Angle * Math.PI / 180;
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
                string color = unmapped;
                int mx = (int)Math.Floor(lmx), my = (int)Math.Floor(lmy);
                if (mx >= 0 && mx < MapManager.MAPSIZE && my >= 0 && my < MapManager.MAPSIZE)
                {
                    if (!drawnplayer && mx == player.TileX && my == player.TileY)
                    {
                        color = playerColor;
                        drawnplayer = true;
                    }
                    else if (_mapManager.seen[mx, my] != SeenFlags.None)
                    {
                        color = RadarTileColor(mx, my, unmapped, floor, door, locked);
                        if (keys.Contains(mx << 8 | my))
                            color = key;
                        if (enemies.Contains(mx << 8 | my))
                            color = enemy;
                    }
                }

                _videoManager.Bar(radar.X + x * scale, top + radar.Y + y * scale, scale, scale, color);
                lmx += xinc;
                lmy += yinc;
            }
            baselmx += yinc;
            baselmy -= xinc;
        }
    }

    // What a seen tile shows as: open floor, a door (closed, or locked), or a wall (as unmapped)
    static string RadarTileColor(int x, int y, string unmapped, string floor, string door, string locked)
    {
        int tile = _mapManager.tilemap[x, y];
        string color;
        if ((tile & BIT_DOOR) != 0 && (tile & BIT_WALL) == 0)
        {
            var d = doorobjlist[tile & ~BIT_DOOR];
            color = d.maplock != null && !d.unlocked ? locked
                : d.action == dooractiontypes.dr_closed ? door
                : floor;
        }
        else if (tile != 0)
            color = unmapped;
        else
            color = floor;
        return color;
    }
}
