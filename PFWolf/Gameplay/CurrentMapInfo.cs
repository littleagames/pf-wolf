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

    /// <summary>A message with the map being played's {floor-text} and {floor} filled in (<see cref="FillFloor"/>)</summary>
    internal string FillFloor(string text, Func<string, string> localize) => FillFloor(text, Map, Default, localize);

    /// <summary>
    /// <paramref name="text"/> with {floor-text} the map's floor-text (default-map's when it has
    /// none; nothing when neither has), a $language key put through <paramref name="localize"/>,
    /// and then {floor} the map's floor number
    /// </summary>
    internal static string FillFloor(string text, MapInfo? map, DefaultMapInfo defaults, Func<string, string> localize)
    {
        if (!text.Contains("{floor", StringComparison.OrdinalIgnoreCase))
            return text;
        if (text.Contains("{floor-text}", StringComparison.OrdinalIgnoreCase))
        {
            var floorText = map?.FloorText ?? defaults.FloorText;
            text = text.Replace("{floor-text}", floorText == null ? "" : localize(floorText), StringComparison.OrdinalIgnoreCase);
        }
        return text.Replace("{floor}", (map?.FloorNumber ?? 0).ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
