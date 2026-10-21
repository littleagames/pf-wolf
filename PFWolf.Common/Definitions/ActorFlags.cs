namespace PFWolf.Definitions;

/// <summary>An actordefs flag the engine checks (a class's `flags:`), and what it does</summary>
/// <param name="Name">The flag, as the YAML writes it (any case)</param>
/// <param name="Description">What it does</param>
public sealed record ActorFlag(string Name, string Description);

/// <summary>
/// The actordefs flags the engine checks, for the documentation and the tests that keep the YAML
/// and the code in step with it (a flag the code checks that isn't here, or one the YAML sets that
/// the code doesn't check, fails them). Add a flag here as the code starts checking it.
/// </summary>
public static class ActorFlags
{
    /// <summary>Every flag, by name</summary>
    public static IReadOnlyList<ActorFlag> All { get; } =
    [
        new("AMBUSH", "Always placed ready to ambush (deaf until it sees the player), whatever floor code it stands on; other enemies only on the ambush code (the bosses, the Pac-Man ghosts)"),
        new("BLIND", "Never goes after the player: it just goes its way (Blake Stone's volatile material transports)"),
        new("BOSS", "A boss: kept off blocktile: bosses tiles, and spared by a weapon with weapon.spares: BOSS. No class in pfwolf.pk3 has it."),
        new("BRIGHT", "Drawn at full light, unshaded, in every state (a frame's bright modifier does it for one frame)"),
        new("BURSTONPLAYER", "A projectile: bursts on the player (or enemy) it hits, going to its Death state, as on a wall (Blake Stone's spit and shots)"),
        new("CHASEDIR", "Closes in straight, without dodging (Blake Stone's floating bomb)"),
        new("COUNTITEM", "Treasure: counts toward the level's treasure ratio"),
        new("FRIENDLY", "Starts friendly: patrolling, it only looks for the player after a noise (Blake Stone's scientists)"),
        new("HURTMONSTERS", "A projectile: hurts the enemies it flies into too, not only players, never its shooter (a trap's fireball)"),
        new("INVENTORY.ALWAYSPICKUP", "Picked up even when it gives the player nothing (health at full, a key already held), rather than left on the floor"),
        new("KEEPTILEONDEATH", "Dying, it keeps the tile it holds rather than moving to the one it's over (Mecha Hitler, so the Hitler it becomes doesn't jump a tile)"),
        new("NODISGUISE", "A decoration no Prop Hunt prop can look like (a puddle flat on the floor)"),
        new("NODOORS", "Never opens doors (Wolf3D's dogs and fake Hitlers, Blake Stone's liquid alien)"),
        new("NOLOCKEDDOORS", "Doesn't open a door that needs a key (Blake Stone's enemies)"),
        new("NOREVIVE", "A reviver can't bring it back (the bosses)"),
        new("NOTCOUNTED", "Not a kill: left out of the level's kill total and ratio (crates, Goldfire, who keeps coming back)"),
        new("NOTICEWHENSEEN", "Notices the player as soon as the player can see it (Blake Stone's aliens)"),
        new("NOTSOLID", "Doesn't block while it lives: the player and actors pass under or through it (hanging turrets, electro-spheres)"),
        new("PATROLPOINT", "A patrol point (the arrows): a patroller reaching its tile turns the way it points"),
        new("PATROLTURNS", "A patroller that's blocked turns aside rather than stopping"),
        new("PHASEDOORS", "Drifts straight through doors without opening them (ghosts, Spectres)"),
        new("POINTSONCE", "Its points are paid only for the first kill (Spectres, which come back)"),
        new("PROJECTILE", "A projectile: its sprite turns by its heading rather than the way it walks, and no Prop Hunt prop can look like it"),
        new("RANDOMTURN", "A PATROLTURNS patroller also turns now and then at random"),
        new("SMART", "Runs for supplies or a door while out of ammo, at half health or less, or after turning mean when talked to (rent-a-cops, pro guards, scientists)"),
        new("SOLID", "Its whole tile is blocked to the player, enemies and projectiles (barrels, pillars, tables), and the automap marks it"),
        new("STATIONARY", "Never moves"),
        new("STEPBACK", "Walking into the player, it heads back to the tile it came from"),
        new("TALKATIVE", "Can be talked to: use, facing it, close by (Blake Stone's scientists)"),
        new("TOUCHDAMAGE", "Hurts the player on contact (ghosts, Spectres)"),
        new("WALLSPRITE", "Drawn as a flat panel standing in its tile, facing its map angles, instead of a billboard (fences, gates, grilles); it blocks along its line"),
        new("WALLSPRITE.PASSABLE", "A wall sprite that's only seen: nothing moving is stopped by it (an arch, cobwebs); it still blocks sight and hitscan unless it's SHOOTTHROUGH"),
        new("WALLSPRITE.PROJECTILETHROUGH", "A wall sprite projectiles fly through"),
        new("WALLSPRITE.SHOOTTHROUGH", "A wall sprite sight and hitscan pass through (a grille, a chain-link fence)"),
        new("WALLSPRITE.TWOSIDED", "A wall sprite that reads the same way round from behind, not mirrored"),
        new("WARPSITE", "A warp site marker: where its warpsite.class warps in"),
        new("WEAPON.ALWAYSGRIN", "Picking it up (or the weapon it gives) makes the status bar face grin while its pickup sound plays (the chaingun)"),
    ];

    /// <summary>A flag by its name, ignoring case; a leading ~ (which takes a flag away) is left off</summary>
    public static ActorFlag? Find(string name)
    {
        var flag = name.TrimStart('~');
        return All.FirstOrDefault(f => f.Name.Equals(flag, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The flags as a record's fields, for the documentation (each its own field, by name)</summary>
    public static FieldSchema Schema() => FieldSchema.Record("flags",
        "The flags an actordefs class can have (`flags: [SOLID, AMBUSH]`). A class has its parents' as well; ~FLAG takes one of them away.",
        [.. All.Select(f => FieldSchema.Value(f.Name, FieldType.Boolean, f.Description))]);
}
