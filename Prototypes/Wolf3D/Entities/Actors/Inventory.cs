using System;
using System.Collections.Generic;
using System.Text;

namespace Wolf3D.Entities.Actors;

internal abstract record Inventory : Actor
{
}

internal record CustomInventory : Inventory
{
}


internal record Ammo : Inventory
{

}

internal record Health : Inventory
{

}

// Not held: picking it up sets the player's armor points and how much of each hit they absorb.
// (Not "Armor": that's Wolf3D's suit of armor decoration.)
internal record BasicArmor : Inventory
{

}

internal record Key : Inventory
{

}

internal record ScoreItem : Inventory
{

}