namespace PFWolf.Definitions;

/// <summary>
/// An actordefs property the engine reads (a class's `properties:`): its name, what it holds and
/// what it does. Properties are free-form in the YAML, so this is where they're listed.
/// </summary>
/// <param name="Name">The property's name, as the YAML writes it ("light.intensity"); a pattern ends in its placeholder ("health.SKILL")</param>
/// <param name="Type">What it holds</param>
/// <param name="Description">What it does</param>
/// <param name="Default">What the engine uses when it's left out; null when that's nothing</param>
/// <param name="Hint">What a text names (a sound, a class...)</param>
/// <param name="Item">What a list's items, or a map's values, are</param>
/// <param name="Needs">The actor class that reads it (`parent:` one of them), or null for any actor</param>
public sealed record ActorProperty(string Name, FieldType Type, string Description, string? Default = null,
    FieldHint Hint = FieldHint.None, FieldType Item = FieldType.Text, string? Needs = null)
{
    /// <summary>Whether the name is a pattern: "health.SKILL" stands for health.normal, health.hard...</summary>
    public bool IsPattern => Name.EndsWith(".SKILL", StringComparison.Ordinal);
}

/// <summary>
/// The actordefs properties the engine reads, for the documentation and the tests that keep the
/// YAML and the code in step with it (a property the code reads that isn't here, or one the YAML
/// sets that the code doesn't read, fails them). Add a property here as the code starts reading it.
/// </summary>
public static class ActorProperties
{
    /// <summary>Every property, by group (the name's prefix), then name</summary>
    public static IReadOnlyList<ActorProperty> All { get; } =
    [
        // Any actor
        Text("tag", "Its name in messages: an obituary's %o, the class menu (a $NAME language key or the name)"),
        Int("health", "Its health. An enemy on a skill whose game-info enemy-health names another property (health.normal) takes that one instead, when it has it."),
        Int("health.SKILL", "Its health on a skill: the property a game-info skill's enemy-health names (health.baby, health.easy, health.normal, health.hard)"),
        Int("speed", "How fast it moves, in 1/65536 tile a tic: a monster's walking speed, a projectile's flight. Left out: 512 for an enemy, 1500 for a mover with no health (the Pac-Man ghosts), 0x2000 for an A_SpawnProjectile or A_FireProjectile projectile, 0x1C00 for an A_FireMissile missile"),
        Int("speed.random", "A projectile: up to this much more speed, at random"),
        Int("spread", "A projectile: aimed up to this many degrees either side, at random"),
        Int("points", "The score for killing it"),
        Text("faction", "The side it's on (factions.yaml), unless a mapdefs thing gives its own", hint: FieldHint.Faction),
        Text("translation", "The sprite color translation it's drawn through (translations.yaml), unless a mapdefs thing gives its own", hint: FieldHint.Translation),
        Text("lighten", "On a shaded level, how much less it darkens with distance: a number of fade steps (of 32), a game-info lighten-levels name (Blake Stone's partial-bright lamps), or bright for unshaded, as the bright flag"),
        Text("damagetype", "The kind of blow it deals (a missile's, a blast's, a monster's), for the death its victim dies (a Death.TYPE state)"),
        Int("knockback", "How far a BlakeMonster body it kills slides back, in 1/65536 tile (an explosion's); 0x5000 when left out. Other enemies don't slide."),
        Text("attackinfo", "Shown when it hurts the player, while Attack Info is on (a $NAME language key or the text; %k is its tag): Blake Stone's REBA \"attacking:\", a presenter script with its picture. game-info attack-message when left out"),
        Text("obituary","The message when it kills the player (a $NAME language key or the text; %o is the player's tag)"),
        Text("obituarystyle", "The hud-messages.yaml style its obituary is shown in; game-info's obituary-style when left out"),
        Text("objective", "A level objective (game-info objectives) it's one of: it counts toward the level's total, and its death completes one"),
        Text("usehint", "The use hint shown while the player faces it and it can be used (statusbar.yaml use-hint's hints, by name)"),
        List("usehint.except", "States it can't be used in: no use hint or use message then"),
        Text("usemessage", "A message kept up while the player faces it and it can be used (\"Press Use to apprehend the Commander\")"),
        Text("usemessage.style", "The hud-messages.yaml style its use message is shown in"),
        Text("switched", "What a smart switch on its tile does to it: kill (crates, pod eggs) or remove; left out, an enemy is woken"),
        Bool("healthbar", "The status bar's enemy-health shows its health while it's after someone (a boss)"),
        Text("killchain", "false: its kills are no part of a kill chain (the commanders); noprize: they carry a chain on but drop nothing (the dog)"),
        Int("heist.value", "Heist: how far it pulls the rope toward the team that took it; treasure's inventory.amount / 100 when left out"),
        Int("prophunt.health", "A decoration: the most health (1-100) a Prop Hunt prop has looking like it; left out, by its radius (50, up to 100 at 32)"),
        List("spawn.random", "A random spawner: it's one of these classes, picked at random (Blake Stone's bio-techs, half of them informants)", hint: FieldHint.Class),
        Text("blocktile", "A marker thing: monsters (every monster) or bosses (BOSS actors) can't walk onto its tile; it's never placed itself"),
        Bool("docktile", "A marker thing: a vehicle can't go onto its tile, and its rider gets off there; it's never placed itself"),
        Int("wallsprite.offset", "A wall sprite: how far its panel sits off the tile's middle toward its front, in texels (32: on the tile's edge)"),

        // Its sounds
        Text("seesound", "Played when it first sees someone", hint: FieldHint.Sound),
        Text("attacksound", "Played as it attacks; a weapon's as the player fires it", hint: FieldHint.Sound),
        Text("deathsound", "Played as it dies (the player's when the player dies; a missile's as it hits)", hint: FieldHint.Sound),
        Text("activesound", "Played by A_ActiveSound while it chases, only in an area connected to the player's (Mecha Hitler's stomp)", hint: FieldHint.Sound),
        Text("meleesound", "Played as it strikes up close", hint: FieldHint.Sound, needs: BlakeMonster),
        Text("woundsound", "Played as it goes down wounded", hint: FieldHint.Sound, needs: BlakeMonster),
        Text("revivesound", "Played as it revives a fallen ally (monster.revive)", hint: FieldHint.Sound),
        Text("touchsound", "Played as it zaps the player it touches (monster.touchdamage)", hint: FieldHint.Sound, needs: BlakeMonster),
        Text("bouncesound", "A missile: played as it bounces", hint: FieldHint.Sound),

        // Missiles in the air
        Number("z", "A missile: its height above the floor as it's fired"),
        Number("zspeed", "A missile: how fast its height rises as it's fired"),
        Number("gravity", "A missile: how much its rising speed drops each tic, in texels a tic"),
        Number("zfloor", "A missile: the height it stops falling at, and stays (where its art meets the floor)"),
        Text("hitpuff", "An actor class left just in front of it when a player's shot hits it (Midnight Assault's blood puff); none (or \"none\") leaves nothing"),

        // Dying
        Text("dropitem", "The item class it leaves when it dies", hint: FieldHint.Class),
        Text("dropitem.alt", "Left in place of dropitem dropitem.altchance times in 256, and always once it's out of ammo (monster.ammo)", hint: FieldHint.Class),
        Int("dropitem.altchance", "How often, in 256, it leaves dropitem.alt", "128"),
        Bool("dropitem.needsammo", "It leaves nothing once it's out of ammo (monster.ammo)"),
        Int("dropitem.onfloor", "It leaves its item only on this floor (game-info floor-number), and only once a level (Goldfire's gold card)"),
        Text("dropweapon", "The weapon class it leaves when it dies, before its dropitem", hint: FieldHint.Class),
        Bool("dropweapon.ifmissing", "It leaves its dropweapon only while the player hasn't got that weapon, else its dropitem"),
        Int("death.gibdamage", "A killing blow of at least this much sends it to its XDeath state"),
        Int("death.gibhealth", "Health left below minus this sends it to its XDeath state"),
        List("death.gibtypes", "Only blows of these damage types send it to its XDeath state"),

        // Items
        Int("inventory.amount", "How many of it picking it up gives (points, for treasure)"),
        Int("health.takenbelow", "A health item: only picked up while the player's health is this or less (Blake Stone's water puddle: 15)"),
        List("inventory.randomamount", "[min, max]: picking it up gives a random amount from min to max instead (Blake Stone's charge pack)", item: FieldType.Integer),
        Int("inventory.maxamount", "The most of it the player can carry; a magazine's: the clip size"),
        Int("inventory.interhubamount", "How much of it the player keeps going to the next level (0: none, as keys)"),
        Text("inventory.type", "The item it's held as: its variants (ClipBox, BlueClip) fill the one stack (Clip)", hint: FieldHint.Class),
        Text("inventory.icon", "A weapon's picture on the status bar", hint: FieldHint.Picture),
        Text("inventory.pickupmessage", "Shown as it's picked up (a $NAME language key or the text); %a in it is how much it gave"),
        Text("inventory.pickupmessagestyle", "The hud-messages.yaml style its pickup message is shown in; game-info's pickup-style when left out"),
        Text("inventory.pickupsound", "Played as it's picked up", hint: FieldHint.Sound),
        Bool("inventory.disguise", "Worn, it's a disguise (Gameplay/Stealth.cs): an enemy only suspects the unseen player"),
        Int("inventory.useslot", "An item the player uses with a weapon slot's key (1-9, 0) when they've no weapon in that slot"),
        Text("inventory.use", "The action calls run when it's used, from its slot or by holding use (A_DropDetonator(...))"),
        Int("inventory.useholdtics", "Use held this many tics with nothing ahead to use uses it"),
        Text("inventory.usenone", "Shown when its slot's key is pressed without any held (\"You do not have any medkits.\")"),
        List("inventory.restrictedto", "The only player classes that pick it up (and those descended from them): a list, or one class", hint: FieldHint.Class),
        List("inventory.forbiddento", "Player classes that don't pick it up (and those descended from them): a list, or one class", hint: FieldHint.Class),
        Int("ammo.backpackamount", "How much of it (ammo, or any item) a backpack gives, each time one's picked up"),
        Int("ammo.backpackmaxamount", "Ammo: the most the player can carry while they have a backpack, in place of inventory.maxamount"),
        Int("armor.saveamount", "Armor: the armor points it gives. It's only picked up when the player has fewer, and replaces their armor, points and savepercent, outright."),
        Int("armor.savepercent", "Armor: the share of each hit's damage taken off the armor points instead of health, 0-100"),
        Int("armor.maxsaveamount", "Armor bonus: it adds its saveamount to what the player has, up to this (and player.maxarmor), keeping their savepercent (or giving its own when they have none); without it, armor replaces the player's when it's more"),
        Text("weapongiver.weapon", "A weapon giver: the weapon it hands out (the gatling upgrade's)", hint: FieldHint.Class),

        // Keys
        Text("key.lockedmessage", "Shown when the player tries a door this key locks without it, unless the door has its own lock-message"),
        Text("key.lockedmessagestyle", "The hud-messages.yaml style for it; game-info's lock-style when left out"),
        Text("key.lockedsound", "Played then too, unless the door or switch has its own locked-sound", hint: FieldHint.Sound),
        Text("key.switchlockedmessage", "The same as lockedmessage, for a switch this key locks"),
        Int("key.statusbarslot", "Its place among the status bar's key slots, 0 at the top, spaced as statusbar.yaml's keys part says; none: it isn't shown"),
        Text("key.statusbarpic", "Its picture on the status bar while carried", hint: FieldHint.Picture),
        Text("key.statusbaremptypic", "Its slot's picture while it isn't carried", hint: FieldHint.Picture),
        Text("key.statusbarcolor", "Its slot's square's color while carried, for a status bar keys part with a size", hint: FieldHint.Color),
        Text("key.statusbaremptycolor", "Its slot's square's color while it isn't carried", hint: FieldHint.Color),

        // Weapons
        Int("weapon.slot", "The slot key (1-9, 0) that picks it; none: only next and previous weapon reach it"),
        Int("weapon.selectionorder", "How good it is: lower is better (the one put in hand, the one picked when ammo runs out)"),
        Text("weapon.ammotype1", "The ammo item it fires", hint: FieldHint.Class),
        Int("weapon.ammouse1", "Ammo a shot costs (0: it needs none, as the knife)"),
        Int("weapon.ammogive1", "Ammo picking it up gives"),
        Text("weapon.magazine", "The item its loaded rounds are held as: shots come out of it, and A_Reload fills it from the ammo", hint: FieldHint.Class),
        Int("weapon.chargetics", "It fires only with a full charge, and takes this many tics to charge again (Blake Stone's auto charge pistol)"),
        Text("weapon.damagetype", "The kind of blow its shots deal, for the death the victim dies (a Death.TYPE state)"),
        Text("weapon.backstabtype", "The kind of blow a back-stab with it deals", "Backstab"),
        Int("weapon.knockback", "How far a BlakeMonster body it kills slides back, in 1/65536 tile; none when left out. Other enemies don't slide."),
        Bool("weapon.silent", "Its shots alert no one"),
        Bool("weapon.stealthkill", "Kills made unseen with it count as stealth kills (the knife)"),
        Text("weapon.spares", "An actor flag (BOSS) whose holders it doesn't hurt at all", hint: FieldHint.Flag),
        Int("weapon.killrange", "A hit within this many tiles kills outright, and one beyond it does nothing"),
        Text("weapon.puff", "An actor class left where a shot that hits nothing strikes a wall or a door that isn't open (Midnight Assault's bullet puff); none on a wall with walls.yaml puffs: false"),
        Text("weapon.bodysprite", "In a game with others: the sprite the player's body is drawn with while it's in hand (PLKN)", hint: FieldHint.Sprite),
        Text("weapon.backhint", "The use hint shown while an enemy faces away within reach (a back-stab)"),
        Text("weapon.facehint", "The use hint shown while an enemy faces the player within reach (the papers, a stealth kill)"),
        Text("weapon.notheld", "Shown when its slot's key is pressed and the player has no weapon in that slot (\"You don't have this weapon\")"),
        Bool("weapon.papers", "Papers: in hand, an enemy that suspects the disguised player can be shown them (Gameplay/Stealth.cs)"),
        Number("weapon.zoom", "A scope: how many times it magnifies at its most"),
        Text("weapon.zoompic", "The picture covering the view while it's zoomed, in place of the weapon", hint: FieldHint.Picture),
        Text("weapon.zoomsound", "Played as the zoom key is pressed", hint: FieldHint.Sound),
        Int("weapon.zoomturn", "Zoomed in, the player turns this many times slower", "1"),
        Bool("weapon.mounted", "A gun the player mans where it stands (A_Mount): they can turn but not move"),
        Int("weapon.mountdamage", "Manning it, the share of a hit the player takes, in percent", "100"),
        Text("weapon.mountnoroom", "Shown when there's nowhere to step off it"),
        Bool("weapon.latch", "The struggle weapon a latched-on monster puts in hand (A_LatchBite)"),
        Int("weapon.slowmove", "With it in hand, the player moves this many times slower", "1"),

        // Power-ups
        Int("powerup.duration", "How long it runs, in tics (70 a second); another picked up adds to what's left"),
        Bool("powerup.invulnerable", "Nothing hurts the player, and the screen doesn't flash red"),
        Text("powerup.weapon", "The weapon put in hand as it's picked up (if the player has it), and taken back when it's over: the one they had comes back, if they still hold it", hint: FieldHint.Class),
        Bool("powerup.weaponkills", "Every blow of its weapon kills"),
        Text("powerup.damagetype", "The death a weaponkills blow gives"),
        Text("powerup.attacksound", "Played for its weapon's attacks in place of their own", hint: FieldHint.Sound),
        Text("powerup.face", "The status bar's face while it runs: a picture, or a list the face looks around through", hint: FieldHint.Picture),
        Text("powerup.timerstyle", "Shows the time left (m:ss) in this hud-messages.yaml style"),
        Bool("powerup.flash", "The screen is kept lit, as a pickup's flash"),
        Text("powerup.endsound", "Played as it runs out", hint: FieldHint.Sound),

        // Player classes
        Int("player.starthealth", "A new game's (and a respawn's) health", "100"),
        Int("player.startlives", "A new game's lives", "3"),
        Int("player.maxhealth", "The most health the player can have. A health item with an inventory.maxamount of its own heals only up to that, when it's lower (blood and gibs help only when nearly dead).", "100"),
        Int("player.maxlives", "The most lives the player can have", "9"),
        Int("player.maxarmor", "The most armor points the player can have; no cap but the armor's own when left out"),
        Map("player.startitem", "A new game's (and a respawn's) inventory: item: amount. The best weapon given (lowest weapon.selectionorder) is the one in hand; an armor (a BasicArmor class) is put on, whatever its amount.", item: FieldType.Integer),
        Map("player.maxamount", "Item caps for this class, in place of the items' own inventory.maxamount: item: amount. Any class of an item's type names it (ClipBox for Clip).", item: FieldType.Integer),
        List("player.weapons", "With a list, the class picks up only these weapons (and those built on them) and the ammo they use; every other weapon and ammo stays on the floor. Left out, any.", hint: FieldHint.Class),
        Number("player.damagedealt", "The share of their weapons' damage the player deals", "1"),
        Number("player.damagetaken", "The share of the damage they're dealt the player takes, before the skill's", "1"),
        Number("player.forwardmove", "How fast the player moves forwards and back, as a share of the usual", "1"),
        Number("player.sidemove", "How fast the player strafes, as a share of the usual", "1"),
        Int("player.eyeheight", "How high the player's eyes are standing, in texels (64 a story; 32 is half way up)", "32"),
        List("player.face", "The status bar's face at every health, in place of statusbar.yaml's faces by health: a picture, or a list the face looks around through (graphicdefs can crop one from a sprite). A grin, then a power-up's face (powerup.face), come before it. The facegrin, facedead and faceouch below only count with it.", hint: FieldHint.Picture),
        Text("player.facedead", "With player.face: the face once dead; its face when left out", hint: FieldHint.Picture),
        Text("player.facegrin", "With player.face: the face grinning (a WEAPON.ALWAYSGRIN pickup); its face when left out", hint: FieldHint.Picture),
        Text("player.faceouch", "With player.face: the face wincing at a big hit; left out, it doesn't wince", hint: FieldHint.Picture),

        // Monsters
        Int("monster.chasespeed", "Its speed once it's after someone, in place of its speed"),
        Int("monster.chasespeedmultiplier", "Without a chasespeed: its speed is multiplied by this once it's after someone"),
        List("monster.reactiondelay", "Tics before it reacts on first seeing someone: [base, divisor], base plus a random 0-255 over divisor (no divisor: none)", "1", item: FieldType.Integer),
        Bool("monster.sharpshooter", "A better shot: it counts the distance to the player as two thirds of it (SS, Hans)"),
        Text("monster.puff", "An actor class left where its shot at the player misses and strikes a wall or a door that isn't open, past the player (Midnight Assault's bullet puff); none on a wall with walls.yaml puffs: false"),
        Int("monster.pointblankdamage", "T_Shoot's extra damage right beside the player (the Ubermutant's 10)"),
        List("monster.damagestates", "States it goes to by the health it has left, in place of Pain: [full, 3/4, 1/2, 1/4]"),
        Bool("monster.painonce", "It only flinches at its first hit"),
        Text("monster.minweapon", "Only this weapon, or a better one (by weapon.selectionorder), in the player's hand hurts it", hint: FieldHint.Class),
        Text("monster.blastedby", "The missile class whose blast breaks it (sends it to its Death state)", hint: FieldHint.Class),
        Text("monster.becomes", "A sleeper: the enemy it becomes, which is the kill rather than it", hint: FieldHint.Class),
        Int("monster.floordeath", "On this floor (game-info floor-number) it isn't killed but goes to its FloorDeath state (Goldfire's morph)"),
        List("monster.floorhealth", "[floor, times]: that many times its health on that floor", item: FieldType.Integer, needs: BlakeMonster),
        Bool("monster.follow", "It falls in with a player on its side once it sees them"),
        Text("monster.disguise", "How it takes a disguised player: fooled (never suspects them: the dog) or seesthrough (notices them as ever)"),
        Text("monster.papersfail", "It isn't fooled by the papers, and says so (the officer)"),
        List("monster.ammo", "Shots it starts with: one number, or [min, max] at random; out of them, a SMART actor runs", item: FieldType.Integer, needs: BlakeMonster),
        Bool("monster.shootmode", "It takes turns at shooting and closing in (Blake Stone's aliens)", needs: BlakeMonster),
        Int("monster.painattack", "Hurt, the chance in 256 it shoots straight back", needs: BlakeMonster),
        List("monster.woundstages", "[n, ...]: one is picked; each 1/(n+1) of its health lost puts it down wounded", item: FieldType.Integer, needs: BlakeMonster),
        List("monster.wakedelay", "A sleeper's wake delay: [min, max] seconds at random (the map's value wins); 0 wakes it only when shot, 255 never", item: FieldType.Integer, needs: BlakeMonster),
        Bool("monster.wakeprotected", "A sleeper on a timer can't be shot awake (the gurney mutant)", needs: BlakeMonster),
        Int("monster.attackrange", "It doesn't attack past this many tiles (the floating bomb's 1)", needs: BlakeMonster),
        Int("monster.attackmindist", "It doesn't attack closer than this many tiles", needs: BlakeMonster),
        Int("monster.attackchance", "It attacks only this many times in 256 it could", "256", needs: BlakeMonster),
        Text("monster.bounce", "It bounces: horizontal, vertical or diagonal (straight until blocked, then back)", "diagonal", needs: BlakeMonster),
        Int("monster.touchdamage", "Within a tile of the player it zaps them for this much", needs: BlakeMonster),
        Int("monster.healthgain", "On an item: a SMART enemy (a BlakeMonster) standing on it takes it, for this much health"),
        Int("monster.ammogain", "On an item: a SMART enemy (a BlakeMonster) out of ammo standing on it takes it, for this many shots"),
        Text("monster.revive.state", "A reviver: the state it revives allies in (its A_Revive)", "Heal"),
        Text("monster.revive.count", "A reviver: how many it brings back at once, nearest first: a number, or [min, max] or \"min-max\" at random", "1"),
        Number("monster.revive.radius", "A reviver: how far off, in tiles, it looks for fallen allies", "2"),
        Int("monster.pickup.heal", "On an item: an enemy reaching its tile takes it, for this much health (no cap)"),
        Bool("monster.pickup.kill", "On an item: an enemy reaching its tile dies (a landmine)"),
        Int("monster.pickup.uses", "On an item: how many enemies it serves before it goes", "1"),
        List("monster.pickup.classes", "On an item: only these classes (and those built on them) take it", hint: FieldHint.Class),
        Text("monster.pickup.sound", "On an item: played where the enemy takes it", hint: FieldHint.Sound),
        Text("monster.pickup.state", "On an item: it goes to this state of its own as an enemy takes it, and stays unless it removes itself"),

        // Talking (Blake Stone)
        Text("talk.header", "Shown over what it says when talked to (a $NAME language key or the text)", needs: BlakeMonster),
        Text("talk.style", "The hud-messages.yaml style what it says is shown in"),
        Text("talk.sound", "Played as it's talked to", hint: FieldHint.Sound, needs: BlakeMonster),
        Text("talk.friendly", "The VGAGRAPH text its friendly sayings come from", needs: BlakeMonster),
        Text("talk.hostile", "The VGAGRAPH text its hostile sayings come from", needs: BlakeMonster),
        Text("talk.hints", "An informant: the VGAGRAPH text its hints come from"),
        List("talk.gifts", "An informant talked to again: the items it gives, one a time", hint: FieldHint.Class),
        List("talk.giftmessages", "What it says giving each of its gifts"),
        Text("talk.killedmessage", "An informant: the warning shown when one is killed"),

        // Warp sites (Blake Stone's Goldfire)
        Text("warpsite.class", "A warp site marker: the class that warps in at one of the level's sites (Goldfire)", hint: FieldHint.Class),
        List("warpsite.firstwait", "[min, max] seconds before it first warps in", item: FieldType.Integer),
        List("warpsite.wait", "[min, max] seconds before it warps in again", item: FieldType.Integer),
        Int("warpsite.quickfloor", "On this floor (game-info floor-number) it warps in after a second"),

        // Light (Entities/Actors/ActorLight.cs)
        Int("light.intensity", "Light it gives off, 0-255 (as zone light: 255 is full); none when left out, unless a state's light gives some. It spreads through open floor and open doors, not walls, and only shows where the level is darker than full light (game-info shading, or a dim light zone). A_SetLight and A_LightOff change it as it plays"),
        Number("light.radius", "How far its light reaches, in tiles", "2"),
        Text("light.color", "#RRGGBB its light tints what it lights; white when left out", hint: FieldHint.Color),
        Text("light.effect", "none, flicker, pulse or strobe, as for light zones", "none"),
        Int("light.low", "The effect's dark end, 0-255; half the intensity when left out"),
        Int("light.tics", "The effect's timing, in tics (flicker 8, pulse 70, strobe 35 when left out)"),
        Int("light.brighttics", "How long a strobe stays at its intensity, in tics", "5"),
    ];

