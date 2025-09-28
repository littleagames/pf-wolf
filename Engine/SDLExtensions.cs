namespace Engine;

internal static class SDLExtensions
{
    internal static SDL.Color[] ToSDLColors(this PFWolf.Common.Assets.Palette palette)
    {
        SDL.Color[] sdlColors = new SDL.Color[palette.Colors.Length];
        for (int i = 0; i < palette.Colors.Length; i++)
        {
            sdlColors[i] = palette.Colors[i].ToSDLColor();
        }
        return sdlColors;
    }
    internal static SDL.Color ToSDLColor(this PFWolf.Common.Assets.PaletteColor color)
    {
        return new SDL.Color
        {
            R = color.Red,
            G = color.Green,
            B = color.Blue,
            A = color.Alpha
        };
    }
}
