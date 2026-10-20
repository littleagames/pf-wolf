using PFWolf.Assets;

namespace PFWolf.Gameplay;

/// <summary>
/// The game-info entry for the map being played (game-info maps: its time limit, radar, stealth,
/// zones...), and the defaults every map falls back on. The game info is asked each time, so a
/// different game or mods loaded since are seen.
/// </summary>
internal sealed class CurrentMapInfo(GameSession session, Func<GameInfoAsset> gameInfo)
{
    /// <summary>The map being played's entry; null when it has none</summary>
    internal MapInfo? Map => For(session, gameInfo());

    /// <summary>The map being played's entry in that game info; null when it has none</summary>
    internal static MapInfo? For(GameSession session, GameInfoAsset gameInfo) =>
        session.GameState.MapOn is { } map && gameInfo.Maps.TryGetValue(map, out var info) ? info : null;

    /// <summary>What every map has unless its own entry says otherwise (game-info default-map)</summary>
    internal DefaultMapInfo Default => gameInfo().DefaultMap;
}
