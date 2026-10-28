namespace PFWolf.Gameplay;

/// <summary>The level's song: started with the level, paused for the menu and pause, and resumed after</summary>
internal sealed class LevelMusic(GameEngineManager engine, AudioManager audio, GameSession session)
{
    internal string LastMusicChunk = "";

    /// <summary>The song to play in place of the level's own, if the scripts say (game.music, game.onmusic); else the same</summary>
    internal Func<string, string>? ChooseSong { get; init; }

    // The level's song, or the one the scripts chose
    string LevelSong()
    {
        var song = engine.GetGameInfo().Maps[session.GameState.MapOn].Music;
        return ChooseSong?.Invoke(song) ?? song;
    }

    internal void StartMusic()
    {
        //audio.SetPaused(true);
        var song = LevelSong();
        LastMusicChunk = song;
        if (!string.IsNullOrEmpty(song))        // a map with no music keeps quiet
            audio.PlayMusic(LastMusicChunk);
    }

    // Music offsets aren't tracked, so this resumes the level's song if it's the one paused, and
    // otherwise (after the menu or a loaded game replaced it) starts it over.
    internal void ContinueMusic(int offs)
    {
        var song = LevelSong();
        LastMusicChunk = song;
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