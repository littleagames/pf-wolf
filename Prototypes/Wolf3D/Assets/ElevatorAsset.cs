namespace Wolf3D.Assets;

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

    public override void Merge(Asset other)
    {
        if (other is ElevatorAsset o)
        {
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

/// <summary>Where the floor's stat bars go (see Program.Elevator.cs ShowFloorStats)</summary>
internal record ElevatorStats
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Font { get; set; }
    public int Color { get; set; }
}
