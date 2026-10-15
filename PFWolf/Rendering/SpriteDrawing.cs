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
/// <param name="DeathCamSprite">The death cam's banner, while it shows (Program.WL_ACT2.cs)</param>
/// <param name="WeaponScale">How big the weapon in hand is drawn (game-info weapon-scale)</param>
/// <param name="DemoActive">Whether a demo is playing or recording (it says DEMO on the view)</param>
internal sealed record SpriteDrawingHooks(
    Func<string?> WeaponShapeName,
    Func<Entities.Actors.Actor, bool> IsPlayerBody,
    Func<PlayerPawn, Entities.Actors.Actor?> VisiblePlayerBody,
    Func<Entities.Actors.Actor, string> DrawnSprite,
    Func<string, bool> SpriteExists,
    Func<Entities.Actors.Actor?> DeathCamSprite,
    Func<double> WeaponScale,
    Func<bool> DemoActive);
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
    internal visobj_t[] vislist = new visobj_t[Program.MAXVISABLE];

    /*
========================
=
= TransformTile
=
= Takes paramaters:
=   tx,ty               : tile the object is centered in
=
= globals:
=   viewx,viewy         : point of view
=   viewcos,viewsin     : sin/cos of viewangle
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
        gx = ((int)tx << MapConstants.TILESHIFT) + 0x8000 - render.viewx;
        gy = ((int)ty << MapConstants.TILESHIFT) + 0x8000 - render.viewy;

        //
        // calculate newx
        //
        gxt = MathUtils.FixedMul(gx, render.viewcos);
        gyt = MathUtils.FixedMul(gy, render.viewsin);
        nx = gxt - gyt - 0x2000;            // 0x2000 is size of object

        //
        // calculate newy
        //
        gxt = MathUtils.FixedMul(gx, render.viewsin);
        gyt = MathUtils.FixedMul(gy, render.viewcos);
        ny = gyt + gxt;


        //
        // calculate height / perspective ratio
        //
        if (nx < Program.MINDIST)                 // too close, don't overflow the divide
            dispheight = 0;
        else
        {
            dispx = (short)(render.centerx + ny * render.scale / nx);
            dispheight = (short)(render.heightnumerator / (nx >> 8));
        }

        //
        // see if it should be grabbed
        //
        if (nx < MapConstants.TILEGLOBAL && ny > -MapConstants.TILEGLOBAL / 2 && ny < MapConstants.TILEGLOBAL / 2)
            return true;
        else
            return false;
    }

    // Computes an actor's screen-space hit-testing/scale data (ViewX/TransX/ViewHeight) from
    // the fixed-point X/Y that Monster's movement code maintains, so enemies,
    // projectiles and the BJ-victory actor can be both rendered and (enemies) shot at.
    internal void TransformActor(Entities.Actors.Actor ob) =>
        TransformActorFrom(ob, render.viewx, render.viewy, render.viewsin, render.viewcos);

    /// <summary>TransformActor from a view other than this frame's (the player's, see PlayerSight)</summary>
    internal void TransformActorFrom(Entities.Actors.Actor ob, int fromx, int fromy, int fromsin, int fromcos)
    {
        var gx = ob.X - fromx;
        var gy = ob.Y - fromy;

        var gxt = MathUtils.FixedMul(gx, fromcos);
        var gyt = MathUtils.FixedMul(gy, fromsin);
        var nx = gxt - gyt - Program.ACTORSIZE;

        gxt = MathUtils.FixedMul(gx, fromsin);
        gyt = MathUtils.FixedMul(gy, fromcos);
        var ny = gyt + gxt;

        ob.TransX = nx;

        if (nx < Program.MINDIST)
        {
            ob.ViewHeight = 0;
            return;
        }

        ob.ViewX = (short)(render.centerx + ny * render.scale / nx);
        ob.ViewHeight = (ushort)(render.heightnumerator / (nx >> 8));
    }

    internal readonly Dictionary<string, bool> _directionalSpriteCache = [];

    // There's no per-frame "rotate" flag in the actordefs schema -- the engine infers it
    // from how many sprite variants actually exist. Walk-cycle sprites (e.g. GARDB1..B8)
    // have 8-directional rotation frames; bosses like Hans only have a single front-facing
    // frame per letter (HANSA0, HANSB0, ...) with no rotations, so they default to 0.
    // Any of the 8 views counts: a mod can add views 2-8 to a frame and leave the front to
    // the game's own single view (multiplayer.pk3's shooting guards, GARDF2..F8 beside GARDF0).
    internal bool HasDirectionalSprites(string sprite, string frameLetter)
    {
        var cacheKey = $"{sprite}{frameLetter}";
        if (_directionalSpriteCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var hasRotations = Enumerable.Range(1, 8).Any(r => hooks.SpriteExists($"{sprite}{frameLetter}{r}"));
        _directionalSpriteCache[cacheKey] = hasRotations;
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
        var viewangle = (int)(render.viewangle + (render.centerx - ob.ViewX) / (8 * render.projectionwidth / 320.0));

        // A projectile (Rocket, the Death Knight's HeavyRocket) has no Dir -- it flies at an
        // arbitrary angle -- so it rotates by its heading. So does the player's body.
        int angle;
        if (ob.Flags.Contains("PROJECTILE", StringComparer.OrdinalIgnoreCase) || hooks.IsPlayerBody(ob))
            angle = (viewangle - 180) - ob.Angle;
        else if (AimAngle(ob) is { } facing)
            angle = (viewangle - 180) - facing;
        else if (ob.Dir == objdirtypes.nodir)
            return 1;                                   // facing no way: toward whoever looks
        else
            angle = (viewangle - 180) - Program.dirangle[(byte)ob.Dir];

        angle += Program.ANGLES / 16;
        while (angle >= Program.ANGLES) angle -= Program.ANGLES;
        while (angle < 0) angle += Program.ANGLES;

        return (angle / (Program.ANGLES / 8)) + 1;
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
            fangle += Program.M_PI * 2;
        return (int)(fangle / (Program.M_PI * 2) * Program.ANGLES) % Program.ANGLES;
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
        var looker = session.camera.Target;

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
                actor = body;                                           // what others see of them (Program.Camera.cs)
            }
            else if (actor == looker)
                continue;                                               // the one looked through

            visobj_t visptr_val = new visobj_t();
            visptr_val.translation = translation;
            //statobj_t statptr_val = statobjlist[statptr];
            if (actor.CurrentState == null)
                continue;
            visptr_val.bright = lighting.shading && Lighting.IsBright(actor);

            // A marker with no sprite (a patrol point) takes no vislist slot. Inventory still
            // goes through below, where touching it is what picks it up.
            if (actor.CurrentState.Sprite == "TNT1" && actor is not Inventory)
                continue;

            // Enemies (Entities.Actors.Monster) move between tiles and need 8-way rotation, so
            // they're transformed like legacy "active objects" (TransformActor/CalcRotate,
            // fixed-point X/Y, checked against all 9 surrounding spotvis tiles) instead of
            // the tile-snapped, always-front-facing path decorations/pickups use below.
            // Projectiles and their smoke/explosions (spawned Active by SpawnAtActor) fly
            // between tiles too, so they take the same path.
            if (actor.ResolvedStates.ContainsKey("Chase") || actor.Active == activetypes.ac_yes)
            {
                var atx = actor.TileX;
                var aty = actor.TileY;

                // A neighbouring tile only counts while it's open floor: a wall the player
                // sees isn't somewhere the actor could be showing from
                bool Seen(int x, int y) => map.spotvis[x, y] && map.tilemap[x, y] == 0;
                if (!(map.spotvis[atx, aty]
                    || Seen(atx - 1, aty)
                    || Seen(atx + 1, aty)
                    || Seen(atx, aty - 1)
                    || Seen(atx - 1, aty - 1)
                    || Seen(atx + 1, aty - 1)
                    || Seen(atx, aty + 1)
                    || Seen(atx - 1, aty + 1)
                    || Seen(atx + 1, aty + 1)))
                {
                    actor.RuntimeFlags &= ~Program.objflags.FL_VISABLE;
                    continue;
                }

                // Once near enough to the view, an enemy is awake for good (MapManager.DoActor);
                // with others, what they see wakes it instead (WakeSeenEnemies)
                if (actor is Entities.Actors.Monster && session.gamemode == GameMode.Single)
                    actor.Active = activetypes.ac_yes;

                // Too close or far away to draw: as the original, it keeps whether it was
                // on screen last frame (enemy aim reads that)
                TransformActor(actor);
                if (actor.ViewHeight == 0)
                    continue;

                actor.RuntimeFlags |= Program.objflags.FL_VISABLE;

                var sprite = hooks.DrawnSprite(actor);                        // a player's body shows their weapon
                visptr_val.shapenum = ActorShapeName(sprite, actor.CurrentState.FrameLetter, actor);
                visptr_val.viewx = actor.ViewX;
                visptr_val.viewheight = (short)actor.ViewHeight;

                // A cloaked enemy shows for a frame when it's hit (bstone's FL2_DAMAGE_CLOAK)
                visptr_val.cloaked = actor.Cloaked && !actor.CloakHit && actor.Hitpoints > 0;
                actor.CloakHit = false;

                if (visptr < Program.MAXVISABLE - 1)
                {
                    visptr_val.tilex = atx;
                    visptr_val.tiley = aty;
                    visptr_val.worldx = actor.X;
                    visptr_val.worldy = actor.Y;
                    vislist[visptr] = visptr_val;
                    visptr++;
                }
                continue;
            }

            visptr_val.shapenum = actor.CurrentState.GetShapeName(objdirtypes.nodir);

            // A panel (Program.cs) is drawn column by column, clipped per column,
            // so it's placed by its own geometry rather than as a billboard in its tile.
            if (WallSprites.IsWallSprite(actor))
            {
                if (wallSprites.WallSpriteSeen(actor) && wallSprites.TransformWallSprite(actor, visptr_val) && visptr < Program.MAXVISABLE - 1)
                {
                    visptr_val.tilex = actor.TileX;
                    visptr_val.tiley = actor.TileY;
                    (visptr_val.worldx, visptr_val.worldy) = Lighting.TileCenter(actor.TileX, actor.TileY);
                    vislist[visptr] = visptr_val;
                    visptr++;
                }
                continue;
            }

            if (!map.spotvis[(int)actor.Position.X, (int)actor.Position.Y])
                continue;                                               // not visable

            // Picking it up is TouchItems' job (Program.PlayerSight.cs), not the renderer's
            TransformTile((int)actor.Position.X, (int)actor.Position.Y, ref visptr_val.viewx, ref visptr_val.viewheight);

            if (visptr_val.viewheight == 0)
                continue;                                               // to close to the object

            if (visptr < (Program.MAXVISABLE - 1))    // don't let it overflow
            {
                visptr_val.tilex = (byte)actor.Position.X;
                visptr_val.tiley = (byte)actor.Position.Y;
                (visptr_val.worldx, visptr_val.worldy) = Lighting.TileCenter(visptr_val.tilex, visptr_val.tiley);
                //visptr_val.flags = actor.Flags;
                vislist[visptr] = visptr_val;
                visptr++;
            }
        }

        //
        // draw from back to front
        //
        numvisable = (int)(visptr);

        // Rain and snow go in among the sprites by distance (Program.cs)
        weather.ProjectWeather();

        for (i = 0; i < numvisable; i++)
        {
            least = 32000;
            for (visstep = 0; visstep < visptr; visstep++)
            {
                visobj_t visstep_val = vislist[visstep];
                height = visstep_val.viewheight;
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
                visobj_t farthest_obj = vislist[farthest];
                weather.DrawWeatherBeyond(farthest_obj.viewheight);
                if (farthest_obj.wallsprite != null)
                    wallSprites.ScaleWallSprite(farthest_obj);
                else
                    scaler.ScaleShape(farthest_obj);

                farthest_obj.viewheight = 32000;
            }
        }

        weather.DrawWeatherBeyond(int.MaxValue);    // and what's nearer than every sprite
    }

    internal void DrawPlayerWeapon()
    {
        if (session.gamestate.victoryflag)
        {
            // The death cam's banner, on whichever frame T_DeathCam has it (TNT1 is the "off" half of its flash)
            if (session.player.CurrentState?.StateName == PlayerPawn.DeathCamState
                && hooks.DeathCamSprite()?.CurrentState is { Sprite.Length: > 0 } state && state.Sprite != "TNT1")
                scaler.SimpleScaleShape(render.viewwidth / 2, $"{state.Sprite}{state.FrameLetter}0", render.viewheight + 1);
            return;
        }

        // Only in the player's own view
        if (!session.camera.OnPlayer)
            return;

        // The frame the weapon's states are on (Program.PlayerWeapon.cs), in the light of the
        // player's tile but not faded: it's at arm's length. Scaled by game-info weapon-scale, it
        // still stands on the view's bottom edge (less any weapon-bob)
        if (hooks.WeaponShapeName() is { } shape)
        {
            int height = (int)((render.viewheight + 1) * hooks.WeaponScale());
            scaler.SimpleScaleShape(render.viewwidth / 2, shape, height,
                session.playerstate.WeaponSprite != null && Lighting.IsBright(session.playerstate.WeaponSprite) ? Lighting.noshade : lighting.LightAt(Lighting.TileIndex(session.player.TileX, session.player.TileY), session.player.X, session.player.Y),
                ((render.viewheight + 1) >> 1) - (height >> 1) + weaponBob.Drop(render.viewheight));
        }

        if (hooks.DemoActive())
            scaler.SimpleScaleShape(render.viewwidth / 2, "DEMOA0", render.viewheight + 1);
    }
}
