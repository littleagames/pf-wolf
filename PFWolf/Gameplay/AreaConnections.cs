namespace PFWolf.Gameplay;

/*
=============================================================================

                                AREA CONNECTIONS

Map tile values 0x6a through 0xa3 (mapdefs floors) are area numbers. Enemies only notice and
hear the player from areas connected to the player's. Each door counts in areaconnect while
it's open, joining the two areas it stands between; areabyplayer is worked out afresh from
the players' areas each time a door opens or closes, or a player crosses into another area.

=============================================================================
*/

internal sealed class AreaConnections
{
    // Sized to the mapdefs area count by Reset (InitDoorList)
    internal byte[,] areaconnect = new byte[0, 0];

    internal byte[] areabyplayer = [];

    /// <summary>A new level's areas: none connected, none by a player</summary>
    internal void Reset(int numareas)
    {
        areabyplayer = new byte[numareas];
        areaconnect = new byte[numareas, numareas];
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
            if (areaconnect[areanumber, i] != 0 && areabyplayer[i] == 0)
            {
                areabyplayer[i] = 1; // true
                RecursiveConnect(i, numareas);
            }
        }
    }

    // With several players, an area is "by the player" when it's connected to any of theirs
    internal void ConnectAreas(IEnumerable<int> playerAreas, int numareas)
    {
        Array.Fill(areabyplayer, (byte)0);
        foreach (var area in playerAreas)
        {
            if (area >= numareas)
                continue;
            areabyplayer[area] = 1; // true
            RecursiveConnect(area, numareas);
        }
    }

    /// <summary>Only the players' own areas, as a level starts</summary>
    internal void InitAreas(IEnumerable<int> playerAreas, int numareas)
    {
        Array.Fill(areabyplayer, (byte)0);
        foreach (var area in playerAreas)
        {
            if (area < numareas)
                areabyplayer[area] = 1; // true
        }
    }
}
