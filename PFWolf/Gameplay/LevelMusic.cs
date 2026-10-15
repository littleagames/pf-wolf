namespace PFWolf.Gameplay;

/// <summary>The level's song: started with the level, paused for the menu and pause, and resumed after</summary>
internal sealed class LevelMusic(GameEngineManager engine, AudioManager audio, GameSession session)
{
    internal string lastmusicchunk = "";

    internal void StartMusic()
    {
        //audio.SetPaused(true);
        var gameInfo = engine.GetGameInfo();
        var song = gameInfo.Maps[session.gamestate.mapon].Music;
        lastmusicchunk = song;
        if (!string.IsNullOrEmpty(song))        // a map with no music keeps quiet
            audio.PlayMusic(lastmusicchunk);
    }

    // Music offsets aren't tracked, so this resumes the level's song if it's the one paused, and
    // otherwise (after the menu or a loaded game replaced it) starts it over.
    internal void ContinueMusic(int offs)
    {
        var gameInfo = engine.GetGameInfo();
        var song = gameInfo.Maps[session.gamestate.mapon].Music;
        lastmusicchunk = song;
        if (string.Equals(audio.CurrentMusicTrack, song, StringComparison.OrdinalIgnoreCase))
            audio.SetPaused(false);
        else if (!string.IsNullOrEmpty(song))
            audio.PlayMusic(song);
    }

    internal int StopMusic()
    {
        audio.SetPaused(true);
        return 0;
    }
}