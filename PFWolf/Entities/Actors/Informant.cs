using static PFWolf.Program;

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
        RuntimeFlags |= objflags.FL_HASAMMO | objflags.FL_HASTOKENS;
        SeekX = SeekY = 0xff;     // no hint chosen yet
    }

    // Worth nothing; shooting one warns the player, the first time and now and then after
    protected override void AwardKillPoints()
    {
        if (!_mapManager.AI.ShouldWarnKilledInformant())
            return;
        if (PropertyStrings("talk.killedmessage") is [var message, ..])
            _hudMessageManager.Show(Managers.HudMessageKind.Other, message, PropertyStrings("talk.style").FirstOrDefault());
    }

    internal override bool TalkTo()
    {
        string? said = null;

        // Asked again, it hands over what it has
        if (RuntimeFlags.HasFlag(objflags.FL_INTERROGATED))
        {
            var gifts = PropertyStrings("talk.gifts");
            var giftMessages = PropertyStrings("talk.giftmessages");
            foreach (var (flag, index) in new[] { (objflags.FL_HASAMMO, 0), (objflags.FL_HASTOKENS, 1) })
            {
                if (!RuntimeFlags.HasFlag(flag) || index >= gifts.Count
                    || _inventoryManager.CreateActor(gifts[index]) is not Inventory gift || !CouldTakeInventory(gift))
                    continue;
                int amount = gift.Properties.TryGetValue("inventory.amount", out var a) ? Convert.ToInt32(a) : 1;
                if (!TryApplyInventory(gift, amount))
                    continue;
                RuntimeFlags &= ~flag;
                said = _hudMessageManager.Localize(giftMessages.ElementAtOrDefault(index) ?? "");
                break;
            }
        }

        if (said == null)
        {
            said = Hint();
            RuntimeFlags |= objflags.FL_INTERROGATED;
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
        var listName = PropertyStrings("talk.hints").FirstOrDefault();
        if (string.IsNullOrEmpty(listName))
            return null;

        var placed = _mapManager.Hints.GetValueOrDefault(listName) ?? [];
        var roomHints = placed.Where(h => h.Area == AreaNumber).ToList();
        if (roomHints.Count > 0)
        {
            if (Ammo != AreaNumber)
                SeekX = 0xff;
            Ammo = AreaNumber;
            if (SeekX == 0xff || SeekX >= roomHints.Count)
                SeekX = (byte)(US_RndT() % roomHints.Count);
            return HintText(listName, roomHints[SeekX].Message);
        }

        var general = placed.Where(h => h.Area == 0xff).ToList();
        if (general.Count == 0)
            return RandomSaying(listName);
        if (SeekY == 0xff || SeekY >= general.Count)
            SeekY = (byte)(US_RndT() % general.Count);
        return HintText(listName, general[SeekY].Message);
    }
}
