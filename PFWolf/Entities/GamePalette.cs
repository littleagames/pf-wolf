using SDL2;

namespace PFWolf.Entities;

internal class GamePalette
{
    public SDL.SDL_Color[] Colors { get; set; } = new SDL.SDL_Color[256];
}
