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

A second loop plays over the first (rain over the wind): a zone's `ambient-2`, with
`ambient-2-disguised` and `ambient-2-undetected`, fading as `ambient-fade` says. A zone with
any ambient sound sets both: one without an ambient-2 stops the second loop as you step in, so
a room's sound is all its own. One with none at all (a door) leaves both as they are. A
script's game.ambient(sound, tics, 2) holds the second loop as the first, each apart.

=============================================================================
*/
/// <param name="ZoneAt">The zone plane's id at a tile</param>
/// <param name="InDisguise">Whether the local player is wearing a disguise</param>
/// <param name="SetAmbient">Loops a sound on an ambient layer (0 the first, 1 the second), or stops it with null, fading over so many tics</param>
/// <param name="TickAmbient">The ambient channel's fades move on by so many tics</param>
internal sealed record ZoneAmbienceHooks(
    Func<int, int, int> ZoneAt,
    Func<bool> InDisguise,
    Action<string?, int, int> SetAmbient,
    Action<int>? TickAmbient = null);

internal sealed class ZoneAmbience(GameSession session, CurrentMapInfo currentMap, ZoneAmbienceHooks hooks)
{
    const string None = "none";

    /// <summary>The ambient loops, one over the other (AudioManager.AmbientLayers)</summary>
    internal const int Layers = 2;

    string? level;                  // the map the zones below are for
    Dictionary<int, ZoneSounds> zones = [];
    readonly string?[] current = new string?[Layers];       // the zones' sounds: the last ambient zone's
    int currentFade;                                        // and the tics they fade in over

    readonly string?[] scriptSound = new string?[Layers];   // what a script asked for (null: the zones have the layer)
    readonly int[] scriptFade = new int[Layers];

    /// <summary>Each tic of the level: the player's zone's sounds (or a script's) on the ambient channel</summary>
    internal void Tick()
    {
        var map = session.GameState.MapOn;
        if (!string.Equals(map, level, StringComparison.OrdinalIgnoreCase))
            Enter(map);

        if (zones.Count > 0 && session.Player is { } pawn && zones.TryGetValue(hooks.ZoneAt(pawn.TileX, pawn.TileY), out var zone))
        {
            for (int layer = 0; layer < Layers; layer++)
            {
                var (sound, disguised, undetected) = zone.Layer(layer);
                sound = disguised is { Length: > 0 } && hooks.InDisguise() ? disguised
                    : undetected is { Length: > 0 } && session.PlayerState.Undetected ? undetected
                    : sound;
                current[layer] = Silent(sound) ? null : sound;
            }
            currentFade = zone.Fade;
        }

        for (int layer = 0; layer < Layers; layer++)
        {
            if (scriptSound[layer] is { } held)
                hooks.SetAmbient(Silent(held) ? null : held, scriptFade[layer], layer);
            else if (zones.Count > 0)
                hooks.SetAmbient(current[layer], currentFade, layer);
        }
        hooks.TickAmbient?.Invoke((int)session.Tics);
    }

    /// <summary>
    /// game.ambient: a script's sound holds a layer ("none": silence), or with null gives it back
    /// to the zones; either way at once, fading over so many tics. Layer 0 is the first loop, 1
    /// the one over it.
    /// </summary>
    internal void SetScriptAmbient(string? sound, int fadeTics, int layer = 0)
    {
        if (layer < 0 || layer >= Layers)
            return;
        sound = string.IsNullOrWhiteSpace(sound) ? null : sound;
        if (sound == null && scriptSound[layer] == null && zones.Count > 0)
            return;         // nothing held to give back: a script asking for nil every tic doesn't cut a zone's fade short
        scriptSound[layer] = sound;
        scriptFade[layer] = Math.Max(fadeTics, 0);
        if (sound != null)
            hooks.SetAmbient(Silent(sound) ? null : sound, scriptFade[layer], layer);
        else
            hooks.SetAmbient(zones.Count > 0 ? current[layer] : null, scriptFade[layer], layer);
    }

    // A new level: its zones' sounds; what the last level's zones played stops
    void Enter(string? map)
    {
        bool had = zones.Count > 0;
        level = map;
        zones = Resolve(currentMap.Map, currentMap.Default);
        Array.Clear(current);
        currentFade = 0;
        for (int layer = 0; layer < Layers; layer++)
            if (had && scriptSound[layer] == null)
                hooks.SetAmbient(null, 0, layer);
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
            var sounds = new ZoneSounds(
                own?.Ambient ?? fallback?.Ambient,
                own?.AmbientDisguised ?? fallback?.AmbientDisguised,
                own?.AmbientUndetected ?? fallback?.AmbientUndetected,
                Math.Max(own?.AmbientFade ?? fallback?.AmbientFade ?? 0, 0),
                own?.Ambient2 ?? fallback?.Ambient2,
                own?.Ambient2Disguised ?? fallback?.Ambient2Disguised,
                own?.Ambient2Undetected ?? fallback?.Ambient2Undetected);
            if (sounds.Any)
                result[id] = sounds;
        }
        return result;
    }
}

/// <summary>
/// A zone's ambient sounds: as usual, in disguise, and while unnoticed; the tics a change to them
/// fades over; and the second loop's, over the first
/// </summary>
internal readonly record struct ZoneSounds(string? Sound, string? Disguised, string? Undetected, int Fade = 0,
    string? Sound2 = null, string? Disguised2 = null, string? Undetected2 = null)
{
    /// <summary>Whether it has any ambient sound</summary>
    public bool Any => Sound != null || Disguised != null || Undetected != null || Sound2 != null || Disguised2 != null || Undetected2 != null;

    /// <summary>A layer's sounds (0 the first loop, 1 the second)</summary>
    public (string? Sound, string? Disguised, string? Undetected) Layer(int layer) =>
        layer == 0 ? (Sound, Disguised, Undetected) : (Sound2, Disguised2, Undetected2);
}
