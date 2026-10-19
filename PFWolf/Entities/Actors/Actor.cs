using System.Numerics;
using PFWolf;
using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Entities.Actors;

internal record Actor : Thinker
{
    public Dictionary<string, List<StateData>> States { get; internal set; } = [];
    public Dictionary<string, ActorStateFrame> ResolvedStates { get; internal set; } = [];
    public ActorStateFrame? CurrentState { get; internal set; }
    public int Radius { get; internal set; }
    public HashSet<string> Flags { get; internal set; } = [];
    public string? Parent { get; internal set; } = null;
    public Dictionary<string, object> Properties { get; internal set; } = [];
    public required string Name { get; internal set; }

    /// <summary>What the actor uses of the game, given as it joins the level (MapManager)</summary>
    internal ActorWorld World { get; set; } = null!;
    public Vector2 Position { get; private set; } = Vector2.Zero;

    // Think/state-transition bookkeeping, driven by MapManager.DoActor.
    public ActiveType Active { get; internal set; } = ActiveType.No;
    public short TicCount { get; internal set; }
    public ObjDir Dir { get; internal set; } = ObjDir.NoDir;
    public short Angle { get; internal set; }
    public short Hitpoints { get; internal set; }
    public int Speed { get; internal set; }
    public int Distance { get; internal set; }
    public short Temp1 { get; internal set; }
    public short Temp2 { get; internal set; }
    public short Temp3 { get; internal set; }
    public bool Hidden { get; internal set; }

    /// <summary>
    /// How much higher than its art puts it the actor's drawn, in texels (64 a story; below it,
    /// negative): a thrown grenade's arc (Gameplay/Missiles.cs). It's only how it looks: the
    /// actor's still on the floor for everything else.
    /// </summary>
    public double Z { get; internal set; }

    /// <summary>How fast Z is rising (falling, negative), in texels a tic</summary>
    public double ZSpeed { get; internal set; }

    public byte AreaNumber { get; internal set; }

    // The tag on the tile it spawned on (MapManager.TagPlane), 0 for none; a switch with the
    // same tag acts on it wherever it has since moved.
    public ushort Tag { get; internal set; }

    // The side it's on (factions.yaml, Managers/FactionManager.cs): its class's `faction`, a
    // mapdefs thing's `faction:`, or the pack's player-faction/monster-faction; null for none
    // (decorations, pickups). Set as it spawns; kept in saved games.
    public string? Faction { get; internal set; }

    // The sprite color translation it's drawn through of its own (translations.yaml,
    // Managers/TranslationManager.cs): a mapdefs thing's `translation:`, its class's
    // `translation` property, or a script's or the console's; null to take its faction's (an
    // enemy) or its player's color. Kept in saved games.
    public string? Translation { get; internal set; }

    // From the floor code it spawned on (mapdefs floors actor-codes, Planet Strike's): what it
    // drops when it dies besides its class's drops, the tile (x << 8 | y, 0 for none) its death
    // works as a smart switch (A_SmartSwitch), and whether it's cloaked (drawn shimmering)
    public List<string> CarriedDrops { get; internal set; } = [];
    public ushort DeathLink { get; internal set; }
    public bool Cloaked { get; internal set; }

    // Hit since it was last drawn: a cloaked actor shows for that frame. Not saved.
    public bool CloakHit { get; internal set; }

    // Set by MapManager.MarkForRemoval (e.g. a projectile that hit something); MapManager.DoActors
    // unlinks the actor once its tic finishes, since removing it mid-walk would break the iteration.
    public bool IsRemoved { get; internal set; }

    // For a projectile: the actor that fired it (Monster.ThrowProjectile), so a
    // death by it is put down to the shooter (Died's obituary, kill credit, being turned on).
    public Actor? Shooter { get; internal set; }

    // Its light (actordefs `light.*` properties and state `light:`, Rendering/ActorLights.cs):
    // set the first time it's looked at, and where its light's effect is. Not saved.
    internal ActorLightState? LightState { get; set; }

    // Sub-tile fixed-point world position and its containing tile -- kept as separate mutable
    // fields because the movement code (MoveObj/TryWalk) updates TileX/TileY the instant a move
    // toward a new tile begins, while X/Y trail behind and approach the new tile center gradually.
    public int X { get; internal set; }
    public int Y { get; internal set; }
    public byte TileX { get; internal set; }
    public byte TileY { get; internal set; }

    // Where it was before the last batch of tics, and which batch that was, for drawing it between
    // tics (Rendering/FrameInterpolation.cs). Only the drawing reads them; not saved.
    internal int PrevX, PrevY, PrevBatch;

    // Runtime combat/AI bookkeeping (Shootable, Ambush, AttackMode, etc. --
    // GameTypes.cs's ObjFlags), distinct from the static, YAML-declared `Flags` above.
    public ObjFlags RuntimeFlags { get; internal set; }

    // Screen-space hit-testing data, recomputed every frame in Rendering/SpriteDrawing.cs's
    // DrawScaleds (set there by TransformActor) so Gameplay/PlayerAttacks.cs's
    // GunAttack/KnifeAttack can find the closest shootable actor under the crosshair.
    public short ViewX { get; internal set; }
    public int TransX { get; internal set; } = int.MaxValue;
    public ushort ViewHeight { get; internal set; }

    internal void SetPosition(int tilex, int tiley)
    {
        Position = new Vector2(tilex, tiley);
        TileX = (byte)tilex;
        TileY = (byte)tiley;
        X = (int)((tilex << MapConstants.TileShift) + MapConstants.TileGlobal / 2);
        Y = (int)((tiley << MapConstants.TileShift) + MapConstants.TileGlobal / 2);
    }

