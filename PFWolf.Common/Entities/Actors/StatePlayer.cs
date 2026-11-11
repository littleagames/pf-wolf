namespace PFWolf.Entities.Actors;

/// <summary>What happened as a <see cref="StatePlayer"/> stepped: a frame's action ran as it ended</summary>
/// <param name="Frame">The frame that ended</param>
/// <param name="Action">Its action, as written (A_FireProjectile("Rocket"))</param>
/// <param name="MayJump">Whether the action can send the actor somewhere other than the frame's next</param>
public sealed record StateEvent(ActorStateFrame Frame, string Action, bool MayJump);

/// <summary>
/// Plays an actor's frames without an actor: counts the tics down and follows each frame's next,
/// as the game's DoActor and Actor.AdvanceFrames do, ending 0-tic frames the moment they're entered
/// and holding on -1. Thinks and actions aren't run; each action is told as its frame ends. For the
/// editor's preview, so it plays a class the way the game does.
/// </summary>
public sealed class StatePlayer
{
    // As Actor.MaxInstantFrames: a loop of 0-tic frames is frozen on rather than spun on forever
    private const int MaxInstantFrames = 1000;

    /// <summary>
    /// Actions that can send the actor somewhere other than the frame's next: jumps, A_ReFire,
    /// and those that change state themselves. Their preview stops there, as it can't know.
    /// </summary>
    public static bool MayJump(string action)
    {
        var name = action.Split('(')[0].Trim();
        return name.StartsWith("A_Jump", StringComparison.Ordinal) || name is "A_ReFire" or "A_Relaunch" or "A_Dormant" or "A_Resume";
    }

    /// <summary>The frame showing; null before Start</summary>
    public ActorStateFrame? Frame { get; private set; }

    /// <summary>Tics left on the frame (the game's TicCount)</summary>
    public int TicsLeft { get; private set; }

    /// <summary>Tics played since Start</summary>
    public long Tic { get; private set; }

    /// <summary>Stopped on a 0-tic loop, or at an action that may jump (when told to stop there)</summary>
    public bool Stuck { get; private set; }

    /// <summary>Stop at an action that may jump (the preview asks where to go), rather than going on to the frame's next</summary>
    public bool StopAtJumps { get; set; }

    /// <summary>
    /// Goes to a frame as the game's SetState does: its tics armed, and a 0-tic frame ended at
    /// once (its action told, on to the next)
    /// </summary>
    public List<StateEvent> Start(ActorStateFrame frame)
    {
        Frame = frame;
        TicsLeft = Math.Max((int)frame.TicTime, 0);
        Tic = 0;
        Stuck = false;
        var events = new List<StateEvent>();
        if (frame.TicTime == 0)
            Advance(events);
        return events;
    }

    /// <summary>Plays a number of tics, as DoActor does each tic; the actions that ran, in order</summary>
    public List<StateEvent> Step(int tics = 1)
    {
        var events = new List<StateEvent>();
        for (int i = 0; i < tics && Frame != null && !Stuck; i++)
        {
            Tic++;
            // A frame that holds forever only thinks; so does one armed with no countdown
            if (Frame.HoldsForever || TicsLeft == 0 && Frame.TicTime > 0)
                continue;
            TicsLeft--;
            Advance(events);
        }
        return events;
    }

    /// <summary>Puts the player on a frame with its whole count to go, running nothing (a frame picked to look at)</summary>
    public void Show(ActorStateFrame frame)
    {
        Frame = frame;
        TicsLeft = Math.Max((int)frame.TicTime, 0);
        Stuck = false;
    }

    /// <summary>Goes on from a frame ended at an action that may jump, as if it went to <paramref name="next"/></summary>
    public List<StateEvent> Resume(ActorStateFrame next)
    {
        Stuck = false;
        Frame = next;
        TicsLeft = next.HoldsForever ? 0 : next.TicTime;
        var events = new List<StateEvent>();
        if (TicsLeft <= 0 && !next.HoldsForever)
            Advance(events);
        return events;
    }

    // Actor.AdvanceFrames: every frame whose count has run out ends, its action told
    private void Advance(List<StateEvent> events)
    {
        for (int steps = 0; TicsLeft <= 0; steps++)
        {
            var frame = Frame;
            if (frame == null || frame.HoldsForever)
                return;
            if (steps == MaxInstantFrames)
            {
                Stuck = true;
                return;
            }

            if (!string.IsNullOrWhiteSpace(frame.Action))
            {
                bool jumps = MayJump(frame.Action);
                events.Add(new StateEvent(frame, frame.Action, jumps));
                if (jumps && StopAtJumps)
                {
                    Stuck = true;
                    return;
                }
            }

            if (frame.Next is not { } next)
                return;
            Frame = next;
            if (next.HoldsForever)
            {
                TicsLeft = 0;
                return;
            }
            TicsLeft += next.TicTime;
        }
    }
}
