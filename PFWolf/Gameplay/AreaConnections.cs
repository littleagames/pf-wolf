namespace PFWolf.Gameplay;

/*
=============================================================================

                                AREA CONNECTIONS

Map tile values 0x6a through 0xa3 (mapdefs floors) are area numbers. Enemies only notice and
hear the player from areas connected to the player's. Each door counts in AreaConnect while
it's open, joining the two areas it stands between; AreaByPlayer is worked out afresh from
the players' areas each time a door opens or closes, and (game-info areas-follow-player, as
Midnight Assault) when a player steps or teleports into an area it doesn't take in. With
game-info areas-mark-player (Blake Stone) such an area is just added to it instead, keeping
the ones the player came from.

A see-through door (Rendering/SeeThroughPosts.cs) joins its two areas for sight even while it's
shut, and a shoot-through one for shots too: AreaSeenByPlayer and AreaShotByPlayer are
AreaByPlayer spread on through them, worked out each time it is. With no such doors on the
level they're AreaByPlayer itself.

=============================================================================
*/

internal sealed class AreaConnections
{
    // Sized to the mapdefs area count by Reset (InitDoorList)
    internal byte[,] AreaConnect = new byte[0, 0];

    internal byte[] AreaByPlayer = [];

    // The see-through doors (and of those, the shoot-through ones) between each two areas, counted
    // as AreaConnect counts open doors: fixed for the level once its doors are spawned
    internal byte[,] SightConnect = new byte[0, 0], ShotConnect = new byte[0, 0];
    bool anySight, anyShot;
    byte[] seen = [], shot = [];

    // The doors, open or shut, between each two areas: fixed for the level once its doors are
    // spawned, for sound muffling (DoorsBetween)
    internal byte[,] DoorJoin = new byte[0, 0];

    /// <summary>The areas enemies can see a player from: AreaByPlayer, and on through shut see-through doors</summary>
    internal byte[] AreaSeenByPlayer => anySight ? seen : AreaByPlayer;

    /// <summary>The areas enemies can shoot a player from: AreaByPlayer, and on through shut shoot-through doors</summary>
    internal byte[] AreaShotByPlayer => anyShot ? shot : AreaByPlayer;

    /// <summary>A new level's areas: none connected, none by a player</summary>
    internal void Reset(int numareas)
    {
        AreaByPlayer = new byte[numareas];
        AreaConnect = new byte[numareas, numareas];
        SightConnect = new byte[numareas, numareas];
        ShotConnect = new byte[numareas, numareas];
        DoorJoin = new byte[numareas, numareas];
        seen = new byte[numareas];
        shot = new byte[numareas];
        anySight = anyShot = false;
    }

    /// <summary>A see-through door stands between the two areas (shootThrough: shots go through it too)</summary>
    internal void JoinThroughDoor(uint area1, uint area2, bool shootThrough)
    {
        SightConnect[area1, area2]++;
        SightConnect[area2, area1]++;
        anySight = true;
        if (!shootThrough)
            return;
        ShotConnect[area1, area2]++;
        ShotConnect[area2, area1]++;
        anyShot = true;
    }

    /// <summary>A door stands between the two areas, open or shut (for DoorsBetween)</summary>
    internal void JoinByDoor(uint area1, uint area2)
    {
        if (area1 >= DoorJoin.GetLength(0) || area2 >= DoorJoin.GetLength(0) || area1 == area2)
            return;
        if (DoorJoin[area1, area2] < byte.MaxValue)
        {
            DoorJoin[area1, area2]++;
            DoorJoin[area2, area1]++;
        }
    }

    /// <summary>A count for an area no door path reaches (DoorsBetween)</summary>
    internal const int Unreached = int.MaxValue;

