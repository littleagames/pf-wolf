using System.Numerics;
using System.Linq;
using Wolf3D.Assets;

namespace Wolf3D.Entities;

internal class MenuMetadata
{
    public Vector2 Position { get; set; } = Vector2.Zero;

    /// <summary>
    /// The type of prefab menu this instance can build from
    /// </summary>
    public string? Type { get; set; } = null;

    /// <summary>
    /// Music track asset name to play when entering this menu.
    /// If the track is already playing it carries on; with no music, the current track keeps playing.
    /// </summary>
    public string? Music { get; set; } = null;

    /// <summary>
    /// List of drawable items on the menu, list them in order of
    /// drawing bottom to top
    /// </summary>
    public List<MenuComponent> Components { get; set; } = new();

    public List<MenuItem> MenuItems { get; set; } = new();
    public int Indent { get; internal set; }

    /// <summary>
    /// Builds the items from game data instead of menu-items:
    /// "episodes" or "skills"
    /// </summary>
    public string? ItemsSource { get; set; }

    /// <summary>
    /// Item the cursor starts on (defaults to the first)
    /// </summary>
    public int? DefaultSelection { get; set; }

    /// <summary>Pixels from one item to the next (default 13)</summary>
    public int? RowHeight { get; set; }

    /// <summary>The items' font (default the menu font, LargeFont)</summary>
    public string? Font { get; set; }

    /// <summary>A color the items get a shadow in, one pixel right and down; none when unset</summary>
    public string? ItemShadow { get; set; }

    /// <summary>A highlight bar in place of Wolf3D's gun cursor</summary>
    public Assets.MenuCursorInfo? Cursor { get; set; }

    /// <summary>
    /// Whether an episodes items-source puts a blank row after each episode, for two-line names
    /// in 13 pixel rows (default true)
    /// </summary>
    public bool? SpacerRows { get; set; }

    /// <summary>
    /// Where the highlighted item's picture (its episode's or skill's pic-name) is drawn, in
    /// place of each episode's beside its name, or Wolf3D's skill face
    /// </summary>
    public Assets.MenuPoint? SelectionPic { get; set; }

    /// <summary>Where a toggle item's checkbox goes from its text's left and top (default -24, 3)</summary>
    public Assets.MenuPoint? CheckboxOffset { get; set; }

    internal static MenuMetadata? BuildFromAsset(MenuAsset asset)
    {
        if (asset is null) return null;

        var metaType = typeof(MenuMetadata);
        var srcType = asset.GetType();
        object meta;

        // Try to create an instance (prefer parameterless constructor, allow non-public)
        var ctor = metaType.GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null,
            System.Type.EmptyTypes,
            null);
        if (ctor != null)
        {
            meta = ctor.Invoke(null)!;
        }
        else
        {
            meta = System.Activator.CreateInstance(metaType, nonPublic: true)
                   ?? throw new InvalidOperationException($"Unable to construct {metaType.FullName}");
        }

        // Map properties by name (case-insensitive) where possible
        var propFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        foreach (var targetProp in metaType.GetProperties(propFlags))
        {
            if (!targetProp.CanWrite) continue;

            var srcProp = srcType.GetProperty(targetProp.Name, propFlags | System.Reflection.BindingFlags.IgnoreCase);
            if (srcProp == null) continue;

            var value = srcProp.GetValue(asset);
            if (value == null) continue;

            try
            {
                if (targetProp.PropertyType.IsAssignableFrom(srcProp.PropertyType))
                {
                    targetProp.SetValue(meta, value);
                    continue;
                }


                // Use a recursive converter for complex types (Vector2, collections, nested objects, enums, primitives)
                var converted = ConvertValue(value, targetProp.PropertyType);
                if (converted != null || (targetProp.PropertyType.IsValueType && converted != null))
                {
                    targetProp.SetValue(meta, converted);
                }

                // Special-case mapping for Components (List<ComponentEntry> -> List<MenuComponent>)
                if (string.Equals(targetProp.Name, "Components", StringComparison.OrdinalIgnoreCase)
                    && value is System.Collections.IEnumerable)
                {
                    if (value is System.Collections.IEnumerable compEnum)
                    {
                        // Try to treat elements as ComponentEntry
                        var compEntries = new List<Wolf3D.Assets.ComponentEntry>();
                        foreach (var item in compEnum)
                        {
                            if (item is Wolf3D.Assets.ComponentEntry ce) compEntries.Add(ce);
                        }

                        if (compEntries.Count > 0)
                        {
                            var convertedList = ConvertComponentEntries(compEntries);
                            targetProp.SetValue(meta, convertedList);
                            continue;
                        }
                    }
                }

                // Special-case mapping for MenuItems (List<MenuItemEntry> -> List<MenuItem>)
                if (string.Equals(targetProp.Name, "MenuItems", StringComparison.OrdinalIgnoreCase)
                    && value is System.Collections.IEnumerable)
                {
                    if (value is System.Collections.IEnumerable compEnum)
                    {
                        // Try to treat elements as MenuItemEntry
                        var menuItemEntries = new List<Wolf3D.Assets.MenuItemEntry>();
                        foreach (var item in compEnum)
                        {
                            if (item is Wolf3D.Assets.MenuItemEntry ce) menuItemEntries.Add(ce);
                        }

                        if (menuItemEntries.Count > 0)
                        {
                            var convertedList = ConvertMenuItemEntries(menuItemEntries);
                            targetProp.SetValue(meta, convertedList);
                            continue;
                        }
                    }
                }
            }
            catch
            {
                throw;
                // best-effort mapping: ignore individual failures
            }
        }

