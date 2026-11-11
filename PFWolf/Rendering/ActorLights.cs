using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Rendering;

/// <summary>
/// Actors can give off light (ActorLightInfo: actordefs `light.*` properties, and a state's
/// `light:` overriding them; ActorLightRules.Current). Each frame every lit actor's light is
/// cast into the light grid's cells around it: how it spreads, falls off, is dithered, tinted
/// and stopped by walls and shut doors, and how a set light (light.mode: set) takes the cells
/// over, is PFWolf.Common's LightCaster, shared with the editor. This says which actors light
/// (those on the level, the players' bodies, the weapon in hand at the player's spot), what
/// the level lets light through (ILightMap: this class), and when. Light can't make anything
/// brighter than full, so actor lights only show where the level is darker than that, and
/// aren't cast at all on a level with no lighting.Shading; a set light turns shading on for a
/// level that has none (lighting.ActorShading), so a bulb can darken a room at full light as it
/// goes off (its dark frames' `light:` a lower intensity).
///
/// A_SetLight and A_LightOff change an actor's light as it plays (LightOverride, saved).
/// </summary>
internal sealed class ActorLights(MapManager map, Doors doors, Lighting lighting, GameSession session) : ILightMap
{
    // The weapon in hand (the acting player's), and the player it's with
    Entities.Actors.Actor? WeaponSprite => session.PlayerState.WeaponSprite;
    readonly Dictionary<string, ActorLightInfo?> lightclasses = new(StringComparer.OrdinalIgnoreCase);

    // Each light color's tints (indexes in tints), from a quarter of the color to all of it;
    // forgotten with the light rows (ResetLightRows)
    internal readonly Dictionary<string, byte[]> ColorTintTables = new(StringComparer.OrdinalIgnoreCase);

    // Casts the lights into the light grid's cells (PFWolf.Common's LightCaster, shared with the editor)
    readonly LightCaster caster = new(lighting.Grid);
    internal bool HasLights;                              // some cell has light added this frame

    /// <summary>An actor's light state, with its class's light looked up the first time it's asked for.</summary>
    internal ActorLightState LightStateOf(Entities.Actors.Actor actor)
    {
        var state = actor.LightState ??= new ActorLightState();
        if (!state.Resolved)
        {
            if (!lightclasses.TryGetValue(actor.Name, out var info))
                lightclasses[actor.Name] = info = ActorLightInfo.From(actor.Name, actor.Properties, actor.ResolvedStates.Values);
            state.Info = info;
            state.Resolved = true;
        }
        return state;
    }

    /// <summary>
    /// The light an actor gives off now (intensity 0-255, radius in tiles, color or null): its
    /// class's light or what A_SetLight set, changed by its state's `light:` and its effect.
    /// A_LightOff, or a state's `light: none`, puts it out.
    /// </summary>
    internal (int Intensity, double Radius, string? Color) CurrentLight(Entities.Actors.Actor actor)
    {
        var state = LightStateOf(actor);
        return ActorLightRules.Current(state.Info, state.Override, actor.CurrentState?.Light, state.Phase, state.FlickerAmount);
    }

    /// <summary>A light color's tints, a quarter of it to all of it; null for white (no color, or not #RRGGBB).</summary>
    internal byte[]? ColorTints(string? color) => LightCaster.ColorTints(color, ColorTintTables, lighting.TintIndex);

    /*
    =============================================================================

                                    ACTIONS

    For actor states (on the actor itself) and switches (on the actors with the switch's tag):

      A_SetLight(intensity, [radius], [color])  gives the actor this light (0-255, radius in
                                                tiles, #RRGGBB or "none"); radius or color left
                                                out (or "") keep the actor's. With no arguments,
                                                puts back its class's light
      A_LightOff                                puts its light out, whatever its states say,
                                                until A_SetLight

    =============================================================================
    */

    internal void RegisterActorLightActions()
    {
        ActorActionRegistry.Register("A_SetLight", (Entities.Actors.Actor actor, string[] args) => SetLightAction(actor, args));
        ActorActionRegistry.Register("A_LightOff", (Entities.Actors.Actor actor, string[] _) => LightOff(actor));
        Entities.MapTriggerRegistry.Register("A_SetLight", (trigger, args) =>
            ForTaggedActors(trigger, actor => SetLightAction(actor, args)));
        Entities.MapTriggerRegistry.Register("A_LightOff", (trigger, _) =>
            ForTaggedActors(trigger, actor => { LightOff(actor); return true; }));
    }

    private bool ForTaggedActors(Entities.TriggerActivation trigger, Func<Entities.Actors.Actor, bool> act)
    {
        bool any = false;
        foreach (var actor in map.TaggedActors(trigger.Tag).ToList())
            any |= act(actor);
        return any;
    }

    // The light A_SetLight starts from: what it set before, else the class's
    (int Intensity, double Radius, string? Color) BaseLight(ActorLightState state) =>
        state.Override is { } set ? (set.Intensity, set.Radius, set.Color)
        : state.Info is { } info ? (info.Intensity, info.Radius, info.Color)
        : (0, ActorLightInfo.DefaultRadius, null);

