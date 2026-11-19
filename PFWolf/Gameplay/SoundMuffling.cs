using PFWolf.Managers;

namespace PFWolf.Gameplay;

/*
=============================================================================

                                SOUND MUFFLING

With Game Options → Sound Muffling on (s_muffle, game-info setting-defaults sound-muffling), a
sound placed in the world (an actor's, a tile's, game.playsound at a place, an actordefs loop)
is quieter for each shut door between its area and the listener's: each one multiplies its
level by game-info's sound-muffle (0.35 by default), counting at most MaxDoors, and an area no
door path reaches at all (only through a wall) as MaxDoors. Open a door and the room beyond
swells (AudioManager moves each sound's level there over a few tics). Unplaced sounds (weapons,
pickups, the ambient loops) aren't muffled, nor is a sound from a tile that's in no area (a
door's own, from its tile).

The areas are the floor codes' (AreaConnections), the listener's the camera's: so for whoever
it's watching while spectating, and this machine's own, never another player's. Only for
show, so it changes nothing in the game. It's off by default: stock Wolf3D's sounds don't
change through doors.

=============================================================================
*/
internal sealed class SoundMuffling(MapManager map, GameSession session, Func<bool> enabled, Func<float> strength)
{
    /// <summary>The most shut doors counted, and what an area no door path reaches counts as</summary>
    internal const int MaxDoors = 3;

    /// <summary>The engine's own strength: what each shut door leaves of a sound</summary>
    internal const float DefaultStrength = 0.35f;

    int[] doors = [];
    int listenerArea = -1;
    float muffle = 1;
    bool on;

    /// <summary>The listener's area last worked out (-1: none yet), for the loops command and tests</summary>
    internal int ListenerArea => listenerArea;

    /// <summary>
    /// Each tic, before the sounds' levels are moved: the listener's area and the shut doors
    /// between it and every other. Areas are few (Wolf3D's 37), so it's worked out afresh each
    /// tic rather than kept and found stale.
    /// </summary>
    internal void Tick()
    {
        muffle = Math.Clamp(strength(), 0, 1);
        on = enabled() && muffle < 1 && map.Floors.NumAreas > 0;
        if (!on)
            return;

        // The camera's area: its tile's, else (in a doorway) its actor's, else the last one
        var camera = session.Camera;
        int area = map.AreaAt(camera.TileX, camera.TileY);
        if (area < 0 && camera.Target is { } target && target.AreaNumber < map.Floors.NumAreas)
            area = target.AreaNumber;
        if (area >= 0)
            listenerArea = area;
        if (listenerArea < 0 || listenerArea >= map.Floors.NumAreas)
        {
            on = false;
            return;
        }

        if (doors.Length != map.Floors.NumAreas)
            doors = new int[map.Floors.NumAreas];
        AreaConnections.DoorsBetween(listenerArea, session.Areas.AreaConnect, session.Areas.DoorJoin, doors);
    }

    /// <summary>What's left of a sound from (x, y), in tiles: 1 unmuffled, less for each shut door between</summary>
    internal float At(float x, float y)
    {
        if (!on || x < 0 || y < 0)
            return 1;
        int area = map.AreaAt((int)x, (int)y);
        return area < 0 || area >= doors.Length ? 1 : Factor(doors[area], muffle);
    }

    /// <summary>What's left of a sound through so many shut doors (AreaConnections.Unreached: as MaxDoors)</summary>
    internal static float Factor(int shutDoors, float muffle) =>
        shutDoors <= 0 ? 1 : MathF.Pow(Math.Clamp(muffle, 0, 1), Math.Min(shutDoors, MaxDoors));
}
