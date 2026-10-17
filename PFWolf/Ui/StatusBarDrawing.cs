using PFWolf.Assets;
using PFWolf.Managers;

namespace PFWolf.Ui;

/// <summary>What the status bar drawing needs from the game</summary>
/// <param name="MapDisplayName">A map's name as the game shows it</param>
/// <param name="WrapText">Text split into lines no wider than a width in a font</param>
internal sealed record StatusBarHooks(
    Func<string, string> MapDisplayName,
    Func<string, string, int, List<string>> WrapText);

/// <summary>
/// The status bar (statusbar.yaml): its layout and the parts drawn from the player's state --
/// numbers, the face, keys, the weapon's picture, Blake Stone's top bar, gauges, info area and
/// heart monitor. The parts are the local player's: nothing's drawn while another player acts.
/// </summary>
internal sealed class StatusBarDrawing(
    GameEngineManager engine,
    AssetManager? assets,
    VideoManager video,
    GraphicManager graphics,
    InventoryManager inventory,
    AudioManager audio,
    HudMessageManager hudMessages,
    UiText ui,
    GameSession session,
    Configuration.GameSettings settings,
    StatusBarHooks hooks)
{
    // The status bar's height in 320x200 lines (background), and a top bar's (top)
    int StatusLines => StatusBar.Get("background")?.Height ?? 0;
    int TopLines => StatusBar.Get("top")?.Height ?? 0;

    /// <summary>
    /// Whether the status bar's parts aren't drawn now: with the view full screen, or for a
    /// player other than this machine's (the bar is the local player's). A bar drawn over the
    /// view (overlay) is drawn only with the frame (DrawingOverlay), as the view under it is.
    /// </summary>
    internal bool StatusBarHidden => (settings.ViewSize == 21 && session.InGame) || !session.ActingIsLocal || (IsOverlay && !DrawingOverlay);

    /// <summary>Whether the status bar is drawn over the view every frame (statusbar.yaml background overlay)</summary>
    internal bool IsOverlay => StatusBar.Get("background")?.Overlay == true;

    /// <summary>Set while a frame draws an overlay status bar's parts (PlayScreen.DrawStatusOverlay)</summary>
    internal bool DrawingOverlay { get; set; }

    private StatusBarAsset? statusbar;
    internal static readonly StatusBarAsset NoStatusBar = new();

    /// <summary>
    /// The game pack's status bar layout (statusbar.yaml); empty if it has none. Only a layout
    /// that was found is kept, so a look before the assets are loaded doesn't stick.
    /// </summary>
    internal StatusBarAsset StatusBar
    {
        get => statusbar ?? (statusbar = assets?.FindInGamePackIfAny<StatusBarAsset>("statusbar")) ?? NoStatusBar;
        set => statusbar = value;       // a layout of a test's own
    }

    /// <summary>Draws a picture at a status bar position (320x200 pixels from its top left corner).</summary>
    internal void StatusDrawPic(string picName, int x, int y)
    {
        using var _ = video.UseUiOrigin(UiAnchor.Bottom);
        graphics.DrawPic(picName, x, 200 - StatusLines + y);
    }

    /// <summary>Draws a picture at the named part of the status bar, if the layout has it.</summary>
    internal void StatusDrawPic(string picName, string part)
    {
        if (StatusBar.Get(part) is { } element)
            StatusDrawPic(picName, element.X, element.Y);
    }

    internal void StatusDrawFace(string picName) => StatusDrawPic(picName, "face");

    /// <summary>
    /// A number right-aligned in the named part's digits, blanking the rest; a number too long
    /// to fit shows its last digits. In the part's font and color, else the layout's numbers'.
    /// </summary>
    internal void LatchNumber(string part, int number, bool hide = false)
    {
        if (StatusBar.Get(part) is not { } element)
            return;
        var numbers = StatusBar.Get("numbers");
        var font = element.Font ?? numbers?.Font;
        if (string.IsNullOrEmpty(font))
            return;

        // What the number is drawn over: a part under it, or a box to clear the old one
        if (element.Behind is { Length: > 0 } behind)
            DrawStatusPart(behind);
        ClearBox(element);
        if (hide)
            return;

        // digits 0: as long as the number is, else right-aligned in that many (its last digits if longer)
        string str = number.ToString();
        if (element.Digits > 0)
            str = str.Length <= element.Digits ? str.PadLeft(element.Digits) : str[^element.Digits..];
        str = element.Prefix + str + element.Suffix;

        DrawStatusText(element, str, font, element.Color ?? numbers?.Color ?? "White");
    }

    /// <summary>Text at a status bar part, lined up as its align says</summary>
    internal void DrawStatusText(Assets.StatusBarElement element, string text, string font, string color)
    {
        int x = element.Align.ToLowerInvariant() switch
        {
            "right" => element.X - ui.TextWidth(text, font),
            "center" => element.X - ui.TextWidth(text, font) / 2,
            _ => element.X,
        };
        using var _ = StatusBarOrigin(element);
        graphics.DrawText(x, StatusBarTop(element) + element.Y, text, new Fonts.TextStyle(font, color));
    }

    /// <summary>
    /// Fills a part's box (statusbar.yaml box, box-color) so what it showed before goes; not on a
    /// bar drawn over the view (overlay), where the frame under it is drawn afresh
    /// </summary>
    internal void ClearBox(Assets.StatusBarElement element)
    {
        if (element.Box.Count != 4 || IsOverlay)
            return;
        using var _ = StatusBarOrigin(element);
        video.Bar(element.Box[0], StatusBarTop(element) + element.Box[1], element.Box[2], element.Box[3],
            element.BoxColor ?? "Black");
    }

    /// <summary>A text's width in a font, in 320x200 pixels</summary>
    internal int TextWidth(string text, string font) => ui.TextWidth(text, font);

    /// <summary>Where a part's bar starts down the screen (320x200): the top bar's, or the bottom one's</summary>
    internal int StatusBarTop(Assets.StatusBarElement element) => element.Top ? 0 : 200 - StatusLines;

    /// <summary>
    /// Lines the 320x200 layout up with the edge of the screen a part's bar is on, until
    /// disposed, so the bars stay at the top and bottom of a screen taller than 320x200.
    /// </summary>
    internal VideoManager.UiOriginScope StatusBarOrigin(Assets.StatusBarElement element) =>
        video.UseUiOrigin(element.Top ? UiAnchor.Top : UiAnchor.Bottom);

    /// <summary>Draws a part that a number can sit on (statusbar.yaml behind)</summary>
    internal void DrawStatusPart(string part)
    {
        if (part.Equals("weapon", StringComparison.OrdinalIgnoreCase))
            DrawWeaponPic();
        else if (part.Equals("weapon-corner", StringComparison.OrdinalIgnoreCase))
            DrawWeaponCorner();
    }

    // The face grins while the pickup sound of the WEAPON.ALWAYSGRIN item that made the player grin plays
    internal bool Grinning => session.PlayerState.GrinSound != null && audio.IsPlaying(session.PlayerState.GrinSound);

    // The face (statusbar.yaml face): the grin, else the one for the player's health, looking
    // around through its pics, else once dead the one for what killed them
    internal void DrawFace()
    {
        if (StatusBarHidden) return;
        if (StatusBar.Get("face") is not { } face) return;

        string? pic;
        if (Grinning && !string.IsNullOrEmpty(face.Grin))
            pic = face.Grin;
        else if (session.PlayerState.Health != 0)
        {
            var faces = face.Faces.OrderByDescending(f => f.Health).ToList();
            var pics = (faces.FirstOrDefault(f => session.PlayerState.Health >= f.Health) ?? faces.LastOrDefault())?.Pics;
            pic = pics is { Count: > 0 } ? pics[session.PlayerState.FaceFrame % pics.Count] : null;
        }
        else
            pic = session.PlayerState.LastAttacker != null && face.KilledBy.TryGetValue(session.PlayerState.LastAttacker.Name, out var killedBy) ? killedBy : face.Dead;

        if (!string.IsNullOrEmpty(pic))
            StatusDrawFace(pic);
    }

    internal void DrawHealth()
    {
        if (StatusBarHidden) return;
        LatchNumber("health", session.PlayerState.Health);
    }

    // The status bar's optional armor number (statusbar.yaml `armor`)
    internal void DrawArmor()
    {
        if (StatusBarHidden) return;
        LatchNumber("armor", session.PlayerState.Armor);
    }

    internal void DrawKeys()
    {
        if (StatusBarHidden) return;
        if (StatusBar.Get("keys") is not { } layout) return;
        // Each key with a "key.statusbarslot" (0 at the top) has its own spot on the status bar:
        // its "key.statusbarpic" while carried, else "key.statusbaremptypic". Carried keys are
        // drawn last, so one wins a slot it shares with a key that isn't.
        var keys = inventory.GetClassesDerivedFrom("Key")
            .Select(key => (Key: key, Slot: inventory.GetIntProperty(key, "key.statusbarslot", -1), Has: inventory.Has(key)))
            .Where(key => key.Slot >= 0)
            .OrderBy(key => key.Has);
        using var _ = StatusBarOrigin(layout);
        foreach (var (key, slot, has) in keys)
        {
            int x = layout.X + layout.SpacingX * slot, y = layout.Y + layout.Spacing * slot;

            // size: a colored square for each key ("key.statusbarcolor" / "key.statusbaremptycolor")
            if (layout.Size.Count == 2)
            {
                var color = inventory.GetStringProperty(key, has ? "key.statusbarcolor" : "key.statusbaremptycolor");
                if (!string.IsNullOrEmpty(color))
                    video.Bar(x, StatusBarTop(layout) + y, layout.Size[0], layout.Size[1], color);
                continue;
            }

            var pic = inventory.GetStringProperty(key, has ? "key.statusbarpic" : "key.statusbaremptypic");
            if (!string.IsNullOrEmpty(pic))
                graphics.DrawPic(pic, x, StatusBarTop(layout) + y);
        }
    }

    internal void DrawLevel()
    {
        var gameInfo = engine.GetGameInfo();
        var mapInfo = gameInfo.Maps[session.GameState.MapOn];
        //var mapInfo = MapInfoMappings.GameInfo.Maps[session.GameState.MapOn];
        if (StatusBarHidden) return;
        LatchNumber("level", mapInfo.FloorNumber);
    }

    internal void DrawLives()
    {
        if (StatusBarHidden) return;
        LatchNumber("lives", session.PlayerState.Lives);
    }

    /// <summary>What the score box shows for a player with others playing (Gameplay/Deathmatch.cs: frags, the team's score); unset, their score</summary>
    internal Func<Entities.PlayerState, int>? ScoreShown { get; set; }

    internal void DrawScore()
    {
        if (StatusBarHidden) return;
        // a deathmatch keeps score in frags, a team game the team's
        LatchNumber("score", session.GameMode != GameMode.Single && ScoreShown != null
            ? ScoreShown(session.PlayerState)
            : session.GameMode == GameMode.Deathmatch ? session.PlayerState.Frags : session.PlayerState.Score);
    }

    internal void DrawWeaponPic()
    {
        if (StatusBarHidden) return;
        if (session.PlayerState.Weapon == null) return;

        var icon = inventory.GetStringProperty(session.PlayerState.Weapon, "inventory.icon");
        if (!string.IsNullOrEmpty(icon) && StatusBar.Get("weapon") is { } element)
        {
            using var _ = StatusBarOrigin(element);
            graphics.DrawPic(icon, element.X, StatusBarTop(element) + element.Y);
        }
        DrawWeaponCorner();
    }

    // Planet Strike's corner under the weapon's picture, with the ammo on it (statusbar.yaml
    // weapon-corner): its picture for the weapon in hand
    internal void DrawWeaponCorner()
    {
        if (StatusBarHidden) return;
        if (session.PlayerState.Weapon == null || StatusBar.Get("weapon-corner") is not { } element) return;
        var pic = element.WeaponPics.GetValueOrDefault(session.PlayerState.Weapon);
        using var _ = StatusBarOrigin(element);
        if (!string.IsNullOrEmpty(pic))
            graphics.DrawPic(pic, element.X, StatusBarTop(element) + element.Y);
    }

    /// <summary>The top status bar's picture (statusbar.yaml top), across the top of the screen</summary>
    internal void DrawTopBar()
    {
        if (StatusBarHidden) return;
        if (StatusBar.Get("top") is { Pic: { Length: > 0 } pic } top)
        {
            FillBarSides(top, atTop: true, TopLines);
            using var _ = video.UseUiOrigin(Managers.UiAnchor.Top);
            graphics.DrawPic(pic, 0, 0);
        }
    }

    /// <summary>
    /// On a screen wider than 320x200, fills a bar's screen lines beside its picture (the top
    /// or bottom height layout lines of the screen) in its side-color, so nothing from an
    /// earlier screen shows there. Wolf3D's border sides are painted over this afterwards.
    /// </summary>
    internal void FillBarSides(Assets.StatusBarElement bar, bool atTop, int height)
    {
        int left = video.ScreenX(0), right = video.ScreenX(320);
        if (height <= 0 || (left <= 0 && right >= video.ScreenWidth))
            return;

        int y0 = atTop ? 0 : video.ScreenYAboveBottom(height);
        int y1 = atTop ? video.ToScreenLength(height) : video.ScreenHeight;
        string color = bar.SideColor ?? "Black";
        video.BarScaledCoord(0, y0, left, y1 - y0, color);
        video.BarScaledCoord(right, y0, video.ScreenWidth - right, y1 - y0, color);
    }

    /// <summary>
    /// Each icon part's picture (statusbar.yaml icon-*: pic at x, y), e.g. a heart by the health;
    /// one only-with-magazine only while the weapon in hand has a magazine
    /// </summary>
    internal void DrawIcons()
    {
        if (StatusBarHidden) return;
        bool magazine = session.PlayerState.Weapon is { } weapon
            && inventory.GetStringProperty(weapon, "weapon.magazine") is { Length: > 0 };
        foreach (var (name, element) in StatusBar.Elements)
        {
            if (!name.StartsWith("icon", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(element.Pic)
                || (element.OnlyWithMagazine && !magazine))
                continue;
            using var _ = StatusBarOrigin(element);
            graphics.DrawPic(element.Pic, element.X, StatusBarTop(element) + element.Y);
        }
    }

    /// <summary>Each count part's number: how many of its item the player holds (statusbar.yaml count-*: item, e.g. medkits)</summary>
    internal void DrawCounts()
    {
        if (StatusBarHidden) return;
        foreach (var (name, element) in StatusBar.Elements)
            if (name.StartsWith("count", StringComparison.OrdinalIgnoreCase) && element.Item is { Length: > 0 } item)
                LatchNumber(name, inventory.GetCount(item));
    }

    /// <summary>
    /// The power meter (statusbar.yaml power-meter, Midnight Assault's HUD meter): while a throw
    /// cooks (A_HoldCharge), its pic at x, y and a bar in its box, as long as the box's width is
    /// of the power's 100, in the first of its levels whose below is more than the power (else
    /// its color). Nothing while there's no power. With no throw cooking it's the suspicion meter:
    /// how long the most suspicious enemy still waits for papers (Gameplay/Stealth.cs).
    /// </summary>
    internal void DrawPowerMeter()
    {
        if (StatusBarHidden) return;
        int power = session.PlayerState.WeaponPower > 0 ? session.PlayerState.WeaponPower : session.PlayerState.SuspicionMeter;
        if (power <= 0 || StatusBar.Get("power-meter") is not { } meter)
            return;
        using var _ = StatusBarOrigin(meter);
        int top = StatusBarTop(meter);
        if (!string.IsNullOrEmpty(meter.Pic))
            graphics.DrawPic(meter.Pic, meter.X, top + meter.Y);
        if (meter.Box.Count != 4)
            return;
        int width = meter.Box[2] * Math.Min(power, 100) / 100;
        var color = meter.Levels.FirstOrDefault(level => power < level.Below)?.Lit.FirstOrDefault()
            ?? meter.Color ?? "White";
        if (width > 0)
            video.Bar(meter.Box[0], top + meter.Box[1], width, meter.Box[3], color);
    }

    /// <summary>The weapon in hand's name (statusbar.yaml weapon-name): its `tag` (a $NAME language key or the text)</summary>
    internal void DrawWeaponName()
    {
        if (StatusBarHidden) return;
        if (StatusBar.Get("weapon-name") is not { } element || session.PlayerState.Weapon is not { } weapon)
            return;
        ClearBox(element);
        var font = element.Font ?? StatusBar.Get("numbers")?.Font;
        if (!string.IsNullOrEmpty(font) && inventory.GetStringProperty(weapon, "tag") is { Length: > 0 } tag)
            DrawStatusText(element, hudMessages.Localize(tag), font, element.Color ?? "White");
    }

    /// <summary>The level's name (statusbar.yaml location), as GetMapDisplayName gives it</summary>
    internal void DrawLocation()
    {
        if (StatusBarHidden) return;
        if (StatusBar.Get("location") is not { } element)
            return;
        var font = element.Font ?? StatusBar.Get("numbers")?.Font;
        if (!string.IsNullOrEmpty(font))
            DrawStatusText(element, hooks.MapDisplayName(session.GameState.MapOn), font, element.Color ?? "White");
    }

    /// <summary>The radar's power gauge (statusbar.yaml radar-gauge): its item's count out of its max</summary>
    internal void DrawRadarGauge()
    {
        if (StatusBarHidden) return;
        if (StatusBar.Get("radar-gauge") is { Item: { Length: > 0 } item } gauge)
            DrawGauge(gauge, item);
    }

    /// <summary>A gauge of segments lit from the bottom for how much of the item there is</summary>
    internal void DrawGauge(Assets.StatusBarElement gauge, string item)
    {
        int segments = Math.Max(gauge.Segments, 1);
        int max = inventory.GetMaxAmount(item);
        if (max == int.MaxValue)
            max = 100;
        int ammo = inventory.GetCount(item);
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
                    video.Bar(gauge.X, y, gauge.Width, 1, colors[line % colors.Count]);
        }
    }

    /*
    The info area (statusbar.yaml info-area): messages in a style with `anchor: Status`
    (hud-messages.yaml) are written here, newest last, instead of over the view. Its picture
    (pic, drawn at box's corner) is drawn under them; the text starts at x, y and keeps within
    size [width, height]. With no messages it shows its idle-text.
    */

    string? infoareashown;

    /// <summary>Whether the status bar has an info area showing now</summary>
    internal bool HasInfoArea => StatusBar.Get("info-area") != null && !(settings.ViewSize == 21 && session.InGame);

    internal void DrawInfoArea(bool force)
    {
        if (!HasInfoArea || StatusBar.Get("info-area") is not { } area)
            return;

        var messages = hudMessages.Messages.Where(m => m.Style.Anchor == Managers.HudAnchor.Status).ToList();
        string text = messages.Count > 0
            ? string.Join("\n", messages.Select(m => m.Text))
            : IdleText(area.IdleText);
        if (!force && text == infoareashown)
            return;
        infoareashown = text;

        using var origin = StatusBarOrigin(area);
        int top = StatusBarTop(area);
        if (!string.IsNullOrEmpty(area.Pic))
            graphics.DrawPic(area.Pic, area.Box.ElementAtOrDefault(0), top + area.Box.ElementAtOrDefault(1));
        else if (area.Box.Count == 4)
            video.Bar(area.Box[0], top + area.Box[1], area.Box[2], area.Box[3], area.BoxColor ?? "Black");

        var font = area.Font ?? messages.FirstOrDefault()?.Style.Font ?? UiFonts.SmallFont;
        string color = area.Color ?? "White";
        int width = area.Size.ElementAtOrDefault(0) is > 0 and var w ? w : 100;
        int height = area.Size.ElementAtOrDefault(1) is > 0 and var h ? h : 40;
        int y = top + area.Y;

        // the newest lines win when there are too many: drop the oldest from the top
        var lines = new List<(string Text, string Font, string Color)>();
        if (messages.Count > 0)
            foreach (var message in messages)
                foreach (var line in hooks.WrapText(message.Text, area.Font ?? message.Style.Font, width))
                    lines.Add((line, area.Font ?? message.Style.Font, area.Color ?? message.Style.Color));
        else
            lines.AddRange(hooks.WrapText(text, font, width).Select(line => (line, font, color)));

        int lineHeight = Math.Max(lines.Select(l => { ui.MeasureText(l.Text.Length > 0 ? l.Text : " ", l.Font, out _, out int lh); return lh; }).DefaultIfEmpty(8).Max(), 1);
        int fit = Math.Max(height / lineHeight, 1);
        foreach (var (line, lineFont, lineColor) in lines.Skip(Math.Max(lines.Count - fit, 0)))
        {
            graphics.DrawText(area.X, y, line, new Fonts.TextStyle(lineFont, lineColor));
            y += lineHeight;
        }
    }

    // The idle text, with each {Item} the count of that inventory item
    string IdleText(string? idle)
    {
        if (string.IsNullOrEmpty(idle))
            return "";
        var text = hudMessages.Localize(idle);
        return System.Text.RegularExpressions.Regex.Replace(text, @"\{(\w+)\}",
            m => inventory.GetCount(m.Groups[1].Value).ToString());
    }

    /*
    The heart monitor, as Blake Stone's: a trace scrolling left across count segments, a blip
    entering on the right each beat: a small one (segments 1-8) at 66% health and up, a bigger
    one (9-17) from 33%, and a ragged one (18-27) below. The heart beats while health is above
    bad-below, shows bad below it, and goes out when the player dies.
    */

    readonly int[] ecglegend = new int[6], ecgsegments = new int[6];
    int ecgscrolltics, hearttics;
    string? heartpic;

    /// <summary>Moves the heart monitor on by this tic's time, redrawing it as it changes</summary>
    internal void UpdateHeartMonitor()
    {
        if (StatusBar.Get("heart-monitor") == null || (settings.ViewSize == 21 && session.InGame))
            return;
        ecgscrolltics += (int)session.Tics;
        hearttics += (int)session.Tics;
        DrawHeartMonitor(force: false);
    }

    internal void DrawHeartMonitor(bool force)
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
            if (session.PlayerState.Health > 0 && ecglegend[last] == 0)
            {
                if (session.PlayerState.Health < 33)
                    (ecglegend[last], ecgsegments[last]) = (3, 18);
                else if (session.PlayerState.Health >= 66)
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
        if (session.PlayerState.Health <= 0)
            heart = monitor.HeartOff;
        else if (session.PlayerState.Health < monitor.BadBelow)
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
                graphics.DrawPic(string.Format(monitor.SegmentPic, ecgsegments[i]),
                    monitor.X + i * monitor.SegmentWidth, top + monitor.Y);
        if (!string.IsNullOrEmpty(heartpic))
            graphics.DrawPic(heartpic, monitor.HeartX, top + monitor.HeartY);
    }
}
