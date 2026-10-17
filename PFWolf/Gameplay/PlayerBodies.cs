using PFWolf.Entities.Actors;

namespace PFWolf.Gameplay;

/// <summary>
/// What others see of each player: their class's body, moved and animated with them each
/// frame (TickPlayerBodies), which bodies to draw (VisiblePlayerBody, IsPlayerBody) and the
/// sprite for a body's frame with the weapon held (DrawnSprite).
/// </summary>
internal sealed class PlayerBodies(
    AssetManager assets,
    MapManager map,
    InventoryManager inventory,
    SpriteDrawing spriteDrawing,
    GameSession session)
{
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
    internal void TickPlayerBodies(uint tics)
    {
        foreach (var p in session.players)
            TickPlayerBody(p, tics);
    }

    internal void TickPlayerBody(Entities.PlayerState p, uint tics)
    {
        if (p.Pawn is not { } pawn)
            return;

        if (p.BodyClass != p.playerclass)
        {
            p.BodyClass = p.playerclass;
            p.Body = inventory.CreateActor(p.playerclass);
            if (p.Body?.ResolvedStates.ContainsKey("Spawn") != true)
                p.Body = null;
            else
                p.Body.SetState("Spawn");
            p.BodyHoldsWeapon = p.Body != null && SpawnSprite(p.Body) is { } own
                && string.Equals(own, SpawnSprite(inventory.CreateActor(PlayerClassRules.BasePlayerClass)), StringComparison.OrdinalIgnoreCase);
        }

        if (p.Body is not { } body)
            return;

        bool moving = pawn.X != p.BodyLastX || pawn.Y != p.BodyLastY;
        (p.BodyLastX, p.BodyLastY) = (pawn.X, pawn.Y);
        bool attacking = p.WeaponSprite?.CurrentState is { } weaponState && weaponState.StateName != WeaponInHand.WeaponReadyState;
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
        body.Active = ActiveType.Yes;     // drawn by its exact position, as a moving actor
        map.DoActor(body, tics);
    }

    /// <summary>A player's body to draw this frame: anyone's but the one the camera looks through</summary>
    internal Entities.Actors.Actor? VisiblePlayerBody(PlayerPawn pawn) =>
        session.camera.Target != pawn && pawn.State.Body is { CurrentState: { Sprite.Length: > 0 } state } body && state.Sprite != "TNT1"
            ? body : null;

    /// <summary>Whether an actor is one of the players' bodies (they turn by their Angle, like a projectile)</summary>
    internal bool IsPlayerBody(Entities.Actors.Actor actor) => session.players.Any(p => ReferenceEquals(p.Body, actor));

    /// <summary>
    /// The sprite to draw for an actor's frame: its own, except a player's body, which shows their
    /// weapon's weapon.bodysprite in place of its Spawn sprite (when that sprite has the picture
    /// for the frame from this side: a half-drawn set falls back view by view). Only a body drawn
    /// with Player's own Spawn sprite: a class with its own look (a Guard) keeps it whatever it
    /// holds. After TransformActor.
    /// </summary>
    internal string DrawnSprite(Entities.Actors.Actor actor)
    {
        var state = actor.CurrentState!;

        // Carrying a flag: its sprite in place of the Spawn one, frame by frame where it has it
        if (session.players.FirstOrDefault(p => ReferenceEquals(p.Body, actor)) is { CarrySprite: { } carry, BodyHoldsWeapon: true }
            && actor.ResolvedStates.TryGetValue("Spawn", out var bodySpawn)
            && string.Equals(state.Sprite, bodySpawn.Sprite, StringComparison.OrdinalIgnoreCase)
            && SpriteExists(spriteDrawing.ActorShapeName(carry, state.FrameLetter, actor)))
            return carry;

        if (session.players.FirstOrDefault(p => ReferenceEquals(p.Body, actor)) is not { weapon: { } weapon, BodyHoldsWeapon: true }
            || !actor.ResolvedStates.TryGetValue("Spawn", out var spawn)
            || !string.Equals(state.Sprite, spawn.Sprite, StringComparison.OrdinalIgnoreCase)
            || inventory.GetStringProperty(weapon, "weapon.bodysprite") is not { Length: > 0 } swap)
            return state.Sprite;

        return SpriteExists(spriteDrawing.ActorShapeName(swap, state.FrameLetter, actor)) ? swap : state.Sprite;
    }

    internal string? SpawnSprite(Entities.Actors.Actor? actor) =>
        actor != null && actor.ResolvedStates.TryGetValue("Spawn", out var spawn) ? spawn.Sprite : null;

    internal readonly Dictionary<string, bool> _spriteExistsCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether there's a sprite picture of that name (PLPSA1)</summary>
    internal bool SpriteExists(string name)
    {
        if (!_spriteExistsCache.TryGetValue(name, out var exists))
            _spriteExistsCache[name] = exists = assets.Exists<Assets.SpriteAsset>(name);
        return exists;
    }

    internal bool InStateGroup(Entities.Actors.Actor actor, string group) =>
        actor.CurrentState?.StateName?.Equals(group, StringComparison.OrdinalIgnoreCase) == true;
}
