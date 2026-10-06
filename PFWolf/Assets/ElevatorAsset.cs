namespace PFWolf.Assets;

/// <summary>
/// A game pack's floor-select screen (gamepacks/{pack}/elevator.yaml), as A_FloorSelect shows
/// it (Program.Elevator.cs): Blake Stone's elevator panel. Its buttons are the floors of the
/// level's cluster, by game-info floor-number. Coordinates are on the 320x200 screen and colors
/// are palette indices.
/// </summary>
internal record ElevatorAsset : Asset
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

    public TeleporterLayout? Teleporter { get; set; }

    public override void Merge(Asset other)
    {
        if (other is ElevatorAsset o)
        {
            Style = o.Style;
            Teleporter = o.Teleporter ?? Teleporter;
            UnlockItem = o.UnlockItem ?? UnlockItem;
            Buttons = o.Buttons;
            Frame = o.Frame ?? Frame;
            Panel = o.Panel ?? Panel;
            ButtonLayout = o.ButtonLayout;
            Messages = o.Messages;
            Prompt = o.Prompt ?? Prompt;
            Stats = o.Stats ?? Stats;
            ButtonSound = o.ButtonSound ?? ButtonSound;
            MessageTics = o.MessageTics;
        }
    }
}

/// <summary>Two bevelled boxes, the inner one pressed in: an outer one from (x, y) and an inner one inset by inset-x, inset-y</summary>
internal record ElevatorBox
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int InsetX { get; set; } = 7;
    public int InsetY { get; set; } = 5;
    public int Hi { get; set; } = 0x85;
    public int Med { get; set; } = 0x82;
    public int Lo { get; set; } = 0x80;
}

internal record ElevatorButtons
{
    public string PicPrefix { get; set; } = "FloorButton";
    public string LitPicPrefix { get; set; } = "FloorButtonLit";
    public int X { get; set; }
    public int Y { get; set; }
    public int StepX { get; set; }
    public int StepY { get; set; }

    /// <summary>The selector: a square this size round a button, in these colors</summary>
    public int Size { get; set; } = 17;
    public int CurrentColor { get; set; } = 0x1f;
    public int CurrentAwayColor { get; set; } = 0x18;
    public int SelectedColor { get; set; } = 0xaf;
    public int OtherColor { get; set; } = 0x82;
}

/// <summary>
/// The message area: presenter scripts (VGAGRAPH text names) presented in the box from (x1, y1)
/// to (x2, y2) over pic, with a floor's number printed into them at its own spot
/// </summary>
internal record ElevatorMessages
{
    public IntermissionPic? Pic { get; set; }
    public int X1 { get; set; }
    public int Y1 { get; set; }
    public int X2 { get; set; }
    public int Y2 { get; set; }
    public int Font { get; set; }
    public int LineHeight { get; set; }
    public int Color { get; set; }

    /// <summary>"Current floor: / Select a floor.", with the current floor's number at current-x</summary>
    public string Select { get; set; } = "";
    public int CurrentX { get; set; }

    /// <summary>The unlock item was used to reach the floor</summary>
    public string Unlocked { get; set; } = "";

    /// <summary>"Floor n is locked", with n at locked-x</summary>
    public string Locked { get; set; } = "";
    public int LockedX { get; set; }

    /// <summary>The next floor up, without the unlock item</summary>
    public string NeedItem { get; set; } = "";

    /// <summary>The font number and color the floor numbers are printed in</summary>
    public int NumberFont { get; set; }
    public int NumberColor { get; set; }
}

/// <summary>
/// Planet Strike's teleporter map (Program.Teleporter.cs, bstone's ps_input_floor): pictures
/// drawn first, then each floor's spot on the map, its picture (on for the one chosen) named
/// by the format with the floor's number from 1; up and down buttons lit as they're used; the
/// chosen floor's name in a bar; an overhead map of the floor as last seen; help text.
/// </summary>
internal record TeleporterLayout
{
    public List<IntermissionPic> Background { get; set; } = [];

    /// <summary>Picture names: string.Format with the floor's number from 1, e.g. "TELEON{0:D2}"</summary>
    public string OnPic { get; set; } = "";
    public string OffPic { get; set; } = "";

    /// <summary>Each floor's picture spot, in floor-number order from 0</summary>
    public List<TeleporterSpot> Floors { get; set; } = [];

    public string UpOnPic { get; set; } = "";
    public string UpOffPic { get; set; } = "";
    public string DownOnPic { get; set; } = "";
    public string DownOffPic { get; set; } = "";
    public List<TeleporterSpot> UpSpots { get; set; } = [];
    public List<TeleporterSpot> DownSpots { get; set; } = [];

    /// <summary>The name bar: cleared to bar-color, the floor's name centred at text-y</summary>
    public int BarX { get; set; }
    public int BarY { get; set; }
    public int BarWidth { get; set; }
    public int BarHeight { get; set; }
    public int BarColor { get; set; }
    public int TextY { get; set; }
    public int Font { get; set; }
    public int NameColor { get; set; }
    public string LockedText { get; set; } = "-- TELEPORT DISABLED --";
    public int LockedColor { get; set; }

    /// <summary>The overhead map: 64x64 from (x, y), one pixel a tile; text over it for a floor never mapped</summary>
    public TeleporterSpot Overhead { get; set; } = new();
    public string UnmappedText { get; set; } = "";
    public int UnmappedColor { get; set; }
    public Dictionary<string, int> Colors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The floor's stats (as the elevator's), shown for the floor the player is on</summary>
    public ElevatorStats? Stats { get; set; }

    public string Help { get; set; } = "";
    public TeleporterSpot HelpSpot { get; set; } = new();
    public int HelpColor { get; set; }

    public string LockedSound { get; set; } = "";

    /// <summary>How many times the chosen floor's picture flashes, and how long each half is, in tics</summary>
    public int Flashes { get; set; } = 10;
    public int FlashTics { get; set; } = 4;
}

internal record TeleporterSpot
{
    public int X { get; set; }
    public int Y { get; set; }
}

/// <summary>Where the floor's stat bars go (see Program.Elevator.cs ShowFloorStats)</summary>
internal record ElevatorStats
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Font { get; set; }
    public int Color { get; set; }
}
