using PFWolf.Configuration;
using PFWolf.Extensions;
using PFWolf.Loaders;

namespace PFWolf.Ui;

/// <summary>
/// The Mods screen: a CheckBox for each pk3, zip and folder in the mods folder, in a list that
/// scrolls (the menudef's visible-rows). The ones switched on are saved to mods.cfg on the way
/// out, and as they only load when the game starts, it offers to restart. Tab (or X) opens the
/// mod catalog (ModCatalog) in a browser, and with no mods to switch on it offers to.
/// </summary>
internal sealed class ModsMenu(GameEngineManager engine, AssetManager assets, InputManager input, AudioManager audio, VideoManager video,
    MenuDrawing draw, MenuLoop loop, MenuInput menuInput, MenuDefs defs, UiText ui, GameSession session)
{
    /// <summary>A row of the Mods menu: a mod in mods.cfg or the mods folder</summary>
    internal sealed class ModMenuEntry(string fullPath, ModSource? mod, bool on, bool loadable)
    {
        public string FullPath { get; } = fullPath;

        /// <summary>Null when it can't be read</summary>
        public ModSource? Mod { get; } = mod;

        public bool On { get; set; } = on;

        /// <summary>Whether it can be switched on here: it's readable and made for the running game</summary>
        public bool Loadable { get; } = loadable;

        public string Text => Mod == null
            ? Path.GetFileName(FullPath)
            : string.IsNullOrWhiteSpace(Mod.Info.Version) ? Mod.DisplayName : $"{Mod.DisplayName} {Mod.Info.Version}";
    }

    // The menudef's component that says which key opens the catalog: left out when there's none
    internal const string CatalogHintId = "catalog-hint";

    // Where the scroll marks go: the window's right edge, or past a highlight bar's (Blake Stone's)
    private const int ScrollMarkX = 300, BarScrollMarkGap = 38;
    // Room the scroll marks' "N more" takes, left of them
    private const int ScrollMarkRoom = 38;

    private List<ModMenuEntry> modMenuEntries = [];
    internal ControlPanelItem[] Items = [];
    internal ControlPanelMenuInfo Info = null!;

    /// <summary>The Mods screen, from the menu's Mods item (CP_Mods)</summary>
    internal int Run()
    {
        var language = assets.GetText("en-us");
        var configPath = engine.GetConfigFilePath(ModsConfig.FileName);
        modMenuEntries = LoadModMenuEntries(configPath, out var missingConfigLines);
        var before = ModsSwitchedOn();

        // Nothing to switch on: say so, and offer the catalog
        if (!modMenuEntries.Any(entry => entry.Loadable))
        {
            var none = (modMenuEntries.Count == 0 ? "$STR_MODS_NONE" : "$STR_MODS_NONEFORGAME").ToLanguageText(language);
            defs.DrawMenuComponents("mods", CatalogHintId);
            if (ModCatalog.Url == null)
            {
                draw.Message(none);
                draw.MenuFadeIn();
                input.ClearKeysDown();
                input.Ack();
            }
            else
            {
                draw.MenuFadeIn();
                if (loop.Confirm($"{none}\n\n{"$STR_MODS_FINDONLINE".ToLanguageText(language)}") != 0)
                    OpenCatalog();
            }
            draw.MenuFadeOut();
            return 0;
        }

        (Items, Info) = defs.LoadMenu("mods");
        Info.CurPos = (short)Math.Max(0, Array.FindIndex(Items, item => item.Active != 0));
        DrawModsMenu();
        draw.MenuFadeIn();
        menuInput.WaitKeyUp();

        // Tab, or the middle mouse button or X, opens the catalog (as Tab/X sorts the load and save lists)
        bool findOnline = false;
        bool CatalogKey(ControlInfo ci)
            => findOnline = ModCatalog.Url != null && (ci.Button2 || input.IsKeyDown(ScanCodes.Tab));

        int which;
        do
        {
            which = loop.HandleMenu(Info, Items, null, keys: CatalogKey);
            if (which == -1)
                break;

            if (which == -2)    // scrolled, or the catalog's key
            {
                if (findOnline)
                {
                    findOnline = false;
                    loop.ShootSnd();
                    OpenCatalog();
                }
                DrawModsMenu();
            }
            else if (Items[which].Data is ModMenuEntry entry)
            {
                entry.On = !entry.On;
                DrawModsMenu();
                loop.ShootSnd();
            }
        }
        while (true);

        var after = ModsSwitchedOn();
        if (!after.SequenceEqual(before, StringComparer.OrdinalIgnoreCase))
        {
            // Mods mods.cfg names that aren't there now are kept, for when they're put back
            if (!ModsConfig.Write(configPath, after.Concat(missingConfigLines)))
            {
                draw.Message("$STR_MODS_NOTSAVED".ToLanguageText(language));
                input.ClearKeysDown();
                input.Ack();
            }
            else if (loop.Confirm(session.InGame ? "$STR_MODS_RESTART_INGAME" : "$STR_MODS_RESTART") != 0)
            {
                audio.SetPaused(true);
                audio.StopAll();
                draw.MenuFadeOut();
                engine.Restart();
            }
        }

        draw.MenuFadeOut();
        return 0;
    }

    /// <summary>Whether a mod is readable and made for the running game</summary>
    private bool IsForGame(ModSource? mod) => mod != null && mod.IsForGamePack(engine.GamePackId, assets.BasePackIds);

    /// <summary>
    /// The mods mods.cfg switches on, in its order, then the rest of the mods folder by name.
    /// Lines naming mods that aren't there come back in <paramref name="missingConfigLines"/>.
    /// </summary>
    private List<ModMenuEntry> LoadModMenuEntries(string configPath, out List<string> missingConfigLines)
    {
        var entries = new List<ModMenuEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>(); // an unreadable mod shows as its file name, greyed out
        missingConfigLines = [];

        foreach (var line in ModsConfig.Read(configPath))
        {
            var fullPath = ModSource.ResolvePath(line);
            if (fullPath == null)
                missingConfigLines.Add(line);
            else if (seen.Add(fullPath))
            {
                var mod = ModSource.TryOpen(fullPath, warnings);
                entries.Add(new ModMenuEntry(fullPath, mod, on: true, IsForGame(mod)));
            }
        }

        foreach (var path in ModSource.FindInModsFolder())
        {
            var fullPath = Path.GetFullPath(path);
            if (seen.Add(fullPath))
            {
                var mod = ModSource.TryOpen(fullPath, warnings);
                entries.Add(new ModMenuEntry(fullPath, mod, on: false, IsForGame(mod)));
            }
        }

        return entries;
    }

    /// <summary>The switched-on mods as mods.cfg writes them, in order</summary>
    private List<string> ModsSwitchedOn()
        => modMenuEntries.Where(entry => entry.On).Select(entry => ModsConfig.ConfigName(entry.FullPath)).ToList();

    /// <summary>The Mods menu's rows, one per mod (its items-source); the menudef's visible-rows scrolls them</summary>
    internal ControlPanelItem[] BuildModsRows()
    {
        var menu = assets.GetMenu("mods");
        int nameWidth = menu == null ? 224 : ModNameWidth(menu, modMenuEntries.Count);
        var font = menu?.Font ?? UiFonts.MenuFont;
        return modMenuEntries
            .Select(entry => new ControlPanelItem((short)(entry.Loadable ? 1 : 0), ui.FitText(entry.Text, nameWidth, font), null, entry))
            .ToArray();
    }

    /// <summary>Where the scroll marks go: the window's right edge, or past a highlight bar's</summary>
    private static int MarkX(Assets.MenuCursorInfo? bar) => bar == null ? ScrollMarkX : bar.X + bar.Width + BarScrollMarkGap;

    /// <summary>
    /// How wide a mod's name can be: from the text's left edge to the window's right edge (less a
    /// margin) or a highlight bar's, and clear of the scroll marks when there are more mods than rows
    /// </summary>
    internal static int ModNameWidth(Entities.MenuMetadata menu, int count)
    {
        int left = (int)menu.Position.X + menu.Indent;
        int right = menu.Cursor is { } bar ? bar.X + bar.Width - 2 : ScrollMarkX - 4;
        if (menu.VisibleRows is int rows && count > rows)
            right = Math.Min(right, MarkX(menu.Cursor) - ScrollMarkRoom);
        return Math.Max(right - left, 16);
    }

    /// <summary>
    /// Opens the mod catalog in the player's browser, and says so (in full screen it can open
    /// behind the game), or where it is when no browser could be started
    /// </summary>
    private void OpenCatalog()
    {
        var language = assets.GetText("en-us");
        var where = ModCatalog.ShortText(ModCatalog.Url!);
        draw.Message(ModCatalog.Open()
            ? "$STR_MODS_CATALOG_OPENED".ToLanguageText(language)
            : $"{"$STR_MODS_CATALOG_NOBROWSER".ToLanguageText(language)}\n{where}");
        input.ClearKeysDown();
        input.Ack();
    }

    private void DrawModsMenu()
    {
        if (ModCatalog.Url == null)
            defs.DrawMenuComponents("mods", CatalogHintId);
        else
            defs.DrawMenuComponents("mods");
        draw.DrawMenu(Info, Items);
        for (var i = 0; i < Items.Length; i++)
        {
            if (Items[i].Data is ModMenuEntry entry)
            {
                Items[i].CheckBox = entry.On;   // for a highlight bar to draw it again
                draw.DrawMenuCheckbox(Info, i, entry.On);
            }
        }
        draw.DrawMenuScrollMarks(Info, Items, MarkX(Info.Cursor));
        draw.DrawMenuGun(Info);
        video.Update();
    }
}
