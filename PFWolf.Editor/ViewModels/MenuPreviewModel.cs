using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PFWolf.Editor.Data;
using PFWolf.Editor.Rendering;
using PFWolf.Menus;

namespace PFWolf.Editor.ViewModels;

/// <summary>
/// What a preview's parts are dragged into: the menu window's fields. The menu is drawn as it
/// would be while a part's dragged, and the drag becomes one change when it's let go.
/// </summary>
public interface IMenuLayoutTarget
{
    bool CanMove(MenuPart part);
    bool CanResize(MenuPart part);

    /// <summary>The menu with the part dragged this far, to draw while it's dragged; null to draw it as it is</summary>
    MenuMetadata? Dragged(MenuPart part, Editing.MenuDragMode mode, int dx, int dy, bool unlock, int snap);

    /// <summary>The drag let go: the change made (one step to undo)</summary>
    void Dropped(MenuPart part, Editing.MenuDragMode mode, int dx, int dy, bool unlock, int snap);
}

/// <summary>
/// A menu's live preview, as the game draws it (Rendering/MenuPreview): shared by the menu window
/// and the Definitions window's YAML tab. The cursor moves as the game's does (Up, Down, Home,
/// End, an item's letter), a highlight bar flashes, and a part of the picture can be picked.
/// </summary>
public sealed partial class MenuPreviewModel : ObservableObject, IDisposable
{
    private GameContent? _content;
    private MenuPreview? _preview;
    private string _name = "";
    private MenuMetadata? _menu;
    private MenuPicture? _picture;
    private readonly DispatcherTimer _flash;

    public MenuPreviewModel()
    {
        _flash = new DispatcherTimer();
        _flash.Tick += (_, _) =>
        {
            _barOn = !_barOn;
            Redraw();
        };
    }

    private bool _barOn = true;
    private int? _cursor;

    [ObservableProperty] private Bitmap? _image;
    [ObservableProperty] private double _imageWidth;
    [ObservableProperty] private double _imageHeight;

    /// <summary>Screen pixels to a layout pixel</summary>
    [ObservableProperty] private int _zoom = 2;

    /// <summary>The picture 384 pixels wide, with margins as a wide screen has them</summary>
    [ObservableProperty] private bool _wide;

    /// <summary>The main menu while a game is going on (Back to Game, Save game)</summary>
    [ObservableProperty] private bool _inGame;

    /// <summary>Every other toggle's checkbox on (else all off)</summary>
    [ObservableProperty] private bool _checksOn = true;

    /// <summary>The highlight bar flashes as the game's does</summary>
    [ObservableProperty] private bool _flashing = true;

    /// <summary>How many items-source rows to make up (to try fit-items); 0 or less for the game's own</summary>
    [ObservableProperty] private decimal? _rowCount;

    /// <summary>Whether the menu builds its rows from the game (so a number of them can be tried)</summary>
    [ObservableProperty] private bool _hasItemsSource;

    /// <summary>The part picked, to outline</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPicked), nameof(PickedLabel))]
    private MenuPart? _picked;

    [ObservableProperty] private double _pickedLeft;
    [ObservableProperty] private double _pickedTop;
    [ObservableProperty] private double _pickedWidth;
    [ObservableProperty] private double _pickedHeight;

    /// <summary>Where the cursor is and what the picture shows ("Row 3 of 9")</summary>
    [ObservableProperty] private string _caption = "";

    public bool HasPicked => Picked != null;
    public string PickedLabel => Picked?.Label ?? "";

    /// <summary>What drawing the menu found wrong: unknown colors, fonts that don't build</summary>
    public ObservableCollection<string> Problems { get; } = [];

    /// <summary>A part was picked on the picture (clicked)</summary>
    public event EventHandler<MenuPart>? PartPicked;

    /// <summary>A part was double-clicked: open what it is (a picture, the menu an item opens)</summary>
    public event EventHandler<MenuPart>? PartOpened;

    /// <summary>The menu shown, as drawn last (its rows, layout and parts); null with none</summary>
    public MenuPicture? Picture => _picture;
    public MenuMetadata? Menu => _menu;
    public string Name => _name;

