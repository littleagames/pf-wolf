namespace PFWolf.Assets;

public record MapObjectTranslationAsset : Asset
{
    public Dictionary<int, MapActorTranslation> Things { get; internal set; } = new();
    public Dictionary<int, MapTextureTranslation> Walls { get; internal set; } = new();
    public Dictionary<int, MapTextureTranslation> Doors { get; internal set; } = new();
    public Dictionary<int, MapPlayerStartTranslation> PlayerStarts { get; internal set; } = new();
    public Dictionary<int, MapTriggerTranslation> Triggers { get; internal set; } = new();
    public Dictionary<int, MapDiagonalTranslation> Diagonals { get; internal set; } = new();

    /// <summary>Wall patches by id (1 to 255): the high byte of a wall tile's plane 0 value (MapConstants.PATCHSHIFT)</summary>
    public Dictionary<int, MapPatchTranslation> Patches { get; internal set; } = new();

    public MapFlatsTranslation Flats { get; internal set; } = new();
    public MapFloorsTranslation Floors { get; internal set; } = new();

    /// <summary>
    /// Object-plane values that carry information about the map rather than a thing, keyed by
    /// the value's high byte (Blake Stone's 0xFE00 and 0xFB00): what each holds. See
    /// <see cref="MapInfoCodes"/>.
    /// </summary>
    public Dictionary<int, string> MapInfo { get; internal set; } = new();

    /// <summary>
    /// Object-plane values that lock the door they sit on, with the inventory item that opens it
    /// (Blake Stone puts an access card's number on a door tile). In place of the door's own lock.
    /// </summary>
    public Dictionary<int, string> DoorLocks { get; internal set; } = new();

    /// <summary>
    /// Object-plane values (things) that pass a map-info tag-link's tag on to each other: a link
    /// to one of them tags the whole group touching it (Blake Stone's barriers)
    /// </summary>
    public List<int> LinkedThings { get; internal set; } = [];
}

/// <summary>
/// What a mapdefs map-info code holds. The code's own low byte is unused; the map's value is in
/// the object-plane tile after it (the next one east), which isn't a thing either.
/// </summary>
public static class MapInfoCodes
{
    /// <summary>The next tile's high byte is the ceiling's palette index and its low byte the floor's</summary>
    public const string CeilingFloorColors = "ceiling-floor-colors";

    /// <summary>
    /// The next tile's high byte is the ceiling's index in the mapdefs flats table and its low byte
    /// the floor's: every tile the flat plane leaves at 0 gets them
    /// </summary>
    public const string CeilingFloorFlats = "ceiling-floor-flats";

    /// <summary>Just not a thing: the tile is skipped (and the one after it isn't)</summary>
    public const string None = "none";

    /// <summary>Not a thing, and nor is the tile after it, which holds a value for something else to read</summary>
    public const string Value = "value";

    /// <summary>
    /// The tile after it holds a tile's x (high byte) and y (low byte): the code's own tile (a
    /// switch) and that one share a new tag (plane 4), as do the mapdefs linked-things chained
    /// to it, so the switch's actions act on them. The code's low byte is the floor (game-info
    /// floor-number) the link is on; 255 is this one, and a link to another floor is left alone.
    /// </summary>
    public const string TagLink = "tag-link";

    /// <summary>
    /// "hint:Text": a hint (or saying) the map places: message (low byte, from 1) of the VGAGRAPH
    /// text Text, about the room whose floor it's on, for the actors that talk (BlakeMonster.TalkTo)
    /// </summary>
    public const string HintPrefix = "hint:";

    /// <summary>The text a hint code's messages come from, or null when the kind isn't a hint</summary>
    public static string? HintText(string kind) =>
        kind.StartsWith(HintPrefix, StringComparison.OrdinalIgnoreCase) && kind.Length > HintPrefix.Length
            ? kind[HintPrefix.Length..].Trim()
            : null;

