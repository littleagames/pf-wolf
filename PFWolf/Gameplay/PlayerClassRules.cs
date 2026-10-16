using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>
/// What the player classes (actordefs classes descended from Player, listed in game-info's
/// player-classes) say: which there are, their `player.*` factors, starting and most health,
/// lives and armor, eye height, and which items each may pick up. The acting player's class is
/// asked unless another is named.
/// </summary>
internal sealed class PlayerClassRules(GameEngineManager engine, InventoryManager inventory, GameSession session)
{
    // The base player class (actordefs/wolf3d/player.yaml); every player class is it or descends from it
    internal const string BasePlayerClass = "Player";

    // The class being played as, for its `player.*` properties (inherited from Player where it
    // leaves them out)
    string PlayerClass => session.playerstate.playerclass;

    /// <summary>The classes a new game can be played as: game-info's player-classes, else just Player</summary>
    internal List<string> PlayerClasses()
    {
        var classes = engine.GetGameInfo().PlayerClasses.Keys
            .Select(name => (Name: name, Class: FindPlayerClass(name)))
            .Where(c =>
            {
                if (c.Class == null)
                    Console.WriteLine($"game-info player-classes: '{c.Name}' isn't Player or an actordefs class with Player as a parent");
                return c.Class != null;
            })
            .Select(c => c.Class!)
            .ToList();
        return classes.Count > 0 ? classes : [BasePlayerClass];
    }

    /// <summary>The class a new game is played as when none is picked: the first in game-info's player-classes</summary>
    internal string DefaultPlayerClass => PlayerClasses()[0];

    /// <summary>A player class's exact actordefs spelling, or null if the name isn't Player or a class descended from it</summary>
    internal string? FindPlayerClass(string name) =>
        string.Equals(name, BasePlayerClass, StringComparison.OrdinalIgnoreCase)
            ? BasePlayerClass
            : inventory.FindClass(name, BasePlayerClass);

    /// <summary>A factor from the player class (e.g. `player.damagedealt`), 1 when left out; never below 0</summary>
    internal double PlayerFactor(string key) =>
        inventory.GetProperty(PlayerClass, key) is { } value
        && double.TryParse(value.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var factor)
            ? Math.Max(factor, 0)
            : 1;

    /// <summary>How much of the damage their weapons would do the player deals: the class's `player.damagedealt`</summary>
    internal double DamageDealt => PlayerFactor("player.damagedealt");

    /// <summary>How much of the damage they're dealt the player takes, before the skill's: the class's `player.damagetaken`</summary>
    internal double DamageTakenFactor => PlayerFactor("player.damagetaken");

    /// <summary>A new game's and a respawn's health: the Player class's `player.starthealth` (Wolf3D's 100 when left out)</summary>
    internal short StartingHealth => (short)Math.Clamp(inventory.GetIntProperty(PlayerClass, "player.starthealth", 100), 1, short.MaxValue);

    /// <summary>A new game's lives: the Player class's `player.startlives` (Wolf3D's 3 when left out)</summary>
    internal short StartingLives => (short)Math.Clamp(inventory.GetIntProperty(PlayerClass, "player.startlives", 3), 0, short.MaxValue);

    /// <summary>The most health the player can have: the Player class's `player.maxhealth` (Wolf3D's 100 when left out)</summary>
    internal short MaxHealth => (short)Math.Clamp(inventory.GetIntProperty(PlayerClass, "player.maxhealth", 100), 1, short.MaxValue);

    /// <summary>The most lives the player can have: the Player class's `player.maxlives` (Wolf3D's 9 when left out)</summary>
    internal short MaxLives => (short)Math.Clamp(inventory.GetIntProperty(PlayerClass, "player.maxlives", 9), 0, short.MaxValue);

    /// <summary>The most armor points the player can have: the Player class's `player.maxarmor` (no cap but the armor's own when left out)</summary>
    internal short MaxArmor => (short)Math.Clamp(inventory.GetIntProperty(PlayerClass, "player.maxarmor", short.MaxValue), 0, short.MaxValue);

    /// <summary>How high a player's eyes are standing, in texels: their class's `player.eyeheight` (EYEDEFAULT when left out)</summary>
    internal int StandingEyeZ(Entities.PlayerState p) =>
        Math.Clamp(inventory.GetIntProperty(p.playerclass, "player.eyeheight", GameConstants.EYEDEFAULT), GameConstants.MINEYE, GameConstants.MAXEYE);

    /// <summary>
    /// Whether the class may pick up a weapon or ammo: with a `player.weapons` list, only the
    /// weapons in it (or descended from them) and the ammo they use. Anything else, and every
    /// item for a class without the list, may be.
    /// </summary>
    internal bool PlayerClassUsesItem(string item)
    {
        if (inventory.GetProperty(PlayerClass, "player.weapons") is not { } property)
            return true;
        string[] weapons = property switch
        {
            IEnumerable<object> list => list.Select(w => w.ToString() ?? "").ToArray(),
            _ => [property.ToString() ?? ""],
        };

        if (inventory.FindClass(item, "Weapon") != null)
            return inventory.FindClass(item, weapons) != null;
        if (inventory.FindClass(item, "Ammo") != null)
            return weapons.Any(w => inventory.GetIntProperty(w, "weapon.ammouse1", 0) > 0     // the knife's Clip isn't used
                && inventory.GetStringProperty(w, "weapon.ammotype1") is { Length: > 0 } ammo
                && (inventory.FindClass(item, ammo) != null || inventory.GetItemType(ammo) == inventory.GetItemType(item)));
        return true;
    }

    /// <summary>
    /// Whether the player class may pick up an item: not when it's in the item's
    /// `inventory.forbiddento`, or the item has an `inventory.restrictedto` it isn't in, or it's a
    /// weapon or ammo outside the class's `player.weapons`. A class counts as in a list that names
    /// it or a class it descends from.
    /// </summary>
    internal bool PlayerClassCanPickUp(string item)
    {
        // A list of classes, or one class on its own; null when the item doesn't say
        string[]? Classes(string key) => inventory.GetProperty(item, key) switch
        {
            string one => [one],
            IEnumerable<object> list => list.Select(c => c.ToString() ?? "").ToArray(),
            _ => null,
        };
        bool In(string[] classes) => inventory.FindClass(PlayerClass, classes) != null;

        if (Classes("inventory.forbiddento") is { } forbidden && In(forbidden))
            return false;
        if (!PlayerClassUsesItem(item))
            return false;
        return Classes("inventory.restrictedto") is not { } restricted || In(restricted);
    }
}
