namespace PFWolf.Ui;

/// <summary>
/// The Sound menu (menudefs/sound): each sound device and music switched on or off, and the
/// sound and music volume sliders moved with left/right
/// </summary>
internal sealed class SoundMenu(AudioManager audio, VideoManager video, MenuDrawing draw, MenuLoop loop, MenuInput menuInput, MenuDefs defs)
{
    // Loaded from menudefs/ in CheckForEpisodes
    internal CP_itemtype[] Items = [];
    internal CP_iteminfo Info = null!;
    internal int Run()
    {
        int which;

        DrawSoundMenu();
        draw.MenuFadeIn();
        menuInput.WaitKeyUp();

        do
        {
            which = loop.HandleMenu(Info, Items, null, AdjustSoundVolume);
            //
            // HANDLE MENU CHOICES
            //
            // Each row switches its device; the shot sound previews what the fallback plays now
            switch (MenuDefs.SelectedId(Items, which))
            {
                case "sound-pc":
                    audio.PcSoundEnabled ^= true;
                    DrawSoundMenu();
                    loop.ShootSnd();
                    break;
                case "sound-adlib":
                    audio.AdLibSoundEnabled ^= true;
                    DrawSoundMenu();
                    loop.ShootSnd();
                    break;
                case "sound-digitized":
                    audio.DigitizedSoundEnabled ^= true;
                    DrawSoundMenu();
                    loop.ShootSnd();
                    break;
                case "music":
                    audio.MusicEnabled ^= true;
                    DrawSoundMenu();
                    loop.ShootSnd();
                    break;
            }
        }
        while (which >= 0);

        draw.MenuFadeOut();

        return 0;
    }

    internal void DrawSoundMenu()
    {
        defs.DrawMenuComponents("sound");
        draw.DrawMenu(Info, Items);

        draw.DrawMenuCheckbox(Info, Items, "sound-pc", audio.PcSoundEnabled);
        draw.DrawMenuCheckbox(Info, Items, "sound-adlib", audio.AdLibSoundEnabled);
        draw.DrawMenuCheckbox(Info, Items, "sound-digitized", audio.DigitizedSoundEnabled);
        draw.DrawMenuCheckbox(Info, Items, "music", audio.MusicEnabled);
        draw.DrawMenuSlider(Info, Items, "sound-volume", audio.SoundVolume, AudioManager.MaxVolume);
        draw.DrawMenuSlider(Info, Items, "music-volume", audio.MusicVolume, AudioManager.MaxVolume);

        draw.DrawMenuGun(Info);
        video.Update();
    }

    // Left/right on a volume row. The sound volume previews with a menu sound; the music
    // volume is heard straight away on the menu's track.
    private void AdjustSoundVolume(int which, int delta)
    {
        switch (MenuDefs.SelectedId(Items, which))
        {
            case "sound-volume":
                int soundVolume = audio.SoundVolume;
                audio.SoundVolume += delta;
                if (audio.SoundVolume == soundVolume)
                    return;
                draw.DrawMenuSlider(Info, Items, "sound-volume", audio.SoundVolume, AudioManager.MaxVolume);
                break;

            case "music-volume":
                int musicVolume = audio.MusicVolume;
                audio.MusicVolume += delta;
                if (audio.MusicVolume == musicVolume)
                    return;
                draw.DrawMenuSlider(Info, Items, "music-volume", audio.MusicVolume, AudioManager.MaxVolume);
                break;

            default:
                return;
        }

        video.Update();
        audio.Play("menu/move1");
    }
}
