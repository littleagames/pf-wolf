namespace PFWolf.Assets;

/// <summary>
/// A game pack's status bar layout (gamepacks/{pack}/statusbar.yaml): its background and where
/// each part is drawn on it, by part name. A pack that builds on a base-pack starts from its
/// layout; the pack's own parts replace them one by one. statusbar-fullscreen.yaml is a layout
/// of the same parts drawn over the view while it's full screen (view size 21), as an overlay bar.
/// </summary>
public record StatusBarAsset : Asset
{
    public StatusBarAsset()
    {
    }

    public StatusBarAsset(Dictionary<string, StatusBarElement> elements)
    {
        foreach (var (name, element) in elements)
            Elements[name] = element;
    }

    public Dictionary<string, StatusBarElement> Elements { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A part of the layout, or null if the status bar doesn't have it (it isn't drawn).</summary>
    public StatusBarElement? Get(string name) => Elements.GetValueOrDefault(name);
}

/// <summary>
/// One part of statusbar.yaml. Positions are in 320x200 pixels from the status bar's top left
/// corner; which fields a part uses depends on the part.
/// </summary>
public class StatusBarElement
{
    /// <summary>Where the part goes across, from the status bar's left edge (its left edge, or as align says)</summary>
    public int X { get; set; }

    /// <summary>Where the part goes down, from the status bar's top edge</summary>
    public int Y { get; set; }

    /// <summary>background: the picture drawn across the bottom of the screen</summary>
    public string? Pic { get; set; }

    /// <summary>background: how many 320x200 pixel lines the status bar takes from the view; enemy-health: each bar's height</summary>
    public int? Height { get; set; }

    /// <summary>
    /// background: the bar takes nothing from the view; its parts (and its picture, if it has one)
    /// are drawn over the bottom height lines of the view every frame, as Midnight Assault's HUD
    /// was. Numbers' boxes aren't filled then.
    /// </summary>
    public bool Overlay { get; set; }

    /// <summary>weapon-slots: how many slot numbers across (1 up), each followed by a space</summary>
    public int Slots { get; set; } = 8;

    /// <summary>
    /// background, top: on a screen wider than the picture, the color its lines are filled with
    /// beside it (Black when left out). Wolf3D's bar is filled by its border's sides instead.
    /// </summary>
    public string? SideColor { get; set; }

    /// <summary>Numbers: how many digits the number takes, right-aligned (a longer number shows its last digits)</summary>
    public int Digits { get; set; } = 1;

    /// <summary>Numbers: a font name from fonts.yaml</summary>
    public string? Font { get; set; }

    /// <summary>Numbers: a color name, #RRGGBB or palette index</summary>
    public string? Color { get; set; }

    /// <summary>lives: the sound played when the player is given an extra life</summary>
    public string? Sound { get; set; }

    /// <summary>keys: pixels between one key slot and the next, downwards; enemy-health: between one bar and the next</summary>
    public int Spacing { get; set; }

    /// <summary>
    /// border: on a screen wider than the picture, the border color fills from each screen
    /// edge to this many pixels into the picture
    /// </summary>
    public int Sides { get; set; }

    /// <summary>
    /// border: the picture's own border-colored areas, as [x, y, width, height], painted over
    /// when the border is another color. One touching the picture's left or right edge reaches
    /// out to that screen edge.
    /// </summary>
    public List<List<int>> Rects { get; set; } = [];

    /// <summary>
    /// face: the faces by health. The player shows the first whose health they have at least
    /// (highest first), one of its pics at a time as the face looks around.
    /// </summary>
    public List<StatusBarFace> Faces { get; set; } = [];

    /// <summary>face: the face once the player is dead</summary>
    public string? Dead { get; set; }

