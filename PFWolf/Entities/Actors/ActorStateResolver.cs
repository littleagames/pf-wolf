using PFWolf.Assets;

namespace PFWolf.Entities.Actors;

/// <summary>
/// Flattens the DECORATE-style, grouped <see cref="StateData"/> lists parsed from actor
/// definitions into a linked sequence of single-frame <see cref="ActorStateFrame"/> nodes,
/// where each frame's `Next` is its following frame, a named next state, or a loop/stop.
/// </summary>
internal static class ActorStateResolver
{
    /// <param name="ignored">Told of each next-state, goto, stop or loop that's never reached, as
    /// the state's name, its entries and the marker: one on an entry before the last, which the
    /// next entry's frames follow instead</param>
    internal static Dictionary<string, ActorStateFrame> Resolve(Dictionary<string, List<StateData>> states,
        Action<string, List<StateData>, StateData>? ignored = null)
    {
        var firstFrameByGroup = new Dictionary<string, ActorStateFrame>();

        // Where a control marker (Stop/Loop/Goto) falls relative to the frame it follows.
        // Resolved in a second pass once every group's first frame is known, so a marker in
        // one group (e.g. Attack -> Goto "Chase") can reference another group regardless of
        // dictionary iteration order. Implied: the loop a state without a marker at its end gets.
        var pendingMarkers = new List<(ActorStateFrame? AfterFrame, string Group, StateData Marker, bool Implied)>();

        foreach (var (groupName, entries) in states)
        {
            ActorStateFrame? previous = null;

            foreach (var entry in entries)
            {
                if (entry is ActorStatesData frameData)
                {
                    foreach (var letter in frameData.Frames)
                    {
                        var frame = new ActorStateFrame
                        {
                            StateName = groupName,
                            Sprite = frameData.Sprite,
                            FrameLetter = letter,
                            TicTime = ToTicTime(frameData.TicsPerFrame),
                            Modifiers = frameData.Modifiers,
                            Light = frameData.Light,
                            Think = string.IsNullOrEmpty(frameData.Think) ? null : frameData.Think,
                            Action = string.IsNullOrEmpty(frameData.Action) ? null : frameData.Action,
                        };

                        if (previous != null)
                            previous.Next = frame;
                        firstFrameByGroup.TryAdd(groupName, frame);
                        previous = frame;
                    }

                    // Real actordefs content expresses transitions this way: an inline
                    // `next-state:` field on the entry's own last frame, rather than a
                    // trailing Stop/Loop/Goto marker. Same resolution, just a different
                    // authoring shape -- a self-referencing NextState is how content loops.
                    if (!string.IsNullOrEmpty(frameData.NextState) && previous != null)
                        pendingMarkers.Add((previous, groupName, new GoToStateData(frameData.NextState), false));
                }
                else
                {
                    // Only the first marker following a given frame is honored (see below) --
                    // a marker that a later frame in the same list falls through to (e.g. a
                    // conditional action like A_JumpIf) is a runtime decision this step doesn't
                    // support yet, so the plain linear fallthrough wins instead.
                    pendingMarkers.Add((previous, groupName, entry, false));
                }
            }

            // No explicit terminal marker: loop back to this group's own first frame. This only
            // matters for a group whose last frame has a real TicTime (e.g. a flickering light
            // with no next-state) -- a group ending on a -1 frame holds forever regardless of
            // what Next points to, since DoActor never re-consults Next on such a frame. An
            // explicit Stop still means "freeze on this exact frame", distinct from this default.
            if (previous != null && entries.Count > 0 && entries[^1] is ActorStatesData)
                pendingMarkers.Add((previous, groupName, new LoopStateData(), true));
        }

        foreach (var (afterFrame, group, marker, implied) in pendingMarkers)
        {
            if (afterFrame == null || afterFrame.Next != null)
            {
                // nothing to attach to, or a later frame (or an earlier marker) already linked
                // past this point: one the content wrote is then never reached
                if (afterFrame != null && !implied)
                    ignored?.Invoke(group, states[group], marker);
                continue;
            }

            afterFrame.Next = marker switch
            {
                StopStateData => afterFrame,
                LoopStateData => firstFrameByGroup.GetValueOrDefault(group, afterFrame),
                GoToStateData goTo => firstFrameByGroup.GetValueOrDefault(goTo.NextState, afterFrame),
                _ => afterFrame,
            };
        }

        AddRaiseFromDeath(firstFrameByGroup);
        return firstFrameByGroup;
    }

    /// <summary>The warning for a marker Resolve found is never reached</summary>
    internal static string IgnoredMessage(string className, string state, StateData marker) =>
        $"Actor {className}: in its {state} state, " + marker switch
        {
            GoToStateData goTo => $"next-state (or goto) {goTo.NextState}",
            StopStateData => "stop",
            _ => "loop",
        } + " isn't on the state's last entry, so it's never reached: the frames after it play instead";

    /// <summary>The state a corpse brought back by a reviver plays (Monster.Rise)</summary>
    internal const string RaiseState = "Raise";

    // How long a raise shows a frame that held forever dying (the corpse's own), in tics
    private const short RaiseHoldTics = 8;

    /// <summary>
    /// For a class that dies (a Death state) but has no Raise state of its own, a Raise made of
    /// its Death frames backwards, as DOOM's monsters get up: each for as long as it showed
    /// dying, without the death's actions, the corpse's own frame only when it's all there is.
    /// Its last frame runs A_Resume (Monster.Resume: back to chasing or standing), and then
    /// goes to Spawn should that do nothing.
    /// </summary>
    internal static void AddRaiseFromDeath(Dictionary<string, ActorStateFrame> states)
    {
        if (states.ContainsKey(RaiseState) || !states.TryGetValue("Death", out var death))
            return;

        var dying = new List<ActorStateFrame>();
        var seen = new HashSet<ActorStateFrame>();
        for (var frame = death; frame != null && frame.StateName == "Death" && seen.Add(frame); frame = frame.Next)
        {
            dying.Add(frame);
            if (frame.HoldsForever)
                break;
        }
        if (dying.Count > 1 && dying[^1].HoldsForever)
            dying.RemoveAt(dying.Count - 1);        // the corpse is showing it already

        ActorStateFrame? first = null, previous = null;
        for (int i = dying.Count - 1; i >= 0; i--)
        {
            var from = dying[i];
            var frame = new ActorStateFrame
            {
                StateName = RaiseState,
                Sprite = from.Sprite,
                FrameLetter = from.FrameLetter,
                TicTime = from.TicTime > 0 ? from.TicTime : RaiseHoldTics,
                Modifiers = from.Modifiers,
                Light = from.Light,
                Action = i == 0 ? "A_Resume" : null,
            };
            if (previous != null)
                previous.Next = frame;
            first ??= frame;
            previous = frame;
        }

        if (first == null || previous == null)
            return;
        previous.Next = states.GetValueOrDefault("Spawn", previous);
        states[RaiseState] = first;
    }

    private static short ToTicTime(float ticsPerFrame) =>
        ticsPerFrame < 0 ? (short)-1 : (short)MathF.Round(ticsPerFrame);
}
