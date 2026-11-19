using PFWolf.Assets;
using PFWolf.Assets.Sounds;

namespace PFWolf.Gameplay;

/*
=============================================================================

ZONE REVERB

The reverb the player hears (Managers/ReverbMixer.cs): a map's `reverb:` (else the default
map's) as the level starts, then a zone's `reverb:` as the camera steps into it. A zone with no
reverb (a door, a doorway, a light zone) leaves it as it is, so a doorway between two rooms
doesn't flicker through another. `none` is dry. `reverb-fade: <tics>` (the zone's, else the
map's, else 35) crossfades a change; a level starts at once.

A script's game.reverb(preset[, tics]) holds the reverb over the zones until it asks for nil
(an underwater stretch, a dream), as game.ambient does the ambience; the console's reverb
command does the same. Only for show, heard from the camera (whoever's watched while spectating).

=============================================================================
*/
/// <param name="ZoneAt">The zone plane's id at a tile</param>
/// <param name="SetReverb">The reverb (a preset's name, or null for dry), crossfading over so many tics</param>
/// <param name="TickReverb">The crossfade moves on by so many tics</param>
internal sealed record ZoneReverbHooks(
    Func<int, int, int> ZoneAt,
    Action<string?, int> SetReverb,
    Action<int>? TickReverb = null);

internal sealed class ZoneReverb(GameSession session, CurrentMapInfo currentMap, ZoneReverbHooks hooks)
{
    /// <summary>The crossfade when neither the zone nor the map gives one, in tics</summary>
    internal const int DefaultFade = 35;

    string? level;                              // the map the zones below are for
    Dictionary<int, ZoneReverbInfo> zones = [];
    string? current;                            // the reverb the zones and the map give now (null: dry)
    int currentFade;
    int mapFade = DefaultFade;
    bool entering;                              // a level's first tic: no crossfade

    bool scriptHolds;                           // a script's game.reverb (or the console's) holds it
    string? scriptReverb;
    int scriptFade;

    readonly HashSet<string> warned = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What the zones and the map give now, null for dry (for the reverb command)</summary>
    internal string? FromZones => current;

    /// <summary>Whether a script holds the reverb</summary>
    internal bool ScriptHolds => scriptHolds;

    /// <summary>Each tic of the level: the camera's zone's reverb (or a script's)</summary>
    internal void Tick()
    {
        var map = session.GameState.MapOn;
        if (!string.Equals(map, level, StringComparison.OrdinalIgnoreCase))
            Enter(map);

        var camera = session.Camera;
        if (zones.Count > 0 && zones.TryGetValue(hooks.ZoneAt(camera.TileX, camera.TileY), out var zone))
        {
            current = zone.Reverb;
            currentFade = zone.Fade ?? mapFade;
        }

        int fade = entering ? 0 : scriptHolds ? scriptFade : currentFade;
        entering = false;
        hooks.SetReverb(scriptHolds ? scriptReverb : current, fade);
        hooks.TickReverb?.Invoke((int)session.Tics);
    }

    /// <summary>
    /// game.reverb: a script's preset holds the reverb ("none": dry), or with null gives it back
    /// to the zones; either way at once, crossfading over so many tics
    /// </summary>
    internal void SetScriptReverb(string? name, int fadeTics)
    {
        fadeTics = Math.Max(fadeTics, 0);
        if (string.IsNullOrWhiteSpace(name))
        {
            if (!scriptHolds)
                return;         // nothing held to give back: asking each tic doesn't cut a zone's crossfade short
            scriptHolds = false;
            hooks.SetReverb(current, fadeTics);
            return;
        }
        scriptHolds = true;
        scriptReverb = Known(name, "game.reverb");
        scriptFade = fadeTics;
        hooks.SetReverb(scriptReverb, fadeTics);
    }

    // A new level: its map's reverb and its zones'
    void Enter(string? map)
    {
        level = map;
        var mapInfo = currentMap.Map;
        var defaults = currentMap.Default;
        zones = Resolve(mapInfo, defaults, Known);
        current = Known(mapInfo?.Reverb ?? defaults.Reverb, $"{map}'s reverb");
        mapFade = Math.Max(mapInfo?.ReverbFade ?? defaults.ReverbFade ?? DefaultFade, 0);
        currentFade = mapFade;
        entering = true;
    }

    // A preset's name as the list has it, or null for none (dry); an unknown one is dry, with a warning once
    string? Known(string? name, string where)
    {
        if (name == null || ReverbNames.IsNone(name))
            return null;
        if (ReverbNames.Find(name) is { } found)
            return found;
        if (warned.Add(name))
            WarningLog.Write($"{where}: there's no reverb {name}, so it's dry (the reverbs are Creative's EFX presets: cave, sewer-pipe, hallway...)");
        return null;
    }

    /// <summary>The zones with a reverb of their own: each value a map's zone leaves out comes from the default map's</summary>
    internal static Dictionary<int, ZoneReverbInfo> Resolve(MapInfo? map, DefaultMapInfo defaultMap, Func<string?, string, string?> known)
    {
        var result = new Dictionary<int, ZoneReverbInfo>();
        var mapZones = map?.Zones ?? [];
        foreach (var id in defaultMap.Zones.Keys.Union(mapZones.Keys))
        {
            ZoneInfo? own = mapZones.GetValueOrDefault(id), fallback = defaultMap.Zones.GetValueOrDefault(id);
            if ((own?.Reverb ?? fallback?.Reverb) is not { } reverb)
                continue;
            int? fade = own?.ReverbFade ?? fallback?.ReverbFade;
            result[id] = new ZoneReverbInfo(known(reverb, $"zone {id}'s reverb"), fade is int f ? Math.Max(f, 0) : null);
        }
        return result;
    }
}

/// <summary>A zone's reverb (null: dry) and the tics it crossfades over as the camera steps in (null: the map's)</summary>
internal readonly record struct ZoneReverbInfo(string? Reverb, int? Fade);
