using PFWolf.Managers;

namespace PFWolf.Gameplay;

/*
=============================================================================

                                LOOPING SOUNDS

An actor's actordefs `loopsound` (or its frame's `loop-sound:`, none for silence) loops from
where it is, following it, heard louder as the camera comes near: a generator, a waterfall, a
machine. There are AudioManager.LoopVoices voices for them, apart from the effects', and the
nearest emitters within their `loopradius` have them. An emitter keeps its voice until another
is clearly nearer, so two at the edge of the pool don't swap every tic; a voice taken or given up
fades in or out, and an emitter going out of its radius fades away toward its edge.

It's only for show: picked from where the camera is (this machine's), with no game random
numbers, and it changes nothing in the game, so demos and network games stay in step. Nothing
is saved: what loops comes from each actor's class and state.

=============================================================================
*/

/// <summary>An actor that loops a sound now, where it is (in tiles), heard out to Radius tiles</summary>
/// <param name="Key">The actor (who has the voice)</param>
/// <param name="Name">Its class, for the loops command</param>
internal readonly record struct LoopCandidate(object Key, string Name, string Sound, float X, float Y, float Radius);

/// <summary>What the loop voices need from the sound (AudioManager's loop voices)</summary>
/// <param name="Start">A voice loops a sound from (x, y) out to a radius at a level; false: nothing to play</param>
/// <param name="Move">A voice's place and level</param>
/// <param name="Stop">A voice stops</param>
/// <param name="Playing">Whether a voice sounds still (StopAll stops them)</param>
internal sealed record LoopVoiceHooks(
    Func<int, string, float, float, float, float, bool> Start,
    Action<int, float, float, float> Move,
    Action<int> Stop,
    Func<int, bool> Playing);

/// <summary>
/// Which emitters have the loop voices, and how loud each is: given the emitters each tic and
/// where they're heard from. Apart from OpenAL (its hooks), so it can be tested.
/// </summary>
internal sealed class LoopEmitters(LoopVoiceHooks hooks, int voices)
{
    /// <summary>Tics a voice takes to fade in when it's given, or out when it's given up</summary>
    internal const int FadeTics = 8;

    /// <summary>How much nearer, in tiles, another emitter must be to take a voice one has</summary>
    internal const float Hysteresis = 2.0f;

    /// <summary>The share of an emitter's radius it's heard at full; past it, it fades away to nothing at the radius</summary>
    internal const float EdgeStart = 0.75f;

    /// <summary>Tics before a voice that had nothing to play tries again (a sound device switched on)</summary>
    internal const int RetryTics = 70;

    sealed class Voice
    {
        public object? Key;             // the emitter that has it; null: free
        public string Name = "", Sound = "";
        public float X, Y, Radius, Distance;
        public float Fade;              // 0 to 1
        public bool Wanted, Started;
        public int Retry;               // tics until it tries to start again
    }

    readonly Voice[] slots = Enumerable.Range(0, voices).Select(_ => new Voice()).ToArray();
    readonly Dictionary<object, int> candidateAt = new(ReferenceEqualityComparer.Instance);
    readonly List<(object Key, float Distance)> inRange = [];
    readonly List<int> inRangeIndex = [];
    readonly HashSet<object> holding = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The emitters as they are this tic, heard from (listenerX, listenerY), with so many tics gone
    /// by (0: paused, the fades stand still)
    /// </summary>
    internal void Update(IReadOnlyList<LoopCandidate> candidates, float listenerX, float listenerY, int tics)
    {
        // Each emitter's distance; those within their radius can have a voice
        candidateAt.Clear();
        inRange.Clear();
        inRangeIndex.Clear();
        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            candidateAt[c.Key] = i;
            float d = Distance(c.X - listenerX, c.Y - listenerY);
            if (d < c.Radius)
            {
                inRange.Add((c.Key, d));
                inRangeIndex.Add(i);
            }
        }

