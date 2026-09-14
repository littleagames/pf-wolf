using Wolf3D.Assets;

namespace Wolf3D.Entities.Actors;

/// <summary>
/// Flattens the DECORATE-style, grouped <see cref="StateData"/> lists parsed from actor
/// definitions into a linked sequence of single-frame <see cref="ActorStateFrame"/> nodes,
/// mirroring how the legacy per-frame `statestruct` chain resolved a `next` state name.
/// </summary>
internal static class ActorStateResolver
{
    internal static Dictionary<string, ActorStateFrame> Resolve(Dictionary<string, List<StateData>> states)
    {
        var firstFrameByGroup = new Dictionary<string, ActorStateFrame>();

        // Where a control marker (Stop/Loop/Goto) falls relative to the frame it follows.
        // Resolved in a second pass once every group's first frame is known, so a marker in
        // one group (e.g. Attack -> Goto "Chase") can reference another group regardless of
        // dictionary iteration order.
        var pendingMarkers = new List<(ActorStateFrame? AfterFrame, string Group, StateData Marker)>();

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
                        pendingMarkers.Add((previous, groupName, new GoToStateData(frameData.NextState)));
                }
                else
                {
                    // Only the first marker following a given frame is honored (see below) --
                    // a marker that a later frame in the same list falls through to (e.g. a
                    // conditional action like A_JumpIf) is a runtime decision this step doesn't
                    // support yet, so the plain linear fallthrough wins instead.
                    pendingMarkers.Add((previous, groupName, entry));
                }
            }

            // No explicit terminal marker: hold on the last frame forever, same as Stop.
            if (previous != null && entries.Count > 0 && entries[^1] is ActorStatesData)
                pendingMarkers.Add((previous, groupName, new StopStateData()));
        }

        foreach (var (afterFrame, group, marker) in pendingMarkers)
        {
            if (afterFrame == null || afterFrame.Next != null)
                continue; // nothing to attach to, or a later frame already linked past this point

            afterFrame.Next = marker switch
            {
                StopStateData => afterFrame,
                LoopStateData => firstFrameByGroup.GetValueOrDefault(group, afterFrame),
                GoToStateData goTo => firstFrameByGroup.GetValueOrDefault(goTo.NextState, afterFrame),
                _ => afterFrame,
            };
        }

        return firstFrameByGroup;
    }

    private static short ToTicTime(float ticsPerFrame) =>
        ticsPerFrame < 0 ? (short)0 : (short)MathF.Round(ticsPerFrame);
}
