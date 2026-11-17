using PFWolf.Constants;
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
    = A Prop Hunt prop's disguise (PlayerState.Disguise) is drawn instead: the decoration's own
    = Spawn states, played as on the map whatever the prop does. Once the prop has stood still
    = for SettleTics it's drawn in the middle of its tile, where the map's decorations stand.
    =
    = A player riding a vehicle (PlayerState.Vehicle, Gameplay/Vehicles.cs) is drawn as the
    = vehicle, through its riding states: Ride still, RideSee moving, RideMissile while its
    = weapon fires and RidePain hurt, each where the vehicle's class has it, else Ride, else
    = its parked Spawn look.
    =
    = Who's riding can show: the vehicle's `vehicle.ridersprites` (rider class: sprite) gives a
    = sprite drawn in place of the vehicle's Spawn sprite while that class rides it, the way
    = weapon.bodysprite stands in for a body's (VJGD for VAJP with a Guard at the wheel). A class
    = not listed uses its parent's entry (a mod's elite Guard rides as the Guard). Frame by frame
    = and view by view, only where the rider's sprite has the picture; a single picture (B0) of
    = a vehicle drawn from 8 sides stands in for the front view only (and wherever the vehicle
    = shows its own single picture, having no view for that side).
    =
    ====================
    */

    // A body state, as a vehicle being ridden has it (Spawn → Ride, See → RideSee, ...)
    static string RideState(string state) => state == "Spawn" ? "Ride" : "Ride" + state;

    /// <summary>How long a disguised prop stands still before it's drawn in its tile's middle (a second)</summary>
    internal const int SettleTics = 70;

    /// <summary>Moves and animates every player's body, each frame</summary>
    internal void TickPlayerBodies(uint tics)
    {
        foreach (var p in session.Players)
            TickPlayerBody(p, tics);
    }

    internal void TickPlayerBody(Entities.PlayerState p, uint tics)
    {
        if (p.Pawn is not { } pawn)
            return;

        bool riding = p.Disguise == null && p.Vehicle != null && p.Health > 0;
        var looks = p.Disguise ?? (riding ? p.Vehicle! : p.PlayerClass);
        if (p.BodyClass != looks)
        {
            p.BodyClass = looks;
            p.Body = inventory.CreateActor(looks);
            if (p.Body?.ResolvedStates.ContainsKey("Spawn") != true)
                p.Body = null;
            else
                p.Body.SetState(riding && p.Body.ResolvedStates.ContainsKey("Ride") ? "Ride" : "Spawn");
            p.BodyHoldsWeapon = p.Body != null && SpawnSprite(p.Body) is { } own
                && string.Equals(own, SpawnSprite(inventory.CreateActor(PlayerClassRules.BasePlayerClass)), StringComparison.OrdinalIgnoreCase);
        }

        if (p.Body is not { } body)
            return;

        bool moving = pawn.X != p.BodyLastX || pawn.Y != p.BodyLastY;
        (p.BodyLastX, p.BodyLastY) = (pawn.X, pawn.Y);
        p.StillTics = moving ? 0 : Math.Min(p.StillTics + (int)tics, SettleTics);
        bool attacking = p.WeaponSprite?.CurrentState is { } weaponState && weaponState.StateName != WeaponInHand.WeaponReadyState;
        p.PainTics = Math.Max(p.PainTics - (int)tics, 0);

        string want;
        if (riding)
        {
            want = RideState(p.PainTics > 0 ? "Pain" : attacking ? "Missile" : moving ? "See" : "Spawn");
            if (!body.ResolvedStates.ContainsKey(want))
                want = moving && body.ResolvedStates.ContainsKey("RideSee") ? "RideSee"
                    : body.ResolvedStates.ContainsKey("Ride") ? "Ride" : "Spawn";
        }
        else
        {
            want = p.Disguise != null ? "Spawn"
                : p.Health <= 0 ? "Death"
                : p.PainTics > 0 ? (p.PainAlt && body.ResolvedStates.ContainsKey("Pain1") ? "Pain1" : "Pain")
                : attacking ? "Missile"
                : moving ? "See"
                : "Spawn";
            if (!body.ResolvedStates.ContainsKey(want))
                want = moving && body.ResolvedStates.ContainsKey("See") ? "See" : "Spawn";
        }
        if (!InStateGroup(body, want))
            body.SetState(want);

        bool settled = p.Disguise != null && p.StillTics >= SettleTics;
        const int Half = (int)MapConstants.TileGlobal / 2;
        body.X = settled ? (pawn.TileX << (int)MapConstants.TileShift) + Half : pawn.X;
        body.Y = settled ? (pawn.TileY << (int)MapConstants.TileShift) + Half : pawn.Y;
        body.TileX = pawn.TileX;
        body.TileY = pawn.TileY;
        body.Angle = pawn.Angle;
        body.AreaNumber = pawn.AreaNumber;
        body.Active = ActiveType.Yes;     // drawn by its exact position, as a moving actor
        map.DoActor(body, tics);
    }

    /// <summary>A player's body to draw this frame: anyone's but the one the camera looks through (unless it's behind them)</summary>
    internal Entities.Actors.Actor? VisiblePlayerBody(PlayerPawn pawn) =>
        (session.Camera.Target != pawn || session.Camera.Chasing) && pawn.State.Body is { CurrentState: { Sprite.Length: > 0 } state } body && state.Sprite != "TNT1"
            ? body : null;

    /// <summary>Whether an actor is one of the players' bodies (they turn by their Angle, like a projectile)</summary>
    internal bool IsPlayerBody(Entities.Actors.Actor actor) => BodyOwner(actor) != null;

    // The player whose body the actor is, if any: a loop, as it's asked for every sprite drawn
    private PlayerState? BodyOwner(Entities.Actors.Actor actor)
    {
        foreach (var p in session.Players)
            if (ReferenceEquals(p.Body, actor))
                return p;
        return null;
    }

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

        // Riding: who's at the wheel, where the vehicle has a sprite for their class
        if (BodyOwner(actor) is { Vehicle: { } vehicle, Disguise: null, PlayerClass: { } riderClass } rider
            && string.Equals(rider.BodyClass, vehicle, StringComparison.OrdinalIgnoreCase)
            && RiderSprite(vehicle, riderClass) is { } riderSprite)
            return actor.ResolvedStates.TryGetValue("Spawn", out var parked)
                && string.Equals(state.Sprite, parked.Sprite, StringComparison.OrdinalIgnoreCase)
                && RiderShows(riderSprite, state.Sprite, state.FrameLetter, actor)
                ? riderSprite : state.Sprite;

        // Carrying a flag: its sprite in place of the Spawn one, frame by frame where it has it
        if (BodyOwner(actor) is { CarrySprite: { } carry, BodyHoldsWeapon: true }
            && actor.ResolvedStates.TryGetValue("Spawn", out var bodySpawn)
            && string.Equals(state.Sprite, bodySpawn.Sprite, StringComparison.OrdinalIgnoreCase)
            && SpriteExists(spriteDrawing.ActorShapeName(carry, state.FrameLetter, actor)))
            return carry;

        if (BodyOwner(actor) is not { Weapon: { } weapon, BodyHoldsWeapon: true }
            || !actor.ResolvedStates.TryGetValue("Spawn", out var spawn)
            || !string.Equals(state.Sprite, spawn.Sprite, StringComparison.OrdinalIgnoreCase)
            || inventory.GetStringProperty(weapon, "weapon.bodysprite") is not { Length: > 0 } swap)
            return state.Sprite;

        return SpriteExists(spriteDrawing.ActorShapeName(swap, state.FrameLetter, actor)) ? swap : state.Sprite;
    }

    // vehicle.ridersprites' sprite for a rider's class, or their nearest parent's; looked up once a pair
    readonly Dictionary<(string Vehicle, string Rider), string?> riderSprites = [];

    internal string? RiderSprite(string vehicle, string riderClass)
    {
        if (riderSprites.TryGetValue((vehicle, riderClass), out var cached))
            return cached;
        string? sprite = null;
        if (inventory.GetProperty(vehicle, "vehicle.ridersprites") is IDictionary<object, object> sprites)
        {
            var current = riderClass;
            for (int depth = 0; depth < 32 && current != null && sprite == null; depth++)    // bounded: a parent cycle
            {
                foreach (var (key, value) in sprites)
                    if (string.Equals(key?.ToString(), current, StringComparison.OrdinalIgnoreCase)
                        && value?.ToString() is { Length: > 0 } name)
                        sprite = name.Trim();
                current = inventory.ParentOf(current);
            }
        }
        return riderSprites[(vehicle, riderClass)] = sprite;
    }

    // Whether the rider's sprite has the picture for this frame from where it's seen. A rider's
    // picture is the whole vehicle, so any view of it will do; but its single picture (B0, a
    // front) on a vehicle drawn from 8 sides only stands in from the front, or where the
    // vehicle itself falls back to its own single picture: never a front seen from behind,
    // including a rider drawn for some views (VJGDB1-B3) seen from one it hasn't got.
    bool RiderShows(string riderSprite, string vehicleSprite, string frameLetter, Entities.Actors.Actor actor)
    {
        var rider = spriteDrawing.ActorShapeName(riderSprite, frameLetter, actor);
        if (!SpriteExists(rider))
            return false;
        if (rider[^1] != '0' || !spriteDrawing.HasDirectionalSprites(vehicleSprite, frameLetter))
            return true;
        return spriteDrawing.CalcRotate(actor) == 1 || spriteDrawing.ActorShapeName(vehicleSprite, frameLetter, actor)[^1] == '0';
    }

    internal string? SpawnSprite(Entities.Actors.Actor? actor) =>
        actor != null && actor.ResolvedStates.TryGetValue("Spawn", out var spawn) ? spawn.Sprite : null;

    internal readonly Dictionary<string, bool> SpriteExistsCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether there's a sprite picture of that name (PLPSA1)</summary>
    internal bool SpriteExists(string name)
    {
        if (!SpriteExistsCache.TryGetValue(name, out var exists))
            SpriteExistsCache[name] = exists = assets.Exists<Assets.SpriteAsset>(name);
        return exists;
    }

    internal bool InStateGroup(Entities.Actors.Actor actor, string group) =>
        actor.CurrentState?.StateName?.Equals(group, StringComparison.OrdinalIgnoreCase) == true;
}
