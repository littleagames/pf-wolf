using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Rendering;

/// <summary>What drawing the sprites needs from the game</summary>
/// <param name="WeaponShapeName">The weapon in hand's sprite this frame (WeaponInHand), or null</param>
/// <param name="IsPlayerBody">Whether an actor is one of the players' bodies (they turn by their Angle)</param>
/// <param name="VisiblePlayerBody">A player's body to draw this frame, if any (not the one the camera looks through)</param>
/// <param name="DrawnSprite">The sprite to draw for an actor's frame (a body shows its weapon's bodysprite)</param>
/// <param name="SpriteExists">Whether there's a sprite picture of that name</param>
/// <param name="DeathCamSprite">The death cam's banner, while it shows (Gameplay/PlayerDeath.cs)</param>
/// <param name="WeaponScale">How big the weapon in hand is drawn (game-info weapon-scale)</param>
internal sealed record SpriteDrawingHooks(
    Func<string?> WeaponShapeName,
    Func<Entities.Actors.Actor, bool> IsPlayerBody,
    Func<PlayerPawn, Entities.Actors.Actor?> VisiblePlayerBody,
    Func<Entities.Actors.Actor, string> DrawnSprite,
    Func<string, bool> SpriteExists,
    Func<Entities.Actors.Actor?> DeathCamSprite,
    Func<double> WeaponScale);
