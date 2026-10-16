using PFWolf.Entities.Actors;
using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>What the player's stats need from the game</summary>
/// <param name="GodMode">The god mode cheat: 0 off, 1 no damage, 2 no damage and no red flash</param>
/// <param name="DemoActive">Whether a demo is playing or recording: the face doesn't wait on sounds</param>
/// <param name="FullView">Whether the view fills the screen (no status bar to wince on)</param>
/// <param name="SkillDamageTaken">The skill's share of the damage the player takes (the easiest's 0.25)</param>
/// <param name="Died">The player died alone: the level starts again</param>
/// <param name="PlayerDies">The acting player died with others playing: the rest play on</param>
/// <param name="PlayPlayerSound">A sound the acting player makes</param>
internal sealed record PlayerStatsHooks(
    Func<int> GodMode,
    Func<bool> DemoActive,
    Func<bool> FullView,
    Func<double> SkillDamageTaken,
    Action Died,
    Action PlayerDies,
    Action<string> PlayPlayerSound);

/// <summary>
/// The acting player's health, armor, lives and score: taking damage, healing, putting on
/// armor, extra lives, points, the status bar face that goes with them, a new game's loadout,
/// and what a picked-up item gives.
/// </summary>
internal sealed class PlayerStats(
    GameEngineManager engine,
    AssetManager assets,
    VideoManager video,
    InventoryManager inventory,
    StatusBarDrawing statusBar,
    PlayerClassRules classes,
    PlayerWeapons weapons,
    GameSession session,
    PlayerStatsHooks hooks)
{
    Entities.PlayerState playerstate => session.playerstate;

    /// <summary>
    /// Starts the grin if the item, or the weapon it gives, is flagged WEAPON.ALWAYSGRIN: the
    /// status bar's face grin shows while the item's pickup sound plays.
    /// </summary>
    internal void GrinAtPickup(Inventory item, string? pickupSound)
    {
        const string AlwaysGrin = "WEAPON.ALWAYSGRIN";
        var given = item.Properties.TryGetValue("weapongiver.weapon", out var weapon) ? weapon?.ToString() : null;
        if (string.IsNullOrEmpty(pickupSound)
            || !(item.Flags.Contains(AlwaysGrin, StringComparer.OrdinalIgnoreCase)
                || given != null && inventory.CreateActor(given)?.Flags.Contains(AlwaysGrin, StringComparer.OrdinalIgnoreCase) == true))
            return;

        playerstate.GrinSound = pickupSound;
        playerstate.FaceTimes = 38;     // how long demos hold it, as they can't depend on the sound playing
        playerstate.FaceCount = 0;
        statusBar.DrawFace();
    }

    /*
    ===============
    =
    = UpdateFace
    =
    = Calls draw face if time to change
    =
    ===============
    */

    internal void UpdateFace()
    {
        // don't make demo depend on sound playback, nor a game every player's machine plays alike
        if (hooks.DemoActive() || session.gamemode != GameMode.Single)
        {
            if (playerstate.FaceTimes > 0)
            {
                playerstate.FaceTimes--;
                return;
            }
        }
        else if (statusBar.Grinning)
            return;

        playerstate.FaceCount += (int)session.tics;
        if (playerstate.FaceCount > session.random.RndT())
        {
            playerstate.faceframe = (short)(session.random.RndT() >> 6);
            if (playerstate.faceframe == 3)
                playerstate.faceframe = 1;

            playerstate.FaceCount = 0;
            statusBar.DrawFace();
        }
    }

    /*
    ===============
    =
    = TakeDamage
    =
    ===============
    */

    // LastAttacker feeds Died()'s swing-around-to-face-the-killer (GameHost.WL_GAME.cs) and the
    // needle-death face in DrawFace. Every attacker -- enemies (Entities.Actors.Monster) and
    // projectiles (GameHost.WL_ACT2.cs) -- is an Entities.Actors.Actor now.
    internal void TakeDamage(int points, Entities.Actors.Actor attacker)
    {
        playerstate.LastAttacker = attacker;
        ApplyDamageToPlayer(points);
    }

    internal void ApplyDamageToPlayer(int points)
    {
        if (session.gamestate.victoryflag)
            return;
        // With others playing, a dead player lies there until they come back (T_PlayerDead)
        if (session.gamemode != GameMode.Single && playerstate.health <= 0)
            return;
        // The skill's damage-taken (the easiest skill's 0.25 is vanilla's points >> 2), then the class's
        points = (int)(points * Math.Max(hooks.SkillDamageTaken(), 0) * classes.DamageTakenFactor);

        int godmode = hooks.GodMode();
        if (godmode == 0)
        {
            // Armor takes its share of the hit off its points, as far as they go
            int saved = Math.Min(points * playerstate.armorpercent / 100, playerstate.armor);
            playerstate.armor -= (short)saved;
            if (playerstate.armor == 0)
                playerstate.armorpercent = 0;
            points -= saved;

            playerstate.health -= (short)points;
        }

        // How others see it (their body's Pain or Pain1, as an enemy's by its health left)
        if (points > 0 && playerstate.health > 0)
        {
            playerstate.PainTics = 10;
            playerstate.PainAlt = (playerstate.health & 1) == 0;
        }

        if (playerstate.health <= 0)
        {
            playerstate.health = 0;
            if (session.gamemode == GameMode.Single)
                hooks.Died();                   // Died(): the level starts again
            else
                hooks.PlayerDies();             // the rest play on (GameHost.Players.cs)
        }

        if (godmode != 2 && session.ActingIsLocal)
            video.StartDamageFlash(points);

        statusBar.DrawHealth();
        statusBar.DrawArmor();
        statusBar.DrawFace();

        // A heavy hit makes BJ wince (Spear of Destiny); demos rely on the face's count restarting.
        // Vanilla only restarted it with the status bar showing; here it always does, so how a
        // demo plays doesn't depend on the view size.
        if (statusBar.StatusBar.Get("face") is { Ouch.Length: > 0 } face && points > face.OuchDamage
            && playerstate.health != 0 && godmode == 0 && assets.Exists<Assets.GraphicAsset>(face.Ouch))
        {
            if (!hooks.FullView() && session.ActingIsLocal)
                statusBar.StatusDrawFace(face.Ouch);
            playerstate.FaceCount = 0;
        }
    }

    /// <summary>
    /// Puts on a BasicArmor class (native.yaml): an armor replaces the player's when it has more
    /// points; a bonus (armor.maxsaveamount) adds to it. False when it would change nothing.
    /// </summary>
    internal bool TryGiveArmor(string armorClass)
    {
        int amount = inventory.GetIntProperty(armorClass, "armor.saveamount", 0);
        short percent = (short)Math.Clamp(inventory.GetIntProperty(armorClass, "armor.savepercent", 0), 0, 100);

        if (inventory.GetProperty(armorClass, "armor.maxsaveamount") != null)
        {
            int cap = Math.Min(inventory.GetIntProperty(armorClass, "armor.maxsaveamount", 0), classes.MaxArmor);
            if (playerstate.armor >= cap || amount <= 0)
                return false;
            if (playerstate.armor == 0)
                playerstate.armorpercent = percent;
            playerstate.armor = (short)Math.Min(playerstate.armor + amount, cap);
        }
        else
        {
            amount = Math.Min(amount, classes.MaxArmor);
            if (playerstate.armor >= amount)
                return false;
            playerstate.armor = (short)amount;
            playerstate.armorpercent = percent;
        }

        statusBar.DrawArmor();
        return true;
    }

    /// <summary>Heals the player by <paramref name="points"/>, up to <paramref name="upTo"/> (their max health when null)</summary>
    internal void HealSelf(int points, int? upTo = null)
    {
        int cap = Math.Min(upTo ?? classes.MaxHealth, classes.MaxHealth);
        playerstate.health = (short)Math.Min(playerstate.health + points, Math.Max(cap, playerstate.health));

        statusBar.DrawHealth();
        statusBar.DrawFace();
    }

    /*
    ===============
    =
    = GiveExtraMan
    =
    ===============
    */

    internal void GiveExtraMan()
    {
        if (playerstate.lives < classes.MaxLives)
            playerstate.lives++;
        statusBar.DrawLives();
        if (statusBar.StatusBar.Get("lives")?.Sound is { Length: > 0 } sound)
            hooks.PlayPlayerSound(sound);
    }

    /*
    ===============
    =
    = GivePoints
    =
    ===============
    */

    /// <summary>The score between extra lives (game-info extra-life-score); 0 for none</summary>
    internal int ExtraLifeScore => Math.Max(engine.GetGameInfo().ExtraLifeScore, 0);

    internal void GivePoints(int points)
    {
        playerstate.score += points;
        while (ExtraLifeScore > 0 && playerstate.score >= playerstate.nextextra)
        {
            playerstate.nextextra += ExtraLifeScore;
            GiveExtraMan();
        }
        statusBar.DrawScore();
    }

    /// <summary>
    /// New game / respawn loadout: the Player class's `player.startitem` (item: amount), with
    /// the best weapon in it selected.
    /// </summary>
    internal void GiveStartingInventory()
    {
        inventory.Clear();
        playerstate.armor = playerstate.armorpercent = 0;

        var playerClass = playerstate.playerclass;
        if (inventory.GetProperty(playerClass, "player.startitem") is IDictionary<object, object> items)
        {
            foreach (var (item, amount) in items)
            {
                // Armor isn't held: it's put on (the amount doesn't matter)
                if (inventory.FindClass(item.ToString() ?? "", "BasicArmor") is { } armor)
                    TryGiveArmor(armor);
                else
                    inventory.Give(item.ToString() ?? "", Convert.ToInt32(amount));
            }
        }
        else
            Console.WriteLine($"No player.startitem on actordefs class {playerClass}: the player starts with nothing.");

        playerstate.weapon = playerstate.chosenweapon = weapons.BestWeapon();
    }

    /// <summary>Whether giving the item would do anything now (a health item, unless health is full); without giving it</summary>
    internal bool CouldTakeInventory(Inventory item)
    {
        if (item is not Entities.Actors.Health)
            return true;
        int itemMax = inventory.GetIntProperty(item.Name, "inventory.maxamount", 0);
        int cap = itemMax > 0 ? Math.Min(itemMax, classes.MaxHealth) : classes.MaxHealth;
        return playerstate.health < cap;
    }

    /// <summary>
    /// Gives the player <paramref name="amount"/> of <paramref name="item"/>. Returns false
    /// when nothing could be taken, so the pickup should be left where it is.
    /// </summary>
    internal bool TryApplyInventory(Inventory item, int amount)
    {
        switch (item)
        {
            case Entities.Actors.Health:
            {
                // A health item's inventory.maxamount is the most it heals to (Wolf3D's blood and
                // gibs only help when nearly dead); 0 is up to the player's max health
                int itemMax = inventory.GetIntProperty(item.Name, "inventory.maxamount", 0);
                int cap = itemMax > 0 ? Math.Min(itemMax, classes.MaxHealth) : classes.MaxHealth;
                if (playerstate.health >= cap)
                    return false;
                HealSelf(amount, cap);
                return true;
            }
            case Entities.Actors.Ammo:
                return weapons.GiveAmmo(item.Name, amount) > 0;
            case Entities.Actors.BasicArmor:
                return TryGiveArmor(item.Name);
            case Entities.Actors.Weapon weapon:
                return weapons.TryGiveWeapon(weapon);
            case Entities.Actors.Key:
                if (inventory.Give(item.Name, amount) == 0)
                    return false;
                statusBar.DrawKeys();
                return true;
            case Entities.Actors.ScoreItem:
                GivePoints(amount);
                return true;
            default:
                return true;
        }
    }
}