    // Keeps the tile-snapped Position (used by the static-object render/visibility path)
    // in sync whenever AI movement code updates the fixed-point X/Y or TileX/TileY directly.
    internal void SyncPosition()
    {
        Position = new Vector2(TileX, TileY);
    }

    /// <summary>Whether its actordefs `flags:` include <paramref name="flag"/> (any case)</summary>
    internal bool HasFlag(string flag)
    {
        // A loop over the set itself: asked of every actor every tic, and LINQ's Contains with
        // a comparer would box an enumerator each time
        foreach (var f in Flags)
            if (f.Equals(flag, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>A property as a list of strings: one value, or each in a list; empty when it isn't set</summary>
    internal List<string> PropertyStrings(string key) =>
        !Properties.TryGetValue(key, out var value) || value == null ? []
        : value is string one ? [one]
        : value is IEnumerable<object> many ? many.Select(v => v?.ToString() ?? "").Where(v => v.Length > 0).ToList()
        : [value.ToString() ?? ""];

    /// <summary>A property as a list of whole numbers (one value or a list); those that aren't numbers are left out</summary>
    internal List<int> PropertyInts(string key) =>
        PropertyStrings(key).Select(s => int.TryParse(s, out var n) ? (int?)n : null).OfType<int>().ToList();

    internal int PropertyInt(string key, int fallback) =>
        Properties.TryGetValue(key, out var value) && int.TryParse(value?.ToString(), out var n) ? n : fallback;

    internal bool PropertyBool(string key) =>
        Properties.TryGetValue(key, out var value) && (value is true || bool.TryParse(value?.ToString(), out var b) && b);

    // A chain of 0-tic frames that loops back on itself would otherwise spin forever inside
    // one tic; ZDoom treats that as a content error, and so do we.
    private const int MaxInstantFrames = 1000;

    // Set while AdvanceFrames runs, so an action that calls SetState doesn't start a second,
    // nested walk -- the outer loop carries on from whatever state the action picked.
    private bool _advancingFrames;

    // A frame an action asked to go to next (JumpTo), taken in place of the ended frame's Next.
    private ActorStateFrame? _pendingJump;

    /// <summary>
    /// Puts the actor on <paramref name="frame"/> and arms its countdown, without running
    /// anything. A 0-tic frame armed this way is ended by DoActor on the actor's next tic;
    /// used when spawning, where no actions should run yet.
    /// </summary>
    internal void ArmState(ActorStateFrame frame)
    {
        CurrentState = frame;
        TicCount = Math.Max(frame.TicTime, (short)0);
    }

    /// <summary>
    /// Enters <paramref name="frame"/> (legacy NewState). If it's a 0-tic frame, it ends right
    /// away: its Action runs and the actor moves on, through any further 0-tic frames.
    /// </summary>
    internal void SetState(ActorStateFrame frame)
    {
        ArmState(frame);
        if (frame.TicTime == 0 && !_advancingFrames)
            AdvanceFrames();
    }

    /// <summary>Enters the named state (as <see cref="SetState(ActorStateFrame)"/>); nothing happens when it has none by that name.</summary>
    internal void SetState(string stateName)
    {
        if (ResolvedStates.TryGetValue(stateName, out var state))
            SetState(state);
    }

    /// <summary>
    /// ZDoom-style state jump (A_WeaponReady, A_ReFire): the actor goes to exactly
    /// <paramref name="frame"/>. From inside an action it replaces the ended frame's Next, unlike
    /// SetState there, which (as in the original DoActor) carries on from the new state's Next.
    /// </summary>
    internal void JumpTo(ActorStateFrame frame)
    {
        if (_advancingFrames)
            _pendingJump = frame;
        else
            SetState(frame);
    }

    /// <summary>
    /// Ends every frame whose countdown has run out (TicCount &lt;= 0), running each one's Action
    /// and following Next, until the actor is on a frame with tics left or one that holds
    /// forever. 0-tic frames end the moment they're entered.
    /// </summary>
    internal void AdvanceFrames()
    {
        _advancingFrames = true;
        try
        {
            for (var steps = 0; TicCount <= 0; steps++)
            {
                var state = CurrentState;
                if (state == null || state.HoldsForever)
                    return;

                if (steps == MaxInstantFrames)
                {
                    Console.WriteLine($"Actor '{Name}': 0-tic frames loop forever from state '{state.StateName}'; freezing it");
                    TicCount = short.MaxValue;
                    return;
                }

                _pendingJump = null;
                ActorActionRegistry.Invoke(state.Action, this);
                if (IsRemoved)
                    return;

                // An action may switch state itself (the Angel's A_Relaunch, a Spectre's A_Dormant);
                // like the original DoActor, carry on from the state it left rather than the old one.
                // A JumpTo lands on its frame instead.
                var next = _pendingJump ?? (CurrentState ?? state).Next;
                _pendingJump = null;
                if (next == null)
                    return; // the resolver never leaves Next null in practice; defensive only.
                CurrentState = next;

                if (next.HoldsForever)
                {
                    TicCount = 0;
                    return;
                }
                TicCount += next.TicTime;
            }
        }
        finally
        {
            _advancingFrames = false;
        }
    }

    /// <summary>
    /// Walks a named state's resolved frame chain once, firing each frame's Action along the
    /// way -- for event-triggered states like "Pickup" that aren't ticked by DoActor, rather
    /// than looped/held like "Spawn". Stops as soon as a frame repeats (self-loop/terminal).
    /// </summary>
    internal void RunState(string stateName)
    {
        if (!ResolvedStates.TryGetValue(stateName, out var frame))
            return;

        var visited = new HashSet<ActorStateFrame>();
        while (frame != null && visited.Add(frame))
        {
            ActorActionRegistry.Invoke(frame.Action, this);
            frame = frame.Next;
        }
    }
}
