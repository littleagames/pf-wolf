using PFWolf.Entities.Actors;
using PFWolf.Managers;

namespace PFWolf.Gameplay.MatchModes;

/*
=============================================================================

                        DOMINATION AND KING OF THE HILL

    A zone is the square of tiles around a flag. Players of one team alone in
    it take it over, faster the more of them; with both teams in it, nobody
    gains. Each second, each zone a team holds scores it a point.

    Domination: three control points (ControlPoint markers, mapdefs 442; else
    three spread over the map, A to C), each held until the other team takes
    it: they first bring it back to nobody's, then make it theirs.

    King of the Hill: one hill at a time, at one of the HillSpot markers
    (mapdefs 443; else spots spread over the map). The team alone on it holds
    it, until they leave or die, or the other team comes; it moves to another
    spot at random every HillTics.

=============================================================================
*/

internal abstract class ControlZones(MapManager map, GameSession session, Deathmatch match, HudMessageManager hud, MatchModeHooks hooks)
    : MatchObjective(map, session, match, hud, hooks)
{
    /// <summary>The class of the flag a zone shows (colored for who holds it)</summary>
    internal const string FlagClass = "ObjectiveFlag";

    /// <summary>A zone: its middle, who holds it (-1 nobody), and how far a team has got taking it (+ red, - blue)</summary>
    internal sealed class Zone
    {
        public (int x, int y) At;
        public int Owner = -1;
        public int Progress;
    }

    internal readonly List<Zone> Zones = [];

    /// <summary>The last whole second the zones scored in</summary>
    internal int ScoredSecond;

    /// <summary>How many tiles out from its middle a zone reaches (1: a 3x3 square)</summary>
    protected virtual int Radius => 1;

    /// <summary>The zones being played for (King of the Hill: just the hill)</summary>
    protected virtual IEnumerable<Zone> ActiveZones => Zones;

    /// <summary>Each team's living players in a zone</summary>
    protected (int red, int blue) PlayersIn(Zone zone)
    {
        int red = 0, blue = 0;
        foreach (var pawn in LivingPawns)
            if (InZone(pawn, zone.At.x, zone.At.y, Radius))
            {
                if (pawn.State.Team == 0) red++;
                else if (pawn.State.Team == 1) blue++;
            }
        return (red, blue);
    }

    /// <summary>Points a team has scored in each second gone by, for the zones it holds</summary>
    protected void ScoreSeconds()
    {
        int second = Session.GameState.TimeCount / 70;
        for (; ScoredSecond < second; ScoredSecond++)
            foreach (var zone in ActiveZones.Where(z => z.Owner >= 0).ToList())
            {
                // and each of the team in it scores one for themselves
                foreach (var pawn in LivingPawns.Where(pawn => pawn.State.Team == zone.Owner && InZone(pawn, zone.At.x, zone.At.y, Radius)))
                    pawn.State.Objective++;
                Match.AddTeamScore(zone.Owner, 1);
            }
    }

    protected void ShowZoneFlags()
    {
        var shown = ActiveZones.Select(z => z.At).ToArray();
        HideFlags(FlagClass, shown);
        foreach (var zone in ActiveZones)
            ShowFlag(FlagClass, zone.At.x, zone.At.y, FlagTranslation(zone.Owner));
    }

    /// <summary>Each zone's place and holder: "A 3,4 Red; B 30,30 -"</summary>
    public string? Details() =>
        string.Join("; ", Zones.Select((z, i) => $"{Letter(i)} {z.At.x},{z.At.y} {(z.Owner >= 0 ? GameModes.TeamLabel(z.Owner) : "-")}"));

    public override void Write(BinaryWriter bw)
    {
        bw.Write(ScoredSecond);
        bw.Write(Zones.Count);
        foreach (var zone in Zones)
        {
            WriteTile(bw, zone.At);
            bw.Write(zone.Owner);
            bw.Write(zone.Progress);
        }
    }

    public override void Read(BinaryReader br)
    {
        ScoredSecond = br.ReadInt32();
        Zones.Clear();
        for (int i = br.ReadInt32(); i > 0; i--)
            Zones.Add(new Zone { At = ReadTile(br), Owner = br.ReadInt32(), Progress = br.ReadInt32() });
    }

    // A zone's letter: A, B, C...
    protected static string Letter(int index) => ((char)('A' + index % 26)).ToString();
}