        holding.Clear();
        foreach (var slot in slots)
            if (slot.Key != null && slot.Wanted)
                holding.Add(slot.Key);
        var chosen = PickLoopVoices(inRange, holding, slots.Length, Hysteresis);
        var chosenKeys = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var pick in chosen)
            chosenKeys.Add(inRange[pick].Key);

        // The voices there are: still wanted or not, where their emitter is now, its sound now
        foreach (var slot in slots)
        {
            if (slot.Key == null)
                continue;
            slot.Wanted = chosenKeys.Contains(slot.Key);
            if (candidateAt.TryGetValue(slot.Key, out var at))
            {
                var c = candidates[at];
                (slot.X, slot.Y, slot.Radius) = (c.X, c.Y, c.Radius);
                slot.Distance = Distance(c.X - listenerX, c.Y - listenerY);
                if (slot.Wanted && !string.Equals(c.Sound, slot.Sound, StringComparison.OrdinalIgnoreCase))
                {
                    // another sound (a state with another loop-sound): it starts that, as loud as it was
                    slot.Sound = c.Sound;
                    slot.Started = false;
                    slot.Retry = 0;
                }
            }
            chosenKeys.Remove(slot.Key);
        }

        // Emitters chosen with no voice yet take a free one, else the quietest given up
        foreach (var pick in chosen)
        {
            var key = inRange[pick].Key;
            if (!chosenKeys.Contains(key))
                continue;
            var slot = FreeSlot();
            if (slot == null)
                break;
            if (slot.Key != null && slot.Started)
                hooks.Stop(Array.IndexOf(slots, slot));
            var c = candidates[inRangeIndex[pick]];
            slot.Key = key;
            (slot.Name, slot.Sound, slot.X, slot.Y, slot.Radius, slot.Distance) = (c.Name, c.Sound, c.X, c.Y, c.Radius, inRange[pick].Distance);
            (slot.Fade, slot.Wanted, slot.Started, slot.Retry) = (0, true, false, 0);
        }

        // Fades, starts, and the voices' places and levels
        float step = tics / (float)FadeTics;
        for (int v = 0; v < slots.Length; v++)
        {
            var slot = slots[v];
            if (slot.Key == null)
                continue;
            slot.Fade = slot.Wanted ? Math.Min(1, slot.Fade + step) : Math.Max(0, slot.Fade - step);
            if (!slot.Wanted && (slot.Fade <= 0 || slot.Started && !hooks.Playing(v)))
            {
                if (slot.Started)
                    hooks.Stop(v);
                Free(slot);
                continue;
            }

            float level = Level(slot);
            if (slot.Wanted && (!slot.Started || !hooks.Playing(v)))
            {
                if (slot.Retry > 0)
                {
                    slot.Retry -= Math.Max(tics, 0);
                    slot.Started = false;
                    continue;
                }
                slot.Started = hooks.Start(v, slot.Sound, slot.X, slot.Y, slot.Radius, level);
                if (!slot.Started)
                    slot.Retry = RetryTics;
            }
            else if (slot.Started)
                hooks.Move(v, slot.X, slot.Y, level);
        }
    }

    /// <summary>Every voice stops, and none is held (a new level, sound switched off)</summary>
    internal void Clear()
    {
        for (int v = 0; v < slots.Length; v++)
        {
            if (slots[v].Key != null && slots[v].Started)
                hooks.Stop(v);
            Free(slots[v]);
        }
    }

    /// <summary>The voices held, for the loops command: emitter, sound, distance, level</summary>
    internal IEnumerable<string> Describe()
    {
        for (int v = 0; v < slots.Length; v++)
        {
            var slot = slots[v];
            if (slot.Key == null)
                continue;
            string how = !slot.Started ? slot.Retry > 0 ? " (nothing to play)" : " (starting)"
                : !slot.Wanted ? " (fading out)" : slot.Fade < 1 ? " (fading in)" : "";
            yield return string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"{v + 1}: {slot.Name} {slot.Sound}, {slot.Distance:0.0} tiles off, level {Level(slot):0.00}{how}");
        }
    }

    /// <summary>The emitter each voice has (null: free), for tests</summary>
    internal object? KeyOf(int voice) => slots[voice].Key;

    /// <summary>A voice's level now, for tests</summary>
    internal float LevelOf(int voice) => Level(slots[voice]);

    // A voice's level: its fade, and its edge (fading away over the last quarter of its radius)
    static float Level(Voice slot) => slot.Fade * Edge(slot.Distance, slot.Radius);

    internal static float Edge(float distance, float radius)
    {
        if (radius <= 0)
            return 0;
        float start = radius * EdgeStart;
        return distance <= start ? 1 : Math.Clamp((radius - distance) / (radius - start), 0, 1);
    }

    // A free voice, else the quietest one given up and fading out; null when every one's wanted
    Voice? FreeSlot()
    {
        Voice? quietest = null;
        foreach (var slot in slots)
        {
            if (slot.Key == null)
                return slot;
            if (!slot.Wanted && (quietest == null || Level(slot) < Level(quietest)))
                quietest = slot;
        }
        return quietest;
    }

    static void Free(Voice slot)
    {
        slot.Key = null;
        (slot.Wanted, slot.Started, slot.Fade, slot.Retry) = (false, false, 0, 0);
    }

    static float Distance(float dx, float dy) => MathF.Sqrt(dx * dx + dy * dy);

    /// <summary>
    /// Which emitters (indexes into <paramref name="candidates"/>, each with its distance) have the
    /// <paramref name="count"/> voices: the nearest, those <paramref name="holding"/> one counted
    /// <paramref name="hysteresis"/> tiles nearer than they are, so another must be clearly
    /// nearer to take it. Ties go to the one listed first.
    /// </summary>
    internal static List<int> PickLoopVoices(IReadOnlyList<(object Key, float Distance)> candidates, IReadOnlySet<object> holding,
        int count, float hysteresis)
    {
        var order = new List<int>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
            order.Add(i);
        float Rank(int i) => candidates[i].Distance - (holding.Contains(candidates[i].Key) ? hysteresis : 0);
        order.Sort((a, b) =>
        {
            int byRank = Rank(a).CompareTo(Rank(b));
            return byRank != 0 ? byRank : a.CompareTo(b);
        });
        if (order.Count > count)
            order.RemoveRange(count, order.Count - count);
        return order;
    }
}

