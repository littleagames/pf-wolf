using PFWolf.Configuration;
using PFWolf.Extensions;
using PFWolf.Loaders;

namespace PFWolf.Ui;

/// <summary>
/// The Mods screen: a CheckBox for each pk3, zip and folder in the mods folder. The ones
/// switched on are saved to mods.cfg on the way out, and as they only load when the game
/// starts, it offers to restart. Its last row opens the mod catalog (ModCatalog) in a browser,
/// and with no mods to switch on it offers to.
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

    // Rows the menu's window holds. The last is "Find more mods online...", and with more mods
    // than the rest hold, the one before it "More mods..."
    internal const int ModsPageSize = 9;
    private const int ModRows = ModsPageSize - 1;
    // From the text's left edge to the window's right edge, less a margin
    private const int ModNameWidth = 224;

    private List<ModMenuEntry> modMenuEntries = [];
    private int modMenuPage;
    internal ControlPanelItem[] Items = [];
    internal ControlPanelMenuInfo Info = null!;

    /// <summary>Whether that many mods need more than one page</summary>
    internal static bool Paged(int count) => count > ModRows;

    /// <summary>Mods on a page: the rows the catalog's leaves, less one for "More mods..." when there are pages</summary>
    internal static int PerPage(int count) => Paged(count) ? ModRows - 1 : ModRows;

    internal static int PageCount(int count) => Math.Max(1, (count + PerPage(count) - 1) / PerPage(count));

    private bool ModsPaged => Paged(modMenuEntries.Count);
    private int ModsPerPage => PerPage(modMenuEntries.Count);
    private int ModsPageCount => PageCount(modMenuEntries.Count);

    /// <summary>The Mods screen, from the menu's Mods item (CP_Mods)</summary>
    internal int Run()
    {
        var language = assets.GetText("en-us");
        var configPath = engine.GetConfigFilePath(ModsConfig.FileName);
        modMenuEntries = LoadModMenuEntries(configPath, out var missingConfigLines);
        var before = ModsSwitchedOn();

        // Nothing to switch on: say so, and offer the catalog
        if (!ModsPaged && !modMenuEntries.Any(entry => entry.Loadable))
        {
            var none = (modMenuEntries.Count == 0 ? "$STR_MODS_NONE" : "$STR_MODS_NONEFORGAME").ToLanguageText(language);
            defs.DrawMenuComponents("mods");
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

        modMenuPage = 0;
        LoadModsPage();
        DrawModsMenu();
        draw.MenuFadeIn();
        menuInput.WaitKeyUp();

        int which;
        do
        {
            which = loop.HandleMenu(Info, Items, null);
            if (which < 0)
                break;

            if (Items[which].Id == "more")
            {
                modMenuPage = (modMenuPage + 1) % ModsPageCount;
                LoadModsPage();
                DrawModsMenu();
                audio.Play("menu/move1");
            }
            else if (Items[which].Id == "catalog")
            {
                loop.ShootSnd();
                OpenCatalog();
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

    private void LoadModsPage()
    {
        (Items, Info) = defs.LoadMenu("mods");
        Info.CurPos = (short)Math.Max(0, Array.FindIndex(Items, item => item.Active != 0));
    }

    /// <summary>The Mods menu's rows for the page showing (its items-source)</summary>
    internal ControlPanelItem[] BuildModsPage()
    {
        var language = assets.GetText("en-us");
        var rows = modMenuEntries
            .Skip(modMenuPage * ModsPerPage)
            .Take(ModsPerPage)
            .Select(entry => new ControlPanelItem((short)(entry.Loadable ? 1 : 0), ui.FitText(entry.Text, ModNameWidth, UiFonts.MenuFont), null, entry));

        if (ModsPaged)
            rows = rows.Append(new ControlPanelItem(1, "$STR_MODS_MORE".ToLanguageText(language), null) { Id = "more" });
        if (ModCatalog.Url != null)
            rows = rows.Append(new ControlPanelItem(1, "$STR_MODS_CATALOG".ToLanguageText(language), null) { Id = "catalog" });

        return rows.ToArray();
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
        defs.DrawMenuComponents("mods");
        draw.DrawMenu(Info, Items);
        for (var i = 0; i < Items.Length; i++)
        {
            if (Items[i].Data is ModMenuEntry entry)
                draw.DrawMenuCheckbox(Info, i, entry.On);
        }
        draw.DrawMenuGun(Info);
        video.Update();
    }
}
