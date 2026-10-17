namespace PFWolf.Gameplay;

/*
=============================================================================

                                GAME MODES

    What sets each way of playing together apart, in one place:

    Coop            together against the level, keys shared
    Deathmatch      everyone against everyone: frags
    TeamDeathmatch  red against blue: the team's frags
    CaptureTheFlag  take the other team's flag home to your own: captures
    Heist           grab the level's treasure: each piece pulls the team's
                    rope (its worth); the first to pull it their way by the
                    limit wins, or whoever leads when the time runs out
    Domination      three control points: each one a team holds scores
    KingOfTheHill   one hill at a time: the team holding it scores, and it
                    moves on when its time is up
    Horde           together against waves of enemies (horde.yaml): the
                    match ends when every player is dead at once
    PropHunt        props (one team) look like the level's decorations and
                    hide; hunters (the other) find and shoot them: rounds

    Every versus mode gives each player every key (no door is shut to
    anyone), except Heist, where the best treasure is behind locked doors:
    keys are found, and lost with a death.

=============================================================================
*/

internal static class GameModes
{
    /// <summary>The two teams' names: their color translations (translations.yaml) and how messages call them</summary>
    internal static readonly string[] TeamNames = ["red", "blue"];

    /// <summary>A team's name for the screen: "Red"</summary>
    internal static string TeamLabel(int team) =>
        team is 0 or 1 ? char.ToUpperInvariant(TeamNames[team][0]) + TeamNames[team][1..] : "-";

    /// <summary>Every mode a game with others can be played as, in the lobby's order</summary>
    internal static readonly GameMode[] NetModes =
    [
        GameMode.Coop, GameMode.Deathmatch, GameMode.TeamDeathmatch, GameMode.CaptureTheFlag,
        GameMode.Heist, GameMode.Domination, GameMode.KingOfTheHill, GameMode.Horde, GameMode.PropHunt,
    ];

    /// <summary>Players fight each other</summary>
    internal static bool IsVersus(this GameMode mode) =>
        mode is GameMode.Deathmatch or GameMode.TeamDeathmatch or GameMode.CaptureTheFlag
            or GameMode.Heist or GameMode.Domination or GameMode.KingOfTheHill or GameMode.PropHunt;

    /// <summary>Players are on two teams, red and blue</summary>
    internal static bool IsTeamGame(this GameMode mode) => mode.IsVersus() && mode != GameMode.Deathmatch;

    /// <summary>Players are on one side, against the level (keys shared, no friendly fire)</summary>
    internal static bool IsCoopLike(this GameMode mode) => mode is GameMode.Coop or GameMode.Horde;

    /// <summary>A weapon on the floor stays for everyone without it, and anything else taken comes back (versus modes and Horde)</summary>
    internal static bool KeepsItems(this GameMode mode) => mode.IsVersus() || mode == GameMode.Horde;

    /// <summary>Each player carries every key (every versus mode but Heist)</summary>
    internal static bool GivesAllKeys(this GameMode mode) => mode.IsVersus() && mode != GameMode.Heist;

    /// <summary>The mode's name in game-info's map `modes:` lists and the `GameMode` command</summary>
    internal static string Id(this GameMode mode) => mode switch
    {
        GameMode.Coop => "coop",
        GameMode.Deathmatch => "deathmatch",
        GameMode.TeamDeathmatch => "team-deathmatch",
        GameMode.CaptureTheFlag => "ctf",
        GameMode.Heist => "heist",
        GameMode.Domination => "domination",
        GameMode.KingOfTheHill => "king-of-the-hill",
        GameMode.Horde => "horde",
        GameMode.PropHunt => "prop-hunt",
        _ => "single",
    };

    /// <summary>A mode by its id (or a short name: dm, tdm, dom, koth, ph), or null</summary>
    internal static GameMode? Parse(string name) => name.ToLowerInvariant() switch
    {
        "coop" or "co-op" => GameMode.Coop,
        "deathmatch" or "dm" or "ffa" => GameMode.Deathmatch,
        "team-deathmatch" or "teamdeathmatch" or "tdm" => GameMode.TeamDeathmatch,
        "ctf" or "capture-the-flag" or "capturetheflag" => GameMode.CaptureTheFlag,
        "heist" => GameMode.Heist,
        "domination" or "dom" => GameMode.Domination,
        "king-of-the-hill" or "kingofthehill" or "koth" => GameMode.KingOfTheHill,
        "horde" => GameMode.Horde,
        "prop-hunt" or "prophunt" or "ph" => GameMode.PropHunt,
        _ => null,
    };

    /// <summary>The mode's name for the lobby and the LAN list (a language key)</summary>
    internal static string LabelKey(this GameMode mode) => mode switch
    {
        GameMode.Deathmatch => "$STR_MP_DEATHMATCH",
        GameMode.TeamDeathmatch => "$STR_MP_TEAMDM",
        GameMode.CaptureTheFlag => "$STR_MP_CTF",
        GameMode.Heist => "$STR_MP_HEIST",
        GameMode.Domination => "$STR_MP_DOMINATION",
        GameMode.KingOfTheHill => "$STR_MP_KOTH",
        GameMode.Horde => "$STR_MP_HORDE",
        GameMode.PropHunt => "$STR_MP_PROPHUNT",
        _ => "$STR_MP_COOP",
    };

    /// <summary>What the mode's score counts, for the scoreboard's column and the rules: "Frags"</summary>
    internal static string ScoreName(this GameMode mode) => mode switch
    {
        GameMode.Deathmatch or GameMode.TeamDeathmatch => "Frags",
        GameMode.CaptureTheFlag => "Captures",
        GameMode.Heist => "Loot",
        GameMode.Domination or GameMode.KingOfTheHill => "Points",
        GameMode.Horde => "Waves",
        GameMode.PropHunt => "Rounds",
        _ => "Score",
    };

    /// <summary>
    /// What the score limit means: a deathmatch's frags, a team's captures or points, Heist's
    /// lead in loot, Horde's waves to survive (0 for no limit: Horde goes on until everyone dies),
    /// Prop Hunt's rounds a team must win
    /// </summary>
    internal static int DefaultScoreLimit(this GameMode mode) => mode switch
    {
        GameMode.PropHunt => 3,
        GameMode.Deathmatch => 20,
        GameMode.TeamDeathmatch => 50,
        GameMode.CaptureTheFlag => 3,
        GameMode.Heist => 100,
        GameMode.Domination => 250,
        GameMode.KingOfTheHill => 100,
        GameMode.Horde => 10,
        _ => 0,
    };

    /// <summary>The score limits the host picks from in the lobby</summary>
    internal static int[] ScoreLimits(this GameMode mode) => mode switch
    {
        GameMode.CaptureTheFlag => [0, 1, 2, 3, 4, 5, 7, 10],
        GameMode.Heist => [0, 25, 50, 75, 100, 150, 200, 300],
        GameMode.Domination => [0, 100, 150, 200, 250, 300, 400, 500],
        GameMode.KingOfTheHill => [0, 30, 60, 100, 150, 200, 300],
        GameMode.Horde => [0, 5, 10, 15, 20, 25, 30, 50],
        GameMode.PropHunt => [0, 2, 3, 4, 5, 7, 10],
        _ => [0, 5, 10, 15, 20, 25, 30, 50, 75, 100],
    };

    /// <summary>The mode starts with the map's enemies about (they can still be switched off in the rules)</summary>
    internal static bool DefaultMonsters(this GameMode mode) => mode is GameMode.Coop or GameMode.Heist;
}