    /// <summary>A property by its name, ignoring case; health.normal and the like find health.SKILL</summary>
    /// <summary>
    /// Whether a property gives an actor health: "health", or a skill's ("health.normal"), not a
    /// health item's own setting ("health.takenbelow")
    /// </summary>
    public static bool IsHealthKey(string key) =>
        key == "health" || key.StartsWith("health.", StringComparison.Ordinal) && Find(key) is { IsPattern: true };

    public static ActorProperty? Find(string name)
    {
        var exact = All.FirstOrDefault(p => !p.IsPattern && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
            return exact;
        return All.FirstOrDefault(p => p.IsPattern && name.Length > p.Name.Length - "SKILL".Length
            && name.StartsWith(p.Name[..^"SKILL".Length], StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The properties as a record's fields, for the documentation (each its own field, by name)</summary>
    public static FieldSchema Schema() => FieldSchema.Record("properties",
        "The properties an actordefs class can set (`properties:`), by name. A class has its parents' unless it sets them.",
        [.. All.Select(Field)]);

    private static FieldSchema Field(ActorProperty p)
    {
        var description = Describe(p.Description, p.Needs);
        return p.Type switch
        {
            FieldType.List => FieldSchema.List(p.Name, description, FieldSchema.Value("item", p.Item, null, hint: p.Hint), defaultText: p.Default),
            FieldType.Dictionary => FieldSchema.Entries(p.Name, description, FieldSchema.Value("amount", p.Item, null)),
            _ => FieldSchema.Value(p.Name, p.Type, description, p.Default, p.Hint),
        };
    }

    /// <summary>A property's or flag's description, with the class it needs, as the actions reference says it (GameActions.Describe)</summary>
    internal static string Describe(string description, string? needs) =>
        needs == null ? description : $"{description.TrimEnd('.')}. Only for a {needs} (`parent: {needs}`): on any other actor it does nothing, and the game warns of it.";

    private const string BlakeMonster = "BlakeMonster";

    private static ActorProperty Text(string name, string description, string? defaultText = null, FieldHint hint = FieldHint.None, string? needs = null) =>
        new(name, FieldType.Text, description, defaultText, hint, Needs: needs);

    private static ActorProperty Int(string name, string description, string? defaultText = null, string? needs = null) =>
        new(name, FieldType.Integer, description, defaultText, Needs: needs);

    private static ActorProperty Number(string name, string description, string? defaultText = null) =>
        new(name, FieldType.Number, description, defaultText);

    private static ActorProperty Bool(string name, string description, string? needs = null) =>
        new(name, FieldType.Boolean, description, "false", Needs: needs);

    private static ActorProperty List(string name, string description, string? defaultText = null, FieldHint hint = FieldHint.None, FieldType item = FieldType.Text, string? needs = null) =>
        new(name, FieldType.List, description, defaultText, hint, item, needs);

    private static ActorProperty Map(string name, string description, FieldType item) =>
        new(name, FieldType.Dictionary, description, null, FieldHint.None, item);
}
