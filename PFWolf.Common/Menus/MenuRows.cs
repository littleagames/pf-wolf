namespace PFWolf.Menus;

/// <summary>
/// A menudefs menu's rows and where they go: what the game's MenuDefs.LoadMenu builds from a
/// menu, shared so the editor's preview lays a menu out as the game does
/// </summary>
public static class MenuRows
{
    /// <summary>The rows of a menu's menu-items in the running pack</summary>
    /// <param name="inPack">Whether an item with these game-packs belongs in the running pack</param>
    /// <param name="translate">An item's text as shown: a $language key's string</param>
    /// <param name="routine">What a MenuSwitcher runs when picked; null for none</param>
    public static ControlPanelItem[] FromItems(MenuMetadata menu, Func<List<string>?, bool> inPack, Func<string, string> translate,
        Func<MenuSwitcher?, Func<int, int>?> routine)
        => menu.MenuItems
            .Where(mi => inPack(mi.GamePacks))
            .Select(mi =>
                new ControlPanelItem(
                    (short)(mi.Enabled && mi is not BlankMenuItem ? (mi.Highlighted ? 2 : 1) : 0),
                    translate(mi.Text ?? ""),
                    routine(mi as MenuSwitcher))
                {
                    Id = mi.Id,
                    ShortKey = string.IsNullOrEmpty(mi.ShortKey) ? '\0' : mi.ShortKey[0],
                    Data = (mi as MusicMenuItem)?.Music
                })
            .ToArray();

    /// <summary>
    /// Where a menu's rows go and how they look, for these rows: its position (moved for
    /// fit-items), row height, scrolling, font, cursor and the rest, and the fit-items worked out
    /// </summary>
    /// <param name="name">The menu's name, for warnings</param>
    /// <param name="curpos">The row the cursor starts on, unless the menu sets default-selection</param>
    /// <param name="pictures">Whether any row has a picture (a skill's or class's pic-name), which fit-items keeps room for</param>
    /// <param name="warn">Told what's wrong with the menu (a default-selection out of range)</param>
    public static (ControlPanelMenuInfo Info, MenuFit Fit) Layout(string name, MenuMetadata menu, ControlPanelItem[] items,
        short curpos, bool pictures, Action<string> warn)
    {
        if (menu.DefaultSelection is int selection)
        {
            if (selection >= 0 && selection < items.Length)
                curpos = (short)selection;
            else if (menu.ItemsSource != null)
                curpos = (short)Math.Max(0, items.Length - 1);    // the game has fewer (one skill, say): the last
            else
                warn($"Menu '{name}': default-selection {selection} is out of range (0-{items.Length - 1})");
        }

        int rowHeight = menu.RowHeight is > 0 and int rows ? rows : 13;
        var fit = MenuFitting.Fit(menu.FitItems, items.Length, rowHeight, pictures);

        var info = new ControlPanelMenuInfo(
            (short)menu.Position.X,
            (short)(menu.Position.Y + fit.ItemsDy),
            amount: (short)items.Length,
            curpos: curpos,
            indent: (short)menu.Indent)
        {
            LayoutY = (int)menu.Position.Y,
            RowHeight = rowHeight,
            VisibleRows = menu.VisibleRows is > 0 and int visibleRows ? visibleRows : fit.VisibleRows,
            ScrollMarkX = fit.VisibleRows > 0 ? MenuFitting.GrownRight(menu) - 6 : null,
            Font = menu.Font,
            ItemShadow = menu.ItemShadow,
            Cursor = menu.Cursor,
            SelectionPic = menu.SelectionPic,
            CheckboxX = menu.CheckboxOffset?.X ?? -24,
            CheckboxY = menu.CheckboxOffset?.Y ?? 3,
        };

        return (info, fit);
    }
}