    /// <summary>Whether the code's value is in the tile after it</summary>
    public static bool HasValue(string kind) =>
        kind.Equals(CeilingFloorColors, StringComparison.OrdinalIgnoreCase)
        || kind.Equals(CeilingFloorFlats, StringComparison.OrdinalIgnoreCase)
        || kind.Equals(Value, StringComparison.OrdinalIgnoreCase)
        || kind.Equals(TagLink, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The floor codes on plane 0: the values that mark open floor rather than a wall or door.
/// Each area code is a room, for sound and sight (areas connect through open doors); the
/// ambush code makes the enemy standing on it wait deaf until it sees the player, then takes
/// on a neighbouring area's code; standing on the secret exit code while flipping the
/// elevator switch goes to the secret level. Every value is optional, so a file that only
/// sets one merges over the rest.
/// </summary>
public record MapFloorsTranslation
{
    /// <summary>The first area code; area n is AreaStart + n.</summary>
    public int? AreaStart { get; set; }

    /// <summary>How many area codes there are (at most 255).</summary>
    public int? AreaCount { get; set; }

    /// <summary>The ambush code, or -1 for none.</summary>
    public int? Ambush { get; set; }

    /// <summary>The secret exit floor code, or -1 for none.</summary>
    public int? SecretExit { get; set; }

    /// <summary>
    /// The first hidden area code, or -1 for none: HiddenAreaStart + n is area n too, but kept
    /// off the automap (Blake Stone). The map loads with them turned into the plain area codes.
    /// </summary>
    public int? HiddenAreaStart { get; set; }

    /// <summary>
    /// Floor codes that set off a trigger action (MapTriggerRegistry, e.g. A_VictoryRun) when
    /// the player steps onto them: Blake Stone's way out
    /// </summary>
    public Dictionary<int, string>? Triggers { get; set; }

    /// <summary>
    /// Floor codes that do something to the enemy standing on them (Planet Strike's): it carries
    /// an item to drop, is cloaked, waits in ambush, or works a smart switch when it dies. The
    /// floor takes on a neighbouring area's code, as under an ambush.
    /// </summary>
    public Dictionary<int, MapActorCodeTranslation>? ActorCodes { get; set; }
}

/// <summary>What a floors actor-code does to the enemy standing on it. See <see cref="MapFloorsTranslation.ActorCodes"/>.</summary>
public record MapActorCodeTranslation
{
    /// <summary>An inventory item class it drops when it dies (a key, a weapon, the detonator). Empty: none.</summary>
    public string Drop { get; set; } = "";

    /// <summary>Drawn shimmering, hard to see (bstone's FL2_CLOAKED)</summary>
    public bool Cloak { get; set; }

    /// <summary>Waits deaf until it sees the player, as on the ambush code</summary>
    public bool Ambush { get; set; }

    /// <summary>
    /// "east": the object-plane value on the tile east of it is a tile's x (high byte) and y
    /// (low byte), which its death works as a smart switch would (A_SmartSwitch("off")).
    /// Empty: none.
    /// </summary>
    public string Link { get; set; } = "";
}

/// <summary>
/// ECWolf's flats table: the texture for each index on the flat plane (plane 2). The low byte of
/// a tile's value is its floor's index and the high byte its ceiling's, 0 to 255 each. An index
/// with no entry here uses the map's default-floor or default-ceiling.
/// </summary>
public record MapFlatsTranslation
{
    public Dictionary<int, string> Floor { get; set; } = new();
    public Dictionary<int, string> Ceiling { get; set; } = new();

    /// <summary>
    /// Floors that hurt whoever stands on them (Midnight Assault's electrified water), by the
    /// texture's name as the floor shows it (after any A_ChangeTexture; an animation by its own
    /// name): a switch that changes the texture turns them on or off.
    /// </summary>
    public Dictionary<string, MapFloorDamage> Damage { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The damage a floor texture does, if any</summary>
    public MapFloorDamage? DamageFor(string? texture) =>
        texture == null || Damage.Count == 0 ? null
        : Damage.TryGetValue(texture, out var damage) ? damage
        : Damage.FirstOrDefault(d => d.Key.Equals(texture, StringComparison.OrdinalIgnoreCase)).Value;
}

/// <summary>A damaging floor (flats damage): <see cref="Amount"/> every <see cref="Tics"/> tics to each player and enemy on it</summary>
public record MapFloorDamage
{
    public int Amount { get; init; }

    /// <summary>How often it hurts, in tics (the original's DAMAGE_INTERVAL, 20)</summary>
    public int Tics { get; init; } = 20;

    /// <summary>Whether enemies standing on it are hurt too (the original's CheckObjDamagingFloors)</summary>
    public bool Enemies { get; init; } = true;
}

/// <summary>
/// An object-plane marker that turns the wall tile under it into a 45 degree wall. The wall
/// keeps its plane 0 texture id; this only gives it a shape (and optionally its diagonal face's
/// texture). A marker on anything but a wall tile is ignored.
/// </summary>
public record MapDiagonalTranslation
{
    public Enums.WallShape Shape { get; set; }

    /// <summary>The diagonal face's texture. Empty means the wall's own North texture.</summary>
    public string Texture { get; set; } = "";
}

/// <summary>
/// A wall patch: a picture with see-through pixels (palette index 0xFF) drawn over a wall's face,
/// such as a sign, a switch or a crack, so one wall can carry any decoration. A wall tile gets
/// one by the high byte of its plane 0 value (MapConstants.PATCHSHIFT); it moves with the wall
/// when it's pushed. Patches on doors and open floor are ignored.
/// </summary>
public record MapPatchTranslation
{
    /// <summary>The picture: a texture as wide as a wall's (any height), or an animdefs animation</summary>
    public string Texture { get; init; } = "";

    /// <summary>
    /// The picture on east and west faces, where that's another (as Wolf3D's walls have a darker
    /// east/west side; Midnight Assault's truck hood); empty: <see cref="Texture"/> on every face
    /// </summary>
    public string TextureEw { get; init; } = "";

    /// <summary>The picture on a face ("north", "south", "east" or "west")</summary>
    public string TextureFor(string face) =>
        TextureEw.Length > 0 && (face.Equals("east", StringComparison.OrdinalIgnoreCase) || face.Equals("west", StringComparison.OrdinalIgnoreCase))
            ? TextureEw
            : Texture;

    /// <summary>The wall faces it's on (north, south, east, west: the way the face looks out). Empty means all four.</summary>
    public List<string> Sides { get; init; } = [];

    /// <summary>
    /// The story of a tall wall it's on, 1 (the bottom one, the default) and up; 0 repeats it on
    /// every story, as the wall's own texture does
    /// </summary>
    public int Story { get; init; } = 1;

    /// <summary>Makes the patch a switch the player can use (see <see cref="MapSwitchTranslation"/>)</summary>
    public MapSwitchTranslation? Switch { get; init; }

    /// <summary>Whether it's on the face ("north", "south", "east" or "west")</summary>
    public bool IsOn(string face) => Sides.Count == 0 || Sides.Contains(face, StringComparer.OrdinalIgnoreCase);
}

/// <summary>An object-plane tile the player starts the level on, facing <see cref="Angles"/>.</summary>
public record MapPlayerStartTranslation
{
    /// <summary>0=east, 90=north, 180=west, 270=south, as for <see cref="MapActorTranslation.Angles"/>.</summary>
    public int Angles { get; set; }

    /// <summary>A deathmatch start: where players come into a deathmatch, not the level's own start</summary>
    public bool Deathmatch { get; set; }
}

/// <summary>
/// An object-plane tile the player sets off (e.g. a pushwall, the end-of-castle exit), running
/// <see cref="Action"/> through Entities.MapTriggerRegistry. One-shot: once the action goes off,
/// the tile is cleared, unless it's <see cref="Repeat"/>.
/// </summary>
public record MapTriggerTranslation
{
    /// <summary>The action call to run, e.g. `A_PushWall`.</summary>
    public string Action { get; set; } = "";

    /// <summary>
    /// How the player sets it off: "use" (pressing use while facing its tile, the default) or
    /// "walk" (stepping onto its tile).
    /// </summary>
    public string Activation { get; set; } = "use";

    public bool IsWalkOver => Activation.Equals("walk", StringComparison.OrdinalIgnoreCase);

    /// <summary>Counts toward the level's secret ratio (the total on load, found when the action goes off).</summary>
    public bool Secret { get; set; }

    /// <summary>
    /// Goes off every time it's set off (a teleport, a ladder) instead of once: its tile isn't
    /// cleared. (A use trigger goes off once a press of use.)
    /// </summary>
    public bool Repeat { get; set; }

    /// <summary>
    /// An actordefs class put on its tile as the level loads, as a thing would be: what shows
    /// there (a ladder, a teleport's marker), solid or not by its own flags.
    /// </summary>
    public string Class { get; set; } = "";
}

public record MapActorTranslation
{
    public string Class { get; set; } = "";
    public int Angles { get; set; }
    public int Patrol { get; set; }
    public int MinSkill { get; set; }

    /// <summary>
    /// What's placed instead below <see cref="MinSkill"/> (Blake Stone's canister aliens are
    /// plain canisters on easier skills); nothing when empty
    /// </summary>
    public string Else { get; set; } = "";

    /// <summary>
    /// Placed on a wall tile too (`in-wall: true`), not only on open floor: something that
    /// works from inside a wall, such as a trap shooting out of the face it looks out of (its
    /// <see cref="Angles"/>). Other things on walls are left out, since an object-plane value
    /// there is often something else (Blake Stone's door locks, switch and teleporter data).
    /// </summary>
    public bool InWall { get; set; }

    /// <summary>
    /// The side it's on (a factions.yaml faction), in place of its class's: a guard placed as
    /// one of the player's allies. Empty: its class's.
    /// </summary>
    public string Faction { get; set; } = "";

    /// <summary>
    /// The sprite color translation (translations.yaml) it's drawn with, in place of its class's
    /// or its faction's. Empty: theirs.
    /// </summary>
    public string Translation { get; set; } = "";
}

public record MapTextureTranslation
{
    public string North { get; init; } = "";
    public string South { get; init; } = "";
    public string East { get; init; } = "";
    public string West { get; init; } = "";

    /// <summary>
    /// The texture on one face: "north", "south", "east" or "west", the way the face looks out
    /// (a wall's north face is seen from the tile north of it). Empty if it doesn't give one.
    /// </summary>
    public string Side(string face) => face switch
    {
        "north" => North,
        "south" => South,
        "east" => East,
        _ => West,
    };

    /// <summary>The face opposite <paramref name="face"/> ("north" for "south" and so on)</summary>
    public static string OppositeFace(string face) => face switch { "north" => "south", "south" => "north", "east" => "west", _ => "east" };

    /// <summary>
    /// A wall's texture on one face (<see cref="Side"/>): a face it doesn't give falls back to the
    /// one opposite, so a wall that only gives North and East shows them on all four faces.
    /// </summary>
    public string WallFace(string face)
    {
        var texture = Side(face);
        return texture.Length > 0 ? texture : Side(OppositeFace(face));
    }

    /// <summary>
    /// Walls only: the faces (north, south, east, west: the way the face looks out) whose texture
    /// is drawn mirrored left to right, such as the two sides of a truck from one texture. A
    /// patch on a flipped face isn't mirrored. Empty: none.
    /// </summary>
    public List<string> Flip { get; init; } = [];

    /// <summary>Whether the face ("north", "south", "east" or "west") is drawn mirrored (<see cref="Flip"/>)</summary>
    public bool IsFlipped(string face) => Flip.Contains(face, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Doors only: the inventory item class (e.g. "GoldKey") the player must carry to open
    /// this door. Empty means unlocked.
    /// </summary>
    public string Lock { get; init; } = "";

    /// <summary>
    /// Doors only: what's shown when the player tries this door without its <see cref="Lock"/>
    /// item (a $NAME language key or the text itself). Empty uses the lock item's
    /// `key.lockedmessage`.
    /// </summary>
    public string LockMessage { get; init; } = "";

    /// <summary>
    /// Doors only: the hud-messages.yaml style <see cref="LockMessage"/> is shown in. Empty uses
    /// the lock item's `key.lockedmessagestyle`.
    /// </summary>
    public string LockMessageStyle { get; init; } = "";

    /// <summary>
    /// Doors only: the door's color on the automap (a theme color name or #RRGGBB), e.g. its
    /// key's color on a locked door. Empty uses the theme's AutomapDoor. A theme color named
    /// "Automap" + <see cref="Lock"/> (e.g. AutomapGoldKey) wins over it.
    /// </summary>
    public string AutomapColor { get; init; } = "";

    /// <summary>
    /// Doors only: the sound when the player tries this door without its <see cref="Lock"/>
    /// item. Empty uses the lock item's `key.lockedsound`.
    /// </summary>
    public string LockedSound { get; init; } = "";

    /// <summary>Doors only: played from the door as it starts to open, when the player can hear it</summary>
    public string OpenSound { get; init; } = "";

    /// <summary>Doors only: played from the door as it starts to close, when the player can hear it</summary>
    public string CloseSound { get; init; } = "";

    /// <summary>
    /// Doors only: the faces drawn while the door is locked (any left out are its usual ones),
    /// such as Blake Stone's doors with their lock lights on
    /// </summary>
    public MapTextureTranslation? Locked { get; init; }

    /// <summary>
    /// Doors only: opening the door with its lock item uses the item up and unlocks the door for
    /// good (Blake Stone's access cards), instead of the item opening it every time
    /// </summary>
    public bool TakesKey { get; init; }

    /// <summary>
    /// Doors only: a one-way door, opened only from this side (north, south, east or west); from
    /// the other side it won't open. Empty: either side. A side's face is what's seen from it,
    /// so a one-way door's two faces can differ.
    /// </summary>
    public string OpensFrom { get; init; } = "";

    /// <summary>
    /// Doors only: the door parts in the middle, its two halves sliding out to either side
    /// (Blake Stone's doors), instead of sliding as one into the wall
    /// </summary>
    public bool Split { get; init; }

    /// <summary>Doors only: shown when the player tries a one-way door from the wrong side (a $NAME language key or the text)</summary>
    public string WrongSideMessage { get; init; } = "";

    /// <summary>Doors only: played when the player tries a one-way door from the wrong side</summary>
    public string WrongSideSound { get; init; } = "";

    /// <summary>
    /// Doors only: the door runs north-south, so it's passed through going east or west (its
    /// East/West faces are the door). False, it runs east-west (North/South are the door).
    /// </summary>
    public bool Vertical { get; init; }

    /// <summary>Walls only: makes this wall a switch the player can use.</summary>
    public MapSwitchTranslation? Switch { get; init; }

    /// <summary>Walls only: makes this wall an outlet enemies come out of (Blake Stone's electro-aliens).</summary>
    public MapOutletTranslation? Outlet { get; init; }

    public static MapTextureTranslation None => new(); // TODO: Missing texture
}

/// <summary>
/// A wall that lets enemies out (GameHost.BlakeMachines.cs): every so often, while the room beside
/// it joins the player's, one comes out onto open floor beside it.
/// </summary>
public record MapOutletTranslation
{
    /// <summary>The actordefs class that comes out</summary>
    public string Class { get; init; } = "";

    /// <summary>Seconds between them, [min, max] at random; a 0xFA value on the wall's object-plane tile sets it instead</summary>
    public List<int> Delay { get; init; } = [8, 30];

    /// <summary>How many from this outlet can be about at once, by skill (the last for any skill past the list)</summary>
    public List<int> Max { get; init; } = [1];

    /// <summary>Played where one appears</summary>
    public string Sound { get; init; } = "";
}

/// <summary>
/// A wall the player uses (like a door) to set things off. Using it turns it into wall
/// <see cref="To"/>, plays <see cref="Sound"/> and runs <see cref="Actions"/> through
/// Entities.MapTriggerRegistry, which act on whatever shares the switch tile's tag. If wall
/// <see cref="To"/> has a switch of its own (back to this one, say), it's a toggle; if not, the
/// switch is thrown once and stays that way. Other walls with the switch's tag that are this
/// wall or wall <see cref="To"/> turn into <see cref="To"/> with it, without running their
/// actions, so switches linked to the same things stay in step (Blake Stone's barrier switches).
/// </summary>
public record MapSwitchTranslation
{
    /// <summary>
    /// The wall id it turns into when used (1 to 255); 0 leaves the wall as it is. On a patch's
    /// switch, the patch id it turns into (1 to 255).
    /// </summary>
    public int To { get; init; }

    /// <summary>Patches only: once used, the patch comes off the wall (a switch that works once, say, or an item taken off a shelf)</summary>
    public bool Remove { get; init; }

    /// <summary>
    /// The faces it can be used from (north, south, east, west): using it while facing east
    /// presses its west face. Empty means any.
    /// </summary>
    public List<string> Sides { get; init; } = [];

    /// <summary>Played as it's thrown. Empty: silent.</summary>
    public string Sound { get; init; } = "";

    /// <summary>The inventory item class (e.g. "GoldKey") the player must carry to use it. Empty: none.</summary>
    public string Lock { get; init; } = "";

    /// <summary>
    /// Shown when it's used without its <see cref="Lock"/> item (a $NAME language key or the
    /// text itself). Empty uses the lock item's `key.lockedmessage`.
    /// </summary>
    public string LockMessage { get; init; } = "";

    /// <summary>The hud-messages.yaml style for <see cref="LockMessage"/>. Empty uses the lock item's `key.lockedmessagestyle`.</summary>
    public string LockMessageStyle { get; init; } = "";

    /// <summary>Played when it's used without its <see cref="Lock"/> item. Empty uses the lock item's `key.lockedsound`.</summary>
    public string LockedSound { get; init; } = "";

    /// <summary>The action calls to run, in order, e.g. `A_Exit`.</summary>
    public List<string> Actions { get; init; } = [];

    /// <summary>
    /// A switch that turns back by itself (Midnight Assault's timed switches): this many tics
    /// after it's thrown, it and the linked ones thrown with it go back to what they were, and
    /// <see cref="ResetActions"/> run. Make its `to` a wall or patch that isn't a switch, so it
    /// can't be used meanwhile. 0: it stays thrown.
    /// </summary>
    public int ResetTics { get; init; }

    /// <summary>The action calls run as a <see cref="ResetTics"/> switch turns back (A_CloseDoor, say)</summary>
    public List<string> ResetActions { get; init; } = [];

    /// <summary>
    /// How the switch finds what it works besides its tag. "object-plane": the switch tile's
    /// object-plane value is a tile's x (high byte) and y (low byte) on this floor, and the
    /// switch shares that tile's tag, as a map-info tag-link would give it (Planet Strike's
    /// barrier switches). Empty: its tag only.
    /// </summary>
    public string Link { get; init; } = "";

    /// <summary>
    /// "on" or "off": which way the switch is thrown, for a smart switch told to turn things
    /// on or off (A_SmartSwitch), which leaves it be if it's already that way. Empty: a smart
    /// switch always throws it.
    /// </summary>
    public string State { get; init; } = "";

    /// <summary>Whether the player can use it while facing <paramref name="dir"/>.</summary>
    public bool UsableFrom(controldirs dir)
    {
        if (Sides.Count == 0)
            return true;

        var face = dir switch
        {
            controldirs.di_east => "west",
            controldirs.di_west => "east",
            controldirs.di_north => "south",
            _ => "north",
        };
        return Sides.Contains(face, StringComparer.OrdinalIgnoreCase);
    }
}