        // Map fields by name (case-insensitive) where possible
        //foreach (var targetField in metaType.GetFields(propFlags))
        //{
        //    var srcField = srcType.GetField(targetField.Name, propFlags | System.Reflection.BindingFlags.IgnoreCase);
        //    if (srcField == null) continue;

        //    var value = srcField.GetValue(asset);
        //    if (value == null) continue;

        //    try
        //    {
        //        if (targetField.FieldType.IsAssignableFrom(srcField.FieldType))
        //        {
        //            targetField.SetValue(meta, value);
        //        }
        //        else
        //        {
        //            var converted = ConvertValue(value, targetField.FieldType);
        //            if (converted != null)
        //                targetField.SetValue(meta, converted);
        //        }
        //    }
        //    catch
        //    {
        //        throw;
        //        // ignore
        //    }
        //}

        return meta as MenuMetadata ?? throw new InvalidOperationException($"Failed to convert to {typeof(MenuMetadata).FullName}");
    }

    // Helper: recursively convert values to the target type (best-effort)
    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value == null) return null;

        var srcType = value.GetType();
        if (targetType.IsAssignableFrom(srcType)) return value;

        // Handle nullable<T>
        var underlying = Nullable.GetUnderlyingType(targetType);
        if (underlying != null)
        {
            return ConvertValue(value, underlying);
        }

        // Enums
        if (targetType.IsEnum)
        {
            if (value is string s) return Enum.Parse(targetType, s, true);
            try { return Enum.ToObject(targetType, value); } catch { return null; }
        }

        // Vector2
        if (targetType == typeof(Vector2))
        {
            var v = ConvertToVector2(value);
            return v;
        }

        // Collections: List<T>, IEnumerable<T>
        if (targetType.IsGenericType)
        {
            var genDef = targetType.GetGenericTypeDefinition();
            if (genDef == typeof(List<>) || genDef == typeof(IEnumerable<>) || genDef == typeof(IList<>))
            {
                var elemType = targetType.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(elemType);
                var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
                if (value is System.Collections.IEnumerable srcEnum)
                {
                    foreach (var item in srcEnum)
                    {
                        var conv = ConvertValue(item!, elemType);
                        list.Add(conv);
                    }
                }

                return list;
            }
        }

        // IDictionary -> object mapping
        if (value is System.Collections.IDictionary dict && !(value is string))
        {
            try
            {
                var obj = Activator.CreateInstance(targetType, nonPublic: true);
                if (obj == null) return null;
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                foreach (var prop in targetType.GetProperties(flags))
                {
                    if (!prop.CanWrite) continue;
                    foreach (var key in dict.Keys)
                    {
                        var keyStr = key?.ToString();
                        if (string.Equals(keyStr, prop.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            var raw = dict[key];
                            var conv = ConvertValue(raw!, prop.PropertyType);
                            prop.SetValue(obj, conv);
                            break;
                        }
                    }
                }

                return obj;
            }
            catch
            {
                // fall through to attempt simple conversions
            }
        }

        // Fallback: try Convert.ChangeType for primitives and strings
        try
        {
            if (value is string strValue && targetType != typeof(string))
            {
                return System.Convert.ChangeType(strValue, targetType, System.Globalization.CultureInfo.InvariantCulture);
            }

            return System.Convert.ChangeType(value, targetType);
        }
        catch
        {
            return null;
        }
    }

    private static Vector2? ConvertToVector2(object value)
    {
        if (value is Vector2 v) return v;
        if (value is string s)
        {
            var parts = s.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 &&
                float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) &&
                float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y))
            {
                return new Vector2(x, y);
            }
        }

        if (value is System.Collections.IDictionary dict)
        {
            float x = 0, y = 0;
            foreach (var k in dict.Keys)
            {
                var key = k?.ToString();
                if (string.Equals(key, "x", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "X", StringComparison.OrdinalIgnoreCase))
                {
                    var raw = dict[k];
                    if (raw != null && float.TryParse(raw.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rx)) x = rx;
                }
                else if (string.Equals(key, "y", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "Y", StringComparison.OrdinalIgnoreCase))
                {
                    var raw = dict[k];
                    if (raw != null && float.TryParse(raw.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ry)) y = ry;
                }
            }

            return new Vector2(x, y);
        }

        if (value is System.Collections.IEnumerable enumVal)
        {
            var list = new List<float>();
            foreach (var item in enumVal)
            {
                if (float.TryParse(item?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f)) list.Add(f);
                if (list.Count >= 2) break;
            }
            if (list.Count >= 2) return new Vector2(list[0], list[1]);
        }

        return null;
    }

    private static List<MenuComponent> ConvertComponentEntries(IEnumerable<Wolf3D.Assets.ComponentEntry> entries)
    {
        var result = new List<MenuComponent>();
        var compBase = typeof(MenuComponent);
        var assembly = compBase.Assembly;

        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Type)) continue;
            var compType = assembly.GetTypes().FirstOrDefault(t => compBase.IsAssignableFrom(t) && string.Equals(t.Name, entry.Type, StringComparison.OrdinalIgnoreCase));
            if (compType == null)
            {
                Console.WriteLine($"Menu: unknown component type '{entry.Type}', skipped");
                continue;
            }

            object? instance = null;
            try
            {
                instance = Activator.CreateInstance(compType, nonPublic: true) ?? Activator.CreateInstance(compType);
            }
            catch
            {
                // ignore and skip
            }

            if (instance == null) continue;

            // Flatten params (list of single-key dictionaries) into a single map.
            // Keys are normalized so YAML-style names ("horizontal-orientation") match property names ("HorizontalOrientation").
            var paramMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (entry.Params != null)
            {
                foreach (var d in entry.Params)
                {
                    if (d == null) continue;
                    foreach (var kv in d)
                    {
                        var key = NormalizeParamName(kv.Key);
                        if (!paramMap.ContainsKey(key)) paramMap[key] = kv.Value;
                    }
                }
            }

            // Set matching properties from params
            var usedParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in compType.GetProperties(flags))
            {
                if (!prop.CanWrite) continue;
                // game-packs sits beside type/params, not inside params
                if (prop.Name == nameof(MenuComponent.GamePacks)) continue;
                if (paramMap.TryGetValue(NormalizeParamName(prop.Name), out var sval))
                {
                    usedParams.Add(NormalizeParamName(prop.Name));
                    var conv = ConvertValue(sval, prop.PropertyType);
                    if (conv != null) prop.SetValue(instance, conv);
                    else Console.WriteLine($"Menu: {entry.Type}.{prop.Name} could not use value '{sval}'");
                }
            }

            foreach (var unused in paramMap.Keys.Where(k => !usedParams.Contains(k)))
                Console.WriteLine($"Menu: {entry.Type} has no param '{unused}', ignored");

            var component = (MenuComponent)instance;
            component.GamePacks = entry.GamePacks;
            result.Add(component);
        }

        return result;
    }

    private static string NormalizeParamName(string name)
        => name.Replace("-", "").Replace("_", "");

    private static List<MenuItem> ConvertMenuItemEntries(IEnumerable<Wolf3D.Assets.MenuItemEntry> entries)
    {
        var result = new List<MenuItem>();
        var compBase = typeof(MenuItem);
        var assembly = compBase.Assembly;

        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Type)) continue;
            var menuItemType = assembly.GetTypes().FirstOrDefault(t => compBase.IsAssignableFrom(t) && string.Equals(t.Name, entry.Type, StringComparison.OrdinalIgnoreCase));
            if (menuItemType == null)
            {
                Console.WriteLine($"Menu: unknown menu item type '{entry.Type}', skipped");
                continue;
            }

            object? instance = null;
            try
            {
                instance = Activator.CreateInstance(menuItemType, nonPublic: true) ?? Activator.CreateInstance(menuItemType);
            }
            catch
            {
                // ignore and skip
            }

            if (instance == null) continue;

            // Map properties from MenuItemEntry to the MenuItem instance
            var entryType = entry.GetType();
            foreach (var prop in menuItemType.GetProperties(flags))
            {
                if (!prop.CanWrite) continue;

                var srcProp = entryType.GetProperty(prop.Name, flags | System.Reflection.BindingFlags.IgnoreCase);
                if (srcProp == null) continue;

                var value = srcProp.GetValue(entry);
                if (value == null) continue;

                try
                {
                    if (prop.PropertyType.IsAssignableFrom(srcProp.PropertyType))
                    {
                        prop.SetValue(instance, value);
                    }
                    else
                    {
                        var conv = ConvertValue(value, prop.PropertyType);
                        if (conv != null || (prop.PropertyType.IsValueType && conv != null))
                        {
                            prop.SetValue(instance, conv);
                        }
                    }
                }
                catch
                {
                    // best-effort mapping: ignore individual failures
                }
            }

            result.Add((MenuItem)instance);
        }

        return result;
    }
}

