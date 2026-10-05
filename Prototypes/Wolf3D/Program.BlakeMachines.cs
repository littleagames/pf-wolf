using Wolf3D.Constants;

namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                        BLAKE STONE'S LEVEL SPAWNERS

    What a Blake Stone level does on its own, as bstone has it (3d_act1.cpp, 3d_act2.cpp):
    enemies coming out of wall outlets (mapdefs walls `outlet:`) and Goldfire warping in at his
    spawn sites (WARPSITE markers). The machines themselves are Entities.Actors.BlakeMonster's.

    =============================================================================
    */

    // Set once a level's `dropitem.onfloor` item has been dropped (DeathDrop)
    internal static bool floordropgiven;

    /// <summary>Sets up a fresh or loaded level's outlets and warp sites (SetupGameLevel)</summary>
    internal static void InitLevelSpawners()
    {
        floordropgiven = false;
        InitOutlets();
        InitWarpSites();
    }

    /// <summary>Runs the level's outlets and warp sites, once a tic (PlayLoop)</summary>
    internal static void TickLevelSpawners()
    {
        TickOutlets();
        TickWarpSites();
    }

    /*
    =============================================================================

                                WALL OUTLETS

    A wall with an `outlet:` (mapdefs walls) lets its actor out onto open floor beside it, in
    a room that joins the player's: every so many seconds (a 0xFA value on its object-plane tile,
    else its delay), as long as fewer than its max for the skill are about. One comes out where
    the player can see it, or now and then anyway, never within a tile of the player.

    =============================================================================
    */

    sealed class Outlet
    {
        public int X, Y, Delay;
        public required Assets.MapOutletTranslation Spec;
    }

    static List<Outlet> outlets = [];

    static void InitOutlets()
    {
        outlets = [];
        var walls = _mapManager.GetMapData().Walls;
        for (int y = 0; y < _mapManager.mapheight; y++)
            for (int x = 0; x < _mapManager.mapwidth; x++)
                if (walls.TryGetValue(_mapManager.MAPSPOT(x, y, 0), out var wall) && wall.Outlet is { Class.Length: > 0 } spec)
                {
                    var outlet = new Outlet { X = x, Y = y, Spec = spec };
                    outlet.Delay = OutletDelay(outlet);
                    outlets.Add(outlet);
                }
    }

    static int OutletDelay(Outlet outlet)
    {
        int spot = _mapManager.MAPSPOT(outlet.X, outlet.Y, 1);
        if ((spot & 0xff00) == 0xfa00)
            return 60 * (spot & 0xff);
        int min = outlet.Spec.Delay.ElementAtOrDefault(0), max = Math.Max(outlet.Spec.Delay.ElementAtOrDefault(1), min);
        return 60 * min + US_RndT() * 60 * (max - min) / 256;
    }

    // An actor that came out of an outlet keeps its tile (SeekX/SeekY, plus one), so each
    // outlet knows how many of its own are about
    static int OutletCount(Outlet outlet) =>
        _mapManager.GetActors().Count(a => !a.IsRemoved && a.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE)
            && a.SeekX == outlet.X + 1 && a.SeekY == outlet.Y + 1 && a.Name == outlet.Spec.Class);

    static void TickOutlets()
    {
        foreach (var outlet in outlets)
        {
            var max = outlet.Spec.Max;
            if (max.Count == 0 || OutletCount(outlet) >= max[Math.Min(gamestate.difficulty, max.Count - 1)])
                continue;

            if (outlet.Delay > tics)
            {
                outlet.Delay -= (int)tics;
                continue;
            }
            outlet.Delay = 1;       // try again next tic, unless one comes out

            // Beside it, in a room joined to the player's
            (int X, int Y)? spot = null;
            foreach (var (dx, dy) in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
            {
                int area = _mapManager.AreaAt(outlet.X + dx, outlet.Y + dy);
                if (area >= 0 && area < _mapManager.Floors.NumAreas && areabyplayer[area] != 0)
                {
                    spot = (outlet.X + dx, outlet.Y + dy);
                    break;
                }
            }
            if (spot is not { } tile || _mapManager.actorat[tile.X, tile.Y] != null || _mapManager.IsShootableActorAt(tile.X, tile.Y)
                || Math.Abs(player.TileX - tile.X) < 2 && Math.Abs(player.TileY - tile.Y) < 2)
                continue;

            // Where the player can see it, or now and then anyway
            if (!PlayerCanSee(tile.X, tile.Y) && US_RndT() < 200)
                continue;

            var spawned = _mapManager.SpawnThing(tile.X, tile.Y, new Assets.MapActorTranslation { Class = outlet.Spec.Class, Angles = -1 }, countKill: false);
            if (spawned != null)
            {
                spawned.SeekX = (byte)(outlet.X + 1);
                spawned.SeekY = (byte)(outlet.Y + 1);
                if (outlet.Spec.Sound.Length > 0)
                    PlaySoundLocActor(outlet.Spec.Sound, spawned);
            }
            outlet.Delay = OutletDelay(outlet);
            break;
        }
    }

    /// <summary>Whether the player has a clear line to a tile's centre</summary>
    static bool PlayerCanSee(int tilex, int tiley)
    {
        var probe = new Entities.Actors.Actor { Name = "" };
        probe.SetPosition(tilex, tiley);
        return CheckLine(probe);
    }

    /*
    =============================================================================

                            WARP SITES (Goldfire)

    A WARPSITE marker's `warpsite.class` (Goldfire) warps in at one of the level's sites: one
    the player can see (not where he last did, when there are others), after a wait (first
    `warpsite.firstwait`, then `warpsite.wait`, both [min, max] seconds; one second on its
    `warpsite.quickfloor`), once nothing's on it and the player is more than a tile off. Only one
    is about at a time; when it dies (A_WarpSiteGone) the next wait starts.

    =============================================================================
    */

    static readonly List<Entities.Actors.Actor> warpsites = [];
    static int warpwait, warplast = -1, warpchosen = -1;
    static bool warpfirst = true;
    static string? warpclass;

    static void InitWarpSites()
    {
        warpsites.Clear();
        warpsites.AddRange(_mapManager.GetActors().Where(a => a.HasFlag("WARPSITE")));
        warpclass = warpsites.Select(s => s.PropertyStrings("warpsite.class").FirstOrDefault()).FirstOrDefault(c => !string.IsNullOrEmpty(c));
        warpfirst = true;
        warplast = warpchosen = -1;
        warpwait = warpsites.Count > 0 ? WarpWait(warpsites[0], first: true) : 0;
    }

    static int WarpWait(Entities.Actors.Actor site, bool first)
    {
        if (site.PropertyInt("warpsite.quickfloor", -1) == _mapManager.CurrentFloorNumber)
            return 60;
        var range = site.PropertyInts(first ? "warpsite.firstwait" : "warpsite.wait");
        int min = range.ElementAtOrDefault(0), max = Math.Max(range.ElementAtOrDefault(1), min);
        return 60 * min + US_RndT() * 60 * (max - min) / 256;
    }

    static void TickWarpSites()
    {
        if (warpsites.Count == 0 || warpclass == null)
            return;
        if (_mapManager.GetActors().Any(a => !a.IsRemoved && a.Name == warpclass))
            return;     // it's about (or still warping out)

        if (warpwait > tics)
        {
            warpwait -= (int)tics;
            return;
        }

        if (warpchosen < 0)
        {
            // Pick a site the player can see
            warpwait = 0;
            for (int i = 0; i < warpsites.Count; i++)
            {
                if (warpsites.Count > 1 && i == warplast)
                    continue;
                if (!PlayerCanSee(warpsites[i].TileX, warpsites[i].TileY))
                    continue;
                warpchosen = warplast = i;
                warpwait = WarpWait(warpsites[i], warpfirst);
                break;
            }
            return;
        }

        var site = warpsites[warpchosen];
        if (_mapManager.IsShootableActorAt(site.TileX, site.TileY)
            || Math.Abs(player.TileX - site.TileX) <= 1 || Math.Abs(player.TileY - site.TileY) <= 1)
            return;

        _mapManager.SpawnThing(site.TileX, site.TileY, new Assets.MapActorTranslation { Class = warpclass }, countKill: false);
        warpfirst = false;
        warpchosen = -1;
    }

    /// <summary>A_WarpSiteGone: the warp site's actor has gone (warped out); the wait for the next starts</summary>
    internal static void A_WarpSiteGone(Entities.Actors.Actor ob)
    {
        if (warpsites.Count > 0)
            warpwait = WarpWait(warpsites[0], first: false);
        warpchosen = -1;
    }

    /// <summary>A_WarpSitesOff: no more comes; the level's warp sites go (Goldfire, morphed)</summary>
    internal static void A_WarpSitesOff(Entities.Actors.Actor ob)
    {
        foreach (var site in warpsites)
            _mapManager.MarkForRemoval(site);
        warpsites.Clear();
        warpchosen = -1;
    }
}
