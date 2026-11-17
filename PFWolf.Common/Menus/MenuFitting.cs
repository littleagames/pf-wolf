using PFWolf.Assets;

namespace PFWolf.Menus;

/// <summary>
/// How a fit-items menu changes for its number of items: the items move ItemsDy down, the grow
/// components get Grow taller and they and the move components move FrameDy down; VisibleRows is
/// how many show at once when they scroll (0: all of them)
/// </summary>
public readonly record struct MenuFit(int ItemsDy, int FrameDy, int Grow, int VisibleRows);

/// <summary>A menudefs menu's fit-items: laid out for one number of items, fitted to the number it has</summary>
public static class MenuFitting
{
    /// <summary>fit-items worked out for <paramref name="count"/> items in rows <paramref name="rowHeight"/> high</summary>
    /// <param name="pictures">Whether any item has a picture to show beside them (min-rows keeps room for it)</param>
    public static MenuFit Fit(MenuFitItems? fit, int count, int rowHeight, bool pictures = true)
    {
        if (fit is not { Rows: > 0 })
            return default;

        int min = pictures ? Math.Max(fit.MinRows ?? 1, 1) : 1;
        int max = Math.Max(fit.MaxRows ?? int.MaxValue, min);
        int shown = Math.Min(count, max);
        int grow = (Math.Max(shown, min) - fit.Rows) * rowHeight;
        // Halves rounded the same way for both, so the items stay where they were in the window
        return new MenuFit(
            ItemsDy: -((shown - fit.Rows) * rowHeight / 2),
            FrameDy: -(grow / 2),
            Grow: grow,
            VisibleRows: count > max ? max : 0);
    }

    /// <summary>The first grow component's right edge (where a scrolling fit-items menu's marks go), or the screen's</summary>
    public static int GrownRight(MenuMetadata menu)
    {
        var grown = menu.Components.FirstOrDefault(c => c.Id != null && menu.FitItems!.Grow.Contains(c.Id, StringComparer.OrdinalIgnoreCase));
        return grown switch
        {
            Window window => window.X + window.Width,
            Fill fill => fill.X + fill.Width,
            _ => 306,
        };
    }

    /// <summary>A component as drawn for the menu's fit-items: grown, moved, or as it is</summary>
    public static MenuComponent Fitted(MenuComponent component, MenuFitItems? fitItems, MenuFit fit)
    {
        if (fitItems == null || component.Id is not { } id || fit == default)
            return component;

        bool grows = fitItems.Grow.Contains(id, StringComparer.OrdinalIgnoreCase);
        if (!grows && !fitItems.Move.Contains(id, StringComparer.OrdinalIgnoreCase))
            return component;

        int dy = fit.FrameDy, dh = grows ? fit.Grow : 0;
        return component switch
        {
            Window window => window with { Y = window.Y + dy, Height = Math.Max(window.Height + dh, 0) },
            Fill fill => fill with { Y = fill.Y + dy, Height = Math.Max(fill.Height + dh, 0) },
            Label label => label with { Y = label.Y + dy },
            Graphic graphic when graphic.VerticalOrientation == VerticalOrientation.Top => graphic with { Y = graphic.Y + dy },
            Stripe stripe => stripe with { Y = stripe.Y + dy },
            _ => component,
        };
    }
}
