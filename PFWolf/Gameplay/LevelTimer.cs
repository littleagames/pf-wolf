namespace PFWolf.Gameplay;

/// <param name="TimeUp">Kills the players: the time's run out</param>
/// <param name="SecondChanged">The time left shown has changed (a status bar that isn't redrawn every frame redraws it)</param>
internal sealed record LevelTimerHooks(Action TimeUp, Action? SecondChanged = null);

/// <summary>
/// A level against the clock (game-info map `time-limit`: Midnight Assault's Timed Challenge,
/// UpdateLevelTimer). It counts down on the level's own clock (GameState.TimeCount), so it
/// starts again with the level, after a death too, and a saved game keeps what's left. When it
/// runs out the player dies.
/// </summary>
internal sealed class LevelTimer(GameSession session, CurrentMapInfo currentMap, LevelTimerHooks hooks)
{
    int shownSeconds = -1;

    /// <summary>Tics left on the level's clock, or -1 for a level that isn't timed</summary>
    internal int TicsLeft
    {
        get
        {
            int limit = currentMap.Map?.TimeLimit ?? 0;     // game-info map time-limit, in seconds
            return limit > 0 ? Math.Max(limit * 70 - session.GameState.TimeCount, 0) : -1;
        }
    }

    /// <summary>Each tic, after everyone's moved: the time's up once the clock reaches the limit</summary>
    internal void Tick()
    {
        int left = TicsLeft;
        if (left < 0)
            return;

        int seconds = (left + 69) / 70;
        if (seconds != shownSeconds)
        {
            shownSeconds = seconds;
            hooks.SecondChanged?.Invoke();
        }

        if (left == 0 && session.PlayState == PlayState.StillPlaying && session.Players.Any(p => p.Health > 0))
            hooks.TimeUp();
    }
}