    /// <summary>The game's colors changed (the palette window): the picture is drawn again</summary>
    public void Recolor()
    {
        _preview = _content != null ? new MenuPreview(_content) : null;
        Redraw();
    }

    /// <summary>Shows a menu (null: nothing); the cursor goes back to where the menu starts it unless it's the same menu again</summary>
    public void Show(GameContent? content, string name, MenuMetadata? menu)
    {
        if (!ReferenceEquals(content, _content))
        {
            _content = content;
            _preview = content != null ? new MenuPreview(content) : null;
        }
        if (!name.Equals(_name, StringComparison.OrdinalIgnoreCase))
        {
            _cursor = null;
            Picked = null;
            RowCount = null;
        }
        _name = name;
        _menu = menu;
        HasItemsSource = menu?.ItemsSource != null;
        RestartFlash();
        Redraw();
    }

    private void RestartFlash()
    {
        _flash.Stop();
        _barOn = true;
        if (Flashing && _menu?.Cursor is { FlashTics: > 0 } bar)
        {
            _flash.Interval = TimeSpan.FromSeconds(bar.FlashTics / 70.0);
            _flash.Start();
        }
    }

    partial void OnZoomChanged(int value) => Redraw();
    partial void OnWideChanged(bool value) => Redraw();
    partial void OnInGameChanged(bool value) => Redraw();
    partial void OnChecksOnChanged(bool value) => Redraw();
    partial void OnFlashingChanged(bool value) { RestartFlash(); Redraw(); }
    partial void OnRowCountChanged(decimal? value) => Redraw();

    /// <summary>Draws the menu again as it stands</summary>
    public void Redraw()
    {
        var old = Image;
        Problems.Clear();
        if (_content == null || _preview == null || _menu == null)
        {
            _picture = null;
            Image = null;
            Caption = "";
            old?.Dispose();
            return;
        }

        var state = new MenuPreviewState(_cursor, InGame, ChecksOn, RowCount is > 0 and var count ? (int)count : null, _barOn, Wide);
        try
        {
            _picture = _preview.Draw(_name, _menu, state);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or NullReferenceException or IndexOutOfRangeException or FormatException)
        {
            _picture = null;
            Image = null;
            Caption = "";
            Problems.Add($"It can't be drawn: {e.Message}");
            old?.Dispose();
            return;
        }

        foreach (var problem in _picture.Problems)
            Problems.Add(problem);
        Image = ArtCache.Render(_picture.Picture, _content.Palette);
        ImageWidth = _picture.Picture.Width * Zoom;
        ImageHeight = _picture.Picture.Height * Zoom;
        var rows = _picture.Rows;
        Caption = rows.Length == 0 ? "No rows" : $"Row {_picture.Info.CurPos + 1} of {rows.Length}"
            + (_picture.Info.Scrolls ? $", {_picture.Info.VisibleRows} shown at once" : "");
        if (Picked != null)
            Picked = _picture.Parts.FirstOrDefault(part => part.Kind == Picked.Kind && part.Index == Picked.Index) ?? Picked;
        PlacePicked();
        GuideX = (_picture.OriginX + 160) * Zoom;
        GuideY = (_picture.OriginY + 100) * Zoom;
        (LayoutLeft, LayoutTop) = (_picture.OriginX * Zoom, _picture.OriginY * Zoom);
        (LayoutWidth, LayoutHeight) = (320.0 * Zoom, 200.0 * Zoom);
        old?.Dispose();
    }

    partial void OnPickedChanged(MenuPart? value) => PlacePicked();

    // The outline round the part picked, in the image's pixels
    private void PlacePicked()
    {
        if (Picked is not { } part || _picture == null)
            return;
        PickedLeft = (part.X + _picture.OriginX) * Zoom;
        PickedTop = (part.Y + _picture.OriginY) * Zoom;
        PickedWidth = Math.Max(part.Width * Zoom, 2);
        PickedHeight = Math.Max(part.Height * Zoom, 2);
    }

