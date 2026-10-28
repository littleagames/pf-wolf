namespace PFWolf.Assets;

/// <summary>
/// A game pack's floor-select screen (gamepacks/{pack}/elevator.yaml), as A_FloorSelect shows
/// it (Ui/ElevatorScreens.cs): Blake Stone's elevator panel. Its buttons are the floors of the
/// level's cluster, by game-info floor-number. Coordinates are on the 320x200 screen and colors
/// are palette indices.
/// </summary>
public record ElevatorAsset : Asset
{
    /// <summary>
    /// The item that unlocks the next floor up, used up as it does; with none every floor is
    /// open. A floor is unlocked for good once the player has been on it.
    /// </summary>
    public string? UnlockItem { get; set; }

    /// <summary>How many buttons the panel has, floors 1 to buttons</summary>
    public int Buttons { get; set; } = 10;

    /// <summary>The bevelled frame drawn first</summary>
    public ElevatorBox? Frame { get; set; }

    /// <summary>The panel's picture, drawn over the frame (Blake Stone's FloorSelect)</summary>
    public IntermissionPic? Panel { get; set; }

    /// <summary>
    /// The buttons: button n's picture is pic-prefix + n (lit-pic-prefix when pressed); button 1
    /// sits at (x, y), each next one step-x across, and each second one step-y up
    /// </summary>
    public ElevatorButtons ButtonLayout { get; set; } = new();

    /// <summary>The message area at the panel's top: what the screen says to do, and why a floor can't be reached</summary>
    public ElevatorMessages Messages { get; set; } = new();

    /// <summary>A presenter script shown in the message box (presenter.yaml) under the panel; a VGAGRAPH text name</summary>
    public string? Prompt { get; set; }

    /// <summary>Where the floor's stats go: the left of the bars and the top of the first</summary>
    public ElevatorStats? Stats { get; set; }

    /// <summary>The floor's overhead map on the panel, as the player has seen it; none when left out</summary>
    public ElevatorOverhead? Overhead { get; set; }

    /// <summary>Played as a button is pressed</summary>
    public string? ButtonSound { get; set; }

    /// <summary>How long a "can't go there" message stays up, in tics, unless a key is pressed</summary>
    public int MessageTics { get; set; } = 210;

    /// <summary>
    /// "panel" (the default): Blake Stone's elevator buttons, the fields above. "teleporter":
    /// Planet Strike's teleporter map, laid out by <see cref="Teleporter"/>; a floor is open once
    /// it's been unlocked (A_UnlockFloor) or been on.
    /// </summary>
    public string Style { get; set; } = "panel";

    /// <summary>The teleporter map's layout, for style: teleporter (Planet Strike)</summary>
    public TeleporterLayout? Teleporter { get; set; }
}

/// <summary>
/// The floor's overhead map on the elevator panel (bstone's ShowOverhead with the whole map):
/// a pixel a tile, north up, of what the player has seen, with keys lying about and the player
/// </summary>
public record ElevatorOverhead
{
    /// <summary>The map's top left corner</summary>
    public int X { get; set; }

    /// <summary>The map's top edge</summary>
    public int Y { get; set; }

    /// <summary>Palette indices by name: background (unseen), unmapped (walls), floor, hidden (floor in a hidden area), door, locked, key, player</summary>
    public Dictionary<string, int> Colors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Two bevelled boxes, the inner one pressed in: an outer one from (x, y) and an inner one inset by inset-x, inset-y</summary>
public record ElevatorBox
{
    /// <summary>The outer box's left edge</summary>
    public int X { get; set; }

    /// <summary>The outer box's top edge</summary>
    public int Y { get; set; }

    /// <summary>The outer box's width</summary>
    public int Width { get; set; }

    /// <summary>The outer box's height</summary>
    public int Height { get; set; }

    /// <summary>How far in the inner box is from the outer one's left and right edges</summary>
    public int InsetX { get; set; } = 7;

    /// <summary>How far in the inner box is from the outer one's top and bottom edges</summary>
    public int InsetY { get; set; } = 5;

    /// <summary>The bevel's light edge (the outer box's top and left; the inner one's bottom and right, so it looks pressed in)</summary>
    public int Hi { get; set; } = 0x85;

    /// <summary>The boxes' face</summary>
    public int Med { get; set; } = 0x82;