internal abstract record MenuComponent
{
    /// <summary>
    /// Game packs this is drawn in; null means every pack
    /// </summary>
    public List<string>? GamePacks { get; set; }
}

internal record Background : MenuComponent
{
    public string Color { get; set; } = null!;

    public Background()
    { 
    }
}

internal record Window : MenuComponent
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Color { get; set; } = "BKGDCOLOR";

    public Window()
    {
        
    }
    //public Window(int x, int y, int width, int height)
    //{
    //    X = x;
    //    Y = y;
    //    Width = width;
    //    Height = height;
    //}

    //public Window (int x, int y, int width, int height, string theme)
    //{
    //    X = x;
    //    Y = y;
    //    Width = width;
    //    Height = height;
    //}
}

/// <summary>A plain filled rectangle, without the window's outline (Blake Stone's menu terminal)</summary>
internal record Fill : MenuComponent
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Color { get; set; } = "BKGDCOLOR";

    public Fill()
    {
    }
}

internal record Graphic : MenuComponent
{
    public string Name { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public HorizontalOrientation HorizontalOrientation { get; set; } = HorizontalOrientation.Left;
    public VerticalOrientation VerticalOrientation { get; set; } = VerticalOrientation.Top;


    public Graphic()
    {
        
    }
    //public Graphic(string asset, int x, int y)
    //{
    //    Asset = asset;
    //    X = x;
    //    Y = y;
    //}

    //public Graphic(string asset, int x, VerticalOrientation y)
    //{
    //    Asset = asset;
    //    X = x;
    //    OrientationY = y;
    //}

    //public Graphic(string asset, HorizontalOrientation x, int y)
    //{
    //    Asset = asset;
    //    OrientationX = x;
    //    Y = y;
    //}

    //public Graphic(string asset, HorizontalOrientation x, VerticalOrientation y)
    //{
    //    Asset = asset;
    //    OrientationX = x;
    //    OrientationY = y;
    //}
}

/// <summary>
/// The band across the top of a menu, shaped by game-info menu-stripe; colors left unset come from there too
/// </summary>
internal record Stripe : MenuComponent
{
    public int Y { get; set; } = 0;
    public string? BackingColor { get; set; }
    public string? LineColor { get; set; }

