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
value it leaves out from the default map's zone of the same id. `ambient-fade: <tics>` fades
the change over (the last sound out as the new one comes in) as the player steps into the zone,
or as its sound changes there (spotted: the undetected sound gives way).

The sound goes on the ambient channel (AudioManager.SetAmbient), asked for each tic so it comes
back after anything stops all sounds. On a map whose zones have no ambient sound, the channel is
left to scripts. Midnight Assault's area ambience (DoAmbience) is made this way.

A script's game.ambient(sound[, tics]) wins over the zones until it asks for nil, which gives
the channel back to them (or stops it, on a map whose zones have none); its "none" is silence
while it holds the channel. It holds it from level to level, as the scripts' own state does.

=============================================================================
*/
/// <param name="ZoneAt">The zone plane's id at a tile</param>
/// <param name="InDisguise">Whether the local player is wearing a disguise</param>
/// <param name="SetAmbient">Loops a sound on the ambient channel, or stops it with null, fading over so many tics</param>
/// <param name="TickAmbient">The ambient channel's fades move on by so many tics</param>
internal sealed record ZoneAmbienceHooks(
    Func<int, int, int> ZoneAt,
    Func<bool> InDisguise,
    Action<string?, int> SetAmbient,
    Action<int>? TickAmbient = null);

internal sealed class ZoneAmbience(GameSession session, CurrentMapInfo currentMap, ZoneAmbienceHooks hooks)
{
    const string None = "none";

    string? level;                  // the map the zones below are for
    Dictionary<int, ZoneSounds> zones = [];
    string? current;                // the zones' sound: the last ambient zone's
    int currentFade;                // and the tics it fades in over

    string? scriptSound;            // what a script asked for (null: the zones have the channel)
    int scriptFade;

    /// <summary>Each tic of the level: the player's zone's sound (or a script's) on the ambient channel</summary>
    internal void Tick()
    {
        var map = session.GameState.MapOn;
        if (!string.Equals(map, level, StringComparison.OrdinalIgnoreCase))
            Enter(map);

        if (zones.Count > 0 && session.Player is { } pawn && zones.TryGetValue(hooks.ZoneAt(pawn.TileX, pawn.TileY), out var zone))
        {
            var sound = zone.Disguised is { Length: > 0 } disguised && hooks.InDisguise() ? disguised
                : zone.Undetected is { Length: > 0 } undetected && session.PlayerState.Undetected ? undetected
                : zone.Sound;
            current = Silent(sound) ? null : sound;
            currentFade = zone.Fade;
        }

        if (scriptSound != null)
            hooks.SetAmbient(Silent(scriptSound) ? null : scriptSound, scriptFade);
        else if (zones.Count > 0)
            hooks.SetAmbient(current, currentFade);
        hooks.TickAmbient?.Invoke((int)session.Tics);
    }

    /// <summary>
    /// game.ambient: a script's sound holds the channel ("none": silence), or with null gives
    /// it back to the zones; either way at once, fading over so many tics
    /// </summary>
    internal void SetScriptAmbient(string? sound, int fadeTics)
    {
        sound = string.IsNullOrWhiteSpace(sound) ? null : sound;
        if (sound == null && scriptSound == null && zones.Count > 0)
            return;         // nothing held to give back: a script asking for nil every tic doesn't cut a zone's fade short
        scriptSound = sound;
        scriptFade = Math.Max(fadeTics, 0);
        if (scriptSound != null)
            hooks.SetAmbient(Silent(scriptSound) ? null : scriptSound, scriptFade);
        else
            hooks.SetAmbient(zones.Count > 0 ? current : null, scriptFade);
    }

    // A new level: its zones' sounds; what the last level's zones played stops
    void Enter(string? map)
    {
        bool had = zones.Count > 0;
        level = map;
        zones = Resolve(currentMap.Map, currentMap.Default);
        current = null;
        currentFade = 0;
        if (had && scriptSound == null)
            hooks.SetAmbient(null, 0);
    }

    static bool Silent(string? sound) => sound == null || string.Equals(sound, None, StringComparison.OrdinalIgnoreCase);

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
            var fade = own?.AmbientFade ?? fallback?.AmbientFade ?? 0;
            if (sound != null || disguised != null || undetected != null)
                result[id] = new ZoneSounds(sound, disguised, undetected, Math.Max(fade, 0));
        }
        return result;
    }
}

/// <summary>A zone's ambient sounds: as usual, in disguise, and while unnoticed; and the tics a change to them fades over</summary>
internal readonly record struct ZoneSounds(string? Sound, string? Disguised, string? Undetected, int Fade = 0);