internal sealed class Domination(MapManager map, GameSession session, Deathmatch match, HudMessageManager hud, MatchModeHooks hooks)
    : ControlZones(map, session, match, hud, hooks), IMatchObjective
{
    public override GameMode Mode => GameMode.Domination;

    /// <summary>How long one player alone takes to bring a point over from nobody's (tics); twice that from the other team's</summary>
    internal const int CaptureTics = 3 * 70;

    public override void LevelSetup()
    {
        Zones.Clear();
        ScoredSecond = Session.GameState.TimeCount / 70;
        var spots = Markers("ControlPoint");
        if (spots.Count == 0)
        {
            // The two places farthest apart, and the open tile nearest halfway between them
            var floor = OpenFloor();
            spots = FarthestPair(floor) is { } pair
                ? new[] { pair.a, NearestOf(floor, ((pair.a.x + pair.b.x) / 2, (pair.a.y + pair.b.y) / 2)), pair.b }.Distinct().ToList()
                : SpreadOut(floor, 3, []);
        }
        foreach (var spot in spots)
            Zones.Add(new Zone { At = spot });
        ShowZoneFlags();
    }

    public override void Tick()
    {
        for (int i = 0; i < Zones.Count; i++)
        {
            var zone = Zones[i];
            var (red, blue) = PlayersIn(zone);
            if (red > 0 == blue > 0)
                continue;       // empty, or contested

            int team = red > 0 ? 0 : 1, sign = team == 0 ? 1 : -1;
            if (zone.Owner == team)
            {
                zone.Progress = sign * CaptureTics;     // theirs, and fully so again
                continue;
            }

            zone.Progress = Math.Clamp(zone.Progress + sign * (int)Session.Tics * Math.Max(red, blue), -CaptureTics, CaptureTics);
            if (zone.Owner >= 0 && Math.Sign(zone.Progress) != Math.Sign(zone.Owner == 0 ? 1 : -1))
            {
                Announce($"{GameModes.TeamLabel(zone.Owner)} lost point {Letter(i)}");
                zone.Owner = -1;        // brought back to nobody's
            }
            if (zone.Owner < 0 && zone.Progress * sign >= CaptureTics)
            {
                zone.Owner = team;
                foreach (var pawn in LivingPawns.Where(pawn => pawn.State.Team == team && InZone(pawn, zone.At.x, zone.At.y, Radius)))
                    pawn.State.Objective += 5;
                Announce($"{GameModes.TeamLabel(team)} took point {Letter(i)}!", "misc/k_pkup");
            }
        }

        ScoreSeconds();
        ShowZoneFlags();
    }

    /// <summary>Who holds each point: "A: Red   B: -   C: Blue (taking 40%)"</summary>
    public string? StatusLine() =>
        string.Join("   ", Zones.Select((zone, i) =>
        {
            var holder = zone.Owner >= 0 ? GameModes.TeamLabel(zone.Owner) : "-";
            var (red, blue) = PlayersIn(zone);
            bool taking = red > 0 != blue > 0 && zone.Owner != (red > 0 ? 0 : 1);
            return taking ? $"{Letter(i)}: {holder} ({Math.Abs(zone.Progress) * 100 / CaptureTics}%)" : $"{Letter(i)}: {holder}";
        }));
}

internal sealed class KingOfTheHill(MapManager map, GameSession session, Deathmatch match, HudMessageManager hud, MatchModeHooks hooks)
    : ControlZones(map, session, match, hud, hooks), IMatchObjective
{
    public override GameMode Mode => GameMode.KingOfTheHill;

    /// <summary>How long the hill stays in one place (tics)</summary>
    internal const int HillTics = 60 * 70;

    /// <summary>How many spots the hill moves between on a map without HillSpot markers</summary>
    internal const int Spots = 5;

    /// <summary>Which spot the hill is on now, and when it moves on (GameState.TimeCount)</summary>
    internal int Hill, MovesAt;

    protected override int Radius => 1;

    protected override IEnumerable<Zone> ActiveZones => Zones.Count > 0 ? [Zones[Hill]] : [];

    public override void LevelSetup()
    {
        Zones.Clear();
        ScoredSecond = Session.GameState.TimeCount / 70;
        var spots = Markers("HillSpot");
        if (spots.Count == 0)
            spots = SpreadOut(OpenFloor(), Spots, []);
        foreach (var spot in spots)
            Zones.Add(new Zone { At = spot });
        Hill = 0;       // the first (the middle of the map, without markers)
        MovesAt = Session.GameState.TimeCount + HillTics;
        ShowZoneFlags();
    }

    public override void Tick()
    {
        if (Zones.Count == 0)
            return;

        // Time's up: the hill moves on, to another spot at random
        if (Session.GameState.TimeCount >= MovesAt)
        {
            ScoreSeconds();
            Zones[Hill].Owner = -1;
            if (Zones.Count > 1)
                Hill = (Hill + 1 + Session.Random.RndT() % (Zones.Count - 1)) % Zones.Count;
            MovesAt = Session.GameState.TimeCount + HillTics;
            Announce("The hill has moved!", "world/pushwall");
        }

        // Held by the team alone on it; nobody's with both, or neither
        var zone = Zones[Hill];
        var (red, blue) = PlayersIn(zone);
        int holder = red > 0 == blue > 0 ? -1 : red > 0 ? 0 : 1;
        if (holder != zone.Owner)
        {
            if (holder >= 0)
                Announce($"{GameModes.TeamLabel(holder)} holds the hill", "misc/k_pkup");
            zone.Owner = holder;
        }

        ScoreSeconds();
        ShowZoneFlags();
    }

    /// <summary>Who holds the hill, and when it moves: "Hill: Red   moves in 0:42"</summary>
    public string? StatusLine()
    {
        if (Zones.Count == 0)
            return null;
        int left = Math.Max(0, MovesAt - Session.GameState.TimeCount) / 70;
        var zone = Zones[Hill];
        var holder = zone.Owner >= 0 ? GameModes.TeamLabel(zone.Owner) : PlayersIn(zone) is (> 0, > 0) ? "contested" : "nobody";
        return $"Hill: {holder}   moves in {left / 60}:{left % 60:00}";
    }

    public override void Write(BinaryWriter bw)
    {
        base.Write(bw);
        bw.Write(Hill);
        bw.Write(MovesAt);
    }

    public override void Read(BinaryReader br)
    {
        base.Read(br);
        Hill = br.ReadInt32();
        MovesAt = br.ReadInt32();
    }
}
