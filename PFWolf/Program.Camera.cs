using PFWolf.Entities.Actors;

namespace PFWolf;

internal partial class Program
{
    // Where the view is drawn from (Gameplay/Camera.cs)
    internal static Camera camera => session.camera;

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
    = Player has the PLMP sprites; a class with no Spawn state is never drawn). Like the weapon
    = in hand it's never placed on the map; it stands where the player is and turns with them:
    = Spawn when still, See walking, Missile while their weapon fires, Pain or Pain1 when hurt
    = and Death once dead, each where the class has it. Only for show: nothing the game does
    = depends on it.
    =
    = The weapon in hand can change how they look: its weapon.bodysprite (weapons.yaml) is drawn
    = in place of the body's Spawn sprite wherever a frame uses it (PLKN for PLMP with the knife),
    = so one set of states serves every weapon. Frames using any other sprite (deaths) are kept,
    = as is any frame the weapon's sprite doesn't have.
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
            p.BodyHoldsWeapon = p.Body != null && SpawnSprite(p.Body) is { } own
                && string.Equals(own, SpawnSprite(_inventoryManager.CreateActor(BasePlayerClass)), StringComparison.OrdinalIgnoreCase);
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

    /// <summary>
    /// The sprite to draw for an actor's frame: its own, except a player's body, which shows their
    /// weapon's weapon.bodysprite in place of its Spawn sprite (when that sprite has the picture
    /// for the frame from this side: a half-drawn set falls back view by view). Only a body drawn
    /// with Player's own Spawn sprite: a class with its own look (a Guard) keeps it whatever it
    /// holds. After TransformActor.
    /// </summary>
    internal static string DrawnSprite(Entities.Actors.Actor actor)
    {
        var state = actor.CurrentState!;
        if (players.FirstOrDefault(p => ReferenceEquals(p.Body, actor)) is not { weapon: { } weapon, BodyHoldsWeapon: true }
            || !actor.ResolvedStates.TryGetValue("Spawn", out var spawn)
            || !string.Equals(state.Sprite, spawn.Sprite, StringComparison.OrdinalIgnoreCase)
            || _inventoryManager.GetStringProperty(weapon, "weapon.bodysprite") is not { Length: > 0 } swap)
            return state.Sprite;

        return SpriteExists(ActorShapeName(swap, state.FrameLetter, actor)) ? swap : state.Sprite;
    }

    static string? SpawnSprite(Entities.Actors.Actor? actor) =>
        actor != null && actor.ResolvedStates.TryGetValue("Spawn", out var spawn) ? spawn.Sprite : null;

    static readonly Dictionary<string, bool> _spriteExistsCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether there's a sprite picture of that name (PLPSA1)</summary>
    static bool SpriteExists(string name)
    {
        if (!_spriteExistsCache.TryGetValue(name, out var exists))
            _spriteExistsCache[name] = exists = _assetManager.Exists<Assets.SpriteAsset>(name);
        return exists;
    }

    static bool InStateGroup(Entities.Actors.Actor actor, string group) =>
        actor.CurrentState?.StateName?.Equals(group, StringComparison.OrdinalIgnoreCase) == true;
}
