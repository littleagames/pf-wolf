using Wolf3D.Assets;
using static SDL2.SDL;

namespace Wolf3D.Extensions;

internal static class SDLExtensions
{
    public static SDL_Color[] ToSDLColors(this Palette entry)
    {
        var colors = new SDL_Color[256];
        for (int i = 0; i < 256; i++)
        {
            colors[i] = new SDL_Color
            {
                r = entry.Colors[i].Red,
                g = entry.Colors[i].Green,
                b = entry.Colors[i].Blue,
                a = 255
            };
        }
        return colors;
    }
}