    /// <summary>The part at a point on the image (its pixels): the one drawn last there, a row before what's behind it</summary>
    public MenuPart? PartAt(double x, double y)
    {
        if (_picture == null)
            return null;
        double lx = x / Zoom - _picture.OriginX, ly = y / Zoom - _picture.OriginY;
        var under = _picture.Parts.Where(part => part.Contains(lx, ly)).ToList();
        return under.LastOrDefault(part => part.Kind == MenuPartKind.Row)
               ?? under.LastOrDefault(part => !(part.Kind == MenuPartKind.Component && _menu?.Components[part.Index] is Background))
               ?? under.LastOrDefault();
    }

    /// <summary>Picks what's at a point on the image; a row picked takes the cursor there, as the mouse does in the game</summary>
    public void Click(double x, double y)
    {
        if (PartAt(x, y) is not { } part)
            return;
        Picked = part;
        if (part.Kind == MenuPartKind.Row && _picture is { } picture && picture.Rows[part.Index].Active > 0)
            MoveTo(part.Index);
        PartPicked?.Invoke(this, part);
    }

    /// <summary>Opens what's at a point on the image</summary>
    public void Open(double x, double y)
    {
        if (PartAt(x, y) is { } part)
            PartOpened?.Invoke(this, part);
    }

    /// <summary>Picks a part (the window's list picked it), without moving the cursor</summary>
    public void Pick(MenuPartKind kind, int index)
        => Picked = _picture?.Parts.FirstOrDefault(part => part.Kind == kind && part.Index == index);

    private void MoveTo(int row)
    {
        _cursor = row;
        _barOn = true;
        Redraw();
    }

    /// <summary>Moves the cursor as the game's menus do: down or up to the next row that can be picked, round from the end</summary>
    public void Step(int delta)
    {
        if (_picture is not { Rows.Length: > 0 } picture || !picture.Rows.Any(row => row.Active > 0))
            return;
        int n = picture.Rows.Length, at = picture.Info.CurPos;
        do
            at = ((at + delta) % n + n) % n;
        while (picture.Rows[at].Active == 0);
        MoveTo(at);
    }

    /// <summary>The first (or last) row that can be picked</summary>
    public void End(bool last)
    {
        if (_picture is not { Rows.Length: > 0 } picture)
            return;
        var rows = Enumerable.Range(0, picture.Rows.Length).Where(i => picture.Rows[i].Active > 0);
        if (rows.Any())
            MoveTo(last ? rows.Last() : rows.First());
    }

    /// <summary>An item's letter: the next row it starts (or its short-key is), as the game's menus jump</summary>
    public bool Letter(char letter)
    {
        if (_picture is not { Rows.Length: > 0 } picture)
            return false;
        int n = picture.Rows.Length;
        for (int step = 1; step <= n; step++)
        {
            int at = (picture.Info.CurPos + step) % n;
            var row = picture.Rows[at];
            char key = row.ShortKey != '\0' ? row.ShortKey : row.Text.FirstOrDefault();
            if (row.Active > 0 && char.ToUpperInvariant(key) == char.ToUpperInvariant(letter))
            {
                MoveTo(at);
                return true;
            }
        }
        return false;
    }

    //
    // Laying out: parts dragged, resized and nudged (the menu window's; the YAML tab's preview only picks)
    //

    /// <summary>Where drags go; null when the picture is only looked at</summary>
    public IMenuLayoutTarget? Layout
    {
        get => _layout;
        set
        {
            _layout = value;
            OnPropertyChanged(nameof(CanLayOut));
        }
    }

    private IMenuLayoutTarget? _layout;

    public bool CanLayOut => Layout != null;

    /// <summary>Values dragged to round to 8 pixels</summary>
    [ObservableProperty] private bool _snapToGrid;

    /// <summary>A part is being dragged: the guides show</summary>
    [ObservableProperty] private bool _isDragging;

    // The guides, in the image's pixels: the 320x200 middle's centre lines and edges
    [ObservableProperty] private double _guideX;
    [ObservableProperty] private double _guideY;
    [ObservableProperty] private double _layoutLeft;
    [ObservableProperty] private double _layoutTop;
    [ObservableProperty] private double _layoutWidth;
    [ObservableProperty] private double _layoutHeight;

