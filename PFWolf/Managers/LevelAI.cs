using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Managers;

/// <summary>
/// What the level does on its own, rather than any one actor (GameHost builds it, for the level loop and setup, the player and the actors):
/// enemies coming out of wall outlets (mapdefs walls `outlet:`), Goldfire warping in at his spawn
/// sites (WARPSITE markers), the once-a-level `dropitem.onfloor` drop, and the player talking to
/// Blake Stone's actors, as bstone has them (3d_act1.cpp, 3d_act2.cpp, 3d_agent.cpp). The
/// actors themselves are <see cref="Monster"/>s and <see cref="BlakeMonster"/>s.
/// </summary>
internal sealed class LevelAI(MapManager map, LineOfSight lineOfSight, PositionalSounds sounds, GameSession session)
{
    private readonly MapManager _map = map;

    /// <summary>Sets up a fresh or loaded level's outlets and warp sites (SetupGameLevel)</summary>
    internal void OnLevelStart()
    {
        _floorDropGiven = false;
        InitOutlets();
        InitWarpSites();
    }

    /// <summary>Runs the level's outlets and warp sites, once a tic (PlayLoop)</summary>
    internal void Tick()
    {
        TickOutlets();
        TickWarpSites();
    }

    /// <summary>Whether the player has a clear line to a tile's centre</summary>
    private bool PlayerCanSee(int tilex, int tiley)
    {
        var probe = new Entities.Actors.Actor { Name = "" };
        probe.SetPosition(tilex, tiley);
        return lineOfSight.CheckLine(probe);
    }

    /*
    =============================================================================

                                FLOOR DROPS

    =============================================================================
    */

    private bool _floorDropGiven;

