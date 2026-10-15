using PFWolf.Assets;

namespace PFWolf.Gameplay;

/// <summary>What starting a new game needs from the game</summary>
/// <param name="StartGame">The game starts playing</param>
internal sealed record NewGamesHooks(Action StartGame);

/// <summary>
/// Starting a new game (NewGame): the game's state afresh, no hub levels kept, the one local
/// player starting as their class; and the skill being played, or that demos are played on.
/// </summary>
internal sealed class NewGames(
    GameEngineManager engine,
    InventoryManager inventory,
    Hubs hubs,
    Players playersManager,
    PlayerClassRules classes,
    GameSession session,
    NewGamesHooks hooks)
{
    /// <summary>The skill being played (game-info skills, by gamestate.difficulty); an ordinary one if it's missing</summary>
    internal SkillInfo CurrentSkill =>
        engine.GetGameInfo().Skills.Values.ElementAtOrDefault(session.gamestate.difficulty) ?? new SkillInfo();

    /// <summary>
    /// The skill demos are recorded and played back on (they only replay right on the skill they
    /// were made on): game-info's demo-skill, else the hardest (last) skill
    /// </summary>
    internal short DemoSkill
    {
        get
        {
            var gameInfo = engine.GetGameInfo();
            var keys = gameInfo.Skills.Keys.ToList();
            var index = gameInfo.DemoSkill == null ? -1
                : keys.FindIndex(k => k.Equals(gameInfo.DemoSkill, StringComparison.OrdinalIgnoreCase));
            if (index < 0 && gameInfo.DemoSkill != null)
                Console.WriteLine($"game-info demo-skill '{gameInfo.DemoSkill}' isn't one of the skills; using the hardest");
            return (short)(index >= 0 ? index : Math.Max(keys.Count - 1, 0));
        }
    }

    /// <summary>The class the next new game is played as (the `playerclass` command); null for the default</summary>
    internal string? newGamePlayerClass;

    /// <summary>
    /// Starts a game as <paramref name="playerClass"/>: when null, the `playerclass` command's pick,
    /// else game-info's first player class
    /// </summary>
    internal void NewGame(short difficulty, EpisodeInfo epInfo, MapInfo mapInfo, string? playerClass = null)
    {
        session.gamestate = new gametype();
        session.LevelRatios = [];       // the win tally averages only this game's floors
        hubs.ResetHubs();            // no levels kept, no floors been on (Gameplay/Hubs.cs)
        session.gamestate.difficulty = difficulty;
        playersManager.ResetPlayers();         // just the local player, until anyone joins (Gameplay/Players.cs)
        inventory.ClearShared();
        playersManager.StartPlayer(playerClass ?? newGamePlayerClass ?? classes.DefaultPlayerClass);
        session.gamestate.cluster = mapInfo.Cluster;
        session.gamestate.mapon = epInfo.StartMap;

        hooks.StartGame();
    }
}
