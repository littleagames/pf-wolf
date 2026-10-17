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

        if (video.ScreenFaded)
        {
            frame.ThreeDRefresh();
            video.FadeIn();
        }

        session.PlayerState.Weapon = null;                     // take away weapon
        session.PlayerState.Pitch = 0;                             // and face the attacker straight on
        session.Camera.FollowPlayer();                       // seen through their own eyes
        if (inventory.GetStringProperty(session.PlayerState.PlayerClass, "deathsound") is { Length: > 0 } deathSound)
            audio.Play(deathSound);     // the Player class's deathsound
        hooks.ShowObituary();

        //
        // swing around to face attacker
        //
        if (session.PlayerState.LastAttacker != null)
        {
            dx = session.PlayerState.LastAttacker.X - session.Player.X;
            dy = session.Player.Y - session.PlayerState.LastAttacker.Y;

            fangle = (float)Math.Atan2((float)dy, (float)dx);     // returns -pi to pi
            if (fangle < 0)
                fangle = (float)(GameConstants.MathPi * 2 + fangle);

            iangle = (int)(fangle / (GameConstants.MathPi * 2) * GameConstants.Angles);
        }
        else
        {
            iangle = session.Player.Angle + GameConstants.Angles / 2;
            if (iangle >= GameConstants.Angles) iangle -= GameConstants.Angles;
        }

        if (session.Player.Angle > iangle)
        {
            counter = session.Player.Angle - iangle;
            clockwise = GameConstants.Angles - session.Player.Angle + iangle;
        }
        else
        {
            clockwise = iangle - session.Player.Angle;
            counter = session.Player.Angle + GameConstants.Angles - iangle;
        }

        curangle = session.Player.Angle;

        //
        // rotate the shorter way round, while the view drops (game-info death-drop-height)
        //
        bool rotateclockwise = clockwise < counter;
        if (rotateclockwise && curangle > iangle)
            curangle -= GameConstants.Angles;
        else if (!rotateclockwise && curangle < iangle)
            curangle += GameConstants.Angles;

        int dropto = hooks.Settings().DeathDropHeight ?? session.PlayerState.EyeZ;
        do
        {
            if (curangle != iangle)
            {
                change = rotateclockwise ? (int)(session.Tics * hooks.Settings().DeathTurnSpeed) : -(int)session.Tics * hooks.Settings().DeathTurnSpeed;
                if (rotateclockwise ? curangle + change > iangle : curangle + change < iangle)
                    change = iangle - curangle;

                curangle += change;
                session.Player.Angle += (short)change;
                if (session.Player.Angle >= GameConstants.Angles)
                    session.Player.Angle -= GameConstants.Angles;
                else if (session.Player.Angle < 0)
                    session.Player.Angle += GameConstants.Angles;
            }

            if (session.PlayerState.EyeZ > dropto)
                session.PlayerState.EyeZ = Math.Max(session.PlayerState.EyeZ - (int)session.Tics * hooks.Settings().DeathDropSpeed, dropto);

            frame.ThreeDRefresh();
            frame.CalcTics();
            hudMessages.Tick((int)session.Tics);
        } while (curangle != iangle || session.PlayerState.EyeZ > dropto);

        //
        // fade to red
        //
        video.FinishPaletteShifts();

        video.BarScaledCoord(render.ViewScreenX, render.ViewScreenY, render.ViewWidth, render.ViewHeight, hooks.Settings().DeathFadeColor);
        hooks.DrawHudMessages();      // the obituary stays up on the red, through the fade and the wait after it

        input.ClearKeysDown();

        video.Transition(hooks.Settings().DeathFadeStyle, render.ViewScreenX, render.ViewScreenY, render.ViewWidth, render.ViewHeight, (uint)hooks.Settings().DeathFadeTics);

        input.UserInput((uint)hooks.Settings().DeathHoldTics);
        audio.WaitSoundDone();
        audio.StopAll();
        session.PlayerState.EyeZ = classes.StandingEyeZ(session.PlayerState);      // back on their feet for the restart

        // A life less, unless the game has none to lose (game-info lives: false, Midnight Assault)
        var gameInfo = engine.GetGameInfo();
        if (gameInfo.Lives)
            session.PlayerState.Lives--;

        if (session.PlayerState.Lives > -1)
        {
            // Back as they came into the level (Blake Stone), or fresh with the starting items and
            // the death-keeps items as they came in (Midnight Assault's medkits)
            if (!gameInfo.DeathRestoresLevelStart || !hooks.RestoreLevelStart())
            {
                session.PlayerState.Health = classes.StartingHealth;
                stats.GiveStartingInventory();
                if (gameInfo.DeathKeeps.Count > 0 && hooks.LevelStartItems() is { } cameWith)
                    KeepItems(gameInfo.DeathKeeps, cameWith);
            }
            pushWalls.ClearPushWalls();
            session.PlayerState.WeaponSprite = null;            // the weapon in hand starts on its Ready state

            if (settings.ViewSize != 21)
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

    internal void StartDeathCam(Entities.Actors.Actor ob)
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

        if (session.GameState.VictoryFlag)
        {
            session.PlayState = PlayState.Victorious;
            return;
        }

        // With others there's no replay: it would move one player's view, on one machine
        if (session.GameMode != GameMode.Single)
        {
            session.PlayState = PlayState.Victorious;
            return;
        }

        session.GameState.VictoryFlag = true;
        uint fadeheight = (uint)(settings.ViewSize != 21 ? video.ScreenYAboveBottom(playArea.StatusLines) : video.ScreenHeight);
        video.BarScaledCoord(0, 0, video.ScreenWidth, (int)fadeheight, session.BorderCol);
        video.Transition(hooks.Settings().DeathFadeStyle, 0, 0, video.ScreenWidth, (int)fadeheight, (uint)hooks.Settings().DeathFadeTics);

        foreach (var label in deathcam.Labels)
            ui.TextAt(label.X, label.Y, intermission.IntermissionTextStyle).Print(label.Text.ToLanguageText(language));

        video.Update();

        input.UserInput((uint)Math.Max(deathcam.HoldTics, 0));

        DeathCamSprite = null;      // T_DeathCam builds a fresh one, so the flash starts from its first frame
        session.Player.SetState(Entities.Actors.PlayerPawn.DeathCamState);

        // The camera, not the player, moves: from where the killing shot came, back from the
        // boss until it's clear of walls
        dx = ob.X - session.PlayerState.KillX;
        dy = session.PlayerState.KillY - ob.Y;

        fangle = (float)Math.Atan2((float)dy, (float)dx);
        if (fangle < 0)
            fangle = (float)(GameConstants.MathPi * 2 + fangle);

        var camangle = (short)(fangle / (GameConstants.MathPi * 2) * GameConstants.Angles);
        int camx, camy;

        dist = 0x14000;
        do
        {
            xmove = MathUtils.FixedMul(dist, TrigTables.CosTable[camangle]);
            ymove = -MathUtils.FixedMul(dist, TrigTables.SinTable[camangle]);

            camx = ob.X - xmove;
            camy = ob.Y - ymove;
            dist += 0x1000;

            // Off the map with nowhere clear on the way (a boss dying right beside the killer starts
            // the search behind them, as vanilla's does, which then runs into the wall there): from the
            // killer's own spot
            if ((camx >> MapConstants.TileShift) is < 0 or >= MapConstants.MapSize
                || (camy >> MapConstants.TileShift) is < 0 or >= MapConstants.MapSize)
            {
                (camx, camy) = (session.PlayerState.KillX, session.PlayerState.KillY);
                break;
            }
        } while (!projectiles.CheckPosition(camx, camy));
        session.Camera.SetFixed(camx, camy, camangle);

        playScreen.DrawPlayBorder();

        session.FizzleIn = true;

        if (ob.ResolvedStates.ContainsKey("DeathCam"))
            ob.SetState("DeathCam");
    }

    // The death cam's "LET'S SEE THAT AGAIN!" banner (actordefs/deathcam.yaml), drawn by
    // DrawPlayerWeapon. Like the weapon in hand it's never placed in the world: the player's
    // DeathCam state runs its states instead.
    internal Entities.Actors.Actor? DeathCamSprite;

    /// <summary>The player's think while on its DeathCam state: animates the banner.</summary>
    internal void ThinkDeathCam(Entities.Actors.Actor ob)
    {
        DeathCamSprite ??= inventory.CreateActor("DeathCam");
        if (DeathCamSprite != null)
            map.DoActor(DeathCamSprite, session.Tics);
    }
}
