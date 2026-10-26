using static PFWolf.Gameplay.GameConstants;
using static PFWolf.Gameplay.LineOfSight;
using static PFWolf.Rendering.TrigTables;

namespace PFWolf.Entities.Actors;

/// <summary>
/// Blake Stone's informant (the scientist who isn't an enemy): it never goes after the player,
/// isn't a kill and leaves nothing, and shot (and still alive) it just flinches. Talked to, it
/// gives a hint: one of the map's hints for the room it's in when the map has any there, else one
/// of the map's general ones; talked to again, its gifts (`talk.gifts`, with `talk.giftmessages`).
/// Killing one warns the player (`talk.killedmessage`). An actordefs class is one with
/// `parent: Informant`, or named Informant.
/// </summary>
internal record Informant : BlakeMonster
{
    internal override bool SightPlayer() => false;
    internal override bool IsKill => false;
    protected override bool LeavesDrops => false;
    protected override bool NoticesWhenHurt => false;

    internal override void OnSpawned(int tilex, int tiley)
    {
        base.OnSpawned(tilex, tiley);
        RuntimeFlags |= ObjFlags.HasAmmo | ObjFlags.HasTokens;
        SeekX = SeekY = 0xff;     // no hint chosen yet
    }

    // Worth nothing; shooting one warns the player, the first time and now and then after
    protected override void AwardKillPoints()
    {
        if (!LevelAI.ShouldWarnKilledInformant())
            return;
        if (PropertyStrings("talk.killedmessage") is [var message, ..])
            HudMessageManager.Show(Managers.HudMessageKind.Other, message, PropertyStrings("talk.style").FirstOrDefault());
    }

    internal override bool TalkTo()
    {
        string? said = null;

        // Asked again, it hands over what it has
        if (RuntimeFlags.Has(ObjFlags.Interrogated))
        {
            var gifts = PropertyStrings("talk.gifts");
            var giftMessages = PropertyStrings("talk.giftmessages");
            foreach (var (flag, index) in new[] { (ObjFlags.HasAmmo, 0), (ObjFlags.HasTokens, 1) })
            {
                if (!RuntimeFlags.Has(flag) || index >= gifts.Count
                    || InventoryManager.CreateActor(gifts[index]) is not Inventory gift || !CouldTakeInventory(gift))
                    continue;
                int amount = gift.Properties.TryGetValue("inventory.amount", out var a) ? Convert.ToInt32(a) : 1;
                if (!TryApplyInventory(gift, amount))
                    continue;
                RuntimeFlags &= ~flag;
                said = HudMessageManager.Localize(giftMessages.ElementAtOrDefault(index) ?? "");
                break;
            }
        }

        if (said == null)
        {
            said = Hint();
            RuntimeFlags |= ObjFlags.Interrogated;
        }

        Say(said);
        return true;
    }

    /// <summary>
    /// Its hint: one of the map's hints for the room it's in, if the map has any, else one of
    /// its general ones. It keeps to the one it picked (SeekX for a room's, SeekY for a general
    /// one; Ammo the room it picked it in).
    /// </summary>
    private string? Hint()
    {
        // Just short of all the floor's points (97-99%), it says where an enemy left to kill
        // is, or else treasure left to take (bstone's informant)
        if (LastPointsHint() is { } lastPoints)
            return lastPoints;

        var listName = PropertyStrings("talk.hints").FirstOrDefault();
        if (string.IsNullOrEmpty(listName))
            return null;

        var placed = MapManager.Hints.GetValueOrDefault(listName) ?? [];
        var roomHints = placed.Where(h => h.Area == AreaNumber).ToList();
        if (roomHints.Count > 0)
        {
            if (Ammo != AreaNumber)
                SeekX = 0xff;
            Ammo = AreaNumber;
            if (SeekX == 0xff || SeekX >= roomHints.Count)
                SeekX = (byte)(NextRandom() % roomHints.Count);
            return HintText(listName, roomHints[SeekX].Message);
        }

        var general = placed.Where(h => h.Area == 0xff).ToList();
        if (general.Count == 0)
            return RandomSaying(listName);
        if (SeekY == 0xff || SeekY >= general.Count)
            SeekY = (byte)(NextRandom() % general.Count);
        return HintText(listName, general[SeekY].Message);
    }

    /// <summary>
    /// With the floor's points ratio at 97 to 99%: where the first enemy still to count as a kill
    /// is ($BS_HINT_ENEMY), else the first treasure still lying about ($BS_HINT_TREASURE), else
    /// $BS_HINT_ALLTREASURE; each with the tile, and the player's own after it. Null otherwise.
    /// </summary>
    private string? LastPointsHint()
    {
        int total = GameState.PointsTotal;
        int percent = total == 0 ? 100 : GameState.PointsCount * 100 / total;
        if (percent <= 96 || percent >= 100)
            return null;

        var actors = MapManager.GetActors().Where(a => !a.IsRemoved).ToList();
        static string Where(Actor a) => $"{a.TileX},{a.TileY}";
        var player = Where(Player);

        string text;
        if (actors.OfType<Monster>().FirstOrDefault(m => m.CountsAsKill && m.RuntimeFlags.Has(ObjFlags.Shootable)) is { } enemy)
            text = string.Format(HudMessageManager.Localize("$BS_HINT_ENEMY"), ActorTag(enemy), Where(enemy), player);
        else if (actors.OfType<Inventory>().FirstOrDefault(i => i.HasFlag("COUNTITEM")) is { } treasure)
            text = string.Format(HudMessageManager.Localize(treasure.PropertyBool("tag.plural") ? "$BS_HINT_TREASURES" : "$BS_HINT_TREASURE"),
                SpacedName(ActorTag(treasure)), Where(treasure), player);
        else
            text = HudMessageManager.Localize("$BS_HINT_ALLTREASURE");
        return text.ToUpperInvariant();
    }

    // A class name as words (MoneyBag: Money Bag), for a thing with no tag of its own
    private static string SpacedName(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ");
}
