using PFWolf.Entities.Actors;
using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>What the player's stats need from the game</summary>
/// <param name="SkillDamageTaken">The skill's share of the damage the player takes (the easiest's 0.25)</param>
/// <param name="PlayerDies">The acting player died with others playing: the rest play on</param>
/// <param name="PlayPlayerSound">A sound the acting player makes</param>
/// <param name="ShowTimer">Shows (or with null text takes down) the message with a key, in a style (HudMessageManager.ShowKeyed): a power-up's timer</param>
/// <param name="ShowAttacker">Shows who's attacking the local player (game-info attack-message), with Attack Info on</param>
internal sealed record PlayerStatsHooks(
    Func<double> SkillDamageTaken,
    Action PlayerDies,
    Action<string> PlayPlayerSound,
    Action<string, string?, string?>? ShowTimer = null,
    Action<Entities.Actors.Actor>? ShowAttacker = null);

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
    Configuration.GameSettings settings,
    PlayerStatsHooks hooks)
{
    Entities.PlayerState PlayerState => session.PlayerState;

    /// <summary>
    /// Starts the grin if the item, or the weapon it gives, is flagged WEAPON.ALWAYSGRIN: the
    /// status bar's face grin shows while the item's pickup sound plays.
    /// </summary>
    internal void GrinAtPickup(Inventory item, string? pickupSound)
    {
        const string AlwaysGrin = "WEAPON.ALWAYSGRIN";
        var given = item.Properties.TryGetValue("weapongiver.weapon", out var weapon) ? weapon?.ToString() : null;
        if (string.IsNullOrEmpty(pickupSound)
            || !(item.HasFlag(AlwaysGrin)
                || given != null && inventory.CreateActor(given)?.Flags.Contains(AlwaysGrin, StringComparer.OrdinalIgnoreCase) == true))
            return;

        PlayerState.GrinSound = pickupSound;
        PlayerState.FaceTimes = 38;     // how long demos hold it, as they can't depend on the sound playing
        PlayerState.FaceCount = 0;
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
        if ((session.DemoPlayback || session.DemoRecord) || session.GameMode != GameMode.Single)
        {
            if (PlayerState.FaceTimes > 0)
            {
                PlayerState.FaceTimes--;
                return;
            }
        }
        else if (statusBar.Grinning)
            return;

        PlayerState.FaceCount += (int)session.Tics;
        if (PlayerState.FaceCount > session.Random.RndT())
        {
            PlayerState.FaceFrame = (short)(session.Random.RndT() >> 6);
            if (PlayerState.FaceFrame == 3)
                PlayerState.FaceFrame = 1;

            PlayerState.FaceCount = 0;
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

    // LastAttacker feeds Died()'s swing-around-to-face-the-killer (Gameplay/PlayerDeath.cs) and the
    // needle-death face in DrawFace. Every attacker -- enemies (Entities.Actors.Monster) and
    // projectiles (Gameplay/Projectiles.cs) -- is an Entities.Actors.Actor now.
    internal void TakeDamage(int points, Entities.Actors.Actor attacker)
    {
        PlayerState.LastAttacker = attacker;
        // Who it was, with Attack Info on (Blake Stone's REBA, bstone's TakeDamage)
        // (a shot's: whoever fired it)
        if ((attacker as Monster ?? attacker?.Shooter as Monster) is { } foe && session.ActingIsLocal && settings.Options.AttackInfo)
            hooks.ShowAttacker?.Invoke(foe);
        ApplyDamageToPlayer(points);
    }

    internal void ApplyDamageToPlayer(int points)
    {
        if (session.GameState.VictoryFlag)
            return;
        // With others playing, a dead player lies there until they come back (T_PlayerDead)
        if (session.GameMode != GameMode.Single && PlayerState.Health <= 0)
            return;
        // The skill's damage-taken (the easiest skill's 0.25 is vanilla's points >> 2), then the class's,
        // then a manned gun's cover (weapon.mountdamage: Gameplay/MountedWeapons.cs)
        points = (int)(points * Math.Max(hooks.SkillDamageTaken(), 0) * classes.DamageTakenFactor);
        if (PlayerState.Mount != null && PlayerState.MountGun is { } gun)
            points = points * Math.Max(inventory.GetIntProperty(gun, "weapon.mountdamage", 100), 0) / 100;

        int godmode = session.GodMode;
        if (godmode == 0 && Powerups.Invulnerable(inventory))
            godmode = 2;                // unhurt, without the red flash (End of Destiny's god mode)
        if (godmode == 0)
        {
            // Armor takes its share of the hit off its points, as far as they go
            int saved = Math.Min(points * PlayerState.ArmorPercent / 100, PlayerState.Armor);
            PlayerState.Armor -= (short)saved;
            if (PlayerState.Armor == 0)
                PlayerState.ArmorPercent = 0;
            points -= saved;

            PlayerState.Health -= (short)points;
        }

        // How others see it (their body's Pain or Pain1, as an enemy's by its health left)
        if (points > 0 && PlayerState.Health > 0)
        {
            PlayerState.PainTics = 10;
            PlayerState.PainAlt = (PlayerState.Health & 1) == 0;
        }

        if (PlayerState.Health <= 0)
        {
            PlayerState.Health = 0;
            if (session.GameMode == GameMode.Single)
                session.PlayState = PlayState.Died;                   // Died(): the level starts again
            else
                hooks.PlayerDies();             // the rest play on (Gameplay/Players.cs)
        }

        if (godmode != 2 && session.ActingIsLocal)
            video.StartDamageFlash(points);

        statusBar.DrawHealth();
        statusBar.DrawArmor();
        statusBar.DrawFace();

        // A heavy hit makes BJ wince (Spear of Destiny), or a player class with its own face
        // (player.faceouch); demos rely on the face's count restarting. Vanilla only restarted it
        // with the status bar showing; here it always does, so how a demo plays doesn't depend on
        // the view size.
        if (statusBar.StatusBar.Get("face") is { } face && points > face.OuchDamage
            && PlayerState.Health != 0 && godmode == 0
            && statusBar.OuchPic() is { } ouch && assets.Exists<Assets.GraphicAsset>(ouch))
        {
            if (!(settings.ViewSize == 21) && session.ActingIsLocal)
                statusBar.StatusDrawFace(ouch);
            PlayerState.FaceCount = 0;
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
            if (PlayerState.Armor >= cap || amount <= 0)
                return false;
            if (PlayerState.Armor == 0)
                PlayerState.ArmorPercent = percent;
            PlayerState.Armor = (short)Math.Min(PlayerState.Armor + amount, cap);
        }
        else
        {
            amount = Math.Min(amount, classes.MaxArmor);
            if (PlayerState.Armor >= amount)
                return false;
            PlayerState.Armor = (short)amount;
            PlayerState.ArmorPercent = percent;
        }

        statusBar.DrawArmor();
        return true;
    }

    /// <summary>Heals the player by <paramref name="points"/>, up to <paramref name="upTo"/> (their max health when null)</summary>
    internal void HealSelf(int points, int? upTo = null)
    {
        int cap = Math.Min(upTo ?? classes.MaxHealth, classes.MaxHealth);
        PlayerState.Health = (short)Math.Min(PlayerState.Health + points, Math.Max(cap, PlayerState.Health));

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
        if (PlayerState.Lives < classes.MaxLives)
            PlayerState.Lives++;
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
        PlayerState.Score += points;
        while (ExtraLifeScore > 0 && PlayerState.Score >= PlayerState.NextExtra)
        {
            PlayerState.NextExtra += ExtraLifeScore;
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
        PlayerState.Armor = PlayerState.ArmorPercent = 0;

        var playerClass = PlayerState.RoleClass ?? PlayerState.PlayerClass;
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

        PlayerState.Weapon = PlayerState.ChosenWeapon = weapons.BestWeapon();
    }

    /// <summary>Whether giving the item would do anything now (a health item, unless health is full); without giving it</summary>
    internal bool CouldTakeInventory(Inventory item)
    {
        if (item is not Entities.Actors.Health)
            return true;
        int itemMax = inventory.GetIntProperty(item.Name, "inventory.maxamount", 0);
        int cap = itemMax > 0 ? Math.Min(itemMax, classes.MaxHealth) : classes.MaxHealth;
        return PlayerState.Health < cap;
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
                if (PlayerState.Health >= cap)
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
            case Entities.Actors.Powerup:
                return GivePowerup(item.Name);
            case Entities.Actors.BackpackItem:
                return GiveBackpack(item.Name);
            default:
                return true;
        }
    }

    /// <summary>
    /// A backpack (BackpackItem): held from now on (to the next death or a new game, as an item
    /// with inventory.interhubamount 1), every item type's cap is its `ammo.backpackmaxamount`
    /// where it has one (InventoryManager.GetMaxAmount), and each type with an
    /// `ammo.backpackamount` is given that much. Another one only gives the ammo.
    /// </summary>
    internal bool GiveBackpack(string backpack)
    {
        bool got = inventory.Give(backpack, 1) > 0;
        var types = inventory.GetClassesDerivedFrom("Inventory")
            .Select(inventory.GetItemType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var type in types)
        {
            int amount = inventory.GetIntProperty(type, "ammo.backpackamount", 0);
            if (amount <= 0)
                continue;
            got |= inventory.FindClass(type, "Ammo") != null ? weapons.GiveAmmo(type, amount) > 0 : inventory.Give(type, amount) > 0;
        }
        weapons.DrawAmmo();
        return got;
    }

    /*
    ===============
    =
    = POWER-UPS (Gameplay/Powerups.cs)
    =
    ===============
    */

    // The weapon each player had before a power-up put another in their hand, back in it when that ends
    readonly Dictionary<Entities.PlayerState, string?> _weaponBeforePowerup = [];

    /// <summary>Starts a power-up, or adds its duration to what's left of it; false for one with none</summary>
    internal bool GivePowerup(string powerup)
    {
        int duration = inventory.GetIntProperty(powerup, "powerup.duration", 0);
        if (duration <= 0)
            return false;

        if (inventory.GetStringProperty(powerup, "powerup.weapon") is { Length: > 0 } weapon && inventory.Has(weapon))
        {
            if (!Powerups.HoldsAWeapon(inventory))
                _weaponBeforePowerup[PlayerState] = PlayerState.Weapon;
            if (PlayerState.Weapon != weapon)
            {
                PlayerState.Weapon = PlayerState.ChosenWeapon = weapon;
                weapons.DrawWeapon();
                weapons.DrawAmmo();
            }
        }

        inventory.Give(powerup, duration);
        ShowPowerupTimers();
        statusBar.DrawFace();
        return true;
    }

    /// <summary>Each tic of the acting player's think: their power-ups' time runs down, and those that run out end</summary>
    internal void TickPowerups()
    {
        var running = Powerups.Running(inventory).ToList();
        if (running.Count == 0)
            return;

        foreach (var powerup in running)
        {
            inventory.Take(powerup, Math.Min((int)session.Tics, inventory.GetCount(powerup)));
            if (inventory.Has(powerup))
            {
                if (Powerups.Flag(inventory, powerup, "powerup.flash") && session.ActingIsLocal)
                    video.StartBonusFlash();
                continue;
            }
            EndPowerup(powerup);
        }
        ShowPowerupTimers();
    }

    void EndPowerup(string powerup)
    {
        if (inventory.GetStringProperty(powerup, "powerup.endsound") is { Length: > 0 } sound)
            hooks.PlayPlayerSound(sound);

        // The weapon it put in hand goes back to the one before, if it's still the one held
        if (inventory.GetStringProperty(powerup, "powerup.weapon") is { Length: > 0 } weapon && !Powerups.HoldsAWeapon(inventory)
            && _weaponBeforePowerup.Remove(PlayerState, out var before)
            && string.Equals(PlayerState.Weapon, weapon, StringComparison.OrdinalIgnoreCase)
            && before != null && before != weapon && inventory.Has(before) && weapons.CanFire(before))
        {
            PlayerState.Weapon = PlayerState.ChosenWeapon = before;
            weapons.DrawWeapon();
            weapons.DrawAmmo();
        }
        statusBar.DrawFace();
    }

    // The local player's power-up timers (powerup.timerstyle), kept up to date: gone once one's over
    void ShowPowerupTimers()
    {
        if (hooks.ShowTimer == null || !session.ActingIsLocal)
            return;
        foreach (var powerup in inventory.GetClassesDerivedFrom(Powerups.BaseClass))
        {
            if (inventory.GetStringProperty(powerup, "powerup.timerstyle") is not { Length: > 0 } style)
                continue;
            int left = inventory.GetCount(powerup);
            hooks.ShowTimer(Powerups.TimerKey(powerup), left > 0 ? Powerups.TimerText(left) : null, style);
        }
    }
}
