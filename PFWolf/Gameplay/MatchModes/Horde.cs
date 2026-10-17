using PFWolf.Assets;
using PFWolf.Entities.Actors;
using PFWolf.Managers;

namespace PFWolf.Gameplay.MatchModes;

/*
=============================================================================

                                  HORDE

    The players together against waves of enemies (game-info horde: Wolf3D's
    are mutants), coming in a few at a time at the map's HordeSpawn markers
    (mapdefs 444) or, without any, on open floor joined to the players' and
    well away from them, already hunting. Once a wave is all dead a break
    follows (break-seconds), when the dead can come back (only then) and the
    next wave is announced. Outlast the score limit's waves to win; the match
    is lost when every player is dead at once. Taken items come back, and
    weapons stay for everyone, as in a deathmatch.

=============================================================================
*/

internal sealed class Horde(MapManager map, GameSession session, Deathmatch match, HudMessageManager hud, MatchModeHooks hooks,
    Func<HordeInfo> settings)
    : MatchObjective(map, session, match, hud, hooks), IMatchObjective
{
    public override GameMode Mode => GameMode.Horde;

    internal enum Phase { Break, Wave }

    /// <summary>The wave being fought (or coming next, in a break), from 1</summary>
    internal int wave;

    /// <summary>Waves the players have outlasted</summary>
    internal int survived;

    internal Phase phase;

    /// <summary>When the break ends, or the next enemy can come in (gamestate.TimeCount)</summary>
    internal int nextat;

    /// <summary>The wave's enemies still to come in, by class, in order</summary>
    internal readonly List<(string Class, int Count)> tocome = [];

    /// <summary>The match is lost: everyone died at once</summary>
    internal bool wiped;

    // Where enemies can come in: the markers, else the map's open floor
    List<(int x, int y)> spawns = [];
    bool markerspawns;

    public override void LevelSetup()
    {
        spawns = Markers("HordeSpawn");
        markerspawns = spawns.Count > 0;
        if (!markerspawns)
            spawns = OpenFloor();
        wave = 1;
        survived = 0;
        wiped = false;
        tocome.Clear();
        StartBreak();
    }

    void StartBreak()
    {
        phase = Phase.Break;
        nextat = session.gamestate.TimeCount + Math.Max(settings().BreakSeconds, 1) * 70;
    }

    /// <summary>
    /// The enemies a wave sends: its own from game-info's list, or past the end, the last one
    /// with `grow` more of each class per wave; half as many again for each player past the first
    /// </summary>
    internal List<(string Class, int Count)> WaveEnemies(int number, int players)
    {
        var info = settings();
        var waves = info.Waves.Count > 0 ? info.Waves : [new HordeWave { Enemies = new() { ["Mutant"] = 4 } }];
        int index = Math.Clamp(number - 1, 0, waves.Count - 1);
        int extra = Math.Max(0, number - waves.Count) * info.Grow;
        return waves[index].Enemies
            .Select(e => (e.Key, (Math.Max(e.Value, 0) + extra) * (2 + Math.Max(players - 1, 0)) / 2))
            .Where(e => e.Item2 > 0)
            .ToList();
    }

    public override void Tick()
    {
        if (wiped)
            return;
        var time = session.gamestate.TimeCount;
        var info = settings();

        if (phase == Phase.Break)
        {
            if (time < nextat)
                return;
            phase = Phase.Wave;
            tocome.Clear();
            tocome.AddRange(WaveEnemies(wave, map.Players.Count(pawn => !pawn.State.Gone)));
            nextat = time;
            Announce($"Wave {wave}!", "misc/yeah");
        }

        // Bring the next one in, while there's room
        int alive = AliveEnemies();
        if (tocome.Count > 0 && time >= nextat && alive < Math.Max(info.MaxAlive, 1))
        {
            if (SpawnEnemy(tocome[0].Class))
            {
                tocome[0] = (tocome[0].Class, tocome[0].Count - 1);
                if (tocome[0].Count <= 0)
                    tocome.RemoveAt(0);
                alive++;
            }
            nextat = time + Math.Max(info.SpawnTics, 1);
        }

        // The wave's beaten: a break, and the dead can come back
        if (tocome.Count == 0 && alive == 0)
        {
            survived = wave;
            Announce($"Wave {wave} cleared!", "misc/100percent");
            if (match.netrules.ScoreLimit > 0 && survived >= match.netrules.ScoreLimit)
            {
                match.EndMatch();
                return;
            }
            wave++;
            StartBreak();
        }
    }

    /// <summary>The level's living enemies (any of them: a wave's, or the map's own with monsters on)</summary>
    int AliveEnemies() =>
        map.GetActors().Count(a => a is Monster && !a.IsRemoved && a.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE));

    /// <summary>One of a wave's enemies comes in, hunting, at a spawn out of the players' way: whether one did</summary>
    bool SpawnEnemy(string className)
    {
        var players = LivingPawns.ToList();
        bool Reachable((int x, int y) t)
        {
            int area = map.AreaAt(t.x, t.y);
            return area < 0 || area >= map.Floors.NumAreas || session.areas.areabyplayer[area] != 0;
        }
        int Nearest((int x, int y) t) =>
            players.Count == 0 ? int.MaxValue : players.Min(pawn => Math.Max(Math.Abs(pawn.TileX - t.x), Math.Abs(pawn.TileY - t.y)));

        var free = spawns.Where(t => hooks.IsFreeFloor(t.x, t.y) && Reachable(t)).ToList();
        if (free.Count == 0)
            return false;
        var far = free.Where(t => Nearest(t) >= (markerspawns ? 2 : 8)).ToList();
        if (far.Count > 0)
            free = far;
        var spot = free[((session.random.RndT() << 8) | session.random.RndT()) % free.Count];

        bool nomonsters = map.NoMonsters;
        map.NoMonsters = false;     // the map's own may be left out; a wave's never are
        var spawned = map.SpawnThing(spot.x, spot.y, new MapActorTranslation { Class = className, Angles = -1 }, countKill: false);
        map.NoMonsters = nomonsters;
        if (spawned is not Monster monster)
        {
            if (spawned != null)
                map.MarkForRemoval(spawned);
            return false;
        }
        monster.Active = activetypes.ac_yes;
        monster.FirstSighting();        // straight after the players
        return true;
    }

    /// <summary>A player has died: with everyone dead at once, the match is lost</summary>
    public void PlayerDied(Entities.PlayerState victim)
    {
        if (session.players.Where(p => !p.Gone && p.Pawn != null).All(p => p.health <= 0))
        {
            wiped = true;
            Announce($"Everyone's dead! You survived {survived} wave{(survived == 1 ? "" : "s")}.");
            match.EndMatch();
        }
    }

    /// <summary>The dead come back only in a break between waves</summary>
    public bool MayRespawn(Entities.PlayerState p) => phase == Phase.Break;

    public string? StatusLine()
    {
        if (phase == Phase.Break)
            return $"Wave {wave} in {Math.Max(0, nextat - session.gamestate.TimeCount) / 70 + 1}s";
        int left = tocome.Sum(e => e.Count) + AliveEnemies();
        return $"Wave {wave}: {left} left" + (session.localplayer.health <= 0 ? " - back after the wave" : "");
    }

    public string? FinalTitle() =>
        wiped ? $"Survived {survived} wave{(survived == 1 ? "" : "s")}"
        : survived > 0 && match.netrules.ScoreLimit > 0 && survived >= match.netrules.ScoreLimit ? $"Victory! All {survived} waves beaten"
        : $"Survived {survived} wave{(survived == 1 ? "" : "s")}";

    public string? Details() =>
        $"wave {wave} ({phase}), {survived} survived, {AliveEnemies()} alive, to come: "
        + (tocome.Count == 0 ? "none" : string.Join(", ", tocome.Select(e => $"{e.Count} {e.Class}")))
        + $"; {spawns.Count} spawn spots ({(markerspawns ? "markers" : "open floor")})";

    public override void Write(BinaryWriter bw)
    {
        bw.Write(wave);
        bw.Write(survived);
        bw.Write((byte)phase);
        bw.Write(nextat);
        bw.Write(wiped);
        bw.Write(tocome.Count);
        foreach (var (cls, count) in tocome)
        {
            bw.Write(cls);
            bw.Write(count);
        }
    }

    public override void Read(BinaryReader br)
    {
        wave = br.ReadInt32();
        survived = br.ReadInt32();
        phase = (Phase)br.ReadByte();
        nextat = br.ReadInt32();
        wiped = br.ReadBoolean();
        tocome.Clear();
        for (int i = br.ReadInt32(); i > 0; i--)
            tocome.Add((br.ReadString(), br.ReadInt32()));
    }
}
