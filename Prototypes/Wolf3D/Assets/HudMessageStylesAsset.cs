namespace Wolf3D.Assets;

/// <summary>
/// A game pack's message styles (gamepacks/{pack}/hud-messages.yaml), by name. A pack that
/// builds on a base-pack starts from its styles; the pack's own replace them name by name.
/// </summary>
internal record HudMessageStylesAsset : Asset
{
    public HudMessageStylesAsset()
    {
    }

    public HudMessageStylesAsset(Dictionary<string, HudMessageStyleDefinition> styles)
    {
        foreach (var (name, style) in styles)
            Styles[name] = style;
    }

    public Dictionary<string, HudMessageStyleDefinition> Styles { get; } = new(StringComparer.OrdinalIgnoreCase);

    public override void Merge(Asset other)
    {
        if (other is HudMessageStylesAsset otherAsset)
        {
            foreach (var (name, style) in otherAsset.Styles)
                Styles[name] = style;
        }
    }
}

/// <summary>
/// One style in hud-messages.yaml. Anything left out comes from the parent style, and a style
/// with no parent builds on Default.
/// </summary>
internal class HudMessageStyleDefinition
{
    /// <summary>The style this one starts from; defaults to Default</summary>
    public string? Parent { get; set; }

    /// <summary>
    /// Where in the 3D view the messages sit: TopLeft, Top, TopRight, Left, Center, Right,
    /// BottomLeft, Bottom or BottomRight
    /// </summary>
    public string? Anchor { get; set; }

    /// <summary>How far to move the messages right (negative for left) from the anchor, in 320x200 pixels</summary>
    public int? X { get; set; }

    /// <summary>How far to move the messages down (negative for up) from the anchor, in 320x200 pixels</summary>
    public int? Y { get; set; }

    /// <summary>The gap kept between the messages and the edges of the view</summary>
    public int? Margin { get; set; }

    /// <summary>A font name from fonts.yaml</summary>
    public string? Font { get; set; }

    /// <summary>A color name, #RRGGBB or palette index</summary>
    public string? Color { get; set; }

    /// <summary>How long a message stays up, in tics (70 a second)</summary>
    public int? Duration { get; set; }

    /// <summary>How many messages can be up at this position at once; the oldest goes first</summary>
    public int? MaxLines { get; set; }
}