    public Stripe()
    {
    }

    public Stripe(int y)
    {
        Y = y;
    }

    public Stripe(int y, string backingColor, string lineColor)
    {
        Y = y;
        BackingColor = backingColor;
        LineColor = lineColor;
    }
}

/// <summary>
/// A line of printed text. Language keys ("$STR_...") are translated when drawn.
/// Center orientation centers the text across the full screen width, ignoring X, or between
/// X and X + Width when a width is given.
/// </summary>
internal record Label : MenuComponent
{
    public string Text { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public HorizontalOrientation HorizontalOrientation { get; set; } = HorizontalOrientation.Left;
    public string Color { get; set; } = "TEXTCOLOR";
    public string Font { get; set; } = "LargeFont";

    /// <summary>
    /// true gives the text a drop shadow, false takes away the font's own (fonts.yaml); left out,
    /// the text has whatever shadow its font has. Setting a shadow-x, -y or -color turns it on too.
    /// </summary>
    public bool? Shadow { get; set; }

    /// <summary>How far right of the text the shadow sits; defaults to 1</summary>
    public int? ShadowX { get; set; }

    /// <summary>How far down from the text the shadow sits; defaults to 1</summary>
    public int? ShadowY { get; set; }

    /// <summary>The shadow's color; defaults to Black</summary>
    public string? ShadowColor { get; set; }

    /// <summary>
    /// true shades the text's color lighter to darker down the glyphs, false takes away the font's
    /// own gradient (fonts.yaml); left out, the text has whatever its font has. Setting a
    /// gradient-top or -bottom turns it on too.
    /// </summary>
    public bool? Gradient { get; set; }

    /// <summary>Percent toward white (negative: black) at the top row; defaults to 40</summary>
    public int? GradientTop { get; set; }

    /// <summary>Percent toward white (negative: black) at the bottom row; defaults to -40</summary>
    public int? GradientBottom { get; set; }

    /// <summary>
    /// true draws a border round the text, false takes away the font's own (fonts.yaml); left
    /// out, the text has whatever its font has. Setting an outline-color or -thickness turns it on too.
    /// </summary>
    public bool? Outline { get; set; }

    /// <summary>The outline's color; defaults to Black</summary>
    public string? OutlineColor { get; set; }

    /// <summary>How many pixels wide the outline is; defaults to 1</summary>
    public int? OutlineThickness { get; set; }

    /// <summary>
    /// true puts a halo round the text fading into the menu background, false takes away the
    /// font's own (fonts.yaml); left out, the text has whatever its font has. Setting a
    /// glow-color, -radius or -strength turns it on too.
    /// </summary>
    public bool? Glow { get; set; }

    /// <summary>The glow's color; defaults to the text's color</summary>
    public string? GlowColor { get; set; }

    /// <summary>How many pixels it spreads out; defaults to 2</summary>
    public int? GlowRadius { get; set; }

    /// <summary>Percent of the glow color nearest the text; defaults to 50</summary>
    public int? GlowStrength { get; set; }

    public Label()
    {
    }
}

internal enum HorizontalOrientation
{
    Left,
    Center,
    Right
}

internal enum VerticalOrientation
{
    Top,
    Center,
    Bottom
}

internal abstract record MenuItem
{
    public string? Id { get; set; }
    public string Text { get; set; } = null!;
    public string? ShortKey { get; set; }
    public bool Enabled { get; set; }
    /// <summary>
    /// Game packs this item appears in; null means every pack
    /// </summary>
    public List<string>? GamePacks { get; set; }
    /// <summary>Drawn in the read-it colors (READCOLOR, READHCOLOR)</summary>
    public bool Highlighted { get; set; }
}

internal record MenuSwitcher : MenuItem
{
    public MenuSwitcher()
    {
    }

