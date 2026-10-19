using System;
using System.Collections.Generic;
using System.Text;

namespace PFWolf.Entities.Actors;

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

// A timed power-up (Gameplay/Powerups.cs): held as the tics it has left
internal record Powerup : Inventory
{

}

// A backpack: held, it raises item caps to their ammo.backpackmaxamount (PlayerStats.GiveBackpack)
internal record BackpackItem : Inventory
{

}