using PFWolf.Assets;
using PFWolf.Managers;

namespace PFWolf.Ui;

/// <summary>What the status bar drawing needs from the game</summary>
/// <param name="MapDisplayName">A map's name as the game shows it</param>
/// <param name="WrapText">Text split into lines no wider than a width in a font</param>
/// <param name="MostHealth">
/// The most health a player can have for now, where the match mode says (a Prop Hunt prop's
/// look), else null: the face then goes by their share of it
/// </param>
/// <param name="LevelTicsLeft">Tics left on a level against the clock, -1 for one that isn't (Gameplay/LevelTimer.cs)</param>
/// <param name="UseHint">The use hint that applies now, by name, or null (Gameplay/PlayerUse.cs USE HINTS)</param>
internal sealed record StatusBarHooks(
    Func<string, string> MapDisplayName,
    Func<string, string, int, List<string>> WrapText,
    Func<Entities.PlayerState, int?>? MostHealth = null,
    Func<int>? LevelTicsLeft = null,
    Func<string?>? UseHint = null,
    Func<IEnumerable<(int Left, int Full)>>? HealthBars = null,
    InfoScriptHooks? InfoScripts = null);

/// <summary>
/// The info area's messages with presenter codes (Ui/TextPresenter.cs THE STATUS BAR'S INFO AREA):
/// drawn at a place in a font (name), color, shadow and background; their animation moved on
/// (true when it drew); and taken away
/// </summary>
internal sealed record InfoScriptHooks(
    Action<string, int, int, string, int, int, int> Draw,
    Func<bool> Tick,
    Action Clear);

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
    /// Whether the status bar's parts aren't drawn now: with the view full screen and no
    /// full-screen layout, or for a player other than this machine's (the bar is the local
    /// player's). A bar drawn over the view (overlay) is drawn only with the frame
    /// (DrawingOverlay), as the view under it is.
    /// </summary>
    internal bool StatusBarHidden => NoBarInFullScreen || !session.ActingIsLocal || (IsOverlay && !DrawingOverlay);

    /// <summary>Whether the view is full screen in a game (view size 21)</summary>
    internal bool FullScreen => settings.ViewSize == 21 && session.InGame;

    /// <summary>With the view full screen, whether there's no status bar at all (no statusbar-fullscreen.yaml)</summary>
    internal bool NoBarInFullScreen => FullScreen && FullScreenLayout == null;

    /// <summary>
    /// Whether the status bar is drawn over the view every frame: statusbar.yaml background
    /// overlay, or the full-screen layout while the view is full screen
    /// </summary>
    internal bool IsOverlay => StatusBar.Get("background")?.Overlay == true || (FullScreen && FullScreenLayout != null);

    /// <summary>Set while a frame draws an overlay status bar's parts (PlayScreen.DrawStatusOverlay)</summary>
    internal bool DrawingOverlay { get; set; }

    private StatusBarAsset? statusbar, fullscreenbar;
    private bool fullscreenbarLooked;
    internal static readonly StatusBarAsset NoStatusBar = new();

    /// <summary>
    /// The layout the parts are drawn from now: the full-screen one (statusbar-fullscreen.yaml)
    /// while the view is full screen and the game pack has one, else the status bar's
    /// (<see cref="BarLayout"/>). Setting it sets the status bar's.
    /// </summary>
    internal StatusBarAsset StatusBar
    {
        get => FullScreen && FullScreenLayout is { } fullscreen ? fullscreen : BarLayout;
        set => statusbar = value;       // a layout of a test's own
    }

    /// <summary>
    /// The game pack's status bar layout (statusbar.yaml), whatever the view size; empty if it has
    /// none. Only a layout that was found is kept, so a look before the assets are loaded doesn't
    /// stick. The play area goes by this one (PlayArea).
    /// </summary>
    internal StatusBarAsset BarLayout => statusbar ?? (statusbar = assets?.FindInGamePackIfAny<StatusBarAsset>("statusbar")) ?? NoStatusBar;

    /// <summary>
    /// The game pack's layout for a full-screen view (statusbar-fullscreen.yaml), or null: drawn
    /// over the view every frame, as an overlay bar is. Looked for once in a game, when the
    /// assets are loaded.
    /// </summary>
    internal StatusBarAsset? FullScreenLayout
    {
        get
        {
            if (!fullscreenbarLooked && session.InGame)
            {
                fullscreenbar ??= assets?.FindInGamePackIfAny<StatusBarAsset>("statusbar-fullscreen");
                fullscreenbarLooked = assets != null;
            }
            return fullscreenbar;
        }
        set => (fullscreenbar, fullscreenbarLooked) = (value, true);    // a layout of a test's own
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
        if (StatusBar.Get(part) is not { } element)
            return;
        using var _ = StatusBarOrigin(element);
        graphics.DrawPic(picName, element.X, StatusBarTop(element) + element.Y);
    }

    /// <summary>A face picture at the face part, over its box (statusbar.yaml box) so the last face goes</summary>
    internal void StatusDrawFace(string picName)
    {
        if (StatusBar.Get("face") is { } face)
            ClearBox(face);
        StatusDrawPic(picName, "face");
    }

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
        {
            // With no box to clear, blanks over its digits, as Wolf3D's LatchNumber drew
            // N_BLANKPIC: a number hidden after it was shown (only-with-ammo) goes
            if (element.Box.Count != 4 && element.Behind is not { Length: > 0 } && element.Digits > 0)
                DrawStatusText(element, element.Prefix + new string(' ', element.Digits) + element.Suffix, font,
                    element.Color ?? numbers?.Color ?? "White");
            return;
        }

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
    /// Fills a part's box (statusbar.yaml box, box-color or box-pic) so what it showed before goes; not on a
    /// bar drawn over the view (overlay), where the frame under it is drawn afresh
    /// </summary>
    internal void ClearBox(Assets.StatusBarElement element)
    {
        if (element.Box.Count != 4 || IsOverlay)
            return;
        using var _ = StatusBarOrigin(element);
        int top = StatusBarTop(element) + element.Box[1];
        // box-pic: the picture across it, side by side (Blake Stone's ECG grid pieces under the health)
        if (!string.IsNullOrEmpty(element.BoxPic) && graphics.PicSize(element.BoxPic) is { Width: > 0 } piece)
        {
            for (int x = element.Box[0]; x < element.Box[0] + element.Box[2]; x += piece.Width)
                graphics.DrawPic(element.BoxPic, x, top);
            return;
        }
        video.Bar(element.Box[0], top, element.Box[2], element.Box[3], element.BoxColor ?? "Black");
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
        video.UseUiOrigin(element.Top ? UiAnchor.Top : UiAnchor.Bottom, PinSide(element));

    /// <summary>
    /// The side of a wide screen a part lines up with (statusbar.yaml pin): its own pin, else with
    /// the background's pin-edges the nearer edge by its x, else centered
    /// </summary>
    internal UiSide PinSide(Assets.StatusBarElement element) => element.Pin?.ToLowerInvariant() switch
    {
        "left" => UiSide.Left,
        "right" => UiSide.Right,
        "center" => UiSide.Center,
        _ when StatusBar.Get("background")?.PinEdges == true => element.X < 160 ? UiSide.Left : UiSide.Right,
        _ => UiSide.Center,
    };

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

    // The face (statusbar.yaml face): the grin, else the one for the player's health (as a share
    // of 100, where the mode caps it lower: a Prop Hunt prop), looking around through its pics,
    // else once dead the one for what killed them
    internal void DrawFace()
    {
        if (StatusBarHidden) return;
        if (FacePic() is { Length: > 0 } pic)
            StatusDrawFace(pic);
    }

    /// <summary>
    /// The face picture to show now, or null for none. A player class with its own face
    /// (actordefs player.face) shows that at every health, and its player.facegrin and
    /// player.facedead where it has them, else its face; a power-up's face still comes first.
    /// </summary>
    internal string? FacePic()
    {
        if (StatusBar.Get("face") is not { } face) return null;

        var player = session.PlayerState;
        var classFace = ClassFace("player.face");
        if (Grinning && (classFace != null ? ClassFace("player.facegrin") ?? classFace : face.Grin) is { Length: > 0 } grin)
            return grin;
        if (player.Health != 0 && Gameplay.Powerups.Face(inventory, player.FaceFrame) is { } powerupFace)
            return powerupFace;     // a power-up's own (End of Destiny's god mode face)
        if (player.Health != 0 && classFace != null)
            return classFace;
        if (player.Health != 0)
        {
            int health = player.Health;
            if (hooks.MostHealth?.Invoke(player) is > 0 and var most)
                health = Math.Max(1, health * 100 / most);
            var faces = face.Faces.OrderByDescending(f => f.Health).ToList();
            var pics = (faces.FirstOrDefault(f => health >= f.Health) ?? faces.LastOrDefault())?.Pics;
            return pics is { Count: > 0 } ? pics[player.FaceFrame % pics.Count] : null;
        }
        if (classFace != null)
            return ClassFace("player.facedead") ?? classFace;
        return player.LastAttacker != null && face.KilledBy.TryGetValue(player.LastAttacker.Name, out var killedBy) ? killedBy : face.Dead;
    }

    /// <summary>
    /// The face shown on a hit hard enough to wince (statusbar.yaml ouch, ouch-damage): a player
    /// class with its own face shows its player.faceouch, or doesn't wince without one; null for none
    /// </summary>
    internal string? OuchPic()
    {
        if (ClassFace("player.face") != null)
            return ClassFace("player.faceouch");
        return StatusBar.Get("face")?.Ouch is { Length: > 0 } ouch ? ouch : null;
    }

    /// <summary>
    /// One of the player class's face properties (player.face and the rest), its part in the
    /// mode's first (PlayerState.RoleClass): a picture name, or a list that the face looks
    /// around through; null when neither says
    /// </summary>
    string? ClassFace(string key)
    {
        var player = session.PlayerState;
        foreach (var playerClass in new[] { player.RoleClass, player.PlayerClass })
        {
            if (string.IsNullOrEmpty(playerClass))
                continue;
            switch (inventory.GetProperty(playerClass, key))
            {
                case string { Length: > 0 } pic:
                    return pic;
                case IList<object> { Count: > 0 } pics:
                    return pics[Math.Abs(player.FaceFrame) % pics.Count]?.ToString();
            }
        }
        return null;
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
            // rows: the slots fill a column of that many, then the next one along
            int row = layout.Rows > 0 ? slot % layout.Rows : slot, column = layout.Rows > 0 ? slot / layout.Rows : 0;
            int x = layout.X + layout.SpacingX * row + layout.ColumnSpacing * column, y = layout.Y + layout.Spacing * row;

            // size: a colored square for each key ("key.statusbarcolor" / "key.statusbaremptycolor")
            if (layout.Size.Count == 2)
            {
                var color = inventory.GetStringProperty(key, has ? "key.statusbarcolor" : "key.statusbaremptycolor");
                if (!string.IsNullOrEmpty(color))
                    video.Bar(x, StatusBarTop(layout) + y, layout.Size[0], layout.Size[1], color);
                continue;
            }

            // key-pics: the layout's own picture for each key, shown only while it's carried
            var pic = layout.KeyPics.Count > 0
                ? (has ? layout.KeyPics.GetValueOrDefault(key) : null)
                : inventory.GetStringProperty(key, has ? "key.statusbarpic" : "key.statusbaremptypic");
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
        if (StatusBar.Get("weapon") is { } element)
        {
            using var _ = StatusBarOrigin(element);
            if (!string.IsNullOrEmpty(icon))
            {
                graphics.DrawPic(icon, element.X, StatusBarTop(element) + element.Y);
                shownWeaponIcon = icon;
            }
            else if (shownWeaponIcon != null && !IsOverlay && !element.Top && graphics.PicSize(shownWeaponIcon) is { } size
                && StatusBar.Get("background") is { Pic: { Length: > 0 } bar })
            {
                // A weapon with no picture (a Prop Hunt prop's empty hands): the bar's own picture where the last one was
                graphics.DrawPicRegion(bar, element.X, element.Y, size.Width, size.Height, element.X, StatusBarTop(element) + element.Y);
                shownWeaponIcon = null;
            }
        }
        DrawWeaponCorner();
    }

    // The weapon picture last drawn, to take away for a weapon with none
    string? shownWeaponIcon;

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
        var p = session.PlayerState;
        int power = p.WeaponPower > 0 ? p.WeaponPower : p.Vehicle != null ? p.VehicleThrottle : p.SuspicionMeter;
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

    /// <summary>
    /// On a level against the clock (game-info map time-limit), the time left (statusbar.yaml
    /// level-timer) as minutes:seconds, the minutes right-aligned in its digits, and its label
    /// (level-timer-label: text). Nothing on a level that isn't timed, nor once the time's up, as
    /// Midnight Assault's DrawLevelTimer.
    /// </summary>
    internal void DrawLevelTimer()
    {
        if (StatusBarHidden) return;
        int left = hooks.LevelTicsLeft?.Invoke() ?? -1;
        if (left < 0)
            return;
        if (StatusBar.Get("level-timer-label") is { Text: { Length: > 0 } label } labelPart)
        {
            ClearBox(labelPart);
            if (left > 0 && (labelPart.Font ?? StatusBar.Get("numbers")?.Font) is { Length: > 0 } labelFont)
                DrawStatusText(labelPart, hudMessages.Localize(label), labelFont, labelPart.Color ?? "White");
        }
        if (StatusBar.Get("level-timer") is not { } element)
            return;
        ClearBox(element);
        if (left == 0 || (element.Font ?? StatusBar.Get("numbers")?.Font) is not { Length: > 0 } font)
            return;
        int seconds = (left + 69) / 70;
        string minutes = (seconds / 60).ToString().PadLeft(Math.Max(element.Digits, 1));
        DrawStatusText(element, $"{element.Prefix}{minutes}:{seconds % 60:00}{element.Suffix}", font, element.Color ?? StatusBar.Get("numbers")?.Color ?? "White");
    }

    /// <summary>
    /// The use hint (statusbar.yaml use-hint): while what the use key would act on gives a hint
    /// that the part has pictures for, one of them at x, y, back and forth frame-tics apiece on
    /// the level's clock (Midnight Assault's DrawIndicators)
    /// </summary>
    internal void DrawUseHint()
    {
        if (StatusBarHidden) return;
        if (StatusBar.Get("use-hint") is not { } element || hooks.UseHint?.Invoke() is not { Length: > 0 } hint
            || !element.Hints.TryGetValue(hint, out var pics) || pics.Count == 0)
            return;
        var pic = pics[HintFrame(session.GameState.TimeCount, Math.Max(element.FrameTics, 1), pics.Count)];
        using var _ = StatusBarOrigin(element);
        graphics.DrawPic(pic, element.X, StatusBarTop(element) + element.Y);
    }

    /// <summary>
    /// Health bars (statusbar.yaml enemy-health, Midnight Assault's DrawEnemyHealthHUDMeters): for
    /// up to count of the enemies with `healthbar: true` that are after someone, a box-color frame
    /// at x, y (each spacing lines below the last) round a bar width wide at full health and
    /// height high, in the first of its levels whose below is more than the bar's width (else its
    /// color); and while any show, the enemy-health-label part's text.
    /// </summary>
    internal void DrawEnemyHealth()
    {
        if (StatusBarHidden) return;
        if (StatusBar.Get("enemy-health") is not { } element || hooks.HealthBars?.Invoke() is not { } bars)
            return;
        int shown = 0, height = Math.Max(element.Height ?? 2, 1);
        using (StatusBarOrigin(element))
        {
            int top = StatusBarTop(element) + element.Y;
            foreach (var (left, full) in bars.Take(Math.Max(element.Count, 1)))
            {
                int y = top + shown++ * element.Spacing;
                int width = HealthBarWidth(left, full, element.Width);
                video.Bar(element.X, y, element.Width + 2, height + 2, element.BoxColor ?? "Black");
                var color = element.Levels.FirstOrDefault(level => width < level.Below)?.Lit.FirstOrDefault() ?? element.Color ?? "White";
                if (width > 0)
                    video.Bar(element.X + 1, y + 1, width, height, color);
            }
        }
        if (shown > 0 && StatusBar.Get("enemy-health-label") is { Text: { Length: > 0 } label } labelPart
            && (labelPart.Font ?? StatusBar.Get("numbers")?.Font) is { Length: > 0 } font)
            DrawStatusText(labelPart, hudMessages.Localize(label), font, labelPart.Color ?? "White");
    }

    /// <summary>A health bar's width: its share of the full width for the health left, rounded down</summary>
    internal static int HealthBarWidth(int left, int full, int width) =>
        full <= 0 ? 0 : Math.Clamp((int)(width * (left / (double)full)), 0, width);

    /// <summary>Which of count pictures shows at a time: 0 up to the last and back down, tics apiece</summary>
    internal static int HintFrame(int time, int tics, int count)
    {
        if (count <= 1)
            return 0;
        int period = 2 * count - 2;
        int step = time / tics % period;
        return step < count ? step : period - step;
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
    internal bool HasInfoArea => StatusBar.Get("info-area") != null && !NoBarInFullScreen;

    internal void DrawInfoArea(bool force)
    {
        if (!HasInfoArea || StatusBar.Get("info-area") is not { } area)
            return;

        var messages = hudMessages.Messages.Where(m => m.Style.Anchor == Managers.HudAnchor.Status).ToList();
        string text = messages.Count > 0
            ? string.Join("\n", messages.Select(m => m.Text))
            : IdleText(area.IdleText);

        // The newest message with presenter codes is shown on its own (Blake Stone's, with its
        // shapes and animation: Ui/TextPresenter.cs)
        var script = messages.Count > 0 && hooks.InfoScripts != null && TextPresenter.IsInfoScript(messages[^1].Text) ? messages[^1] : null;
        if (!force && text == infoareashown)
        {
            if (script != null)
            {
                using var animOrigin = StatusBarOrigin(area);
                hooks.InfoScripts!.Tick();
            }
            return;
        }
        infoareashown = text;

        using var origin = StatusBarOrigin(area);
        int top = StatusBarTop(area);
        if (!string.IsNullOrEmpty(area.Pic))
            graphics.DrawPic(area.Pic, area.Box.ElementAtOrDefault(0), top + area.Box.ElementAtOrDefault(1));
        else if (area.Box.Count == 4)
            video.Bar(area.Box[0], top + area.Box[1], area.Box[2], area.Box[3], area.BoxColor ?? "Black");

        hooks.InfoScripts?.Clear();
        if (script != null)
        {
            hooks.InfoScripts!.Draw(script.Text, area.X, top + area.Y, area.Font ?? script.Style.Font,
                int.TryParse(area.Color, out var c) ? c : 0xa6, area.ScriptShadow, area.ScriptBackground);
            return;
        }

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
        if (StatusBar.Get("heart-monitor") == null || NoBarInFullScreen)
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
