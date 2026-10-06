using PFWolf.Assets;
using PFWolf.Extensions;

namespace PFWolf.Managers;

/// <summary>Where in the 3D view a stack of messages sits</summary>
internal enum HudAnchor : byte
{
    TopLeft, Top, TopRight,
    Left, Center, Right,
    BottomLeft, Bottom, BottomRight,

    /// <summary>In the status bar's info area (statusbar.yaml info-area), not over the view</summary>
    Status,
}

/// <summary>A message style from hud-messages.yaml, with everything its parents give it filled in</summary>
internal sealed record HudMessageStyle(string Name, HudAnchor Anchor, int X, int Y, int Margin,
    string Font, string Color, int Duration, int MaxLines)
{
    /// <summary>Where messages in this style stack up: styles that share it share a stack</summary>
    public HudMessagePosition Position => new(Anchor, X, Y, Margin);
}

internal readonly record struct HudMessagePosition(HudAnchor Anchor, int X, int Y, int Margin);

/// <summary>What a message is about, which picks its default style (game-info hud-messages)</summary>
internal enum HudMessageKind : byte
{
    /// <summary>The `msg` command's: shown even with messages off, in Default</summary>
    Other,
    Pickup,
    Lock,
    Obituary,
}

internal sealed class HudMessage(string text, HudMessageStyle style)
{
    public string Text { get; } = text;
    public HudMessageStyle Style { get; } = style;
    public int TicsLeft { get; set; } = style.Duration;
}

/// <summary>
/// The messages shown over the 3D view (item pickups, locked doors, what killed the player),
/// each in a style from the game pack's hud-messages.yaml. Messages whose styles share a
/// position stack up there, oldest at the top, and each goes when its time runs out. They're
/// drawn by Program.DrawHudMessages and aren't kept in saved games.
/// </summary>
internal class HudMessageManager
{
    internal const string DefaultStyleName = "Default";

    /// <summary>What Default is when hud-messages.yaml doesn't say</summary>
    private static readonly HudMessageStyle BuiltInDefault =
        new(DefaultStyleName, HudAnchor.TopLeft, 0, 0, 2, "SmallFont", "White", 210, 4);

    public HudMessageManager(Lazy<AssetManager> assetManager, Lazy<ConsoleManager> consoleManager)
    {
        this.assetManager = assetManager;
        this.consoleManager = consoleManager;
    }

    private readonly Lazy<AssetManager> assetManager;
    private readonly Lazy<ConsoleManager> consoleManager;
    private readonly List<HudMessage> messages = [];
    private readonly Dictionary<string, HudMessageStyle> styles = new(StringComparer.OrdinalIgnoreCase);
    private HudMessageStylesAsset? definitions;
    private bool definitionsRead;

    /// <summary>
    /// Whether pickups, locked doors and obituaries show messages: the player's choice (the
    /// `msg_enabled` setting, saved in the config) or, until they make one, the game pack's
    /// (game-info hud-messages enabled)
    /// </summary>
    internal bool Enabled => EnabledSetting ?? GameInfo?.Enabled ?? false;

    /// <summary>The player's `msg_enabled` choice; null for the game pack's default</summary>
    internal bool? EnabledSetting { get; set; }

    private HudMessagesInfo? GameInfo =>
        gameInfo ??= assetManager.Value.FindInGamePack<GameInfoAsset>("game-info")?.HudMessages;
    private HudMessagesInfo? gameInfo;

    /// <summary>The style a kind of message is shown in when nothing more particular gives one</summary>
    internal string KindStyle(HudMessageKind kind) => kind switch
    {
        HudMessageKind.Pickup => GameInfo?.PickupStyle ?? DefaultStyleName,
        HudMessageKind.Lock => GameInfo?.LockStyle ?? DefaultStyleName,
        HudMessageKind.Obituary => GameInfo?.ObituaryStyle ?? DefaultStyleName,
        _ => DefaultStyleName,
    };

    /// <summary>The messages up now, oldest first</summary>
    internal IReadOnlyList<HudMessage> Messages => messages;

    /// <summary>The styles hud-messages.yaml defines, and Default</summary>
    internal IEnumerable<string> StyleNames =>
        (Definitions?.Styles.Keys ?? Enumerable.Empty<string>()).Append(DefaultStyleName).Distinct(StringComparer.OrdinalIgnoreCase);

    internal bool StyleExists(string name) =>
        name.Equals(DefaultStyleName, StringComparison.OrdinalIgnoreCase) || Definitions?.Styles.ContainsKey(name) == true;