    public MenuSwitcher(string text, bool isEnabled, string action)
    {
        Text = text;
        Enabled = isEnabled;
        Action = action;
    }

    public string? Action { get; init; } = null;

    /// <summary>The presenter text a CP_TextScreen item shows</summary>
    public string? Script { get; init; } = null;
}

internal record ToggleMenuItem : MenuItem
{
    public bool State { get; set; } = false;
    public ToggleMenuItem()
    {
        
    }

    //public ToggleMenuItem(string text, bool isEnabled, bool defaultState)
    //{
    //    Text = text;
    //    IsEnabled = isEnabled;
    //    State = defaultState;
    //}
}

internal record BlankMenuItem : MenuItem
{
    public BlankMenuItem()
    {
        Text = "";
        Enabled = false;
    }
}

/// <summary>
/// A level adjusted with left/right while highlighted (the Sound menu's volumes)
/// </summary>
internal record SliderMenuItem : MenuItem
{
    public SliderMenuItem()
    {
    }
}

/// <summary>
/// Plays a music track when chosen (jukebox)
/// </summary>
internal record MusicMenuItem : MenuItem
{
    public string? Music { get; set; }

    public MusicMenuItem()
    {
    }
}

/// <summary>
/// A row of the Customize Controls screen: the keys and buttons bound to one control
/// </summary>
internal record ControlMenuItem : MenuItem
{
    /// <summary>The control's controls.cfg name ("attack", "am_zoomin")</summary>
    public string? Control { get; set; }

    public ControlMenuItem()
    {
    }
}

/// <summary>
/// A title over a group of rows in a list that can't be selected (Customize Controls' sections)
/// </summary>
internal record HeaderMenuItem : MenuItem
{
    public HeaderMenuItem()
    {
        Enabled = false;
    }
}

/// <summary>
/// One value picked from a list, stepped with left/right (or Enter) while highlighted.
/// The menu's code supplies the values, since lists like resolutions come from the display.
/// </summary>
internal record ChoiceMenuItem : MenuItem
{
    public ChoiceMenuItem()
    {
    }
}