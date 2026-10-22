namespace PFWolf.Assets;

public record MapObjectTranslationAsset : Asset
{
    /// <summary>
    /// Things by object-plane (plane 1) value: the enemies, items and decorations placed on the
    /// tiles that have it
    /// </summary>
    public Dictionary<int, MapActorTranslation> Things { get; internal set; } = new();

    /// <summary>
    /// Walls by plane 0 value (1 to 255): every tile with one listed is a solid wall, with these
    /// textures. A plane 0 value that isn't a wall or a door is open floor (floors).
    /// </summary>
    public Dictionary<int, MapTextureTranslation> Walls { get; internal set; } = new();

    /// <summary>
    /// Doors by plane 0 value: the two faces across the doorway are the door (east and west for a
    /// vertical one, north and south otherwise), the other two the jambs on the walls beside it
    /// </summary>
    public Dictionary<int, MapTextureTranslation> Doors { get; internal set; } = new();

    /// <summary>The object-plane values the player starts the level on, facing their angles</summary>
    public Dictionary<int, MapPlayerStartTranslation> PlayerStarts { get; internal set; } = new();

    /// <summary>Object-plane values the player sets off, by using or walking onto their tile (a pushwall, an exit)</summary>
    public Dictionary<int, MapTriggerTranslation> Triggers { get; internal set; } = new();

    /// <summary>Object-plane values that turn the wall tile under them into a 45 degree wall</summary>
    public Dictionary<int, MapDiagonalTranslation> Diagonals { get; internal set; } = new();

    /// <summary>Wall patches by id (1 to 255): the high byte of a wall tile's plane 0 value (MapConstants.PatchShift)</summary>
    public Dictionary<int, MapPatchTranslation> Patches { get; internal set; } = new();

    /// <summary>The floor and ceiling textures for the flat plane (plane 2), and the floors that hurt</summary>
    public MapFlatsTranslation Flats { get; internal set; } = new();

    /// <summary>What the plane 0 values that are open floor mean: the area codes, ambush and secret exit</summary>
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

    /// <summary>How UWMF levels' names and numbers are read for this game (mapdefs uwmf.yaml)</summary>
    public MapUwmfTranslation Uwmf { get; internal set; } = new();

    /// <summary>
    /// These mapdefs with a level's own definitions laid over them (each entry replacing the one of
    /// its code), and its area count where it gives one
    /// </summary>
    public MapObjectTranslationAsset With(MapObjectTranslationAsset? local)
    {
        if (local == null)
            return this;

        static Dictionary<int, T> Merged<T>(Dictionary<int, T> under, Dictionary<int, T> over)
        {
            var merged = new Dictionary<int, T>(under);
            foreach (var (key, value) in over)
                merged[key] = value;
            return merged;
        }

        return this with
        {
            Things = Merged(Things, local.Things),
            Walls = Merged(Walls, local.Walls),
            Doors = Merged(Doors, local.Doors),
            PlayerStarts = Merged(PlayerStarts, local.PlayerStarts),
            Triggers = Merged(Triggers, local.Triggers),
            Diagonals = Merged(Diagonals, local.Diagonals),
            Patches = Merged(Patches, local.Patches),
            Flats = Flats with { Floor = Merged(Flats.Floor, local.Flats.Floor), Ceiling = Merged(Flats.Ceiling, local.Flats.Ceiling) },
            Floors = local.Floors.AreaCount is { } count ? Floors with { AreaCount = count } : Floors,
        };
    }
}

/// <summary>
/// How a game reads UWMF levels (ECWolf's text format): the ECWolf class names its things use
/// that this game's actordefs call something else, and the classes Door_Open's lock numbers
/// stand for. A name not listed is the class's own.
/// </summary>
public record MapUwmfTranslation
{
    /// <summary>ECWolf class name to this game's (WolfensteinSS: SS)</summary>
    public Dictionary<string, string> Classes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Door_Open's lock argument (arg3) to the inventory class that opens the door (1: GoldKey)</summary>
    public Dictionary<int, string> Locks { get; set; } = new();