/// <summary>
/// The level's sprites: each actor and item worth drawing is placed on the view (TransformActor),
/// given the sprite for its angle to the viewer (CalcRotate), and they're drawn farthest first
/// behind the walls in front of them (DrawScaleds), wall sprites and weather among them; then the
/// weapon in hand over everything (DrawPlayerWeapon).
/// </summary>
internal sealed class SpriteDrawing(
    AssetManager assets,
    MapManager map,
    TranslationManager translations,
    Lighting lighting,
    SpriteScaler scaler,
    WallSpriteDrawing wallSprites,
    Weather weather,
    RenderState render,
    WeaponBob weaponBob,
    GameSession session,
    SpriteDrawingHooks hooks)
{
    internal VisibleObject[] VisList = new VisibleObject[GameConstants.MaxVisible];

    /*
========================
=
= TransformTile
=
= Takes paramaters:
=   tx,ty               : tile the object is centered in
=
= globals:
=   ViewX,ViewY         : point of view
=   ViewCos,ViewSin     : sin/cos of ViewAngle
=   scale               : conversion from global value to screen value
=
= sets:
=   screenx,transx,transy,screenheight: projected edge location and size
=
= Returns true if the tile is withing getting distance
=
========================
*/

    internal bool TransformTile(int tx, int ty, ref short dispx, ref short dispheight)
    {
        int gx, gy, gxt, gyt, nx, ny;

        //
        // translate point to view centered coordinates
        //
        gx = ((int)tx << MapConstants.TileShift) + 0x8000 - render.ViewX;
        gy = ((int)ty << MapConstants.TileShift) + 0x8000 - render.ViewY;

        //
        // calculate newx
        //
        gxt = MathUtils.FixedMul(gx, render.ViewCos);
        gyt = MathUtils.FixedMul(gy, render.ViewSin);
        nx = gxt - gyt - 0x2000;            // 0x2000 is size of object

        //
        // calculate newy
        //
        gxt = MathUtils.FixedMul(gx, render.ViewSin);
        gyt = MathUtils.FixedMul(gy, render.ViewCos);
        ny = gyt + gxt;


        //
        // calculate height / perspective ratio
        //
        if (nx < GameConstants.MinDist)                 // too close, don't overflow the divide
            dispheight = 0;
        else
        {
            dispx = (short)(render.CenterX + ny * render.Scale / nx);
            dispheight = (short)(render.HeightNumerator / (nx >> 8));
        }

        //
        // see if it should be grabbed
        //
        if (nx < MapConstants.TileGlobal && ny > -MapConstants.TileGlobal / 2 && ny < MapConstants.TileGlobal / 2)
            return true;
        else
            return false;
    }

    // Computes an actor's screen-space hit-testing/scale data (ViewX/TransX/ViewHeight) from
    // the fixed-point X/Y that Monster's movement code maintains, so enemies,
    // projectiles and the BJ-victory actor can be both rendered and (enemies) shot at.
    internal void TransformActor(Entities.Actors.Actor ob) =>
        TransformActorFrom(ob, render.ViewX, render.ViewY, render.ViewSin, render.ViewCos);

    /// <summary>TransformActor from a view other than this frame's (the player's, see PlayerSight)</summary>
    internal void TransformActorFrom(Entities.Actors.Actor ob, int fromx, int fromy, int fromsin, int fromcos)
    {
        var gx = ob.X - fromx;
        var gy = ob.Y - fromy;

        var gxt = MathUtils.FixedMul(gx, fromcos);
        var gyt = MathUtils.FixedMul(gy, fromsin);
        var nx = gxt - gyt - GameConstants.ActorSize;

        gxt = MathUtils.FixedMul(gx, fromsin);
        gyt = MathUtils.FixedMul(gy, fromcos);
        var ny = gyt + gxt;

        ob.TransX = nx;

        if (nx < GameConstants.MinDist)
        {
            ob.ViewHeight = 0;
            return;
        }

        ob.ViewX = (short)(render.CenterX + ny * render.Scale / nx);
        ob.ViewHeight = (ushort)(render.HeightNumerator / (nx >> 8));
    }

    internal readonly Dictionary<string, bool> DirectionalSpriteCache = [];

    // There's no per-frame "rotate" flag in the actordefs schema -- the engine infers it
    // from how many sprite variants actually exist. Walk-cycle sprites (e.g. GARDB1..B8)
    // have 8-directional rotation frames; bosses like Hans only have a single front-facing
    // frame per letter (HANSA0, HANSB0, ...) with no rotations, so they default to 0.
    // Any of the 8 views counts: a mod can add views 2-8 to a frame and leave the front to
    // the game's own single view (multiplayer.pk3's shooting guards, GARDF2..F8 beside GARDF0).
    internal bool HasDirectionalSprites(string sprite, string frameLetter)
    {
        var cacheKey = $"{sprite}{frameLetter}";
        if (DirectionalSpriteCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var hasRotations = Enumerable.Range(1, 8).Any(r => hooks.SpriteExists($"{sprite}{frameLetter}{r}"));
        DirectionalSpriteCache[cacheKey] = hasRotations;
        return hasRotations;
    }

    /// <summary>
    /// The picture of an actor's frame from where it's seen (after TransformActor): the view
    /// for its rotation when the frame has views, else its single view (letter 0). A view the
    /// frame lacks falls back to the single view, when there is one.
    /// </summary>
    internal string ActorShapeName(string sprite, string frameLetter, Entities.Actors.Actor actor)
    {
        var single = $"{sprite}{frameLetter}0";
        if (!HasDirectionalSprites(sprite, frameLetter))
            return single;

        var view = $"{sprite}{frameLetter}{CalcRotate(actor)}";
        return hooks.SpriteExists(view) || !hooks.SpriteExists(single) ? view : single;
    }

    /// <summary>The picture of a frame seen from the front: view 1, else its single view</summary>
    internal string FrontShapeName(string sprite, string frameLetter) =>
        hooks.SpriteExists($"{sprite}{frameLetter}1") ? $"{sprite}{frameLetter}1" : $"{sprite}{frameLetter}0";

    internal int CalcRotate(Entities.Actors.Actor ob)
    {
        var viewangle = (int)(render.ViewAngle + (render.CenterX - ob.ViewX) / (8 * render.ProjectionWidth / 320.0));

        // A projectile (Rocket, the Death Knight's HeavyRocket) has no Dir -- it flies at an
        // arbitrary angle -- so it rotates by its heading. So does the player's body.
        int angle;
        if (ob.Flags.Contains("PROJECTILE", StringComparer.OrdinalIgnoreCase) || hooks.IsPlayerBody(ob))
            angle = (viewangle - 180) - ob.Angle;
        else if (AimAngle(ob) is { } facing)
            angle = (viewangle - 180) - facing;
        else if (ob.Dir == ObjDir.NoDir)
            return 1;                                   // facing no way: toward whoever looks
        else
            angle = (viewangle - 180) - GameConstants.DirAngle[(byte)ob.Dir];

        angle += GameConstants.Angles / 16;
        while (angle >= GameConstants.Angles) angle -= GameConstants.Angles;
        while (angle < 0) angle += GameConstants.Angles;

        return (angle / (GameConstants.Angles / 8)) + 1;
    }

    /// <summary>
    /// The way an enemy is drawn facing, when not the way it's going: at the player it's after
    /// while it attacks. The original only drew attacks from the front (it always faced you);
    /// with views for them (multiplayer.pk3) the others see it aim at whoever it shoots. Only
    /// for show: reads the target without picking one (Monster.Target would).
    /// </summary>
    internal int? AimAngle(Entities.Actors.Actor ob)
    {
        if (ob is not Entities.Actors.Monster { TargetIfAny: { } target }
            || ob.CurrentState?.StateName?.Equals("Attack", StringComparison.OrdinalIgnoreCase) != true)
            return null;

        var fangle = Math.Atan2(ob.Y - target.Y, target.X - ob.X);
        if (fangle < 0)
            fangle += GameConstants.MathPi * 2;
        return (int)(fangle / (GameConstants.MathPi * 2) * GameConstants.Angles) % GameConstants.Angles;
    }

    internal void DrawScaleds()
    {
        int i, least, numvisable, height;
        int statptr;
        int farthest = -1;
        int visptr, visstep;

        visptr = 0;

        //
        // place static objects
        //
        //for (statptr = 0; statptr != laststatobj; statptr++)
        var actorList = map.GetActors();
        var looker = session.Camera.Target;

        var node = actorList.First;
        while (node != null)
        {
            var actor = node.Value;
            node = node.Next;

            if (actor == null)
                continue;                                               // object has been deleted

            // Its colors (the pawn's, for a player's body)
            var translation = translations.TableOf(actor);

            if (actor is PlayerPawn pawn)
            {
                if (hooks.VisiblePlayerBody(pawn) is not { } body)
                    continue;                                           // looked through, or with no sprites
                actor = body;                                           // what others see of them (Gameplay/PlayerBodies.cs)
            }
            else if (actor == looker)
                continue;                                               // the one looked through

            VisibleObject visptr_val = new VisibleObject();
            visptr_val.Translation = translation;
            //statobj_t statptr_val = statobjlist[statptr];
            if (actor.CurrentState == null)
                continue;
            visptr_val.Bright = lighting.Shading && Lighting.IsBright(actor);

            // A marker with no sprite (a patrol point) takes no VisList slot. Inventory still
            // goes through below, where touching it is what picks it up.
            if (actor.CurrentState.Sprite == "TNT1" && actor is not Inventory)
                continue;

            // Enemies (Entities.Actors.Monster) move between tiles and need 8-way rotation, so
            // they're transformed like legacy "active objects" (TransformActor/CalcRotate,
            // fixed-point X/Y, checked against all 9 surrounding spotvis tiles) instead of
            // the tile-snapped, always-front-facing path decorations/pickups use below.
            // Projectiles and their smoke/explosions (spawned Active by SpawnAtActor) fly
            // between tiles too, so they take the same path.
            if (actor.ResolvedStates.ContainsKey("Chase") || actor.Active == ActiveType.Yes)
            {
                var atx = actor.TileX;
                var aty = actor.TileY;

                // A neighbouring tile only counts while it's open floor: a wall the player
                // sees isn't somewhere the actor could be showing from
                bool Seen(int x, int y) => map.Spotvis[x, y] && map.TileMap[x, y] == 0;
                if (!(map.Spotvis[atx, aty]
                    || Seen(atx - 1, aty)
                    || Seen(atx + 1, aty)
                    || Seen(atx, aty - 1)
                    || Seen(atx - 1, aty - 1)
                    || Seen(atx + 1, aty - 1)
                    || Seen(atx, aty + 1)
                    || Seen(atx - 1, aty + 1)
                    || Seen(atx + 1, aty + 1)))
                {
                    actor.RuntimeFlags &= ~ObjFlags.Visible;
                    continue;
                }

                // Once near enough to the view, an enemy is awake for good (MapManager.DoActor);
                // with others, what they see wakes it instead (WakeSeenEnemies)
                if (actor is Entities.Actors.Monster && session.GameMode == GameMode.Single)
                    actor.Active = ActiveType.Yes;

                // Too close or far away to draw: as the original, it keeps whether it was
                // on screen last frame (enemy aim reads that)
                TransformActor(actor);
                if (actor.ViewHeight == 0)
                    continue;

                actor.RuntimeFlags |= ObjFlags.Visible;

                var sprite = hooks.DrawnSprite(actor);                        // a player's body shows their weapon
                visptr_val.ShapeNum = ActorShapeName(sprite, actor.CurrentState.FrameLetter, actor);
                visptr_val.ViewX = actor.ViewX;
                visptr_val.ViewHeight = (short)actor.ViewHeight;
                visptr_val.Z = actor.Z;

                // A cloaked enemy shows for a frame when it's hit (bstone's FL2_DAMAGE_CLOAK)
                visptr_val.Cloaked = actor.Cloaked && !actor.CloakHit && actor.Hitpoints > 0;
                actor.CloakHit = false;

                if (visptr < GameConstants.MaxVisible - 1)
                {
                    visptr_val.TileX = atx;
                    visptr_val.TileY = aty;
                    visptr_val.WorldX = actor.X;
                    visptr_val.WorldY = actor.Y;
                    VisList[visptr] = visptr_val;
                    visptr++;
                }
                continue;
            }

            visptr_val.ShapeNum = actor.CurrentState.GetShapeName(ObjDir.NoDir);

            // A panel (GameHost.cs) is drawn column by column, clipped per column,
            // so it's placed by its own geometry rather than as a billboard in its tile.
            if (WallSprites.IsWallSprite(actor))
            {
                if (wallSprites.WallSpriteSeen(actor) && wallSprites.TransformWallSprite(actor, visptr_val) && visptr < GameConstants.MaxVisible - 1)
                {
                    visptr_val.TileX = actor.TileX;
                    visptr_val.TileY = actor.TileY;
                    (visptr_val.WorldX, visptr_val.WorldY) = Lighting.TileCenter(actor.TileX, actor.TileY);
                    VisList[visptr] = visptr_val;
                    visptr++;
                }
                continue;
            }

            if (!map.Spotvis[(int)actor.Position.X, (int)actor.Position.Y])
                continue;                                               // not visable

            // Picking it up is TouchItems' job (Rendering/PlayerView.cs), not the renderer's
            TransformTile((int)actor.Position.X, (int)actor.Position.Y, ref visptr_val.ViewX, ref visptr_val.ViewHeight);

            if (visptr_val.ViewHeight == 0)
                continue;                                               // to close to the object
            visptr_val.Z = actor.Z;

            if (visptr < (GameConstants.MaxVisible - 1))    // don't let it overflow
            {
                visptr_val.TileX = (byte)actor.Position.X;
                visptr_val.TileY = (byte)actor.Position.Y;
                (visptr_val.WorldX, visptr_val.WorldY) = Lighting.TileCenter(visptr_val.TileX, visptr_val.TileY);
                //visptr_val.flags = actor.Flags;
                VisList[visptr] = visptr_val;
                visptr++;
            }
        }

        //
        // draw from back to front
        //
        numvisable = (int)(visptr);

        // Rain and snow go in among the sprites by distance (GameHost.cs)
        weather.ProjectWeather();

        for (i = 0; i < numvisable; i++)
        {
            least = 32000;
            for (visstep = 0; visstep < visptr; visstep++)
            {
                VisibleObject visstep_val = VisList[visstep];
                height = visstep_val.ViewHeight;
                if (height < least)
                {
                    least = height;
                    farthest = visstep;
                }
            }
            //
            // draw farthest
            //
            if (farthest != -1)
            {
                VisibleObject farthest_obj = VisList[farthest];
                weather.DrawWeatherBeyond(farthest_obj.ViewHeight);
                if (farthest_obj.WallSprite != null)
                    wallSprites.ScaleWallSprite(farthest_obj);
                else
                    scaler.ScaleShape(farthest_obj);

                farthest_obj.ViewHeight = 32000;
            }
        }

        weather.DrawWeatherBeyond(int.MaxValue);    // and what's nearer than every sprite
    }

    internal void DrawPlayerWeapon()
    {
        if (session.GameState.VictoryFlag)
        {
            // The death cam's banner, on whichever frame T_DeathCam has it (TNT1 is the "off" half of its flash)
            if (session.Player.CurrentState?.StateName == PlayerPawn.DeathCamState
                && hooks.DeathCamSprite()?.CurrentState is { Sprite.Length: > 0 } state && state.Sprite != "TNT1")
                scaler.SimpleScaleShape(render.ViewWidth / 2, $"{state.Sprite}{state.FrameLetter}0", render.ViewHeight + 1);
            return;
        }

        // Only in the player's own view
        if (!session.Camera.OnPlayer)
            return;

        // The frame the weapon's states are on (Gameplay/WeaponInHand.cs), in the light of the
        // player's tile but not faded: it's at arm's length. Scaled by game-info weapon-scale, it
        // still stands on the view's bottom edge (less any weapon-bob)
        if (hooks.WeaponShapeName() is { } shape)
        {
            int height = (int)((render.ViewHeight + 1) * hooks.WeaponScale());
            scaler.SimpleScaleShape(render.ViewWidth / 2, shape, height,
                session.PlayerState.WeaponSprite != null && Lighting.IsBright(session.PlayerState.WeaponSprite) ? Lighting.NoShade : lighting.LightAt(Lighting.TileIndex(session.Player.TileX, session.Player.TileY), session.Player.X, session.Player.Y),
                ((render.ViewHeight + 1) >> 1) - (height >> 1) + weaponBob.Drop(render.ViewHeight));
        }

        if ((session.DemoPlayback || session.DemoRecord))
            scaler.SimpleScaleShape(render.ViewWidth / 2, "DEMOA0", render.ViewHeight + 1);
    }
}
