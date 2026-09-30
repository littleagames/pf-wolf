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
}

/// <summary>One of the face part's faces: from this health up, the pics it looks around through.</summary>
internal class StatusBarFace
{
    public int Health { get; set; }
    public List<string> Pics { get; set; } = [];
}
