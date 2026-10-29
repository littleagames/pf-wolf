namespace PFWolf.Gameplay;

/*
=============================================================================

                                AREA CONNECTIONS

Map tile values 0x6a through 0xa3 (mapdefs floors) are area numbers. Enemies only notice and
hear the player from areas connected to the player's. Each door counts in AreaConnect while
it's open, joining the two areas it stands between; AreaByPlayer is worked out afresh from
the players' areas each time a door opens or closes, or a player crosses into another area.

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
