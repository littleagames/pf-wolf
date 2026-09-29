namespace Wolf3D.Fonts;

/// <summary>
/// How a piece of text is printed: which font (by name, e.g. "SmallFont" or "LargeFont") and
/// in what colors. Colors are names from the game pack's colors.yaml, #RRGGBB or a palette index.
/// </summary>
/// <param name="Background">The color behind the text, which the line input cursor blinks with</param>
/// <param name="Shadow">
/// A shadow for this text in place of the font's own (fonts.yaml); null keeps the font's, and
/// <see cref="FontShadow.None"/> turns it off
/// </param>
/// <param name="ShadowColor">Draws whichever shadow the text has in this color instead of its own</param>
/// <param name="Gradient">
/// Shading of the text's color down the glyphs in place of the font's own; null keeps the
/// font's, and <see cref="FontGradient.None"/> turns it off
/// </param>
internal readonly record struct TextStyle(string Font, string Color, string Background = "BKGDCOLOR",
    FontShadow? Shadow = null, string? ShadowColor = null, FontGradient? Gradient = null)
{
    /// <summary>
    /// The same style with the text and background colors swapped. Its shadow is in the
    /// background color too and it has no gradient, so drawing text in it erases the text and
    /// its shadow.
    /// </summary>
    public TextStyle Inverted => this with
    {
        Color = Background, Background = Color, ShadowColor = Background, Gradient = FontGradient.None
    };

    /// <summary>The same style with a shadow; one pixel right and down, in black, by default</summary>
    public TextStyle WithShadow(FontShadow? shadow = null) => this with { Shadow = shadow ?? FontShadow.Default };

    /// <summary>The same style with a gradient; 40% lighter at the top to 40% darker at the bottom by default</summary>
    public TextStyle WithGradient(FontGradient? gradient = null) => this with { Gradient = gradient ?? FontGradient.Default };
}