    internal bool SetLightAction(Entities.Actors.Actor actor, string[] args)
    {
        var state = LightStateOf(actor);
        if (args.Length == 0)
        {
            state.Override = null;      // back to its class's light
            return true;
        }

        if (!int.TryParse(args[0], out var intensity) || intensity is < 0 or > 255)
        {
            Console.WriteLine($"A_SetLight: '{args[0]}' isn't an intensity (0-255).");
            return false;
        }

        var (_, radius, color) = BaseLight(state);
        var radiusArg = args.ElementAtOrDefault(1);
        if (!string.IsNullOrEmpty(radiusArg))
        {
            if (!double.TryParse(radiusArg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out radius)
                || radius < 0)
            {
                Console.WriteLine($"A_SetLight: '{radiusArg}' isn't a radius in tiles.");
                return false;
            }
        }

        switch (args.ElementAtOrDefault(2))
        {
            case null or "":
                break;
            case var none when none.Equals("none", StringComparison.OrdinalIgnoreCase):
                color = null;
                break;
            case var rgb when Lighting.IsRgb(rgb):
                color = rgb;
                break;
            case var bad:
                Console.WriteLine($"A_SetLight: '{bad}' isn't a #RRGGBB color.");
                return false;
        }

        state.Override = new LightOverride(false, intensity, radius, color);
        return true;
    }

    internal void LightOff(Entities.Actors.Actor actor)
    {
        var state = LightStateOf(actor);
        var (intensity, radius, color) = BaseLight(state);
        state.Override = new LightOverride(true, intensity, radius, color);
    }

    /// <summary>The console's `lights`: each actor giving off light, and what light.</summary>
    internal IEnumerable<string> DescribeActorLights()
    {
        foreach (var actor in LitActors())
        {
            var (intensity, radius, color) = CurrentLight(actor);
            var state = LightStateOf(actor);
            var where = ReferenceEquals(actor, WeaponSprite) ? "in hand" : $"at {actor.TileX},{actor.TileY}";
            bool lit = state.Info?.Sets == true ? radius > 0 : intensity > 0;
            var text = lit ? $"{(state.Info?.Sets == true ? "sets " : "")}{intensity} over {radius:0.##} tiles{(color != null ? $", {color}" : "")}" : "off";
            if (state.Info is { Effect: not ZoneEffect.None } info)
                text += $", {info.Effect.ToString().ToLowerInvariant()}";
            if (state.Override != null)
                text += state.Override.Off ? " (A_LightOff)" : " (A_SetLight)";
            yield return $"{actor.Name} {where}: {text}";
        }
    }

    /// <summary>Runs the actors' light effects for this tic's worth of time.</summary>
    internal void TickActorLights(uint tics)
    {
        foreach (var actor in LitActors())
        {
            var state = LightStateOf(actor);
            if (state.Info is { Effect: not ZoneEffect.None } info)
                LightEffects.Tick(info.Effect, (int)tics, info.Tics, ref state.Phase, ref state.FlickerAmount, Lighting.ZoneRandom);
        }
    }

    // The actors that might give off light: those on the level, the players' bodies (a Prop Hunt
    // prop disguised as a lamp lights up as one) and the weapon in hand
    IEnumerable<Entities.Actors.Actor> LitActors()
    {
        foreach (var actor in map.GetActors())
            if (!actor.IsRemoved && MightLight(actor))
                yield return actor;
        foreach (var p in session.Players)
            if (p.Pawn != null && !p.Gone && p.Body is { } body && MightLight(body))
                yield return body;
        if (WeaponSprite != null && MightLight(WeaponSprite))
            yield return WeaponSprite;
    }

    bool MightLight(Entities.Actors.Actor actor) =>
        LightStateOf(actor) is { } state && (state.Info != null || state.Override != null);

    /// <summary>Clears last frame's actor light from the grid and adds this frame's; called before each frame.</summary>
    internal void CastActorLights()
    {
        // A level of another size: the grid starts afresh, and so do the tiles lit on it
        lighting.SizeToLevel();
        caster.BeginFrame(map.MapArea, map.MapWidth);
        HasLights = false;

        // A set light on a level with no shading turns it on (once a level), so it can darken
        if (!lighting.Shading && !lighting.ActorShading && LitActors().Any(SetsLight))
        {
            lighting.ActorShading = true;
            lighting.RebuildShading();
            lighting.UpdateLightGrid();         // the tiles' base lights the set lights go from
        }
        if (!lighting.Shading)
            return;

        foreach (var actor in LitActors())
        {
            var (intensity, radius, color) = CurrentLight(actor);
            // the weapon in hand isn't placed in the world: it's where the player is
            bool inhand = ReferenceEquals(actor, WeaponSprite);
            caster.CastLight(this, inhand ? session.Player.X : actor.X, inhand ? session.Player.Y : actor.Y, intensity, radius, ColorTints(color), SetsLight(actor));
        }
        HasLights = caster.LitTiles > 0;
    }

    // Whether an actor's light is a set one (light.mode: set)
    bool SetsLight(Entities.Actors.Actor actor) => LightStateOf(actor).Info?.Sets == true;

    // The level as casting sees it (ILightMap)
    public int TileIndexOrNone(int x, int y) => map.TileIndexOrNone(x, y);

    // Light can light a tile: open floor, a door or a diagonal wall (its open half), not a solid wall
    public bool LightEnters(int x, int y)
    {
        int tile = map.TileMap[x, y];
        return tile == 0 || (tile & GameConstants.BitDoor) != 0 || map.WallShapes[x, y] != WallShape.Square;
    }

    // Light carries on through a tile: open floor, or a door that isn't shut
    public bool LightPasses(int x, int y)
    {
        int tile = map.TileMap[x, y];
        if (tile == 0)
            return true;
        return (tile & GameConstants.BitDoor) != 0 && doors.DoorObjList[tile & ~GameConstants.BitDoor].Action != DoorAction.Closed;
    }
}