    /// <summary>face: the face once dead, by the class of what killed the player, over <see cref="Dead"/></summary>
    public Dictionary<string, string> KilledBy { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// face: the grin, while the pickup sound of an item or weapon flagged WEAPON.ALWAYSGRIN plays
    /// </summary>
    public string? Grin { get; set; }

    /// <summary>
    /// face: Spear of Destiny's wince, shown in place of the face on a hit of more than
    /// <see cref="OuchDamage"/> (the face then starts its next look round afresh); only when
    /// the game has the picture
    /// </summary>
    public string? Ouch { get; set; }

    /// <summary>face: how much one hit has to take (more than this) for <see cref="Ouch"/></summary>
    public int OuchDamage { get; set; } = 30;

    /// <summary>Any part: placed on the top status bar (statusbar.yaml top) instead of the bottom one</summary>
    public bool Top { get; set; }

    /// <summary>
    /// Any part: on a screen wider than 320x200, the side its 320 pixels line up with: left or
    /// right (flush with that screen edge) or center. Left out, the background's pin-edges decides.
    /// </summary>
    public string? Pin { get; set; }

    /// <summary>
    /// background: on a screen wider than 320x200, each part without a pin of its own lines up
    /// with the nearer screen edge: left with x under 160, else right (a HUD drawn over the view)
    /// </summary>
    public bool PinEdges { get; set; }

    /// <summary>Numbers: text drawn before and after the number, e.g. "FLOOR: " or "%"</summary>
    public string Prefix { get; set; } = "";

    /// <summary>Numbers: text drawn after the number, e.g. "%"</summary>
    public string Suffix { get; set; } = "";

    /// <summary>Numbers and location: left (x is the left edge, the default), right (x is the right edge) or center (x is the middle)</summary>
    public string Align { get; set; } = "left";

    /// <summary>Numbers: [x, y, width, height] filled with box-color before the number is drawn, so the old one goes</summary>
    public List<int> Box { get; set; } = [];

    /// <summary>Numbers: the color the box is filled with; enemy-health: each bar's frame (Black when left out)</summary>
    public string? BoxColor { get; set; }

    /// <summary>Numbers: another part (weapon) drawn first, under the number</summary>
    public string? Behind { get; set; }

    /// <summary>ammo: shown only while the weapon in hand uses ammo</summary>
    public bool OnlyWithAmmo { get; set; }

    /// <summary>icon-*: shown only while the weapon in hand has a magazine (weapon.magazine), e.g. a divider between the clip and the ammo</summary>
    public bool OnlyWithMagazine { get; set; }

    /// <summary>keys: pixels between one key slot and the next, rightwards (spacing is downwards)</summary>
    public int SpacingX { get; set; }

    /// <summary>
    /// keys: how many slots go in one column (0: all of them); slot <see cref="Rows"/> starts the
    /// next column, <see cref="ColumnSpacing"/> pixels to the right, as End of Destiny's 2x2 block
    /// </summary>
    public int Rows { get; set; }

    /// <summary>keys: pixels between one column of slots and the next (with <see cref="Rows"/>)</summary>
    public int ColumnSpacing { get; set; } = 8;

    /// <summary>keys: [width, height] of a colored square per key ("key.statusbarcolor" / "key.statusbaremptycolor") in place of pictures</summary>
    public List<int> Size { get; set; } = [];

    /// <summary>charge: the pictures for a charging weapon (weapon.chargetics) ready to fire, and charging</summary>
    public string? Ready { get; set; }

    /// <summary>charge: the picture while the weapon is still charging</summary>
    public string? Wait { get; set; }

    /// <summary>ammo-gauge: the strip's width, how many segments it has and each one's height (lines); enemy-health: a bar's width at full health</summary>
    public int Width { get; set; } = 8;

    /// <summary>ammo-gauge, radar-gauge: how many segments the strip has, top to bottom</summary>
    public int Segments { get; set; } = 20;

    /// <summary>ammo-gauge, radar-gauge: each segment's height, in lines (a color per line from its level)</summary>
    public int SegmentHeight { get; set; } = 2;

    /// <summary>
    /// ammo-gauge: the colors by how empty it is: the first level whose below is more than the
    /// segments left unlit gives its lit and dim colors (a color per line of a segment)
    /// </summary>
    public List<StatusBarGaugeLevel> Levels { get; set; } = [];

    /// <summary>heart-monitor: the trace's pictures ({0:00} the segment number) and how many across, each width wide; enemy-health: the most bars shown</summary>
    public string? SegmentPic { get; set; }

    /// <summary>heart-monitor: how many trace segments across; enemy-health: the most bars shown at once</summary>
    public int Count { get; set; } = 6;

    /// <summary>heart-monitor: pixels from one trace segment to the next</summary>
    public int SegmentWidth { get; set; } = 8;

    /// <summary>heart-monitor: tics between trace scrolls, and between heart beats</summary>
    public int ScrollTics { get; set; } = 7;

    /// <summary>heart-monitor: tics from one heart beat to the next (70 a second): half shown good, half off</summary>
    public int PulseTics { get; set; } = 70;

    /// <summary>heart-monitor: the heart, beating (good, then off) above bad-below health, else bad; off when dead</summary>
    public int HeartX { get; set; }

    /// <summary>heart-monitor: the heart's top edge</summary>
    public int HeartY { get; set; }

    /// <summary>heart-monitor: the heart at bad-below health or more, beating: shown, then heart-off, every pulse-tics</summary>
    public string? HeartGood { get; set; }

    /// <summary>heart-monitor: the heart below bad-below health, shown steadily rather than beating</summary>
    public string? HeartBad { get; set; }

    /// <summary>heart-monitor: the picture between beats, and once the player is dead</summary>
    public string? HeartOff { get; set; }

    /// <summary>heart-monitor: the health below which the heart beats bad</summary>
    public int BadBelow { get; set; } = 40;

    /// <summary>
    /// info-area: shown while there are no messages for it (a $NAME language key or the text);
    /// {Item} in it is how many of an inventory item the player has, e.g. {FoodToken}
    /// </summary>
    public string? IdleText { get; set; }

    /// <summary>info-area: a message with presenter codes (Blake Stone's): its text's shadow, a palette index (default 4)</summary>
    public int ScriptShadow { get; set; } = 0x04;

    /// <summary>info-area: a message with presenter codes: what its shapes are drawn on, a palette index (default 1)</summary>
    public int ScriptBackground { get; set; } = 0x01;

    /// <summary>
    /// gauge (radar-gauge): the inventory item whose count it shows, out of its max amount;
    /// radar: the item that powers its zoom (used up while zoomed in)
    /// </summary>
    public string? Item { get; set; }

    /// <summary>radar: how many map tiles across it shows at its widest (1 pixel each)</summary>
    public int Tiles { get; set; } = 32;

    /// <summary>radar: the zoom picture for each zoom (1x, 2x, 4x), drawn at zoom-x, zoom-y</summary>
    public List<string> ZoomPics { get; set; } = [];

    /// <summary>radar: the zoom picture's left edge</summary>
    public int ZoomX { get; set; }

    /// <summary>radar: the zoom picture's top edge</summary>
    public int ZoomY { get; set; }

    /// <summary>
    /// radar: turned so the way the player faces is up (Planet Strike's); false keeps north up,
    /// the player in the middle, as Midnight Assault's corner map
    /// </summary>
    public bool Rotate { get; set; } = true;

    /// <summary>radar: living enemies on seen tiles are shown at every zoom, not only zoomed in</summary>
    public bool Enemies { get; set; }

    /// <summary>
    /// radar: shown only on the maps whose game-info says radar: true (Midnight Assault's
    /// Stealth Challenge), not on every level
    /// </summary>
    public bool MapsOnly { get; set; }

    /// <summary>
    /// radar: its colors (palette indices, color names or #RRGGBB) by what's there: unmapped,
    /// floor, door, locked, player, key, enemy, or an enemy's class name for that class's own
    /// (Midnight Assault's officers). weapon-slots: current (the weapon in hand's slot), held (a
    /// slot with a weapon held) and empty.
    /// </summary>
    public Dictionary<string, string> Colors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>weapon-corner: the picture for each weapon, by its class</summary>
    public Dictionary<string, string> WeaponPics { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// keys: the picture for each key, by its class, in place of its key.statusbarpic; a key
    /// that isn't carried, or isn't listed, shows nothing (a full-screen HUD's keys)
    /// </summary>
    public Dictionary<string, string> KeyPics { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>radar: shown as its item runs out while zoomed in (a $NAME language key or the text)</summary>
    public string? EmptyMessage { get; set; }

    /// <summary>level-timer-label: the text drawn by the level timer (a $NAME language key or the text)</summary>
    public string? Text { get; set; }

    /// <summary>
    /// use-hint: each hint's pictures by name (the names walls, patches, use triggers, actors'
    /// usehint and weapons' backhint/facehint give), drawn at x, y while it applies. Several
    /// pictures animate, frame-tics each, back and forth (Midnight Assault's indicators)
    /// </summary>
    public Dictionary<string, List<string>> Hints { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>use-hint: tics each of a hint's pictures shows</summary>
    public int FrameTics { get; set; } = 16;
}

/// <summary>An ammo gauge's colors while fewer than <see cref="Below"/> of its segments are unlit</summary>
public class StatusBarGaugeLevel
{
    /// <summary>
    /// ammo-gauge, radar-gauge: the level applies while fewer segments than this are unlit;
    /// enemy-health: while a bar is narrower than this. Left out, always.
    /// </summary>
    public int Below { get; set; } = int.MaxValue;

    /// <summary>The colors of a lit segment, one per line, repeated down it (an enemy-health bar uses the first)</summary>
    public List<string> Lit { get; set; } = [];

    /// <summary>The colors of an unlit segment, one per line; the unlit ones are at the top</summary>
    public List<string> Dim { get; set; } = [];
}

/// <summary>One of the face part's faces: from this health up, the pics it looks around through.</summary>
public class StatusBarFace
{
    /// <summary>The least health this face is shown at</summary>
    public int Health { get; set; }

    /// <summary>The pictures the face looks around through (Wolf3D's second looks ahead)</summary>
    public List<string> Pics { get; set; } = [];
}
