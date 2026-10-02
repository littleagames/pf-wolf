namespace Wolf3D.Assets;

internal record MapObjectTranslationAsset : Asset
{
    public Dictionary<int, MapActorTranslation> Things { get; internal set; } = new();
    public Dictionary<int, MapTextureTranslation> Walls { get; internal set; } = new();
    public Dictionary<int, MapTextureTranslation> Doors { get; internal set; } = new();
    public Dictionary<int, MapPlayerStartTranslation> PlayerStarts { get; internal set; } = new();
    public Dictionary<int, MapTriggerTranslation> Triggers { get; internal set; } = new();
    public Dictionary<int, MapDiagonalTranslation> Diagonals { get; internal set; } = new();
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

    public override void Merge(Asset other)
    {
        if (other is MapObjectTranslationAsset otherAsset)
        {
            foreach (var item in otherAsset.Things)
            {
                this.Things[item.Key] = item.Value;
            }

            foreach (var item in otherAsset.Walls)
            {
                this.Walls[item.Key] = item.Value;
            }

            foreach (var item in otherAsset.Doors)
            {
                this.Doors[item.Key] = item.Value;
            }

            foreach (var item in otherAsset.PlayerStarts)
            {
                this.PlayerStarts[item.Key] = item.Value;
            }

            foreach (var item in otherAsset.Triggers)
            {
                this.Triggers[item.Key] = item.Value;
            }

            foreach (var item in otherAsset.Diagonals)
            {
                this.Diagonals[item.Key] = item.Value;
            }

            foreach (var item in otherAsset.Flats.Floor)
            {
                this.Flats.Floor[item.Key] = item.Value;
            }

            foreach (var item in otherAsset.Flats.Ceiling)
            {
                this.Flats.Ceiling[item.Key] = item.Value;
            }

            foreach (var item in otherAsset.MapInfo)
            {
                this.MapInfo[item.Key] = item.Value;
            }

            foreach (var item in otherAsset.DoorLocks)
            {
                this.DoorLocks[item.Key] = item.Value;
            }

            foreach (var item in otherAsset.LinkedThings)
            {
                if (!this.LinkedThings.Contains(item))
                    this.LinkedThings.Add(item);
            }

            this.Floors = this.Floors.MergedWith(otherAsset.Floors);
        }
    }
}

/// <summary>
/// What a mapdefs map-info code holds. The code's own low byte is unused; the map's value is in
/// the object-plane tile after it (the next one east), which isn't a thing either.
/// </summary>
internal static class MapInfoCodes
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
    /// text Text, about the room whose floor it's on, for the actors that talk (Program.BlakeAI.cs)
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
internal record MapFloorsTranslation
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

    public MapFloorsTranslation MergedWith(MapFloorsTranslation other) => new()
    {
        AreaStart = other.AreaStart ?? AreaStart,
        AreaCount = other.AreaCount ?? AreaCount,
        Ambush = other.Ambush ?? Ambush,
        SecretExit = other.SecretExit ?? SecretExit,
        HiddenAreaStart = other.HiddenAreaStart ?? HiddenAreaStart,
        Triggers = other.Triggers ?? Triggers,
        ActorCodes = other.ActorCodes ?? ActorCodes,
    };
}

/// <summary>What a floors actor-code does to the enemy standing on it. See <see cref="MapFloorsTranslation.ActorCodes"/>.</summary>
internal record MapActorCodeTranslation
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
internal record MapFlatsTranslation
{
    public Dictionary<int, string> Floor { get; set; } = new();
    public Dictionary<int, string> Ceiling { get; set; } = new();
}

/// <summary>
/// An object-plane marker that turns the wall tile under it into a 45 degree wall. The wall
/// keeps its plane 0 texture id; this only gives it a shape (and optionally its diagonal face's
/// texture). A marker on anything but a wall tile is ignored.
/// </summary>
internal record MapDiagonalTranslation
{
    public Enums.WallShape Shape { get; set; }

    /// <summary>The diagonal face's texture. Empty means the wall's own North texture.</summary>
    public string Texture { get; set; } = "";
}

/// <summary>An object-plane tile the player starts the level on, facing <see cref="Angles"/>.</summary>
internal record MapPlayerStartTranslation
{
    /// <summary>0=east, 90=north, 180=west, 270=south, as for <see cref="MapActorTranslation.Angles"/>.</summary>
    public int Angles { get; set; }
}

/// <summary>
/// An object-plane tile the player sets off (e.g. a pushwall, the end-of-castle exit), running
/// <see cref="Action"/> through Entities.MapTriggerRegistry. One-shot: once the action goes off,
/// the tile is cleared.
/// </summary>
internal record MapTriggerTranslation
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
}

internal record MapActorTranslation
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
}

internal record MapTextureTranslation
{
    public string North { get; init; } = "";
    public string South { get; init; } = "";
    public string East { get; init; } = "";
    public string West { get; init; } = "";

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
/// A wall that lets enemies out (Program.BlakeMachines.cs): every so often, while the room beside
/// it joins the player's, one comes out onto open floor beside it.
/// </summary>
internal record MapOutletTranslation
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
/// switch is thrown once and stays that way.
/// </summary>
internal record MapSwitchTranslation
{
    /// <summary>The wall id it turns into when used (1 to 63); 0 leaves the wall as it is.</summary>
    public int To { get; init; }

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