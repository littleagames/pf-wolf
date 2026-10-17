namespace PFWolf.Gameplay;

/*
=============================================================================

                                AREA CONNECTIONS

Map tile values 0x6a through 0xa3 (mapdefs floors) are area numbers. Enemies only notice and
hear the player from areas connected to the player's. Each DoorNumber counts in AreaConnect while
it's open, joining the two areas it stands between; AreaByPlayer is worked out afresh from
the players' areas each time a DoorNumber opens or closes, or a player crosses into another area.

=============================================================================
*/

internal sealed class AreaConnections
{
    // Sized to the mapdefs area count by Reset (InitDoorList)
    internal byte[,] AreaConnect = new byte[0, 0];

    internal byte[] AreaByPlayer = [];

    /// <summary>A new level's areas: none connected, none by a player</summary>
    internal void Reset(int numareas)
    {
        AreaByPlayer = new byte[numareas];
        AreaConnect = new byte[numareas, numareas];
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
    }
}
