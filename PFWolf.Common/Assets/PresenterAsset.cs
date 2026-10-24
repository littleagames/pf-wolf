namespace PFWolf.Assets;

/// <summary>
/// A game pack's text presenter settings (gamepacks/{pack}/presenter.yaml): what Blake Stone's
/// ^ codes print with (Ui/TextPresenter.cs), and the screens built on it -- the message box
/// over the status bar and the briefing window. A pack that builds on a base-pack starts from its
/// settings; the pack's own replace them one by one.
/// </summary>
public record PresenterAsset : Asset
{
    /// <summary>The fonts (fonts.yaml) a script's ^FNn picks from: font n is the nth</summary>
    public List<string>? Fonts { get; set; }

    /// <summary>What a script's ^SHnnn draws, by its (hex) number</summary>
    public Dictionary<int, PresenterShape> Shapes { get; set; } = [];

    /// <summary>What a script's ^ANnn animates where it stands while the page is up, by its (hex) number</summary>
    public Dictionary<int, PresenterAnimation> Animations { get; set; } = [];

    /// <summary>The sound a script's ^PSnn plays, by its (hex) number</summary>
    public Dictionary<int, string> Sounds { get; set; } = [];

    /// <summary>The box messages are shown in over the status bar ("Get Ready, Blake!")</summary>
    public PresenterMessageBox? MessageBox { get; set; }

    /// <summary>The window briefings are shown in</summary>
    public PresenterBriefing? Briefing { get; set; }
}

/// <summary>
/// A shape a script can draw: a picture, or a sprite or wall texture at 64x64 (a sprite's
/// see-through parts let what's behind show)
/// </summary>
public record PresenterShape
{
    /// <summary>A picture, drawn at its own size</summary>
    public string? Pic { get; set; }

    /// <summary>A sprite (GARDA1), drawn 64x64 with what's behind showing through its see-through parts</summary>
    public string? Sprite { get; set; }

    /// <summary>A wall texture, drawn 64x64</summary>
    public string? Wall { get; set; }
}

/// <summary>
/// A shape a script animates (bstone's piAnimTable): sprites drawn 64x64 one after another, over
/// and over while the page is up
/// </summary>
public record PresenterAnimation
{
    /// <summary>The frames, in order (RENTB8, RENTC8...)</summary>
    public List<string> Sprites { get; set; } = [];

    /// <summary>Tics each frame shows for</summary>
    public int Tics { get; set; } = 20;

    /// <summary>Back and forth (first to last, then back to the first) instead of round from the first again</summary>
    public bool Rebound { get; set; }
}

/// <summary>
/// A bevelled box across the screen that a message is presented in, its lines centered
/// up and down in it. Colors are palette indices.
/// </summary>
public record PresenterMessageBox
{
    /// <summary>The box's left edge, in 320x200 pixels</summary>
    public int X { get; set; }

    /// <summary>The box's top edge (152: over the status bar)</summary>
    public int Y { get; set; } = 152;

    /// <summary>The box's width</summary>
    public int Width { get; set; } = 320;

    /// <summary>The box's height</summary>
    public int Height { get; set; } = 48;

    /// <summary>The bevel's light edge, face and dark edge</summary>
    public int Hi { get; set; } = 0x85;

    /// <summary>The box's face, behind the text</summary>
    public int Med { get; set; } = 0x82;

    /// <summary>The bevel's dark edge, and the text's shadow</summary>
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
public record PresenterBriefing
{
    /// <summary>Pictures drawn first, framing the window</summary>
    public List<IntermissionPic> Pics { get; set; } = [];

    /// <summary>The window's corners</summary>
    public int X1 { get; set; } = 8;

    /// <summary>The window's top edge</summary>
    public int Y1 { get; set; } = 8;

    /// <summary>The window's right edge</summary>
    public int X2 { get; set; } = 311;

    /// <summary>The window's bottom edge</summary>
    public int Y2 { get; set; } = 175;

    /// <summary>The window's color, filled in first, and behind the help line (a script's ^BC changes it)</summary>
    public int Background { get; set; }

    /// <summary>The light top and left edges of boxes the script draws (^LC changes it)</summary>
    public int Light { get; set; }

    /// <summary>The dark bottom and right edges of boxes the script draws (^DC changes it)</summary>
    public int Dark { get; set; }

    /// <summary>The color of shadowed text's shadow and boxes' drop shadows, the help line's and page numbers' too (^SC changes it)</summary>
    public int Shadow { get; set; }

    /// <summary>The font number the text starts in</summary>
    public int Font { get; set; }

    /// <summary>The help line under the window (a $NAME language key or the text)</summary>
    public string InfoLine { get; set; } = "";

    /// <summary>The font number and color of the help line and page numbers</summary>
    public int InfoFont { get; set; }

    /// <summary>The help line's and page numbers' color</summary>
    public int InfoColor { get; set; }

    /// <summary>Where "PAGE n OF m" goes; none when left out</summary>
    public int? PageX { get; set; }

    /// <summary>The top of "PAGE n OF m"</summary>
    public int PageY { get; set; }

    /// <summary>The music while it's up; none keeps what's playing</summary>
    public string? Music { get; set; }

    /// <summary>
    /// The help line for text pages shown from the menu (the instructions, the story), which
    /// only Esc leaves; the briefing's info-line when unset
    /// </summary>
    public string? PagesInfoLine { get; set; }

    /// <summary>The music for text pages shown from the menu; none keeps what's playing</summary>
    public string? PagesMusic { get; set; }
}
