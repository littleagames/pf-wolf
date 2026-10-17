using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Extensions;

namespace PFWolf.Gameplay;

/// <summary>What a player's death and the death cam need from the game</summary>
/// <param name="Settings">game-info's death settings: turn and drop speeds, fade, hold</param>
/// <param name="ShowObituary">Says who killed the player</param>
/// <param name="DrawHudMessages">The obituary, kept up on the red</param>
/// <param name="RestoreLevelStart">Puts the player back as they came into the level (death-restores-level-start); false if it can't</param>
/// <param name="LevelStartItems">The items the player had coming into the level (for death-keeps), or null</param>
internal sealed record PlayerDeathHooks(
    Func<GameInfoSettings> Settings,
    Action ShowObituary,
    Action DrawHudMessages,
    Func<bool> RestoreLevelStart,
    Func<IReadOnlyDictionary<string, int>?> LevelStartItems);

/// <summary>
/// A player's death: turning to face the killer as the view drops, the fade to red, and back
/// with a life less (Died). And the death cam a boss's death starts: "let's see that again!",
/// then its death replayed from behind where the killing shot came (A_StartDeathCam, T_DeathCam).
/// </summary>
internal sealed class PlayerDeath(
    AssetManager assets,
    GameEngineManager engine,
    VideoManager video,
    InputManager input,
    AudioManager audio,
    InventoryManager inventory,
    HudMessageManager hudMessages,
    MapManager map,
    UiText ui,
    FrameRefresh frame,
    RenderState render,
    PlayerClassRules classes,
    PlayerStats stats,
    PlayerWeapons weapons,
    StatusBarDrawing statusBar,
    PushWalls pushWalls,
    Projectiles projectiles,
    PlayScreen playScreen,
    IntermissionScreen intermission,
    GameSession session,
    PlayArea playArea,
    Configuration.GameSettings settings,
    PlayerDeathHooks hooks)
{
    internal void Died()
    {
        float fangle;
        int dx, dy;
        int iangle, curangle, clockwise, counter, change;

        if (video.screenfaded)
        {
            frame.ThreeDRefresh();
            video.FadeIn();
        }

        session.playerstate.weapon = null;                     // take away weapon
        session.playerstate.Pitch = 0;                             // and face the attacker straight on
        session.camera.FollowPlayer();                       // seen through their own eyes
        if (inventory.GetStringProperty(session.playerstate.playerclass, "deathsound") is { Length: > 0 } deathSound)
            audio.Play(deathSound);     // the Player class's deathsound
        hooks.ShowObituary();

        //
        // swing around to face attacker
        //
        if (session.playerstate.LastAttacker != null)
        {
            dx = session.playerstate.LastAttacker.X - session.player.X;
            dy = session.player.Y - session.playerstate.LastAttacker.Y;

            fangle = (float)Math.Atan2((float)dy, (float)dx);     // returns -pi to pi
            if (fangle < 0)
                fangle = (float)(GameConstants.M_PI * 2 + fangle);

            iangle = (int)(fangle / (GameConstants.M_PI * 2) * GameConstants.ANGLES);
        }
        else
        {
            iangle = session.player.Angle + GameConstants.ANGLES / 2;
            if (iangle >= GameConstants.ANGLES) iangle -= GameConstants.ANGLES;
        }

        if (session.player.Angle > iangle)
        {
            counter = session.player.Angle - iangle;
            clockwise = GameConstants.ANGLES - session.player.Angle + iangle;
        }
        else
        {
            clockwise = iangle - session.player.Angle;
            counter = session.player.Angle + GameConstants.ANGLES - iangle;
        }

        curangle = session.player.Angle;

        //
        // rotate the shorter way round, while the view drops (game-info death-drop-height)
        //
        bool rotateclockwise = clockwise < counter;
        if (rotateclockwise && curangle > iangle)
            curangle -= GameConstants.ANGLES;
        else if (!rotateclockwise && curangle < iangle)
            curangle += GameConstants.ANGLES;

        int dropto = hooks.Settings().deathDropHeight ?? session.playerstate.EyeZ;
        do
        {
            if (curangle != iangle)
            {
                change = rotateclockwise ? (int)(session.tics * hooks.Settings().deathTurnSpeed) : -(int)session.tics * hooks.Settings().deathTurnSpeed;
                if (rotateclockwise ? curangle + change > iangle : curangle + change < iangle)
                    change = iangle - curangle;

                curangle += change;
                session.player.Angle += (short)change;
                if (session.player.Angle >= GameConstants.ANGLES)
                    session.player.Angle -= GameConstants.ANGLES;
                else if (session.player.Angle < 0)
                    session.player.Angle += GameConstants.ANGLES;
            }

            if (session.playerstate.EyeZ > dropto)
                session.playerstate.EyeZ = Math.Max(session.playerstate.EyeZ - (int)session.tics * hooks.Settings().deathDropSpeed, dropto);

            frame.ThreeDRefresh();
            frame.CalcTics();
            hudMessages.Tick((int)session.tics);
        } while (curangle != iangle || session.playerstate.EyeZ > dropto);

        //
        // fade to red
        //
        video.FinishPaletteShifts();

        video.BarScaledCoord(render.viewscreenx, render.viewscreeny, render.viewwidth, render.viewheight, hooks.Settings().deathFadeColor);
        hooks.DrawHudMessages();      // the obituary stays up on the red, through the fade and the wait after it

        input.ClearKeysDown();

        video.Transition(hooks.Settings().deathFadeStyle, render.viewscreenx, render.viewscreeny, render.viewwidth, render.viewheight, (uint)hooks.Settings().deathFadeTics);

        input.UserInput((uint)hooks.Settings().deathHoldTics);
        audio.WaitSoundDone();
        audio.StopAll();
        session.playerstate.EyeZ = classes.StandingEyeZ(session.playerstate);      // back on their feet for the restart

        // A life less, unless the game has none to lose (game-info lives: false, Midnight Assault)
        var gameInfo = engine.GetGameInfo();
        if (gameInfo.Lives)
            session.playerstate.lives--;

        if (session.playerstate.lives > -1)
        {
            // Back as they came into the level (Blake Stone), or fresh with the starting items and
            // the death-keeps items as they came in (Midnight Assault's medkits)
            if (!gameInfo.DeathRestoresLevelStart || !hooks.RestoreLevelStart())
            {
                session.playerstate.health = classes.StartingHealth;
                stats.GiveStartingInventory();
                if (gameInfo.DeathKeeps.Count > 0 && hooks.LevelStartItems() is { } cameWith)
                    KeepItems(gameInfo.DeathKeeps, cameWith);
            }
            pushWalls.ClearPushWalls();
            session.playerstate.WeaponSprite = null;            // the weapon in hand starts on its Ready state

            if (settings.viewsize != 21)
            {
                statusBar.DrawKeys();
                weapons.DrawWeapon();
                weapons.DrawAmmo();
                statusBar.DrawHealth();
                statusBar.DrawArmor();
                statusBar.DrawFace();
                statusBar.DrawLives();
            }
        }
    }

    /// <summary>The items listed back at the counts the player came into the level with</summary>
    internal void KeepItems(IEnumerable<string> items, IReadOnlyDictionary<string, int> cameWith)
    {
        foreach (var item in items)
        {
            int had = cameWith.TryGetValue(item, out var count) ? count : 0, now = inventory.GetCount(item);
            if (had > now)
                inventory.Give(item, had - now);
            else if (now > had)
                inventory.Take(item, now - had);
        }
    }

    internal void A_StartDeathCam(Entities.Actors.Actor ob)
    {
        var language = assets.GetText("en-us");
        int dx, dy;
        float fangle;
        int xmove, ymove;
        int dist;

        video.FinishPaletteShifts();

        // intermission.yaml deathcam
        var deathcam = intermission.Intermission.DeathCam ?? new Assets.DeathCamScreen();
        GameEngineManager.DelayMs((uint)Math.Max(deathcam.PauseMs, 0));

        if (session.gamestate.victoryflag)
        {
            session.playstate = PlayState.Victorious;
            return;
        }

        // With others there's no replay: it would move one player's view, on one machine
        if (session.gamemode != GameMode.Single)
        {
            session.playstate = PlayState.Victorious;
            return;
        }

        session.gamestate.victoryflag = true;
        uint fadeheight = (uint)(settings.viewsize != 21 ? video.ScreenYAboveBottom(playArea.StatusLines) : video.screenHeight);
        video.BarScaledCoord(0, 0, video.screenWidth, (int)fadeheight, session.bordercol);
        video.Transition(hooks.Settings().deathFadeStyle, 0, 0, video.screenWidth, (int)fadeheight, (uint)hooks.Settings().deathFadeTics);

        foreach (var label in deathcam.Labels)
            ui.TextAt(label.X, label.Y, intermission.IntermissionTextStyle).Print(label.Text.ToLanguageText(language));

        video.Update();

        input.UserInput((uint)Math.Max(deathcam.HoldTics, 0));

        deathCamSprite = null;      // T_DeathCam builds a fresh one, so the flash starts from its first frame
        session.player.SetState(Entities.Actors.PlayerPawn.DeathCamState);

        // The camera, not the player, moves: from where the killing shot came, back from the
        // boss until it's clear of walls
        dx = ob.X - session.playerstate.killx;
        dy = session.playerstate.killy - ob.Y;

        fangle = (float)Math.Atan2((float)dy, (float)dx);
        if (fangle < 0)
            fangle = (float)(GameConstants.M_PI * 2 + fangle);

        var camangle = (short)(fangle / (GameConstants.M_PI * 2) * GameConstants.ANGLES);
        int camx, camy;

        dist = 0x14000;
        do
        {
            xmove = MathUtils.FixedMul(dist, TrigTables.costable[camangle]);
            ymove = -MathUtils.FixedMul(dist, TrigTables.sintable[camangle]);

            camx = ob.X - xmove;
            camy = ob.Y - ymove;
            dist += 0x1000;

        } while (!projectiles.CheckPosition(camx, camy));
        session.camera.SetFixed(camx, camy, camangle);

        playScreen.DrawPlayBorder();

        session.fizzlein = true;

        if (ob.ResolvedStates.ContainsKey("DeathCam"))
            ob.SetState("DeathCam");
    }

    // The death cam's "LET'S SEE THAT AGAIN!" banner (actordefs/deathcam.yaml), drawn by
    // DrawPlayerWeapon. Like the weapon in hand it's never placed in the world: the player's
    // DeathCam state runs its states instead.
    internal Entities.Actors.Actor? deathCamSprite;

    /// <summary>The player's think while on its DeathCam state: animates the banner.</summary>
    internal void T_DeathCam(Entities.Actors.Actor ob)
    {
        deathCamSprite ??= inventory.CreateActor("DeathCam");
        if (deathCamSprite != null)
            map.DoActor(deathCamSprite, session.tics);
    }
}
