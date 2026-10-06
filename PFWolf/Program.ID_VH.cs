using PFWolf.Fonts;

namespace PFWolf;


internal partial class Program
{
    internal const string SMALL_FONT = "SmallFont";
    internal const string LARGE_FONT = "LargeFont";

    /// <summary>Control panel menu items are printed in this</summary>
    internal const string MENU_FONT = LARGE_FONT;

    /// <summary>Control panel text in <paramref name="color"/> on the menu background</summary>
    internal static TextStyle MenuStyle(string color) => new(MENU_FONT, color);
}