    /// <summary>The game's class for a UWMF thing type</summary>
    public string ClassFor(string type) =>
        Classes.TryGetValue(type, out var name) ? name
        : Classes.FirstOrDefault(c => c.Key.Equals(type, StringComparison.OrdinalIgnoreCase)).Value ?? type;
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
    /// <summary>The floor textures by index (a flat-plane value's low byte)</summary>
    public Dictionary<int, string> Floor { get; set; } = new();

    /// <summary>The ceiling textures by index (a flat-plane value's high byte)</summary>
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
    /// <summary>The health taken each time it hurts</summary>
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
    /// <summary>
    /// The solid corner (SolidNW, SolidNE, SolidSW or SolidSE): its two tile edges stay square
    /// faces, and the diagonal between the other two corners is the angled face
    /// </summary>
    public Enums.WallShape Shape { get; set; }

    /// <summary>The diagonal face's texture. Empty means the wall's own North texture.</summary>
    public string Texture { get; set; } = "";
}

/// <summary>
/// A wall patch: a picture with see-through pixels (palette index 0xFF) drawn over a wall's face,
/// such as a sign, a switch or a crack, so one wall can carry any decoration. A wall tile gets
/// one by the high byte of its plane 0 value (MapConstants.PatchShift); it moves with the wall
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

    /// <summary>
    /// The use hint shown while the player faces it (statusbar.yaml use-hint's hints, by name:
    /// Midnight Assault's switch and take icons); a switch's only from a side it can be used from
    /// </summary>
    public string Hint { get; init; } = "";

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

    /// <summary>
    /// A team start ("red" or "blue"): where that team's players come into a team game (and back
    /// after dying), a free one at random. Other games don't use it.
    /// </summary>
    public string Team { get; set; } = "";

    /// <summary>
    /// An entrance's start: used only when the level is entered by that entrance (the level
    /// before's game-info next-entrance, Midnight Assault's Base from Laboratories), and then
    /// in place of the level's own start. Empty: the level's own start.
    /// </summary>
    public string Entrance { get; set; } = "";
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

    /// <summary>A use trigger's use hint, shown while the player faces its tile (statusbar.yaml use-hint's hints, by name)</summary>
    public string Hint { get; set; } = "";

    /// <summary>
    /// "facing-message": a use trigger's message, kept up while the player faces its tile (a $NAME
    /// language key or the text: Midnight Assault's "Press Use to Exit Level"). For an exit
    /// (A_Exit, A_SecretExit) the objectives keep shut, their refused message shows instead.
    /// </summary>
    public string FacingMessage { get; set; } = "";

    /// <summary>"facing-style": the hud-messages.yaml style the facing message shows in; the locked-door one when unset</summary>
    public string FacingStyle { get; set; } = "";
}

public record MapActorTranslation
{
    /// <summary>The actordefs class placed</summary>
    public string Class { get; set; } = "";

    /// <summary>The way it faces: 0 east, 90 north, 180 west, 270 south</summary>
    public int Angles { get; set; }

    /// <summary>0: it starts in its Spawn state, standing; 1: in its Path state, patrolling (when its class has one)</summary>
    public int Patrol { get; set; }

    /// <summary>
    /// The easiest skill it's placed on, by the skill's place in game-info skills (0 the first:
    /// Wolf3D's 0 baby, 1 easy, 2 medium, 3 hard). On easier skills, else is placed instead.
    /// </summary>
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
    /// <summary>
    /// The texture on the north face, seen from the tile north of it. On a wall, a face left out
    /// shows the opposite one (south falls back on north, west on east, and the other way round).
    /// </summary>
    public string North { get; init; } = "";

    /// <summary>The texture on the south face, seen from the tile south of it</summary>
    public string South { get; init; } = "";

    /// <summary>The texture on the east face, seen from the tile east of it (Wolf3D's darker side)</summary>
    public string East { get; init; } = "";

