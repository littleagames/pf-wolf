using System.Data;
using Wolf3D.Constants;
using Wolf3D.Enums;
using Wolf3D.Extensions;
using Wolf3D.Managers;

namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                                   LOCAL CONSTANTS

    =============================================================================
    */

    internal const long PROJECTILESIZE = 0xc000L;

    internal const int BJRUNSPEED = 2048;
    internal const int BJJUMPSPEED = 680;

    // Enemy state tables, spawn functions (SpawnStand/SpawnPatrol/SpawnBoss/etc.), and AI
    // think/action functions (T_Stand/T_Chase/T_Shoot/etc., along with starthitpoints and
    // the enemy_states/EnemyStateList lookup) were removed from this file -- enemies now
    // run on the new Entities.Actors.Actor type, spawned by MapManager.LoadMap from
    // mapdefs/wolf3d/enemies.yaml and driven by the AI on Entities.Actors.Monster.
    //
    // Projectiles (Rocket/Smoke/Needle/Fire, actordefs/wolf3d/projectiles.yaml) and the
    // BJ-victory end-of-episode cutscene (actordefs/wolf3d/victory.yaml) run on the same type,
    // spawned into MapManager._actors by MapManager.SpawnAtActor. CheckPosition's only caller
    // is A_StartDeathCam below, placing the camera.

    /*
    =================
    =
    = A_SpawnThing
    =
    =================
    */

    // A_SpawnThing("Smoke"): spawns the given actor at this one's position (a rocket's smoke trail)
    // A_SpawnThing("GreenOoze", 128): with a chance, only that many times in 256
    internal static void A_SpawnThing(Entities.Actors.Actor ob, string[] args)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.WriteLine("A_SpawnThing: no actor given.");
            return;
        }

        if (args.Length > 1 && int.TryParse(args[1], out var chance) && US_RndT() >= chance)
            return;

        _mapManager.SpawnAtActor(args[0], ob);
    }

    // Terminal action for one-shot effects (Smoke, a rocket's Death): the legacy chain ended on a null
    // next state, which removed the object once its last frame's tics ran out.
    internal static void A_Remove(Entities.Actors.Actor ob) => _mapManager.MarkForRemoval(ob);

    internal static void A_DeathScream(Entities.Actors.Actor ob) => PlayActorSound(ob, "deathsound");

    /// <summary>
    /// Plays an actor's sound property from where it is: one sound, or a list to pick one from
    /// at random (Blake Stone's guards have more than one death cry)
    /// </summary>
    internal static void PlayActorSound(Entities.Actors.Actor ob, string key)
    {
        var sounds = ob.PropertyStrings(key);
        if (sounds.Count > 0)
            PlaySoundLocActor(sounds[sounds.Count == 1 ? 0 : US_RndT() % sounds.Count], ob);
    }

    // A_ActiveSound: the actor's "activesound" (Mecha Hitler's stomp), only where the player can
    // hear it -- in an area connected to theirs, as vanilla's A_MechaSound was
    internal static void A_ActiveSound(Entities.Actors.Actor ob)
    {
        if (ob.Properties.TryGetValue("activesound", out var sound) && sound is string soundName
            && (ob.AreaNumber >= _mapManager.Floors.NumAreas || areabyplayer[ob.AreaNumber] != 0))
            PlaySoundLocActor(soundName, ob);
    }

    // Ends the game in victory (the Angel of Death's last death frame)
    internal static void A_Victory(Entities.Actors.Actor ob) => playstate = playstatetypes.ex_victorious;

    // A_PlaySound("angel/breath"): plays a sound, not placed in the world; with "positional"
    // (A_PlaySound("misc/yeah", "positional")), from where the actor is
    internal static void A_PlaySound(Entities.Actors.Actor ob, string[] args)
    {
        if (args.Length == 0)
            return;
        if (args.Skip(1).Any(a => a.Equals("positional", StringComparison.OrdinalIgnoreCase)))
            PlaySoundLocActor(args[0], ob);
        else
            _audioManager.Play(args[0]);
    }

    internal static void A_StartDeathCam(Entities.Actors.Actor ob)
    {
        var language = _assetManager.GetText("en-us");
        int dx, dy;
        float fangle;
        int xmove, ymove;
        int dist;

        _videoManager.FinishPaletteShifts();

        // intermission.yaml deathcam
        var deathcam = Intermission.DeathCam ?? new Assets.DeathCamScreen();
        GameEngineManager.DelayMs((uint)Math.Max(deathcam.PauseMs, 0));

        if (gamestate.victoryflag)
        {
            playstate = playstatetypes.ex_victorious;
            return;
        }

        // With others there's no replay: it would move one player's view, on one machine
        if (gamemode != GameMode.Single)
        {
            playstate = playstatetypes.ex_victorious;
            return;
        }

        gamestate.victoryflag = true;
        uint fadeheight = (uint)(viewsize != 21 ? _videoManager.ScreenYAboveBottom(STATUSLINES) : _videoManager.screenHeight);
        _videoManager.BarScaledCoord(0, 0, _videoManager.screenWidth, (int)fadeheight, bordercol);
        _videoManager.Transition(deathFadeStyle, 0, 0, _videoManager.screenWidth, (int)fadeheight, (uint)deathFadeTics);

        foreach (var label in deathcam.Labels)
            TextAt(label.X, label.Y, IntermissionTextStyle).Print(label.Text.ToLanguageText(language));

        _videoManager.Update();

        _inputManager.UserInput((uint)Math.Max(deathcam.HoldTics, 0));

        deathCamSprite = null;      // T_DeathCam builds a fresh one, so the flash starts from its first frame
        player.SetState(Entities.Actors.PlayerPawn.DeathCamState);

        // The camera, not the player, moves: from where the killing shot came, back from the
        // boss until it's clear of walls
        dx = ob.X - playerstate.killx;
        dy = playerstate.killy - ob.Y;

        fangle = (float)Math.Atan2((float)dy, (float)dx);
        if (fangle < 0)
            fangle = (float)(M_PI * 2 + fangle);

        var camangle = (short)(fangle / (M_PI * 2) * ANGLES);
        int camx, camy;

        dist = 0x14000;
        do
        {
            xmove = MathUtils.FixedMul(dist, costable[camangle]);
            ymove = -MathUtils.FixedMul(dist, sintable[camangle]);

            camx = ob.X - xmove;
            camy = ob.Y - ymove;
            dist += 0x1000;

        } while (!CheckPosition(camx, camy));
        camera.SetFixed(camx, camy, camangle);

        DrawPlayBorder();

        fizzlein = true;

        if (ob.ResolvedStates.ContainsKey("DeathCam"))
            ob.SetState("DeathCam");
    }

    // The death cam's "LET'S SEE THAT AGAIN!" banner (actordefs/deathcam.yaml), drawn by
    // DrawPlayerWeapon. Like the weapon in hand it's never placed in the world: the player's
    // DeathCam state runs its states instead.
    static Entities.Actors.Actor? deathCamSprite;

    /// <summary>The player's think while on its DeathCam state: animates the banner.</summary>
    internal static void T_DeathCam(Entities.Actors.Actor ob)
    {
        deathCamSprite ??= _inventoryManager.CreateActor("DeathCam");
        if (deathCamSprite != null)
            _mapManager.DoActor(deathCamSprite, tics);
    }


    /*
    ===================
    =
    = ProjectileTryMove
    =
    = returns true if move ok
    ===================
    */

    internal const int PROJSIZE = 0x2000;

    internal static bool ProjectileTryMove(Entities.Actors.Actor ob)
    {
        int xl, yl, xh, yh, x, y;
        Actor? check;

        xl = (ob.X - PROJSIZE) >> MapConstants.TILESHIFT;
        yl = (ob.Y - PROJSIZE) >> MapConstants.TILESHIFT;

        xh = (ob.X + PROJSIZE) >> MapConstants.TILESHIFT;
        yh = (ob.Y + PROJSIZE) >> MapConstants.TILESHIFT;

        //
        // check for solid walls
        //
        for (y = yl; y <= yh; y++)
            for (x = xl; x <= xh; x++)
            {
                check = _mapManager.actorat[x, y];
                if (check is null or ActorMark)     // enemies are hit by the projectile's own check
                    continue;

                // a wall sprite only blocks along its panel, unless it lets projectiles through
                if (check is WallSpriteBlocker)
                {
                    if (WallSpriteHitByBox(x, y, x, y, ob.X, ob.Y, PROJSIZE, player: false) != null)
                        return false;
                    continue;
                }

                // a diagonal only blocks on its solid side of the face
                if (_mapManager.wallshape[x, y] is var shape and not WallShape.Square
                    && !BoxHitsDiagonal(shape, x, y, ob.X, ob.Y, PROJSIZE))
                    continue;

                return false;
            }

        return true;
    }

    /*
    =================
    =
    = A_Projectile
    =
    =================
    */

    // A_Projectile(min[, max]): flies the projectile along its Angle; hitting the player deals
    // min..max damage (just min without a max). The roll scales one US_RndT() across the range,
    // so a 32-wide range is exactly the legacy (US_RndT()>>3) + base.
    internal static void A_Projectile(Entities.Actors.Actor ob, string[] args)
    {
        long deltax, deltay;
        int damage = 0;
        int speed;

        speed = (int)(ob.Speed * tics);

        deltax = MathUtils.FixedMul(speed, costable[ob.Angle]);
        deltay = -MathUtils.FixedMul(speed, sintable[ob.Angle]);

        if (deltax > 0x10000L)
            deltax = 0x10000L;
        if (deltay > 0x10000L)
            deltay = 0x10000L;

        ob.X += (int)deltax;
        ob.Y += (int)deltay;

        // the first player (in player order) it's on, if any
        var hit = _mapManager.Players.FirstOrDefault(pawn => Entities.Actors.Monster.IsTargetable(pawn)
            && Math.Abs(ob.X - pawn.X) < PROJECTILESIZE && Math.Abs(ob.Y - pawn.Y) < PROJECTILESIZE);

        if (!ProjectileTryMove(ob))
        {
            // A projectile with a Death state (rockets) blows up against the wall with its
            // deathsound, like the legacy switch to s_boom1; the rest (needles, flames, sparks)
            // just vanish
            if (ob.ResolvedStates.ContainsKey("Death"))
            {
                if (ob.Properties.TryGetValue("deathsound", out var hitSound) && hitSound is string hitSoundName)
                    PlaySoundLocActor(hitSoundName, ob);
                ob.SetState("Death");
            }
            else
                _mapManager.MarkForRemoval(ob);
            return;
        }

        if (hit != null)
        {       // hit the player
            var minDamage = args.Length > 0 && int.TryParse(args[0], out var min) ? min : 0;
            var maxDamage = args.Length > 1 && int.TryParse(args[1], out var max) ? max : minDamage;
            damage = maxDamage > minDamage
                ? minDamage + US_RndT() * (maxDamage - minDamage + 1) / 256
                : minDamage;

            TakeDamage(hit, damage, ob);

            // BURSTONPLAYER projectiles (Blake Stone's spit and shots) burst on the player as on a wall
            if (ob.HasFlag("BURSTONPLAYER") && ob.ResolvedStates.ContainsKey("Death"))
                ob.SetState("Death");
            else
                _mapManager.MarkForRemoval(ob);
            return;
        }

        ob.TileX = (byte)(ob.X >> MapConstants.TILESHIFT);
        ob.TileY = (byte)(ob.Y >> MapConstants.TILESHIFT);
    }

    /*
    ============================================================================

                                        BJ VICTORY

    ============================================================================
    */

    // The BJVictory actor (actordefs/wolf3d/victory.yaml) runs along the map's arrow icons for
    // a few tiles, jumps, and then ends the level.

    /*
    ===============
    =
    = T_BJRun
    =
    ===============
    */

    internal static void T_BJRun(Entities.Actors.Monster ob)
    {
        int move;

        move = (int)(BJRUNSPEED * tics);

        while (move != 0)
        {
            if (move < ob.Distance)
            {
                ob.MoveObj(move);
                break;
            }

            ob.RecenterOnTile();
            move -= ob.Distance;

            ob.SelectPathDir();

            if ((--ob.Temp1) == 0)
            {
                ob.SetState("Jump");
                return;
            }
        }
    }

    /*
    ===============
    =
    = T_BJJump
    =
    ===============
    */

    internal static void T_BJJump(Entities.Actors.Monster ob)
    {
        int move;

        move = (int)(BJJUMPSPEED * tics);
        ob.MoveObj(move);
    }


    /*
    ===============
    =
    = T_BJDone
    =
    ===============
    */

    internal static void T_BJDone(Entities.Actors.Actor ob)
    {
        playstate = playstatetypes.ex_victorious;                              // exit castle tile
    }


    internal static void SpawnBJVictory()
    {
        // Same spot the legacy SpawnNewObj used: BJ's tile is the one south of the player's, but
        // he starts at the player's exact position (SpawnAtActor copies the player's x/y).
        var bj = _mapManager.SpawnAtActor("BJVictory", player);
        if (bj == null)
            return;

        bj.TileY = (byte)(player.TileY + 1);
        bj.SyncPosition();
        bj.AreaNumber = (byte)(_mapManager.MAPSPOT(bj.TileX, bj.TileY, 0) - _mapManager.Floors.AreaTile);

        // SpawnNewObj started every actor a random number of tics into its first frame.
        bj.TicCount = Managers.MapManager.SpawnTicCount(bj.CurrentState);

        bj.Dir = objdirtypes.north;
        bj.Temp1 = 6;                      // tiles to run forward
    }

    //===========================================================================


    /*
    ===============
    =
    = CheckPosition
    =
    ===============
    */
    internal static bool CheckPosition(int obx, int oby)
    {
        int x, y, xl, yl, xh, yh;
        Actor? check;

        xl = (int)((obx - PLAYERSIZE) >> MapConstants.TILESHIFT);
        yl = (int)((oby - PLAYERSIZE) >> MapConstants.TILESHIFT);

        xh = (int)((obx + PLAYERSIZE) >> MapConstants.TILESHIFT);
        yh = (int)((oby + PLAYERSIZE) >> MapConstants.TILESHIFT);

        //
        // check for solid walls
        //
        for (y = yl; y <= yh; y++)
        {
            for (x = xl; x <= xh; x++)
            {
                check = _mapManager.actorat[x, y];
                if (check is not (null or ActorMark))     // only walls and solid things
                    return false;
            }
        }

        return true;
    }
}
