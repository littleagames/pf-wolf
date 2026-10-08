using System.ComponentModel;
using System.Reflection;
using PFWolf.Assets;
using PFWolf.Loaders;

namespace PFWolf.Managers;

public enum GameType
{
    [Description("wolf3d")]
    Wolf3D,
    [Description("spear")]
    SpearOfDestiny,
    [Description("blake")]
    BlakeStone,
    [Description("planetstrike")]
    PlanetStrike,
    [Description("wolf3d-shareware")]
    WolfShareware,
    [Description("wolf3d-apogee")]
    WolfApogee,
    [Description("spear-demo")]
    SpearDemo
}

/// <summary>
/// The games PFWolf runs, and which one the data files in the game folder are for
/// </summary>
public static class GameTypes
{
    /// <summary>
    /// What's played when no game is asked for, or the one asked for has no data files here: the
    /// first of these whose data files are all in the game folder
    /// </summary>
    private static readonly GameType[] FallbackOrder =
        [GameType.Wolf3D, GameType.WolfShareware, GameType.SpearOfDestiny, GameType.SpearDemo, GameType.BlakeStone, GameType.PlanetStrike];

    /// <summary>
    /// The game to run: the one --game asks for when its data files are here, else the first game
    /// whose files are (Wolf3D's shareware first, so asking for Wolf3D with only the shareware's
    /// files plays that; asking for Spear with only its demo's plays the demo). With none, the one
    /// asked for (or Wolf3D), which then says what's missing.
    /// A game's files can be another release's under the same names (Apogee's Wolf3D files are
    /// named as the GT ones wolf3d is for): then that release, which strict-md5 marks, is played.
    /// </summary>
    public static GameType PickGameType(GameType? requested)
    {
        GamePackInfoAsset? gamePackInfo;
        try
        {
            gamePackInfo = PfWolfPk3Loader.ReadGamePackInfo(new Pk3AssetSource(AssetManager.BasePk3FileName));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            gamePackInfo = null;        // loading the assets reports it
        }

        if (gamePackInfo == null)
            return requested ?? GameType.Wolf3D;

        bool IsStrict(GameType type) => gamePackInfo.GetGamePack(GetReleaseId(type)).FilePack?.StrictMd5 == true;

        bool HasDataFiles(GameType type)
            => gamePackInfo.GamePacks.ContainsKey(GetReleaseId(type))
               && gamePackInfo.FindMissingDataFiles(GetReleaseId(type)).Count == 0
               && (!IsStrict(type) || gamePackInfo.FindMismatchedDataFiles(GetReleaseId(type)).Count == 0);

        // The game the files here are for, when they're the given game's by name: the game itself,
        // unless they're another version, and a strict release with the same file names fits them
        GameType? Playable(GameType type)
        {
            if (!HasDataFiles(type))
                return null;
            if (IsStrict(type) || gamePackInfo.FindMismatchedDataFiles(GetReleaseId(type)).Count == 0)
                return type;

            foreach (var other in Enum.GetValues<GameType>())
            {
                if (other != type && gamePackInfo.GamePacks.ContainsKey(GetReleaseId(other)) && IsStrict(other)
                    && gamePackInfo.HasSameDataFileNames(GetReleaseId(type), GetReleaseId(other)) && HasDataFiles(other))
                    return other;
            }
            return type;
        }

        void NoteSwitch(GameType asked, GameType played, string reason)
        {
            if (played != asked)
                WarningLog.Write($"Running {GetGamePackId(played)}: {reason}");
        }

        string Description(GameType type)
        {
            var gamePack = gamePackInfo.GetGamePack(GetReleaseId(type));
            return gamePack.FilePack?.Description ?? gamePack.Title ?? GetGamePackId(type);
        }

        if (requested is { } asked && Playable(asked) is { } game)
        {
            NoteSwitch(asked, game, $"the data files here are {Description(game)}'s.");
            return game;
        }

        // Asked for Wolf3D (or for nothing): its shareware is the closest thing; for Spear, its demo
        var candidates = requested switch
        {
            null or GameType.Wolf3D => FallbackOrder,
            GameType.SpearOfDestiny => [GameType.SpearDemo],
            _ => [],
        };
        foreach (var type in candidates)
        {
            if (Playable(type) is not { } found)
                continue;
            NoteSwitch(requested ?? GameType.Wolf3D, found, requested == null
                ? $"its data files are the ones here ({Description(found)}; --game picks another)."
                : $"{GetGamePackId(requested.Value)}'s data files aren't here.");
            return found;
        }

        return requested ?? GameType.Wolf3D;
    }

    /// <summary>
    /// The game --game names by its pack id ("spear"); null when unset or unknown
    /// </summary>
    public static GameType? ParseGameType(string gamePackId)
    {
        if (string.IsNullOrWhiteSpace(gamePackId))
            return null;

        foreach (var type in Enum.GetValues<GameType>())
        {
            if (GetGamePackId(type).Equals(gamePackId.Trim(), StringComparison.OrdinalIgnoreCase))
                return type;
        }

        WarningLog.Write($"Unknown --game '{gamePackId}' (expected {string.Join(", ", KnownGamePackIds)}); picking by the data files here.");
        return null;
    }

    /// <summary>
    /// Key of a game's release in gamepacks/gamepack-info.yaml, which names its data files and
    /// palette. Fixed per game until the release is detected from the data files.
    /// </summary>
    public static string GetReleaseId(GameType type) => type switch
    {
        GameType.SpearOfDestiny => "spear",
        GameType.BlakeStone => "blake-aog",
        GameType.PlanetStrike => "blake-ps",
        GameType.WolfShareware => "wolf3d-shareware",
        GameType.WolfApogee => "wolf3d-apogee",
        GameType.SpearDemo => "spear-demo",
        _ => "wolf3d",
    };

    /// <summary>
    /// Every game pack name the engine knows about
    /// </summary>
    public static IEnumerable<string> KnownGamePackIds => Enum.GetValues<GameType>().Select(GetGamePackId);

    /// <summary>
    /// Name of a game's pack ("wolf3d", "spear"): the gamepacks/ folder name, and what menudefs
    /// list under game-packs
    /// </summary>
    public static string GetGamePackId(GameType type)
        => typeof(GameType).GetField(type.ToString())?.GetCustomAttribute<DescriptionAttribute>()?.Description
           ?? type.ToString().ToLowerInvariant();
}
