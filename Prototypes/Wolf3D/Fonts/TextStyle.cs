namespace Wolf3D.Fonts;

/// <summary>
/// How a piece of text is printed: which font (by name, e.g. "SmallFont" or "LargeFont") and
/// in what colors. Colors are names from the game pack's colors.yaml, #RRGGBB or a palette index.
/// </summary>
/// <param name="Background">The color behind the text, which the line input cursor blinks with</param>
internal readonly record struct TextStyle(string Font, string Color, string Background = "BKGDCOLOR")
{
    /// <summary>The same style with the text and background colors swapped</summary>
    public TextStyle Inverted => this with { Color = Background, Background = Color };
}
