using System;
using System.Collections.Generic;
using System.Text;

namespace Wolf3D.Entities.Actors;

internal abstract record Inventory : Actor
{
}

internal record CustomInventory : Actor
{
}


internal record Ammo : Inventory
{

}

internal record Health : Inventory
{

}

internal record Key : Inventory
{

}

internal record ScoreItem : Inventory
{

}