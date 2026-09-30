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

    /// <summary>keys: pixels between one key slot and the next, downwards</summary>
    public int Spacing { get; set; }
}