    /// <summary>
    /// A `dropitem.onfloor` item (Goldfire's gold card) is only dropped once a level: true the
    /// first time it's asked for on a level, false after
    /// </summary>
    internal bool TakeFloorDrop()
    {
        if (_floorDropGiven)
            return false;
        _floorDropGiven = true;
        return true;
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

    private sealed class Outlet
    {
        public int X, Y, Delay;
        public required Assets.MapOutletTranslation Spec;
    }

    private List<Outlet> _outlets = [];

    private void InitOutlets()
    {
        _outlets = [];
        var walls = _map.GetMapData().Walls;
        for (int y = 0; y < _map.mapheight; y++)
            for (int x = 0; x < _map.mapwidth; x++)
                if (walls.TryGetValue(_map.MAPSPOT(x, y, 0), out var wall) && wall.Outlet is { Class.Length: > 0 } spec)
                {
                    var outlet = new Outlet { X = x, Y = y, Spec = spec };
                    outlet.Delay = OutletDelay(outlet);
                    _outlets.Add(outlet);
                }
    }

    private int OutletDelay(Outlet outlet)
    {
        int spot = _map.MAPSPOT(outlet.X, outlet.Y, 1);
        if ((spot & 0xff00) == 0xfa00)
            return 60 * (spot & 0xff);
        int min = outlet.Spec.Delay.ElementAtOrDefault(0), max = Math.Max(outlet.Spec.Delay.ElementAtOrDefault(1), min);
        return 60 * min + session.random.RndT() * 60 * (max - min) / 256;
    }

    // A BlakeMonster that came out of an outlet keeps its tile (SeekX/SeekY, plus one), so each
    // outlet knows how many of its own are about; of any other class, every one about counts
    private int OutletCount(Outlet outlet) =>
        _map.GetActors().Count(a => !a.IsRemoved && a.RuntimeFlags.HasFlag(ObjFlags.FL_SHOOTABLE) && a.Name == outlet.Spec.Class
            && (a is not BlakeMonster b || b.SeekX == outlet.X + 1 && b.SeekY == outlet.Y + 1));

    private void TickOutlets()
    {
        foreach (var outlet in _outlets)
        {
            var max = outlet.Spec.Max;
            if (max.Count == 0 || OutletCount(outlet) >= max[Math.Min(session.gamestate.difficulty, max.Count - 1)])
                continue;

            if (outlet.Delay > session.tics)
            {
                outlet.Delay -= (int)session.tics;
                continue;
            }
            outlet.Delay = 1;       // try again next tic, unless one comes out

            // Beside it, in a room joined to the player's
            (int X, int Y)? spot = null;
            foreach (var (dx, dy) in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
            {
                int area = _map.AreaAt(outlet.X + dx, outlet.Y + dy);
                if (area >= 0 && area < _map.Floors.NumAreas && session.areas.areabyplayer[area] != 0)
                {
                    spot = (outlet.X + dx, outlet.Y + dy);
                    break;
                }
            }
            if (spot is not { } tile || _map.actorat[tile.X, tile.Y] != null || _map.IsShootableActorAt(tile.X, tile.Y)
                || Math.Abs(session.player.TileX - tile.X) < 2 && Math.Abs(session.player.TileY - tile.Y) < 2)
                continue;

            // Where the player can see it, or now and then anyway
            if (!PlayerCanSee(tile.X, tile.Y) && session.random.RndT() < 200)
                continue;

            var spawned = _map.SpawnThing(tile.X, tile.Y, new Assets.MapActorTranslation { Class = outlet.Spec.Class, Angles = -1 }, countKill: false);
            if (spawned is BlakeMonster blake)
            {
                blake.SeekX = (byte)(outlet.X + 1);
                blake.SeekY = (byte)(outlet.Y + 1);
            }
            if (spawned != null && outlet.Spec.Sound.Length > 0)
                sounds.PlaySoundLocActor(outlet.Spec.Sound, spawned);
            outlet.Delay = OutletDelay(outlet);
            break;
        }
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

    private readonly List<Entities.Actors.Actor> _warpSites = [];
    private int _warpWait, _warpLast = -1, _warpChosen = -1;
    private bool _warpFirst = true;
    private string? _warpClass;

    private void InitWarpSites()
    {
        _warpSites.Clear();
        _warpSites.AddRange(_map.GetActors().Where(a => a.HasFlag("WARPSITE")));
        _warpClass = _warpSites.Select(s => s.PropertyStrings("warpsite.class").FirstOrDefault()).FirstOrDefault(c => !string.IsNullOrEmpty(c));
        _warpFirst = true;
        _warpLast = _warpChosen = -1;
        _warpWait = _warpSites.Count > 0 ? WarpWait(_warpSites[0], first: true) : 0;
    }

    private int WarpWait(Entities.Actors.Actor site, bool first)
    {
        if (site.PropertyInt("warpsite.quickfloor", -1) == _map.CurrentFloorNumber)
            return 60;
        var range = site.PropertyInts(first ? "warpsite.firstwait" : "warpsite.wait");
        int min = range.ElementAtOrDefault(0), max = Math.Max(range.ElementAtOrDefault(1), min);
        return 60 * min + session.random.RndT() * 60 * (max - min) / 256;
    }

    private void TickWarpSites()
    {
        if (_warpSites.Count == 0 || _warpClass == null)
            return;
        if (_map.GetActors().Any(a => !a.IsRemoved && a.Name == _warpClass))
            return;     // it's about (or still warping out)

        if (_warpWait > session.tics)
        {
            _warpWait -= (int)session.tics;
            return;
        }

        if (_warpChosen < 0)
        {
            // Pick a site the player can see
            _warpWait = 0;
            for (int i = 0; i < _warpSites.Count; i++)
            {
                if (_warpSites.Count > 1 && i == _warpLast)
                    continue;
                if (!PlayerCanSee(_warpSites[i].TileX, _warpSites[i].TileY))
                    continue;
                _warpChosen = _warpLast = i;
                _warpWait = WarpWait(_warpSites[i], _warpFirst);
                break;
            }
            return;
        }

        var site = _warpSites[_warpChosen];
        if (_map.IsShootableActorAt(site.TileX, site.TileY)
            || Math.Abs(session.player.TileX - site.TileX) <= 1 || Math.Abs(session.player.TileY - site.TileY) <= 1)
            return;

        _map.SpawnThing(site.TileX, site.TileY, new Assets.MapActorTranslation { Class = _warpClass }, countKill: false);
        _warpFirst = false;
        _warpChosen = -1;
    }

    /// <summary>A_WarpSiteGone: the warp site's actor has gone (warped out); the wait for the next starts</summary>
    internal void WarpSiteGone()
    {
        if (_warpSites.Count > 0)
            _warpWait = WarpWait(_warpSites[0], first: false);
        _warpChosen = -1;
    }

    /// <summary>A_WarpSitesOff: no more comes; the level's warp sites go (Goldfire, morphed)</summary>
    internal void WarpSitesOff()
    {
        foreach (var site in _warpSites)
            _map.MarkForRemoval(site);
        _warpSites.Clear();
        _warpChosen = -1;
    }

    /*
    =============================================================================

                                TALKING

    Holding use near a TALKATIVE actor that's friendly and in view, facing it, talks to it
    (BlakeMonster.TalkTo).

    =============================================================================
    */

    // How long until the use key held down talks again
    private int _talkDelay;

    /// <summary>The use key held with nothing to use ahead: talk to whoever's there. False if no one was talked to.</summary>
    internal bool TryTalk()
    {
        if (_talkDelay > 0)
        {
            _talkDelay = Math.Max(_talkDelay - (int)session.tics, 0);
            return false;
        }

        const int MaxAngle = 45 / 2;
        BlakeMonster? chosen = null;
        int chosenDist = (int)GameConstants.MINACTORDIST;

        foreach (var ob in _map.GetActors().OfType<BlakeMonster>())
        {
            if (ob.IsRemoved || !ob.HasFlag("TALKATIVE")
                || (ob.RuntimeFlags & (ObjFlags.FL_FRIENDLY | ObjFlags.FL_VISABLE)) != (ObjFlags.FL_FRIENDLY | ObjFlags.FL_VISABLE)
                || Math.Abs(ob.TileX - session.player.TileX) > 2 || Math.Abs(ob.TileY - session.player.TileY) > 2)
                continue;

            int dist = Math.Min(Math.Abs(session.player.X - ob.X), Math.Abs(session.player.Y - ob.Y));
            if (dist >= chosenDist)
                continue;

            if (ob.RuntimeFlags.HasFlag(ObjFlags.FL_ATTACKMODE))
            {
                ob.RuntimeFlags &= ~ObjFlags.FL_FRIENDLY;
                continue;
            }

            var angle = Math.Atan2(session.player.Y - ob.Y, ob.X - session.player.X);
            if (angle < 0)
                angle += Math.PI * 2;
            int facing = Math.Abs(session.player.Angle - (int)(angle / (Math.PI * 2) * GameConstants.ANGLES));
            if (Math.Min(facing, GameConstants.ANGLES - facing) > MaxAngle)
                continue;

            chosen = ob;
            chosenDist = dist;
        }

        if (chosen == null)
            return false;
        _talkDelay = chosen.TalkTo() ? 20 : 120;      // an informant can be asked again sooner
        return true;
    }

    /// <summary>Use let go: the next press talks straight away</summary>
    internal void ResetTalkDelay() => _talkDelay = 0;

    /// <summary>
    /// Whether to warn the player for shooting an informant: the first time, and now and then
    /// after (BlakeMonster's)
    /// </summary>
    internal bool ShouldWarnKilledInformant()
    {
        if (_warnedKilledInformant && session.random.RndT() >= 25)
            return false;
        _warnedKilledInformant = true;
        return true;
    }

    private bool _warnedKilledInformant;
}
