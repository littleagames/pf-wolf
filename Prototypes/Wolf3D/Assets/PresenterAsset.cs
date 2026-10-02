namespace Wolf3D.Assets;

/// <summary>
/// A game pack's text presenter settings (gamepacks/{pack}/presenter.yaml): what Blake Stone's
/// ^ codes print with (Program.TextPresenter.cs), and the screens built on it -- the message box
/// over the status bar and the briefing window. A pack that builds on a base-pack starts from its
/// settings; the pack's own replace them one by one.
/// </summary>
internal record PresenterAsset : Asset
{
    /// <summary>The fonts (fonts.yaml) a script's ^FNn picks from: font n is the nth</summary>
    public List<string>? Fonts { get; set; }

    /// <summary>What a script's ^SHnnn draws, by its (hex) number</summary>
    public Dictionary<int, PresenterShape> Shapes { get; set; } = [];

    /// <summary>The box messages are shown in over the status bar ("Get Ready, Blake!")</summary>
    public PresenterMessageBox? MessageBox { get; set; }

    /// <summary>The window briefings are shown in</summary>
    public PresenterBriefing? Briefing { get; set; }

    public override void Merge(Asset other)
    {
        if (other is PresenterAsset otherAsset)
        {
            Fonts = otherAsset.Fonts ?? Fonts;
            foreach (var (number, shape) in otherAsset.Shapes)
                Shapes[number] = shape;
            MessageBox = otherAsset.MessageBox ?? MessageBox;
            Briefing = otherAsset.Briefing ?? Briefing;
        }
    }
}

/// <summary>A shape a script can draw: a picture</summary>
internal record PresenterShape
{
    public string Pic { get; set; } = "";
}

/// <summary>
/// A bevelled box across the screen that a message is presented in, its lines centered
/// up and down in it. Colors are palette indices.
/// </summary>
internal record PresenterMessageBox
{
    public int X { get; set; }
    public int Y { get; set; } = 152;
    public int Width { get; set; } = 320;
    public int Height { get; set; } = 48;

    /// <summary>The bevel's light edge, face and dark edge</summary>
    public int Hi { get; set; } = 0x85;
    public int Med { get; set; } = 0x82;
    public int Lo { get; set; } = 0x80;

    /// <summary>The text's color until the script changes it</summary>
    public int TextColor { get; set; } = 0xaf;

    /// <summary>The font number (in fonts) the text starts in</summary>
    public int Font { get; set; } = 1;
}

/// <summary>
/// The screen a briefing (a mission's start and end text) is presented on: pictures framing a
/// window, the text in the window, and a line of help under it. Colors are palette indices.
/// </summary>
internal record PresenterBriefing
{
    public List<IntermissionPic> Pics { get; set; } = [];

    /// <summary>The window's corners</summary>
    public int X1 { get; set; } = 8;
    public int Y1 { get; set; } = 8;
    public int X2 { get; set; } = 311;
    public int Y2 { get; set; } = 175;

    public int Background { get; set; }
    public int Light { get; set; }
    public int Dark { get; set; }
    public int Shadow { get; set; }

    /// <summary>The font number the text starts in</summary>
    public int Font { get; set; }

    /// <summary>The help line under the window (a $NAME language key or the text)</summary>
    public string InfoLine { get; set; } = "";

    /// <summary>The font number and color of the help line and page numbers</summary>
    public int InfoFont { get; set; }
    public int InfoColor { get; set; }

    /// <summary>Where "PAGE n OF m" goes; none when left out</summary>
    public int? PageX { get; set; }
    public int PageY { get; set; }

    /// <summary>The music while it's up; none keeps what's playing</summary>
    public string? Music { get; set; }
}