    /// <summary>
    /// The fewest shut doors between <paramref name="from"/> and each area (into
    /// <paramref name="result"/>, one per area): 0 through open doors (connect, AreaConnect) or
    /// none, 1 more for each shut door (doorJoin with no open one beside it), Unreached for an area
    /// no door path reaches (only through a wall: a pushwall). A 0-1 breadth-first search: an
    /// open way first, a shut door after.
    /// </summary>
    internal static void DoorsBetween(int from, byte[,] connect, byte[,] doorJoin, int[] result)
    {
        int count = result.Length;
        Array.Fill(result, Unreached);
        if (from < 0 || from >= count || connect.GetLength(0) < count || doorJoin.GetLength(0) < count)
            return;
        var queue = new LinkedList<int>();
        result[from] = 0;
        queue.AddFirst(from);
        while (queue.First is { } node)
        {
            int area = node.Value;
            queue.RemoveFirst();
            for (int next = 0; next < count; next++)
            {
                if (next == area)
                    continue;
                int cost = connect[area, next] != 0 ? 0 : doorJoin[area, next] != 0 ? 1 : -1;
                if (cost < 0 || result[area] + cost >= result[next])
                    continue;
                result[next] = result[area] + cost;
                if (cost == 0)
                    queue.AddFirst(next);
                else
                    queue.AddLast(next);
            }
        }
    }

    /*
    ==============
    =
    = ConnectAreas
    =
    = Scans outward from playerarea, marking all connected areas
    =
    ==============
    */
    internal void RecursiveConnect(int areanumber, int numareas)
    {
        int i;

        for (i = 0; i < numareas; i++)
        {
            if (AreaConnect[areanumber, i] != 0 && AreaByPlayer[i] == 0)
            {
                AreaByPlayer[i] = 1; // true
                RecursiveConnect(i, numareas);
            }
        }
    }

    // With several players, an area is "by the player" when it's connected to any of theirs
    internal void ConnectAreas(IEnumerable<int> playerAreas, int numareas)
    {
        Array.Fill(AreaByPlayer, (byte)0);
        foreach (var area in playerAreas)
        {
            if (area >= numareas)
                continue;
            AreaByPlayer[area] = 1; // true
            RecursiveConnect(area, numareas);
        }
        SpreadSight();
    }

    /// <summary>Only the players' own areas, as a level starts</summary>
    internal void InitAreas(IEnumerable<int> playerAreas, int numareas)
    {
        Array.Fill(AreaByPlayer, (byte)0);
        foreach (var area in playerAreas)
        {
            if (area < numareas)
                AreaByPlayer[area] = 1; // true
        }
        SpreadSight();
    }

    /// <summary>
    /// Adds one area to the players' (game-info areas-mark-player, bstone's Thrust): only it, not
    /// the areas its open doors join, which the next door to open or close takes in
    /// </summary>
    internal void MarkArea(int area)
    {
        if (area < 0 || area >= AreaByPlayer.Length || AreaByPlayer[area] != 0)
            return;
        AreaByPlayer[area] = 1; // true
        SpreadSight();
    }

    /// <summary>Works out AreaSeenByPlayer and AreaShotByPlayer from AreaByPlayer (after it's set, or loaded)</summary>
    internal void SpreadSight()
    {
        if (anySight)
            Spread(ref seen, SightConnect);
        if (anyShot)
            Spread(ref shot, ShotConnect);
    }

    // AreaByPlayer, and every area joined to one of those by an open door or through
    void Spread(ref byte[] reached, byte[,] through)
    {
        int numareas = AreaByPlayer.Length;
        if (reached.Length != numareas)
            reached = new byte[numareas];
        Array.Copy(AreaByPlayer, reached, numareas);
        for (int area = 0; area < numareas; area++)
        {
            if (AreaByPlayer[area] != 0)
                SpreadFrom(reached, through, area, numareas);
        }
    }

    void SpreadFrom(byte[] reached, byte[,] through, int area, int numareas)
    {
        for (int i = 0; i < numareas; i++)
        {
            if (reached[i] == 0 && (AreaConnect[area, i] != 0 || through[area, i] != 0))
            {
                reached[i] = 1;
                SpreadFrom(reached, through, i, numareas);
            }
        }
    }
}