    /// <summary>
    /// Shows <paramref name="text"/> (a $NAME language key or the text itself) in the named
    /// style, or the one for its <paramref name="kind"/>. Nothing is shown while messages are
    /// off, except the `msg` command's (<see cref="HudMessageKind.Other"/>). ZDoom-style
    /// placeholders in it, such as %k, are filled in from <paramref name="placeholders"/> (%% is
    /// a percent sign). The same text already up at that position is brought back to the bottom
    /// of its stack with its time restarted instead of shown twice. It's printed to the console
    /// too, which keeps a history of them.
    /// </summary>
    internal void Show(HudMessageKind kind, string? text, string? styleName = null,
        IReadOnlyDictionary<char, string>? placeholders = null)
    {
        if (kind != HudMessageKind.Other && !Enabled)
            return;

        text = text == null ? null : FillPlaceholders(Localize(text), placeholders).Trim();
        if (string.IsNullOrEmpty(text))
            return;

        var style = FindStyle(string.IsNullOrWhiteSpace(styleName) ? KindStyle(kind) : styleName);
        messages.RemoveAll(m => m.Style.Position == style.Position && m.Text == text);
        messages.Add(new HudMessage(text, style));

        var stack = messages.Where(m => m.Style.Position == style.Position).ToList();
        for (int i = 0; i < stack.Count - Math.Max(style.MaxLines, 1); i++)
            messages.Remove(stack[i]);

        consoleManager.Value.Print(text);
    }

    /// <summary>The text for a $NAME language key; anything else as it is</summary>
    internal string Localize(string text) => text.ToLanguageText(assetManager.Value.GetText("en-us"));

    /// <summary>
    /// <paramref name="text"/> with each %x that has a value in <paramref name="placeholders"/>
    /// replaced by it and %% by a percent sign; any other % is left as it is
    /// </summary>
    internal static string FillPlaceholders(string text, IReadOnlyDictionary<char, string>? placeholders)
    {
        if (!text.Contains('%'))
            return text;

        var result = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '%' && i + 1 < text.Length)
            {
                char code = text[i + 1];
                if (code == '%')
                {
                    result.Append('%');
                    i++;
                    continue;
                }
                if (placeholders != null && placeholders.TryGetValue(code, out var value))
                {
                    result.Append(value);
                    i++;
                    continue;
                }
            }
            result.Append(text[i]);
        }
        return result.ToString();
    }

    /// <summary>Counts down each message's time, and takes away the ones that have run out</summary>
    internal void Tick(int tics)
    {
        foreach (var message in messages)
            message.TicsLeft -= tics;
        messages.RemoveAll(m => m.TicsLeft <= 0);
    }

    internal void Clear() => messages.Clear();

    /// <summary>The named style, or Default for no name or one hud-messages.yaml doesn't have</summary>
    internal HudMessageStyle FindStyle(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            name = DefaultStyleName;

        if (styles.TryGetValue(name, out var style))
            return style;

        if (!StyleExists(name))
        {
            Console.WriteLine($"No message style '{name}' in hud-messages.yaml; using {DefaultStyleName}");
            style = FindStyle(DefaultStyleName);
        }
        else
            style = Build(name, []);

        styles[name] = style;
        return style;
    }

    /// <summary>
    /// A style with its parents' settings under its own; <paramref name="seen"/> stops a style
    /// that's its own ancestor going round forever
    /// </summary>
    private HudMessageStyle Build(string name, HashSet<string> seen)
    {
        HudMessageStyleDefinition? definition = null;
        Definitions?.Styles.TryGetValue(name, out definition);

        bool isDefault = name.Equals(DefaultStyleName, StringComparison.OrdinalIgnoreCase);
        var parentName = definition?.Parent;
        if (string.IsNullOrWhiteSpace(parentName) && !isDefault)
            parentName = DefaultStyleName;

        HudMessageStyle parent = BuiltInDefault;
        if (!string.IsNullOrWhiteSpace(parentName))
        {
            seen.Add(name);
            if (seen.Contains(parentName))
                Console.WriteLine($"Message style '{name}' has itself as a parent (through '{parentName}')");
            else if (!StyleExists(parentName))
                Console.WriteLine($"Message style '{name}': no parent style '{parentName}'");
            else
                parent = Build(parentName, seen);
        }

        if (definition == null)
            return parent with { Name = name };

        return new HudMessageStyle(
            name,
            ParseAnchor(name, definition.Anchor) ?? parent.Anchor,
            definition.X ?? parent.X,
            definition.Y ?? parent.Y,
            Math.Max(definition.Margin ?? parent.Margin, 0),
            string.IsNullOrWhiteSpace(definition.Font) ? parent.Font : definition.Font,
            string.IsNullOrWhiteSpace(definition.Color) ? parent.Color : definition.Color,
            Math.Max(definition.Duration ?? parent.Duration, 1),
            Math.Max(definition.MaxLines ?? parent.MaxLines, 1));
    }

    private static HudAnchor? ParseAnchor(string styleName, string? anchor)
    {
        if (string.IsNullOrWhiteSpace(anchor))
            return null;
        if (anchor.Equals("Middle", StringComparison.OrdinalIgnoreCase))
            return HudAnchor.Center;
        if (Enum.TryParse<HudAnchor>(anchor, ignoreCase: true, out var value) && Enum.IsDefined(value))
            return value;

        Console.WriteLine($"Message style '{styleName}' has an unknown anchor '{anchor}' ({string.Join(", ", Enum.GetNames<HudAnchor>())})");
        return null;
    }

    private HudMessageStylesAsset? Definitions
    {
        get
        {
            if (!definitionsRead)
            {
                definitionsRead = true;
                if (assetManager.Value.Exists<HudMessageStylesAsset>($"{assetManager.Value.GamePackId}/hud-messages"))
                    definitions = assetManager.Value.FindInGamePack<HudMessageStylesAsset>("hud-messages");
            }
            return definitions;
        }
    }
}
