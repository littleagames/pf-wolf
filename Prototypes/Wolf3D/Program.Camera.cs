using Wolf3D.Entities.Actors;

namespace Wolf3D;

internal partial class Program
{
    /*
    ====================
    =
    = Camera
    =
    = Where the view is drawn from. It follows an actor (the player, normally) or sits at a
    = fixed point (the death cam). The renderer reads only the camera, never the player, so it
    = can look through another actor (spectate) while the player plays on. What the player
    = touches and sees doesn't move with it (Program.PlayerSight.cs).
    =
    = The player's own look up/down and eye height are playerpitch and playereyez; the
    = renderer's viewpitch and vieweyez are this frame's, taken from the camera.
    =
    ====================
    */

    internal sealed class Camera
    {
        const double TURNSPEED = 8;     // degrees a tic the view turns toward the way a watched actor goes

        Entities.Actors.Actor? target;  // null: the player, whichever pawn that is now
        bool isfixed;
        int fixedx, fixedy;
        short fixedangle;
        double fixedpitch;
        int fixedeyez = EYEDEFAULT;
        double smoothangle;             // a watched actor's view angle, turning after its 8-way walk

        /// <summary>
        /// The actor looked through, or null when the camera sits at a fixed point (or there's
        /// no player yet). One removed from the map gives the view back to the player; a new
        /// level always does (SetupGameLevel).
        /// </summary>
        internal Entities.Actors.Actor? Target =>
            isfixed ? null : target is { IsRemoved: false } ? target : localplayer.Pawn;

        /// <summary>Looks through the player: the default, set each level</summary>
        internal void FollowPlayer() => (target, isfixed) = (null, false);

        /// <summary>Looks through an actor, from its position and the way it faces</summary>
        internal void Follow(Entities.Actors.Actor actor)
        {
            (target, isfixed) = (actor, false);
            smoothangle = ActorViewAngle(actor);
        }

        /// <summary>Sits at a fixed point (global fixed-point x, y), facing angle</summary>
        internal void SetFixed(int x, int y, short angle, double pitch = 0, int eyez = EYEDEFAULT)
        {
            (target, isfixed) = (null, true);
            (fixedx, fixedy, fixedangle, fixedpitch, fixedeyez) = (x, y, angle, pitch, eyez);
        }

        /// <summary>Whether the view is the player's own, with their weapon and look</summary>
        internal bool OnPlayer => Target is { } t && t == localplayer.Pawn;

        /// <summary>Whether the view is through an actor other than the player</summary>
        internal bool Spectating => Target is { } t && t != localplayer.Pawn;

        /// <summary>Turns a watched actor's view toward the way it now goes, so it doesn't snap 45 degrees at a time</summary>
        internal void Tick(uint tics)
        {
            if (!Spectating)
                return;

            double delta = ActorViewAngle(Target!) - smoothangle;
            delta = ((delta % ANGLES) + ANGLES * 1.5) % ANGLES - ANGLES / 2.0;    // the shorter way round
            double step = TURNSPEED * tics;
            smoothangle += Math.Clamp(delta, -step, step);
            smoothangle = ((smoothangle % ANGLES) + ANGLES) % ANGLES;
        }

        internal int X => Target?.X ?? fixedx;
        internal int Y => Target?.Y ?? fixedy;
        internal int TileX => X >> (int)MapConstants.TILESHIFT;
        internal int TileY => Y >> (int)MapConstants.TILESHIFT;

        internal short Angle => Target switch
        {
            null => fixedangle,
            PlayerPawn => Target.Angle,
            _ => (short)((int)Math.Round(smoothangle) % ANGLES),
        };

        internal double Pitch => Target switch
        {
            null => fixedpitch,
            PlayerPawn pawn => pawn.State.Pitch,
            _ => 0,
        };

        internal int EyeZ => Target switch
        {
            null => fixedeyez,
            PlayerPawn pawn => pawn.State.EyeZ,
            _ => EYEDEFAULT,
        };
    }

    internal static readonly Camera camera = new();

    /// <summary>
    /// The way an actor other than the player looks: a projectile along its heading, anything
    /// else the way it walks, or its Angle when it stands still.
    /// </summary>
    static short ActorViewAngle(Entities.Actors.Actor actor) =>
        actor.Dir == objdirtypes.nodir || actor.Flags.Contains("PROJECTILE", StringComparer.OrdinalIgnoreCase)
            ? actor.Angle
            : (short)dirangle[(byte)actor.Dir];

    internal static void SetEyeHeight(int z) => playereyez = Math.Clamp(z, MINEYE, MAXEYE);

    /*
    ====================
    =
    = Spectating
    =
    ====================
    */

    /// <summary>The actors `spectate` steps through: living enemies, in the order they think</summary>
    static IEnumerable<Entities.Actors.Actor> SpectateCandidates() =>
        _mapManager.GetActors().Where(a => !a.IsRemoved && a is not PlayerPawn
            && a.ResolvedStates.ContainsKey("Chase") && a.Hitpoints > 0);