    private sealed record Drag(MenuPart Part, Editing.MenuDragMode Mode, double StartX, double StartY, bool Unlock, MenuMetadata? Menu)
    {
        public int Dx { get; set; }
        public int Dy { get; set; }
    }

    private Drag? _drag;
    private int Snap => SnapToGrid ? 8 : 1;

    // A screen pixel's worth of grab round an edge, at least
    private double EdgeZone => Math.Max(5.0 / Zoom, 1.5);

    /// <summary>What dragging from a point on the image would do (for the mouse's look); null for nothing</summary>
    public Editing.MenuDragMode? ModeAt(double x, double y, bool shift)
    {
        if (Layout == null || _picture == null || PartAt(x, y) is not { } part)
            return null;
        return ModeOf(part, x / Zoom - _picture.OriginX, y / Zoom - _picture.OriginY, shift);
    }

    private Editing.MenuDragMode? ModeOf(MenuPart part, double lx, double ly, bool shift)
    {
        if (Layout is not { } layout)
            return null;
        if (shift && part.Kind == MenuPartKind.Row)
            return Editing.MenuDragMode.Spacing;
        if (layout.CanResize(part))
        {
            bool right = lx >= part.X + part.Width - EdgeZone, bottom = ly >= part.Y + part.Height - EdgeZone;
            if (right && bottom)
                return Editing.MenuDragMode.Resize;
            if (right)
                return Editing.MenuDragMode.ResizeWidth;
            if (bottom)
                return Editing.MenuDragMode.ResizeHeight;
        }
        return layout.CanMove(part) ? Editing.MenuDragMode.Move : null;
    }

    /// <summary>A press on the image: the part there can be dragged from it; false when nothing there can</summary>
    public bool BeginDrag(double x, double y, bool shift, bool alt)
    {
        _drag = null;
        if (Layout == null || _picture == null || PartAt(x, y) is not { } part
            || ModeOf(part, x / Zoom - _picture.OriginX, y / Zoom - _picture.OriginY, shift) is not { } mode)
            return false;
        _drag = new Drag(part, mode, x, y, alt, _menu);
        return true;
    }

    /// <summary>The mouse moved while pressed: the menu drawn with the part where it's dragged</summary>
    public void DragTo(double x, double y)
    {
        if (_drag is not { } drag || Layout is not { } layout || _content == null)
            return;
        int dx = (int)Math.Round((x - drag.StartX) / Zoom), dy = (int)Math.Round((y - drag.StartY) / Zoom);
        if (dx == drag.Dx && dy == drag.Dy)
            return;
        (drag.Dx, drag.Dy) = (dx, dy);
        IsDragging = true;
        var menu = layout.Dragged(drag.Part, drag.Mode, dx, dy, drag.Unlock, Snap) ?? drag.Menu;
        _menu = menu;
        Redraw();
    }

    /// <summary>Let go: the change made, if the part moved</summary>
    public void EndDrag()
    {
        if (_drag is not { } drag)
            return;
        _drag = null;
        IsDragging = false;
        if (drag.Dx != 0 || drag.Dy != 0)
            Layout?.Dropped(drag.Part, drag.Mode, drag.Dx, drag.Dy, drag.Unlock, Snap);
    }

    /// <summary>Esc while dragging: back as it was</summary>
    public bool CancelDrag()
    {
        if (_drag is not { } drag)
            return false;
        _drag = null;
        IsDragging = false;
        _menu = drag.Menu;
        Redraw();
        return true;
    }

    /// <summary>Moves the part picked by a pixel or so (Ctrl+arrows, Shift for 8)</summary>
    public void Nudge(int dx, int dy)
    {
        if (Picked is not { } part || Layout is not { } layout || !layout.CanMove(part))
            return;
        layout.Dropped(part, Editing.MenuDragMode.Move, dx, dy, unlock: false, snap: 1);
    }

    public void Dispose()
    {
        _flash.Stop();
        Image?.Dispose();
        Image = null;
    }
}