    /// <summary>The bevel's dark edge (the outer box's bottom and right; the inner one's top and left)</summary>
    public int Lo { get; set; } = 0x80;
}

public record ElevatorButtons
{
    /// <summary>Button n's picture is this followed by n (FloorButton1)</summary>
    public string PicPrefix { get; set; } = "FloorButton";

    /// <summary>Button n's picture while it's pressed is this followed by n (FloorButtonLit1)</summary>
    public string LitPicPrefix { get; set; } = "FloorButtonLit";

    /// <summary>Button 1's left edge; the odd-numbered floors' column</summary>
    public int X { get; set; }

    /// <summary>Button 1's top edge: the bottom row</summary>
    public int Y { get; set; }

    /// <summary>Pixels across to the even-numbered floors' column</summary>
    public int StepX { get; set; }

    /// <summary>Pixels up from one row of two buttons to the next</summary>
    public int StepY { get; set; }

    /// <summary>The selector: a square this size round a button, in these colors</summary>
    public int Size { get; set; } = 17;

    /// <summary>The square round the floor the player is on, while it's selected</summary>
    public int CurrentColor { get; set; } = 0x1f;

    /// <summary>The square round the floor the player is on, while another is selected</summary>
    public int CurrentAwayColor { get; set; } = 0x18;

    /// <summary>The square round the selected floor</summary>
    public int SelectedColor { get; set; } = 0xaf;

    /// <summary>The square round every other button</summary>
    public int OtherColor { get; set; } = 0x82;
}

/// <summary>
/// The message area: presenter scripts (VGAGRAPH text names) presented in the box from (x1, y1)
/// to (x2, y2) over pic, with a floor's number printed into them at its own spot
/// </summary>
public record ElevatorMessages
{
    /// <summary>A picture drawn first, under each message (Blake Stone's FloorSelectBar)</summary>
    public IntermissionPic? Pic { get; set; }

    /// <summary>The box's left edge</summary>
    public int X1 { get; set; }

    /// <summary>The box's top edge; floor numbers are printed 2 below it</summary>
    public int Y1 { get; set; }

    /// <summary>The box's right edge</summary>
    public int X2 { get; set; }

    /// <summary>The box's bottom edge</summary>
    public int Y2 { get; set; }

    /// <summary>The font number (presenter.yaml fonts) the messages start in</summary>
    public int Font { get; set; }

    /// <summary>Pixels from one line of a message to the next</summary>
    public int LineHeight { get; set; }

    /// <summary>The messages' color until a script changes it</summary>
    public int Color { get; set; }

    /// <summary>"Current floor: / Select a floor.", with the current floor's number at current-x</summary>
    public string Select { get; set; } = "";

    /// <summary>Where the current floor's number goes in the select message</summary>
    public int CurrentX { get; set; }

    /// <summary>The unlock item was used to reach the floor</summary>
    public string Unlocked { get; set; } = "";

    /// <summary>"Floor n is locked", with n at locked-x</summary>
    public string Locked { get; set; } = "";

    /// <summary>Where the floor's number goes in the locked message</summary>
    public int LockedX { get; set; }

    /// <summary>The next floor up, without the unlock item</summary>
    public string NeedItem { get; set; } = "";

    /// <summary>The font number and color the floor numbers are printed in</summary>
    public int NumberFont { get; set; }

    /// <summary>The floor numbers' color</summary>
    public int NumberColor { get; set; }
}

/// <summary>
/// Planet Strike's teleporter map (Ui/ElevatorScreens.cs, bstone's ps_input_floor): pictures
/// drawn first, then each floor's spot on the map, its picture (on for the one chosen) named
/// by the format with the floor's number from 1; up and down buttons lit as they're used; the
/// chosen floor's name in a bar; an overhead map of the floor as last seen; help text.
/// </summary>
public record TeleporterLayout
{
    /// <summary>The pictures drawn first, on a black screen (Planet Strike's TELETOP and TELEBOT)</summary>
    public List<IntermissionPic> Background { get; set; } = [];

    /// <summary>Picture names: string.Format with the floor's number from 1, e.g. "TELEON{0:D2}"</summary>
    public string OnPic { get; set; } = "";

    /// <summary>The picture for a floor that isn't chosen, named as on-pic is ("TELEOF{0:D2}")</summary>
    public string OffPic { get; set; } = "";

    /// <summary>Each floor's picture spot, in floor-number order from 0</summary>
    public List<TeleporterSpot> Floors { get; set; } = [];

    /// <summary>The up button while the selector moves up</summary>
    public string UpOnPic { get; set; } = "";

