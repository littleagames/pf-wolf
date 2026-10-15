using SDL2;

namespace PFWolf.Ui;

/// <summary>The jukebox: a page of the game's songs to play, picked at random each time it opens</summary>
internal sealed class Jukebox(
    InputManager input,
    VideoManager video,
    AudioManager audio,
    MenuDrawing draw,
    MenuLoop loop,
    MenuDefs defs)
{
    internal CP_itemtype[] MusicMenu = [];
    // The jukebox shows one page of songs at a time
    internal const int JukeboxPageSize = 6;

    // Loaded from menudefs/ in CheckForEpisodes
    internal CP_iteminfo MusicItems = null!;

    internal void DoJukebox()
    {
        int which, lastsong = -1;

        input.ClearKeysDown();
        //if (!AdLibPresent && !SoundBlasterPresent)
        //    return;

        draw.MenuFadeOut();

        // Show a random page of songs
        int pages = Math.Max(1, (MusicMenu.Length + JukeboxPageSize - 1) / JukeboxPageSize);
        int start = (int)((SDL.SDL_GetTicks() / 10) % (uint)pages) * JukeboxPageSize;
        var page = MusicMenu.Skip(start).Take(JukeboxPageSize).ToArray();
        MusicItems.amount = (short)page.Length;
        MusicItems.curpos = 0;

        //CA_LoadAllSounds();

        defs.DrawMenuComponents("jukebox");

        draw.DrawMenu(MusicItems, page);

        video.Update();
        draw.MenuFadeIn();

        do
        {
            which = loop.HandleMenu(MusicItems, page, null);
            if (which >= 0)
            {
                if (lastsong >= 0)
                    page[lastsong].active = 1;

                if (page[which].data is string song)
                    audio.PlayMusic(song);
                page[which].active = 2;
                draw.DrawMenu(MusicItems, page);
                video.Update();
                lastsong = which;
            }
        } while (which >= 0);

        draw.MenuFadeOut();
        input.ClearKeysDown();
    }
}