    private static void Cmd_Spectate(string[] args)
    {
        var mode = args.Length > 0 ? args[0] : "next";

        if (mode.Equals("player", StringComparison.OrdinalIgnoreCase))
        {
            camera.FollowPlayer();
            _consoleManager.Print("Watching through your own eyes");
            return;
        }

        bool back = mode.Equals("prev", StringComparison.OrdinalIgnoreCase);
        bool any = back || mode.Equals("next", StringComparison.OrdinalIgnoreCase);
        var candidates = SpectateCandidates()
            .Where(a => any || a.Name.Equals(mode, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
        {
            _consoleManager.Print(any ? "Nobody to watch" : $"No living {mode} to watch");
            return;
        }

        // The one after (or before) the actor watched now, round the list; the first (or last)
        // from the player's eyes
        int at = camera.Spectating ? candidates.IndexOf(camera.Target!) : -1;
        int next = at < 0
            ? (back ? candidates.Count - 1 : 0)
            : (at + (back ? -1 : 1) + candidates.Count) % candidates.Count;

        var watched = candidates[next];
        camera.Follow(watched);

        var text = $"Watching {ActorTag(watched)}";
        _consoleManager.Print($"{text} ({watched.Name} at {watched.TileX},{watched.TileY}, {next + 1} of {candidates.Count})");
        _hudMessageManager.Show(Managers.HudMessageKind.Other, text);
    }

    /*
    ====================
    =
    = Player body
    =
    = What others see of a player: their class's own actordefs states, if it has any (Wolf3D's
    = Player has the PLAY sprites; a class with no Spawn state is never drawn). Like the weapon
    = in hand it's never placed on the map; it stands where the player is and turns with them:
    = Spawn when still, See walking, Missile while their weapon fires, Pain or Pain1 when hurt
    = and Death once dead, each where the class has it. Only for show: nothing the game does
    = depends on it.
    =
    ====================
    */

    /// <summary>Moves and animates every player's body, each frame</summary>
    internal static void TickPlayerBodies(uint tics)
    {
        foreach (var p in players)
            TickPlayerBody(p, tics);
    }

    static void TickPlayerBody(Entities.PlayerState p, uint tics)
    {
        if (p.Pawn is not { } pawn)
            return;

        if (p.BodyClass != p.playerclass)
        {
            p.BodyClass = p.playerclass;
            p.Body = _inventoryManager.CreateActor(p.playerclass);
            if (p.Body?.ResolvedStates.ContainsKey("Spawn") != true)
                p.Body = null;
            else
                p.Body.SetState("Spawn");
        }

        if (p.Body is not { } body)
            return;

        bool moving = pawn.X != p.BodyLastX || pawn.Y != p.BodyLastY;
        (p.BodyLastX, p.BodyLastY) = (pawn.X, pawn.Y);
        bool attacking = p.WeaponSprite?.CurrentState is { } weaponState && weaponState.StateName != WeaponReadyState;
        p.PainTics = Math.Max(p.PainTics - (int)tics, 0);

        var want = p.health <= 0 ? "Death"
            : p.PainTics > 0 ? (p.PainAlt && body.ResolvedStates.ContainsKey("Pain1") ? "Pain1" : "Pain")
            : attacking ? "Missile"
            : moving ? "See"
            : "Spawn";
        if (!body.ResolvedStates.ContainsKey(want))
            want = moving && body.ResolvedStates.ContainsKey("See") ? "See" : "Spawn";
        if (!InStateGroup(body, want))
            body.SetState(want);

        body.X = pawn.X;
        body.Y = pawn.Y;
        body.TileX = pawn.TileX;
        body.TileY = pawn.TileY;
        body.Angle = pawn.Angle;
        body.AreaNumber = pawn.AreaNumber;
        body.Active = activetypes.ac_yes;     // drawn by its exact position, as a moving actor
        _mapManager.DoActor(body, tics);
    }

    /// <summary>A player's body to draw this frame: anyone's but the one the camera looks through</summary>
    static Entities.Actors.Actor? VisiblePlayerBody(PlayerPawn pawn) =>
        camera.Target != pawn && pawn.State.Body is { CurrentState: { Sprite.Length: > 0 } state } body && state.Sprite != "TNT1"
            ? body : null;

    /// <summary>Whether an actor is one of the players' bodies (they turn by their Angle, like a projectile)</summary>
    static bool IsPlayerBody(Entities.Actors.Actor actor) => players.Any(p => ReferenceEquals(p.Body, actor));

    static bool InStateGroup(Entities.Actors.Actor actor, string group) =>
        actor.CurrentState?.StateName?.Equals(group, StringComparison.OrdinalIgnoreCase) == true;
}
