namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                              MORE STATUS BAR PARTS

    statusbar.yaml parts beyond Wolf3D's: a bar across the top of the screen (top) with the
    level's name on it (location), the weapon's charge light (charge), an LED ammo gauge
    (ammo-gauge) and a heart monitor (heart-monitor), all Blake Stone's.

    =============================================================================
    */

    /// <summary>The top status bar's picture (statusbar.yaml top), across the top of the screen</summary>
    static void DrawTopBar()
    {
        if (StatusBarHidden) return;
        if (StatusBar.Get("top") is { Pic: { Length: > 0 } pic } top)
        {
            FillBarSides(top, atTop: true, TOPLINES);
            using var _ = _videoManager.UseUiOrigin(Managers.UiAnchor.Top);
            _graphicManager.DrawPic(pic, 0, 0);
        }
    }

    /// <summary>
    /// On a screen wider than 320x200, fills a bar's screen lines beside its picture (the top
    /// or bottom height layout lines of the screen) in its side-color, so nothing from an
    /// earlier screen shows there. Wolf3D's border sides are painted over this afterwards.
    /// </summary>
    static void FillBarSides(Assets.StatusBarElement bar, bool atTop, int height)
    {
        int left = _videoManager.ScreenX(0), right = _videoManager.ScreenX(320);
        if (height <= 0 || (left <= 0 && right >= _videoManager.screenWidth))
            return;

        int y0 = atTop ? 0 : _videoManager.ScreenYAboveBottom(height);
        int y1 = atTop ? _videoManager.ToScreenLength(height) : _videoManager.screenHeight;
        string color = bar.SideColor ?? "Black";
        _videoManager.BarScaledCoord(0, y0, left, y1 - y0, color);
        _videoManager.BarScaledCoord(right, y0, _videoManager.screenWidth - right, y1 - y0, color);
    }

    /// <summary>The level's name (statusbar.yaml location), as GetMapDisplayName gives it</summary>
    static void DrawLocation()
    {
        if (StatusBarHidden) return;
        if (StatusBar.Get("location") is not { } element)
            return;
        var font = element.Font ?? StatusBar.Get("numbers")?.Font;
        if (!string.IsNullOrEmpty(font))
            DrawStatusText(element, GetMapDisplayName(gamestate.mapon), font, element.Color ?? "White");
    }

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
        DrawGauge(gauge, ammoType);
    }

    /// <summary>The radar's power gauge (statusbar.yaml radar-gauge): its item's count out of its max</summary>
    static void DrawRadarGauge()
    {
        if (StatusBarHidden) return;
        if (StatusBar.Get("radar-gauge") is { Item: { Length: > 0 } item } gauge)
            DrawGauge(gauge, item);
    }

    /// <summary>A gauge of segments lit from the bottom for how much of the item there is</summary>
    static void DrawGauge(Assets.StatusBarElement gauge, string item)
    {
        int segments = Math.Max(gauge.Segments, 1);
        int max = _inventoryManager.GetMaxAmount(item);
        if (max == int.MaxValue)
            max = 100;
        int ammo = _inventoryManager.GetCount(item);
        int lit = ammo <= 0 ? 0 : Math.Clamp((int)((long)ammo * segments / max), 1, segments);
        int unlit = segments - lit;

        var level = gauge.Levels.FirstOrDefault(l => unlit < l.Below) ?? gauge.Levels.LastOrDefault();
        if (level == null)
            return;

        using var _ = StatusBarOrigin(gauge);
        int y = StatusBarTop(gauge) + gauge.Y;
        for (int segment = 0; segment < segments; segment++)
        {
            var colors = segment < unlit ? level.Dim : level.Lit;
            for (int line = 0; line < gauge.SegmentHeight; line++, y++)
                if (colors.Count > 0)
                    _videoManager.Bar(gauge.X, y, gauge.Width, 1, colors[line % colors.Count]);
        }
    }

    /*
    The info area (statusbar.yaml info-area): messages in a style with `anchor: Status`
    (hud-messages.yaml) are written here, newest last, instead of over the view. Its picture
    (pic, drawn at box's corner) is drawn under them; the text starts at x, y and keeps within
    size [width, height]. With no messages it shows its idle-text.
    */

    static string? infoareashown;

    /// <summary>Whether the status bar has an info area showing now</summary>
    static bool HasInfoArea => StatusBar.Get("info-area") != null && !(viewsize == 21 && ingame);

    internal static void DrawInfoArea(bool force)
    {
        if (!HasInfoArea || StatusBar.Get("info-area") is not { } area)
            return;

        var messages = _hudMessageManager.Messages.Where(m => m.Style.Anchor == Managers.HudAnchor.Status).ToList();
        string text = messages.Count > 0
            ? string.Join("\n", messages.Select(m => m.Text))
            : IdleText(area.IdleText);
        if (!force && text == infoareashown)
            return;
        infoareashown = text;

        using var origin = StatusBarOrigin(area);
        int top = StatusBarTop(area);
        if (!string.IsNullOrEmpty(area.Pic))
            _graphicManager.DrawPic(area.Pic, area.Box.ElementAtOrDefault(0), top + area.Box.ElementAtOrDefault(1));
        else if (area.Box.Count == 4)
            _videoManager.Bar(area.Box[0], top + area.Box[1], area.Box[2], area.Box[3], area.BoxColor ?? "Black");

        var font = area.Font ?? messages.FirstOrDefault()?.Style.Font ?? SMALL_FONT;
        string color = area.Color ?? "White";
        int width = area.Size.ElementAtOrDefault(0) is > 0 and var w ? w : 100;
        int height = area.Size.ElementAtOrDefault(1) is > 0 and var h ? h : 40;
        int y = top + area.Y;

        // the newest lines win when there are too many: drop the oldest from the top
        var lines = new List<(string Text, string Font, string Color)>();
        if (messages.Count > 0)
            foreach (var message in messages)
                foreach (var line in WrapText(message.Text, area.Font ?? message.Style.Font, width))
                    lines.Add((line, area.Font ?? message.Style.Font, area.Color ?? message.Style.Color));
        else
            lines.AddRange(WrapText(text, font, width).Select(line => (line, font, color)));

        int lineHeight = Math.Max(lines.Select(l => { MeasureText(l.Text.Length > 0 ? l.Text : " ", l.Font, out _, out int lh); return lh; }).DefaultIfEmpty(8).Max(), 1);
        int fit = Math.Max(height / lineHeight, 1);
        foreach (var (line, lineFont, lineColor) in lines.Skip(Math.Max(lines.Count - fit, 0)))
        {
            _graphicManager.DrawText(area.X, y, line, new Fonts.TextStyle(lineFont, lineColor));
            y += lineHeight;
        }
    }

    // The idle text, with each {Item} the count of that inventory item
    static string IdleText(string? idle)
    {
        if (string.IsNullOrEmpty(idle))
            return "";
        var text = _hudMessageManager.Localize(idle);
        return System.Text.RegularExpressions.Regex.Replace(text, @"\{(\w+)\}",
            m => _inventoryManager.GetCount(m.Groups[1].Value).ToString());
    }

    /*
    The heart monitor, as Blake Stone's: a trace scrolling left across count segments, a blip
    entering on the right each beat: a small one (segments 1-8) at 66% health and up, a bigger
    one (9-17) from 33%, and a ragged one (18-27) below. The heart beats while health is above
    bad-below, shows bad below it, and goes out when the player dies.
    */

    static readonly int[] ecglegend = new int[6], ecgsegments = new int[6];
    static int ecgscrolltics, hearttics;
    static string? heartpic;

    /// <summary>Moves the heart monitor on by this tic's time, redrawing it as it changes</summary>
    internal static void UpdateHeartMonitor()
    {
        if (StatusBar.Get("heart-monitor") == null || (viewsize == 21 && ingame))
            return;
        ecgscrolltics += (int)tics;
        hearttics += (int)tics;
        DrawHeartMonitor(force: false);
    }

    internal static void DrawHeartMonitor(bool force)
    {
        if (StatusBarHidden) return;
        if (StatusBar.Get("heart-monitor") is not { } monitor)
            return;
        int count = Math.Min(monitor.Count, ecgsegments.Length);
        bool changed = force;

        if (ecgscrolltics >= monitor.ScrollTics)
        {
            ecgscrolltics = 0;
            changed = true;
            bool carry = false;
            for (int i = count - 1; i >= 0; i--)
            {
                if (carry)
                {
                    carry = false;
                    ecglegend[i] = ecglegend[i + 1];
                    ecgsegments[i] = ecgsegments[i + 1] - 4;
                }
                else if (ecgsegments[i] != 0)
                {
                    ecgsegments[i]++;
                    int seg = ecgsegments[i], legend = ecglegend[i];
                    if (legend == 1 && seg == 5 || legend == 2 && seg == 13 || legend == 3 && (seg == 22 || seg == 27))
                        carry = true;
                    else if (legend == 1 && seg > 8 || legend == 2 && seg > 17 || legend == 3 && seg > 27)
                        ecglegend[i] = ecgsegments[i] = 0;
                }
            }

            int last = count - 1;
            if (playerstate.health > 0 && ecglegend[last] == 0)
            {
                if (playerstate.health < 33)
                    (ecglegend[last], ecgsegments[last]) = (3, 18);
                else if (playerstate.health >= 66)
                {
                    if (last == 0 || ecglegend[last - 1] != 1)
                        (ecglegend[last], ecgsegments[last]) = (1, 1);
                }
                else
                    (ecglegend[last], ecgsegments[last]) = (2, 9);
            }
        }

        // The heart: off when dead, bad when low, else a beat every pulse
        string? heart = heartpic;
        if (playerstate.health <= 0)
            heart = monitor.HeartOff;
        else if (playerstate.health < monitor.BadBelow)
            heart = monitor.HeartBad;
        else if (hearttics >= monitor.PulseTics / 2 || heart == null || heart == monitor.HeartBad)
        {
            heart = heart == monitor.HeartGood ? monitor.HeartOff : monitor.HeartGood;
            hearttics = 0;
        }
        if (heart != heartpic)
        {
            heartpic = heart;
            changed = true;
        }

        if (!changed)
            return;

        using var _ = StatusBarOrigin(monitor);
        int top = StatusBarTop(monitor);
        if (!string.IsNullOrEmpty(monitor.SegmentPic))
            for (int i = 0; i < count; i++)
                _graphicManager.DrawPic(string.Format(monitor.SegmentPic, ecgsegments[i]),
                    monitor.X + i * monitor.SegmentWidth, top + monitor.Y);
        if (!string.IsNullOrEmpty(heartpic))
            _graphicManager.DrawPic(heartpic, monitor.HeartX, top + monitor.HeartY);
    }
}
