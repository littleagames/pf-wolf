namespace Wolf3D.Assets;

/// <summary>
/// A game pack's status bar layout (gamepacks/{pack}/statusbar.yaml): its background and where
/// each part is drawn on it, by part name. A pack that builds on a base-pack starts from its
/// layout; the pack's own parts replace them one by one.
/// </summary>
internal record StatusBarAsset : Asset
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

    public override void Merge(Asset other)
    {
        if (other is StatusBarAsset otherAsset)
        {
            foreach (var (name, element) in otherAsset.Elements)
                Elements[name] = element;
        }
    }
}

/// <summary>
/// One part of statusbar.yaml. Positions are in 320x200 pixels from the status bar's top left
/// corner; which fields a part uses depends on the part.
/// </summary>
internal class StatusBarElement
{
    public int X { get; set; }
    public int Y { get; set; }

    /// <summary>background: the picture drawn across the bottom of the screen</summary>
    public string? Pic { get; set; }

    /// <summary>background: how many 320x200 pixel lines the status bar takes from the view</summary>
    public int? Height { get; set; }

    /// <summary>Numbers: how many digits the number takes, right-aligned (a longer number shows its last digits)</summary>
    public int Digits { get; set; } = 1;

    /// <summary>Numbers: a font name from fonts.yaml</summary>
    public string? Font { get; set; }

    /// <summary>Numbers: a color name, #RRGGBB or palette index</summary>
    public string? Color { get; set; }

    /// <summary>lives: the sound played when the player is given an extra life</summary>
    public string? Sound { get; set; }

    /// <summary>keys: pixels between one key slot and the next, downwards</summary>
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

    /// <summary>Any part: placed on the top status bar (statusbar.yaml top) instead of the bottom one</summary>
    public bool Top { get; set; }

    /// <summary>Numbers: text drawn before and after the number, e.g. "FLOOR: " or "%"</summary>
    public string Prefix { get; set; } = "";
    public string Suffix { get; set; } = "";

    /// <summary>Numbers and location: left (x is the left edge, the default), right (x is the right edge) or center (x is the middle)</summary>
    public string Align { get; set; } = "left";

    /// <summary>Numbers: [x, y, width, height] filled with box-color before the number is drawn, so the old one goes</summary>
    public List<int> Box { get; set; } = [];
    public string? BoxColor { get; set; }

    /// <summary>Numbers: another part (weapon) drawn first, under the number</summary>
    public string? Behind { get; set; }

    /// <summary>ammo: shown only while the weapon in hand uses ammo</summary>
    public bool OnlyWithAmmo { get; set; }

    /// <summary>keys: pixels between one key slot and the next, rightwards (spacing is downwards)</summary>
    public int SpacingX { get; set; }

    /// <summary>keys: [width, height] of a colored square per key ("key.statusbarcolor" / "key.statusbaremptycolor") in place of pictures</summary>
    public List<int> Size { get; set; } = [];

    /// <summary>charge: the pictures for a charging weapon (weapon.chargetics) ready to fire, and charging</summary>
    public string? Ready { get; set; }
    public string? Wait { get; set; }

    /// <summary>ammo-gauge: the strip's width, how many segments it has and each one's height (lines)</summary>
    public int Width { get; set; } = 8;
    public int Segments { get; set; } = 20;
    public int SegmentHeight { get; set; } = 2;

    /// <summary>
    /// ammo-gauge: the colors by how empty it is: the first level whose below is more than the
    /// segments left unlit gives its lit and dim colors (a color per line of a segment)
    /// </summary>
    public List<StatusBarGaugeLevel> Levels { get; set; } = [];

    /// <summary>heart-monitor: the trace's pictures ({0:00} the segment number) and how many across, each width wide</summary>
    public string? SegmentPic { get; set; }
    public int Count { get; set; } = 6;
    public int SegmentWidth { get; set; } = 8;

    /// <summary>heart-monitor: tics between trace scrolls, and between heart beats</summary>
    public int ScrollTics { get; set; } = 7;
    public int PulseTics { get; set; } = 70;

    /// <summary>heart-monitor: the heart, beating (good, then off) above bad-below health, else bad; off when dead</summary>
    public int HeartX { get; set; }
    public int HeartY { get; set; }
    public string? HeartGood { get; set; }
    public string? HeartBad { get; set; }
    public string? HeartOff { get; set; }
    public int BadBelow { get; set; } = 40;

    /// <summary>
    /// info-area: shown while there are no messages for it (a $NAME language key or the text);
    /// {Item} in it is how many of an inventory item the player has, e.g. {FoodToken}
    /// </summary>
    public string? IdleText { get; set; }

    /// <summary>
    /// gauge (radar-gauge): the inventory item whose count it shows, out of its max amount;
    /// radar: the item that powers its zoom (used up while zoomed in)
    /// </summary>
    public string? Item { get; set; }

    /// <summary>radar: how many map tiles across it shows at its widest (1 pixel each)</summary>
    public int Tiles { get; set; } = 32;

    /// <summary>radar: the zoom picture for each zoom (1x, 2x, 4x), drawn at zoom-x, zoom-y</summary>
    public List<string> ZoomPics { get; set; } = [];
    public int ZoomX { get; set; }
    public int ZoomY { get; set; }

    /// <summary>
    /// radar: its colors (palette indices, color names or #RRGGBB) by what's there: unmapped,
    /// floor, door, locked, player, key, enemy
    /// </summary>
    public Dictionary<string, string> Colors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>weapon-corner: the picture for each weapon, by its class</summary>
    public Dictionary<string, string> WeaponPics { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>radar: shown as its item runs out while zoomed in (a $NAME language key or the text)</summary>
    public string? EmptyMessage { get; set; }
}

/// <summary>An ammo gauge's colors while fewer than <see cref="Below"/> of its segments are unlit</summary>
internal class StatusBarGaugeLevel
{
    public int Below { get; set; } = int.MaxValue;
    public List<string> Lit { get; set; } = [];
    public List<string> Dim { get; set; } = [];
}

/// <summary>One of the face part's faces: from this health up, the pics it looks around through.</summary>
internal class StatusBarFace
{
    public int Health { get; set; }
    public List<string> Pics { get; set; } = [];
}
