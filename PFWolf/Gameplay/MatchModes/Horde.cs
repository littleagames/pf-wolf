using PFWolf.Assets;
using PFWolf.Entities.Actors;
using PFWolf.Managers;

namespace PFWolf.Gameplay.MatchModes;

/*
=============================================================================

                                  HORDE

    The players together against waves of enemies (game-info horde: Wolf3D's
    are mutants; a game listing none has no Horde, and the lobby doesn't
    offer it), coming in a few at a time at the map's HordeSpawn markers
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
    internal int Wave;

    /// <summary>Waves the players have outlasted</summary>
    internal int Survived;

    internal Phase CurrentPhase;

    /// <summary>When the break ends, or the next enemy can come in (GameState.TimeCount)</summary>
    internal int NextAt;

    /// <summary>The wave's enemies still to come in, by class, in order</summary>
    internal readonly List<(string Class, int Count)> ToCome = [];

    /// <summary>The match is lost: everyone died at once</summary>
    internal bool Wiped;

    // Where enemies can come in: the markers, else the map's open floor
    List<(int x, int y)> spawns = [];
    bool markerspawns;

    /// <summary>The game-info lists no waves (the game has no Horde): nothing comes</summary>
    internal bool NoWaves => settings().Waves.Count == 0;

    // Wave classes that aren't actordefs classes, already warned about
    readonly HashSet<string> warnedClasses = new(StringComparer.OrdinalIgnoreCase);

    public override void LevelSetup()
    {
        if (NoWaves)
            Announce("This game has no Horde waves (game-info horde: waves).");

        spawns = Markers("HordeSpawn");
        markerspawns = spawns.Count > 0;
        if (!markerspawns)
            spawns = OpenFloor();
        Wave = 1;
        Survived = 0;
        Wiped = false;
        ToCome.Clear();
        StartBreak();
    }

    void StartBreak()
    {
        CurrentPhase = Phase.Break;
        NextAt = Session.GameState.TimeCount + Math.Max(settings().BreakSeconds, 1) * 70;
    }

    /// <summary>
    /// The enemies a wave sends: its own from game-info's list, or past the end, the last one
    /// with `grow` more of each class per wave; half as many again for each player past the first
    /// </summary>
    internal List<(string Class, int Count)> WaveEnemies(int number, int players)
    {
        var info = settings();
        var waves = info.Waves;
        if (waves.Count == 0)
            return [];
        int index = Math.Clamp(number - 1, 0, waves.Count - 1);
        int extra = Math.Max(0, number - waves.Count) * info.Grow;
        return waves[index].Enemies
            .Select(e => (e.Key, (Math.Max(e.Value, 0) + extra) * (2 + Math.Max(players - 1, 0)) / 2))
            .Where(e => e.Item2 > 0)
            .ToList();
    }

    public override void Tick()
    {
        if (Wiped || NoWaves)
            return;
        var time = Session.GameState.TimeCount;
        var info = settings();

        if (CurrentPhase == Phase.Break)
        {
            if (time < NextAt)
                return;
            CurrentPhase = Phase.Wave;
            ToCome.Clear();
            ToCome.AddRange(WaveEnemies(Wave, Map.Players.Count(pawn => !pawn.State.Gone)).Where(e => IsEnemyClass(e.Class)));
            NextAt = time;
            Announce($"Wave {Wave}!", "misc/yeah");
        }

        // Bring the next one in, while there's room
        int alive = AliveEnemies();
        if (ToCome.Count > 0 && time >= NextAt && alive < Math.Max(info.MaxAlive, 1))
        {
            if (SpawnEnemy(ToCome[0].Class))
            {
                ToCome[0] = (ToCome[0].Class, ToCome[0].Count - 1);
                if (ToCome[0].Count <= 0)
                    ToCome.RemoveAt(0);
                alive++;
            }
            NextAt = time + Math.Max(info.SpawnTics, 1);
        }

        // The wave's beaten: a break, and the dead can come back
        if (ToCome.Count == 0 && alive == 0)
        {
            Survived = Wave;
            Announce($"Wave {Wave} cleared!", "misc/100percent");
            if (Match.NetRules.ScoreLimit > 0 && Survived >= Match.NetRules.ScoreLimit)
            {
                Match.EndMatch();
                return;
            }
            Wave++;
            StartBreak();
        }
    }

    // Whether a wave's class is one there is: one that isn't is left out (warned about once),
    // so a typo can't hold a wave up for good
    bool IsEnemyClass(string className)
    {
        if (Map.IsActorClass(className))
            return true;
        if (warnedClasses.Add(className))
            Console.WriteLine($"Horde: wave enemy \"{className}\" isn't an actordefs class; left out.");
        return false;
    }

    /// <summary>The level's living enemies (any of them: a wave's, or the map's own with monsters on)</summary>
    int AliveEnemies() =>
        Map.GetActors().Count(a => a is Monster && !a.IsRemoved && a.RuntimeFlags.HasFlag(ObjFlags.Shootable));

    /// <summary>One of a wave's enemies comes in, hunting, at a spawn out of the players' way: whether one did</summary>
    bool SpawnEnemy(string className)
    {
        var players = LivingPawns.ToList();
        bool Reachable((int x, int y) t)
        {
            int area = Map.AreaAt(t.x, t.y);
            return area < 0 || area >= Map.Floors.NumAreas || Session.Areas.AreaByPlayer[area] != 0;
        }
        int Nearest((int x, int y) t) =>
            players.Count == 0 ? int.MaxValue : players.Min(pawn => Math.Max(Math.Abs(pawn.TileX - t.x), Math.Abs(pawn.TileY - t.y)));

        var free = spawns.Where(t => Hooks.IsFreeFloor(t.x, t.y) && Reachable(t)).ToList();
        if (free.Count == 0)
            return false;
        var far = free.Where(t => Nearest(t) >= (markerspawns ? 2 : 8)).ToList();
        if (far.Count > 0)
            free = far;
        var spot = free[((Session.Random.RndT() << 8) | Session.Random.RndT()) % free.Count];

        bool nomonsters = Map.NoMonsters;
        Map.NoMonsters = false;     // the map's own may be left out; a wave's never are
        var spawned = Map.SpawnThing(spot.x, spot.y, new MapActorTranslation { Class = className, Angles = -1 }, countKill: false);
        Map.NoMonsters = nomonsters;
        if (spawned is not Monster monster)
        {
            if (spawned != null)
                Map.MarkForRemoval(spawned);
            return false;
        }
        monster.Active = ActiveType.Yes;
        monster.FirstSighting();        // straight after the players
        return true;
    }

    /// <summary>A player has died: with everyone dead at once, the match is lost</summary>
    public void PlayerDied(Entities.PlayerState victim)
    {
        if (Session.Players.Where(p => !p.Gone && p.Pawn != null).All(p => p.Health <= 0))
        {
            Wiped = true;
            Announce($"Everyone's dead! You survived {Survived} wave{(Survived == 1 ? "" : "s")}.");
            Match.EndMatch();
        }
    }

    /// <summary>The dead come back only in a break between waves</summary>
    public bool MayRespawn(Entities.PlayerState p) => CurrentPhase == Phase.Break;

    public string? StatusLine()
    {
        if (NoWaves)
            return "No Horde waves in this game";
        if (CurrentPhase == Phase.Break)
            return $"Wave {Wave} in {Math.Max(0, NextAt - Session.GameState.TimeCount) / 70 + 1}s";
        int left = ToCome.Sum(e => e.Count) + AliveEnemies();
        return $"Wave {Wave}: {left} left" + (Session.LocalPlayer.Health <= 0 ? " - back after the wave" : "");
    }

    public string? FinalTitle() =>
        Wiped ? $"Survived {Survived} wave{(Survived == 1 ? "" : "s")}"
        : Survived > 0 && Match.NetRules.ScoreLimit > 0 && Survived >= Match.NetRules.ScoreLimit ? $"Victory! All {Survived} waves beaten"
        : $"Survived {Survived} wave{(Survived == 1 ? "" : "s")}";

    public string? Details() =>
        $"wave {Wave} ({CurrentPhase}), {Survived} survived, {AliveEnemies()} alive, to come: "
        + (ToCome.Count == 0 ? "none" : string.Join(", ", ToCome.Select(e => $"{e.Count} {e.Class}")))
        + $"; {spawns.Count} spawn spots ({(markerspawns ? "markers" : "open floor")})";

    public override void Write(BinaryWriter bw)
    {
        bw.Write(Wave);
        bw.Write(Survived);
        bw.Write((byte)CurrentPhase);
        bw.Write(NextAt);
        bw.Write(Wiped);
        bw.Write(ToCome.Count);
        foreach (var (cls, count) in ToCome)
        {
            bw.Write(cls);
            bw.Write(count);
        }
    }

    public override void Read(BinaryReader br)
    {
        Wave = br.ReadInt32();
        Survived = br.ReadInt32();
        CurrentPhase = (Phase)br.ReadByte();
        NextAt = br.ReadInt32();
        Wiped = br.ReadBoolean();
        ToCome.Clear();
        for (int i = br.ReadInt32(); i > 0; i--)
            ToCome.Add((br.ReadString(), br.ReadInt32()));
    }
}
