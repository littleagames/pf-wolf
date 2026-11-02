namespace PFWolf.Entities.Actors;

/*
=============================================================================

                            ENEMIES PICKING THINGS UP

End of Destiny's CheckEnemyTile / GetEnemyBonus: an enemy walking (chasing, patrolling, dodging)
that reaches the middle of a tile takes what lies there, if the item's class says what that does
to an enemy:

  monster.pickup.heal: n       it gains n health (no cap: EoD's food, dog food and first aid)
  monster.pickup.kill: true    it dies (a landmine, the death artifact)
  monster.pickup.uses: n       how many enemies it takes before it goes (1; the death artifact 4)
  monster.pickup.classes: [..] only these classes (and those derived from them) take it
  monster.pickup.sound: name   played where the enemy is

An item that heals or kills goes once it's used up. The player picks it up as ever. Anything
else an enemy does with what it walks onto is a script's: game.onenter (End of Destiny's Greed).

=============================================================================
*/

internal partial record Monster
{
    // What the enemy takes from the tile it's reached the middle of (ArriveOnTile)
    private void TakePickups()
    {
        if (World == null || !RuntimeFlags.Has(ObjFlags.Shootable))
            return;

        foreach (var item in MapManager.GetActors().OfType<Inventory>()
                     .Where(i => !i.IsRemoved && i.TileX == TileX && i.TileY == TileY).ToList())
        {
            int heal = PickupInt(item, "monster.pickup.heal");
            bool kill = PickupFlag(item, "monster.pickup.kill");
            if (heal <= 0 && !kill)
                continue;
            if (item.Properties.TryGetValue("monster.pickup.classes", out var classes) && classes is IEnumerable<object> list
                && World.Inventory.FindClass(Name, list.Select(c => c?.ToString() ?? "").ToArray()) == null)
                continue;

            if (PickupText(item, "monster.pickup.sound") is { } sound)
                PlaySoundAtActor(sound, this);
            if (heal > 0)
                Hitpoints += (short)heal;
            if (++item.Temp1 >= Math.Max(PickupInt(item, "monster.pickup.uses"), 1))
                MapManager.RemoveActor(item);
            if (kill)
            {
                Kill(null);
                return;
            }
        }
    }

    static string? PickupText(Actor item, string key) =>
        item.Properties.TryGetValue(key, out var value) && value?.ToString() is { Length: > 0 } text ? text : null;

    static int PickupInt(Actor item, string key) =>
        item.Properties.TryGetValue(key, out var value) && int.TryParse(value?.ToString(), out var n) ? n : 0;

    static bool PickupFlag(Actor item, string key) =>
        item.Properties.TryGetValue(key, out var value) && (value is true || bool.TryParse(value?.ToString(), out var b) && b);
}
