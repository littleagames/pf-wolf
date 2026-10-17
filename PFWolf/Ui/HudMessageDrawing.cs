using PFWolf.Fonts;

namespace PFWolf.Ui;

/// <summary>
/// The HUD messages (HudMessageManager) drawn over the view each frame (DrawHudMessages), and
/// the obituary the player's death puts up (ShowObituary).
/// </summary>
internal sealed class HudMessageDrawing(
    VideoManager video,
    GraphicManager graphics,
    InventoryManager inventory,
    HudMessageManager hudMessages,
    UiText ui,
    StatusBarDrawing statusBar,
    Players playersManager,
    RenderState render,
    GameSession session)
{
    /// <summary>Lines kept clear at the top of the view (a match's line across it); unset, none</summary>
    internal Func<int>? TopReserved { get; set; }

    /// <summary>
    /// Draws the messages up now (HudMessageManager) over the 3D view. Each position's stack
    /// sits at its anchor, lined up with that side of the view (centered for the middle ones),
    /// with lines too wide for the view wrapped. Everything stays inside the view, which is
    /// redrawn every frame, so a message that goes leaves nothing behind.
    /// </summary>
    internal void DrawHudMessages()
    {
        bool infoArea = statusBar.HasInfoArea;
        if (infoArea)
            statusBar.DrawInfoArea(force: false);
        if (hudMessages.Messages.Count == 0)
            return;

        // The view in the 320x200 virtual pixels text is drawn in: the ones wholly inside it
        int viewX = video.ToLayoutX(render.ViewScreenX, roundUp: true), viewY = video.ToLayoutY(render.ViewScreenY, roundUp: true);
        int viewW = video.ToLayoutX(render.ViewScreenX + render.ViewWidth) - viewX;
        int viewH = video.ToLayoutY(render.ViewScreenY + render.ViewHeight) - viewY;

        // Messages for the status bar's info area go there; with no status bar showing, over the view's top left
        foreach (var stack in hudMessages.Messages
            .Where(m => m.Style.Anchor != HudAnchor.Status || !infoArea)
            .GroupBy(m => m.Style.Anchor == HudAnchor.Status ? m.Style.Position with { Anchor = HudAnchor.TopLeft } : m.Style.Position))
        {
            var position = stack.Key;
            int areaX = viewX + position.Margin, areaY = viewY + position.Margin;
            int areaW = viewW - 2 * position.Margin, areaH = viewH - 2 * position.Margin;
            if (areaW <= 0 || areaH <= 0)
                continue;

            var lines = new List<(string Text, HudMessageStyle Style, int Width, int Height)>();
            foreach (var message in stack)
            {
                foreach (var line in WrapText(message.Text, message.Style.Font, areaW))
                {
                    ui.MeasureText(line, message.Style.Font, out int w, out int h);
                    lines.Add((line, message.Style, w, h));
                }
            }

            int blockHeight = lines.Sum(l => l.Height);
            int y = VerticalPlace(position.Anchor) switch
            {
                0 => areaY + (TopReserved?.Invoke() ?? 0),     // under a match's line across the top (Ui/NetOverlay.cs)
                1 => areaY + (areaH - blockHeight) / 2,
                _ => areaY + areaH - blockHeight,
            } + position.Y;

            foreach (var (text, style, width, height) in lines)
            {
                // A line that would reach outside the view is left out rather than drawn over the border
                if (y >= viewY && y + height <= viewY + viewH)
                {
                    int x = HorizontalPlace(position.Anchor) switch
                    {
                        0 => areaX,
                        1 => areaX + (areaW - width) / 2,
                        _ => areaX + areaW - width,
                    } + position.X;
                    x = Math.Clamp(x, viewX, Math.Max(viewX, viewX + viewW - width));

                    graphics.DrawText(x, y, text, new TextStyle(style.Font, style.Color));
                }
                y += height;
            }
        }
    }

    /// <summary>
    /// Says what killed the player (session.PlayerState.LastAttacker): its `obituary` in its `obituarystyle` (or
    /// game-info's for obituaries). A
    /// projectile without an obituary of its own is put down to whoever fired it. A killer with
    /// none says "killed by", and no killer at all (the `hurt` command) that the player died.
    /// %o in the text is the player's tag and %k the killer's (the shooter's, for a projectile).
    /// </summary>
    internal void ShowObituary()
    {
        var killer = session.PlayerState.LastAttacker;
        var source = killer?.Shooter != null && !killer.Properties.ContainsKey("obituary") ? killer.Shooter : killer;

        string text = source == null ? "$OB_DIED" : PropertyText(source, "obituary") ?? "$OB_KILLED";
        string? style = source == null ? null : PropertyText(source, "obituarystyle");

        hudMessages.Show(HudMessageKind.Obituary, text, style, new Dictionary<char, string>
        {
            ['o'] = playersManager.PlayerName(session.Player) ?? ActorTag(session.Player),
            ['k'] = killer == null ? "" : (killer.Shooter ?? killer) is Entities.Actors.PlayerPawn rival ? playersManager.PlayerName(rival) ?? ActorTag(rival) : ActorTag(killer.Shooter ?? killer),
        });
    }

    /// <summary>
    /// An actor's name for messages: its `tag` (a $NAME language key or the name), or its class.
    /// The class's actordefs are asked too, for the player, whose actor is built in code.
    /// </summary>
    internal string ActorTag(Entities.Actors.Actor actor) =>
        (PropertyText(actor, "tag") ?? inventory.GetStringProperty(actor.Name, "tag")) is { Length: > 0 } tag
            ? hudMessages.Localize(tag)
            : actor.Name;

    internal string? PropertyText(Entities.Actors.Actor actor, string key) =>
        actor.Properties.TryGetValue(key, out var value) && value?.ToString() is { Length: > 0 } text ? text : null;

    /// <summary>0 for an anchor on the left, 1 for the middle, 2 for the right</summary>
    internal int HorizontalPlace(HudAnchor anchor) => (int)anchor % 3;

    /// <summary>0 for an anchor at the top, 1 for the middle, 2 for the bottom</summary>
    internal int VerticalPlace(HudAnchor anchor) => (int)anchor / 3;

    /// <summary>
    /// <paramref name="text"/> split into lines no wider than <paramref name="width"/>: at its
    /// newlines, then between words, and inside a word only when it's wider than a line by itself
    /// </summary>
    internal List<string> WrapText(string text, string font, int width)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r", "").Split('\n'))
        {
            var line = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (ui.TextWidth(candidate, font) <= width)
                {
                    line = candidate;
                    continue;
                }

                if (line.Length > 0)
                    lines.Add(line);

                line = word;
                while (line.Length > 1 && ui.TextWidth(line, font) > width)
                {
                    var piece = ui.FitText(line, width, font);
                    lines.Add(piece);
                    line = line[piece.Length..];
                }
            }
            lines.Add(line);
        }
        return lines;
    }
}