    /// <summary>The texture on the west face, seen from the tile west of it</summary>
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
    public bool IsFlipped(string face)
    {
        // A plain loop: the renderer asks for every wall column it draws
        foreach (var flipped in Flip)
            if (flipped.Equals(face, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>
    /// Doors only: the inventory item class (e.g. "GoldKey") the player must carry to open
    /// this door. Empty means unlocked.
    /// </summary>
    public string Lock { get; init; } = "";

    /// <summary>
    /// Doors only: several inventory items that lock this door together, such as two or all four
    /// of a level's keys. The player needs <see cref="LocksNeeded"/> of them (all of them when
    /// that's 0), on top of <see cref="Lock"/> if it has one too.
    /// </summary>
    public List<string> Locks { get; init; } = [];

    /// <summary>
    /// Doors only: how many of <see cref="Locks"/> the player must carry to open the door (any of
    /// them). 0, or more than there are, needs all of them.
    /// </summary>
    public int LocksNeeded { get; init; }

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

    /// <summary>
    /// Walls only: the use hint shown while the player faces it (statusbar.yaml use-hint's
    /// hints, by name); a switch's only from a side it can be used from
    /// </summary>
    public string Hint { get; init; } = "";

    /// <summary>Walls only: makes this wall an outlet enemies come out of (Blake Stone's electro-aliens).</summary>
    public MapOutletTranslation? Outlet { get; init; }

    public static MapTextureTranslation None { get; } = new(); // TODO: Missing texture
}

/// <summary>
/// A wall that lets enemies out (Managers/LevelAI.cs): every so often, while the room beside
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
    /// text itself). Empty uses the lock item's `key.switchlockedmessage`, else that it's locked.
    /// </summary>
    public string LockMessage { get; init; } = "";

    /// <summary>The hud-messages.yaml style for <see cref="LockMessage"/>. Empty uses the lock item's `key.lockedmessagestyle`.</summary>
    public string LockMessageStyle { get; init; } = "";

    /// <summary>Played when it's used without its <see cref="Lock"/> item. Empty uses the lock item's `key.lockedsound`.</summary>
    public string LockedSound { get; init; } = "";

    /// <summary>
    /// The action calls to run, in order (`A_Exit`; every switch action is in the actions
    /// reference). They act on whatever shares the switch tile's tag (map plane 4); tag 0 is no
    /// tag, so an untagged switch's find nothing. The switch's own tile is never a target, though
    /// it has the tag, except for A_ChangeTexture, which changes it tagged or not. Write a call
    /// with arguments as a block list item (`- A_SetScroll(7, 0, 4)`): in a [flow, list] its
    /// commas would split it.
    /// </summary>
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
    /// A level objective (game-info objectives) this switch is one of: each wall or patch with it
    /// counts toward the level's total as the level starts, and one is done as it's thrown
    /// (Midnight Assault's sabotage targets). Empty: none.
    /// </summary>
    public string Objective { get; init; } = "";

    /// <summary>
    /// A name unlocked for good when the switch is thrown (Gameplay/Unlocks.cs: progress.cfg),
    /// {map} and {tag} filled in with the level's map and the switch tile's tag, e.g.
    /// "{map}-kilroy-{tag}": Midnight Assault's Kilroys, remembered from game to game. Empty: none.
    /// </summary>
    public string Unlock { get; init; } = "";

    /// <summary>
    /// "unlocked-to": as the level starts afresh, a switch whose <see cref="Unlock"/> name is
    /// unlocked already is shown as this wall or patch id instead, its objective counted done (a
    /// Kilroy drawn in an earlier game, "Kilroy was here"). 0: it's left as it is.
    /// </summary>
    public int UnlockedTo { get; init; }

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
    public bool UsableFrom(ControlDir dir)
    {
        if (Sides.Count == 0)
            return true;

        var face = dir switch
        {
            ControlDir.East => "west",
            ControlDir.West => "east",
            ControlDir.North => "south",
            _ => "north",
        };
        return Sides.Contains(face, StringComparer.OrdinalIgnoreCase);
    }
}