/// <summary>
/// The level's looping sounds: the actors that loop one now (their frame's loop-sound, else their
/// class's loopsound) handed to <see cref="LoopEmitters"/> each tic, heard from the camera.
/// </summary>
internal sealed class LoopSounds
{
    /// <summary>How far off a loop is heard when its class gives no loopradius, in tiles (the effects' MaxDistance)</summary>
    internal const float DefaultRadius = 16.0f;

    readonly MapManager map;
    readonly GameSession session;
    readonly LoopEmitters emitters;

    // Each class's loopsound, whether any of its frames has loop-sound:, and its loopradius; read
    // once a level
    readonly Dictionary<string, (string Sound, bool States, float Radius)> classes = new(StringComparer.OrdinalIgnoreCase);
    readonly List<Entities.Actors.Actor> looping = [];
    readonly List<LoopCandidate> candidates = [];
    int actorsVersion = -1, actorsCount = -1, levelSerial = -1;

    internal LoopSounds(MapManager map, AudioManager audio, GameSession session)
        : this(map, session, new LoopVoiceHooks(audio.StartLoopVoice, audio.MoveLoopVoice, audio.StopLoopVoice, audio.LoopVoicePlaying))
    {
    }

    internal LoopSounds(MapManager map, GameSession session, LoopVoiceHooks hooks)
    {
        this.map = map;
        this.session = session;
        emitters = new LoopEmitters(hooks, Managers.AudioManager.LoopVoices);
    }

    /// <summary>So many tics gone by (0 while the world is paused): the voices follow the emitters</summary>
    internal void Tick(uint tics)
    {
        var actors = map.GetActors();
        if (map.LevelSerial != levelSerial)
        {
            classes.Clear();        // a new level: another game's classes, maybe (mods)
            emitters.Clear();
            levelSerial = map.LevelSerial;
            actorsVersion = -1;
        }
        if (map.ActorsVersion != actorsVersion || actors.Count != actorsCount)
        {
            looping.Clear();
            foreach (var actor in actors)
            {
                if (actor != null && !actor.IsRemoved && ClassOf(actor) is { } known && (known.Sound.Length > 0 || known.States))
                    looping.Add(actor);
            }
            (actorsVersion, actorsCount) = (map.ActorsVersion, actors.Count);
        }

        candidates.Clear();
        foreach (var actor in looping)
        {
            if (actor.IsRemoved)
                continue;
            var known = ClassOf(actor);
            var sound = actor.CurrentState?.LoopSound ?? known.Sound;
            if (sound.Length == 0)
                continue;
            candidates.Add(new LoopCandidate(actor, actor.Name, sound,
                PositionalSounds.FixedToTiles(actor.X), PositionalSounds.FixedToTiles(actor.Y), known.Radius));
        }
        emitters.Update(candidates, PositionalSounds.FixedToTiles(session.Camera.X), PositionalSounds.FixedToTiles(session.Camera.Y), (int)tics);
    }

    /// <summary>For the loops command</summary>
    internal string Describe()
    {
        var voices = emitters.Describe().ToList();
        int heard = candidates.Count;
        return voices.Count == 0
            ? heard == 0 ? "No looping sounds" : $"No looping sounds heard ({heard} out of reach)"
            : $"Looping sounds ({voices.Count} of {Managers.AudioManager.LoopVoices} voices, {heard} emitters):\n" + string.Join("\n", voices);
    }

    (string Sound, bool States, float Radius) ClassOf(Entities.Actors.Actor actor)
    {
        if (classes.TryGetValue(actor.Name, out var known))
            return known;
        var sound = actor.Properties.TryGetValue("loopsound", out var value) ? Loaders.StateDataNodeDeserializer.LoopSoundName(value) : "";
        float radius = actor.Properties.TryGetValue("loopradius", out var r)
            && float.TryParse(r?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0 ? parsed : DefaultRadius;
        bool states = actor.ResolvedStates.Values.Any(first =>
        {
            var seen = new HashSet<Entities.Actors.ActorStateFrame>(ReferenceEqualityComparer.Instance);
            for (var frame = first; frame != null && seen.Add(frame); frame = frame.Next)
                if (frame.LoopSound != null)
                    return true;
            return false;
        });
        classes[actor.Name] = known = (sound, states, radius);
        return known;
    }
}