    /// <summary>The up button otherwise</summary>
    public string UpOffPic { get; set; } = "";

    /// <summary>The down button while the selector moves down</summary>
    public string DownOnPic { get; set; } = "";

    /// <summary>The down button otherwise</summary>
    public string DownOffPic { get; set; } = "";

    /// <summary>Where the up button is drawn: every spot listed (Planet Strike's has one each side)</summary>
    public List<TeleporterSpot> UpSpots { get; set; } = [];

    /// <summary>Where the down button is drawn: every spot listed</summary>
    public List<TeleporterSpot> DownSpots { get; set; } = [];

    /// <summary>Palette index left out of the floor and button pictures (bstone VWB_DrawMPic: 255); -1 for none</summary>
    public int MaskColor { get; set; } = -1;

    /// <summary>The name bar: cleared to bar-color, the floor's name centred at text-y</summary>
    public int BarX { get; set; }

    /// <summary>The name bar's top edge</summary>
    public int BarY { get; set; }

    /// <summary>The name bar's width</summary>
    public int BarWidth { get; set; }

    /// <summary>The name bar's height</summary>
    public int BarHeight { get; set; }

    /// <summary>The name bar's color</summary>
    public int BarColor { get; set; }

    /// <summary>The top of the floor's name in the bar, centred across the screen</summary>
    public int TextY { get; set; }

    /// <summary>The font number (presenter.yaml fonts) for the floor's name, the unmapped text and the help</summary>
    public int Font { get; set; }

    /// <summary>The floor's name's color</summary>
    public int NameColor { get; set; }

    /// <summary>Shown in the bar in place of the name for a floor that isn't open yet</summary>
    public string LockedText { get; set; } = "-- TELEPORT DISABLED --";

    /// <summary>The locked text's color</summary>
    public int LockedColor { get; set; }

    /// <summary>The overhead map: 64x64 from (x, y), one pixel a tile; text over it for a floor never mapped</summary>
    public TeleporterSpot Overhead { get; set; } = new();

    /// <summary>Text over the overhead map for a floor never mapped or not open; \n starts a new line</summary>
    public string UnmappedText { get; set; } = "";

    /// <summary>The unmapped text's color</summary>
    public int UnmappedColor { get; set; }

    /// <summary>
    /// The overhead map's colors, by name: background (tiles not seen), unmapped (walls), floor
    /// (open floor, and open doors), door (a shut door), locked (a door with a lock) and player
    /// (where the player stands, on their own floor)
    /// </summary>
    public Dictionary<string, int> Colors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The floor's stats (as the elevator's), shown for the floor the player is on</summary>
    public ElevatorStats? Stats { get; set; }

    /// <summary>A line of help text, drawn with a black shadow</summary>
    public string Help { get; set; } = "";

    /// <summary>Where the help text starts</summary>
    public TeleporterSpot HelpSpot { get; set; } = new();

    /// <summary>The help text's color</summary>
    public int HelpColor { get; set; }

    /// <summary>Played when the player picks a floor that isn't open</summary>
    public string LockedSound { get; set; } = "";

    /// <summary>How many times the chosen floor's picture flashes, and how long each half is, in tics</summary>
    public int Flashes { get; set; } = 10;

    /// <summary>How long each half of a flash lasts, in tics (70 a second)</summary>
    public int FlashTics { get; set; } = 4;
}

public record TeleporterSpot
{
    /// <summary>Across, in 320x200 pixels</summary>
    public int X { get; set; }

    /// <summary>Down, in 320x200 pixels</summary>
    public int Y { get; set; }
}

/// <summary>Where the floor's stat bars go (see Ui/ElevatorScreens.cs ShowFloorStats)</summary>
public record ElevatorStats
{
    /// <summary>
    /// The bars' left edge; each percentage is printed 52 to the right. The bars are the floor's
    /// points, informants and enemies, then its score, then the mission's.
    /// </summary>
    public int X { get; set; }

    /// <summary>The first bar's top edge; each next one 7 below (the floor's score score-gap below the enemies')</summary>
    public int Y { get; set; }

    /// <summary>How far below the enemies' bar the floor's score is (Aliens of Gold 12, Planet Strike 13)</summary>
    public int ScoreGap { get; set; } = 12;

    /// <summary>The font number (presenter.yaml fonts) the percentages are printed in</summary>
    public int Font { get; set; }

    /// <summary>The percentages' color</summary>
    public int Color { get; set; }
}
