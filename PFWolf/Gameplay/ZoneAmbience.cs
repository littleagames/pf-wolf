using PFWolf.Assets;

namespace PFWolf.Gameplay;

/*
=============================================================================

ZONE AMBIENCE

A sound looped, unplaced, while the player is in a zone (the zone plane, as light zones are):
game-info's default-map or a map's `zones:` give a zone `ambient: <sound>`,
`ambient-disguised: <sound>` to play instead while the player wears a disguise, and
`ambient-undetected: <sound>` while no enemy has noticed them yet (Gameplay/Stealth.cs).
`ambient: none` is silence. Stepping into a zone with no ambient leaves what's playing as it is,
so a door or a light zone between two areas doesn't cut the sound off. A map's zone takes each
value it leaves out from the default map's zone of the same id.

The sound goes on the ambient channel (AudioManager.SetAmbient, as Lua game.ambient), asked for
each tic so it comes back after anything stops all sounds. On a map whose zones have no ambient
sound, the channel is left to scripts. Midnight Assault's area ambience (DoAmbience) is made this way.

=============================================================================
*/
/// <param name="Map">Its game-info map (null for none)</param>
/// <param name="DefaultMap">Game-info's default map</param>
/// <param name="ZoneAt">The zone plane's id at a tile</param>
/// <param name="InDisguise">Whether the local player is wearing a disguise</param>
/// <param name="SetAmbient">Loops a sound on the ambient channel, or stops it with null</param>
internal sealed record ZoneAmbienceHooks(
    Func<MapInfo?> Map,
    Func<DefaultMapInfo> DefaultMap,
    Func<int, int, int> ZoneAt,
    Func<bool> InDisguise,
    Action<string?> SetAmbient);

internal sealed class ZoneAmbience(GameSession session, ZoneAmbienceHooks hooks)
{
    const string None = "none";

    string? level;                  // the map the zones below are for
    Dictionary<int, ZoneSounds> zones = [];
    string? current;                // what's playing: the last ambient zone's sound

    /// <summary>Each tic of the level: the player's zone's sound on the ambient channel</summary>
    internal void Tick()
    {
        var map = session.GameState.MapOn;
        if (!string.Equals(map, level, StringComparison.OrdinalIgnoreCase))
            Enter(map);
        if (zones.Count == 0)
            return;

        if (session.Player is { } pawn && zones.TryGetValue(hooks.ZoneAt(pawn.TileX, pawn.TileY), out var zone))
        {
            var sound = zone.Disguised is { Length: > 0 } disguised && hooks.InDisguise() ? disguised
                : zone.Undetected is { Length: > 0 } undetected && session.PlayerState.Undetected ? undetected
                : zone.Sound;
            current = string.Equals(sound, None, StringComparison.OrdinalIgnoreCase) ? null : sound;
        }
        hooks.SetAmbient(current);
    }

    // A new level: its zones' sounds; what the last level's zones played stops
    void Enter(string? map)
    {
        bool had = zones.Count > 0;
        level = map;
        zones = Resolve(hooks.Map(), hooks.DefaultMap());
        current = null;
        if (had)
            hooks.SetAmbient(null);
    }

    /// <summary>The zones with an ambient sound: each value a map's zone leaves out comes from the default map's</summary>
    internal static Dictionary<int, ZoneSounds> Resolve(MapInfo? map, DefaultMapInfo defaultMap)
    {
        var result = new Dictionary<int, ZoneSounds>();
        var mapZones = map?.Zones ?? [];
        foreach (var id in defaultMap.Zones.Keys.Union(mapZones.Keys))
        {
            ZoneInfo? own = mapZones.GetValueOrDefault(id), fallback = defaultMap.Zones.GetValueOrDefault(id);
            var sound = own?.Ambient ?? fallback?.Ambient;
            var disguised = own?.AmbientDisguised ?? fallback?.AmbientDisguised;
            var undetected = own?.AmbientUndetected ?? fallback?.AmbientUndetected;
            if (sound != null || disguised != null || undetected != null)
                result[id] = new ZoneSounds(sound, disguised, undetected);
        }
        return result;
    }
}

/// <summary>A zone's ambient sounds: as usual, in disguise, and while unnoticed</summary>
internal readonly record struct ZoneSounds(string? Sound, string? Disguised, string? Undetected);
