using PFWolf.Configuration;
using PFWolf.Extensions;
using PFWolf.Loaders;

namespace PFWolf;

internal partial class Program
{
    /// <summary>A row of the Mods menu: a mod in mods.cfg or the mods folder</summary>
    private sealed class ModMenuEntry(string fullPath, ModSource? mod, bool on)
    {
        public string FullPath { get; } = fullPath;

        /// <summary>Null when it can't be read</summary>
        public ModSource? Mod { get; } = mod;

        public bool On { get; set; } = on;

        /// <summary>Whether it can be switched on here: it's readable and made for the running game</summary>
        public bool Loadable => Mod != null && Mod.IsForGamePack(_gameEngineManager.GamePackId, _assetManager.BasePackIds);

        public string Text => Mod == null
            ? Path.GetFileName(FullPath)
            : string.IsNullOrWhiteSpace(Mod.Info.Version) ? Mod.DisplayName : $"{Mod.DisplayName} {Mod.Info.Version}";
    }

    // Rows the menu's window holds; with more mods than that, the last row is "More mods..."
    private const int ModsPageSize = 9;
    // From the text's left edge to the window's right edge, less a margin
    private const int ModNameWidth = 224;

    private static List<ModMenuEntry> modMenuEntries = [];
    private static int modMenuPage;
    internal static CP_itemtype[] ModsMenu = [];
    internal static CP_iteminfo ModsItems = null!;

    private static bool ModsPaged => modMenuEntries.Count > ModsPageSize;
    private static int ModsPerPage => ModsPaged ? ModsPageSize - 1 : ModsPageSize;
    private static int ModsPageCount => Math.Max(1, (modMenuEntries.Count + ModsPerPage - 1) / ModsPerPage);

    /// <summary>
    /// The Mods screen: a checkbox for each pk3, zip and folder in the mods folder. The ones
    /// switched on are saved to mods.cfg on the way out, and as they only load when the game
    /// starts, it offers to restart.
    /// </summary>
    internal static int CP_Mods(int _)
    {
        var language = _assetManager.GetText("en-us");
        var configPath = _gameEngineManager.GetConfigFilePath(ModsConfig.FileName);
        modMenuEntries = LoadModMenuEntries(configPath, out var missingConfigLines);
        var before = ModsSwitchedOn();

        if (!ModsPaged && !modMenuEntries.Any(entry => entry.Loadable))
        {
            DrawMenuComponents("mods");
            Message((modMenuEntries.Count == 0 ? "$STR_MODS_NONE" : "$STR_MODS_NONEFORGAME").ToLanguageText(language));
            MenuFadeIn();
            _inputManager.ClearKeysDown();
            _inputManager.Ack();
            MenuFadeOut();
            return 0;
        }

        modMenuPage = 0;
        LoadModsPage();
        DrawModsMenu();
        MenuFadeIn();
        WaitKeyUp();

        int which;
        do
        {
            which = HandleMenu(ModsItems, ModsMenu, null);
            if (which < 0)
                break;

            if (ModsMenu[which].id == "more")
            {
                modMenuPage = (modMenuPage + 1) % ModsPageCount;
                LoadModsPage();
                DrawModsMenu();
                _audioManager.Play("menu/move1");
            }
            else if (ModsMenu[which].data is ModMenuEntry entry)
            {
                entry.On = !entry.On;
                DrawModsMenu();
                ShootSnd();
            }
        }
        while (true);

        var after = ModsSwitchedOn();
        if (!after.SequenceEqual(before, StringComparer.OrdinalIgnoreCase))
        {
            // Mods mods.cfg names that aren't there now are kept, for when they're put back
            if (!ModsConfig.Write(configPath, after.Concat(missingConfigLines)))
            {
                Message("$STR_MODS_NOTSAVED".ToLanguageText(language));
                _inputManager.ClearKeysDown();
                _inputManager.Ack();
            }
            else if (Confirm(ingame ? "$STR_MODS_RESTART_INGAME" : "$STR_MODS_RESTART") != 0)
            {
                _audioManager.SetPaused(true);
                _audioManager.StopAll();
                MenuFadeOut();
                _gameEngineManager.Restart();
            }
        }

        MenuFadeOut();
        return 0;
    }

    /// <summary>
    /// The mods mods.cfg switches on, in its order, then the rest of the mods folder by name.
    /// Lines naming mods that aren't there come back in <paramref name="missingConfigLines"/>.
    /// </summary>
    private static List<ModMenuEntry> LoadModMenuEntries(string configPath, out List<string> missingConfigLines)
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
                entries.Add(new ModMenuEntry(fullPath, ModSource.TryOpen(fullPath, warnings), on: true));
        }

        foreach (var path in ModSource.FindInModsFolder())
        {
            var fullPath = Path.GetFullPath(path);
            if (seen.Add(fullPath))
                entries.Add(new ModMenuEntry(fullPath, ModSource.TryOpen(fullPath, warnings), on: false));
        }

        return entries;
    }

    /// <summary>The switched-on mods as mods.cfg writes them, in order</summary>
    private static List<string> ModsSwitchedOn()
        => modMenuEntries.Where(entry => entry.On).Select(entry => ModsConfig.ConfigName(entry.FullPath)).ToList();

    private static void LoadModsPage()
    {
        (ModsMenu, ModsItems) = LoadMenu("mods");
        ModsItems.curpos = (short)Math.Max(0, Array.FindIndex(ModsMenu, item => item.active != 0));
    }

    /// <summary>The Mods menu's rows for the page showing (its items-source)</summary>
    private static CP_itemtype[] BuildModsPage()
    {
        var language = _assetManager.GetText("en-us");
        var rows = modMenuEntries
            .Skip(modMenuPage * ModsPerPage)
            .Take(ModsPerPage)
            .Select(entry => new CP_itemtype((short)(entry.Loadable ? 1 : 0), FitText(entry.Text, ModNameWidth, MENU_FONT), null, entry));

        if (ModsPaged)
            rows = rows.Append(new CP_itemtype(1, "$STR_MODS_MORE".ToLanguageText(language), null) { id = "more" });

        return rows.ToArray();
    }

    private static void DrawModsMenu()
    {
        DrawMenuComponents("mods");
        DrawMenu(ModsItems, ModsMenu);
        for (var i = 0; i < ModsMenu.Length; i++)
        {
            if (ModsMenu[i].data is ModMenuEntry entry)
                DrawMenuCheckbox(ModsItems, i, entry.On);
        }
        DrawMenuGun(ModsItems);
        _videoManager.Update();
    }
}
