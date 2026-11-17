using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Extensions;
using PFWolf.Fonts;
using PFWolf.Menus;
using PFWolf.Gameplay;
using PFWolf.Managers;
using PFWolf.Ui;
using SDL2;

namespace PFWolf.Tests;

/// <summary>
/// Every menudefs menu of every game, the Multiplayer mod and each example mod with menus, drawn
/// as the game draws it (its components, its rows, sample checkboxes, sliders and choice values,
/// the gun or highlight bar) on a 320x200 screen, a wide one and one at twice the scale, written
/// out as a hash of each screen. Kept so a change to how menus are drawn (the menu editor's move
/// of the menu drawing into PFWolf.Common) can be checked to change nothing: set
/// PFWOLF_MENU_SNAPSHOT to a file path and the snapshot is written there, to compare before and
/// after. Needs a built game folder with the data files.
/// </summary>
[Category("GameData")]
public class MenuDrawingSnapshotTests
{
    private static readonly string[] Games =
        ["wolf3d", "wolf3d-apogee", "wolf3d-shareware", "spear", "spear-demo", "blake", "blake-shareware", "planetstrike"];

    private static readonly (int Width, int Height, double Scale)[] Screens = [(320, 200, 1), (384, 200, 1), (640, 400, 2)];

    [Test]
    public void Every_Menu_Of_Every_Game_And_Mod_Is_Drawn()
    {
        // Arrange
        var gameBin = Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64");
        var folder = new[] { Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA"), Path.Combine(gameBin, "Debug", "net10.0"), Path.Combine(gameBin, "Release", "net10.0") }
            .FirstOrDefault(folder => !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, "pfwolf.pk3")));
        if (folder == null)
            Assert.Ignore("No built game folder with pfwolf.pk3; build PFWolf for x64 or set PFWOLF_GAMEDATA");

        var root = TestPaths.RepoRoot();
        List<(string Title, string Game, string? Mod)> runs =
        [
            .. Games.Select(game => (game, game, (string?)null)),
            ("wolf3d + multiplayer-pk3", "wolf3d", Path.Combine(root, "multiplayer-pk3")),
            .. Directory.GetDirectories(Path.Combine(root, "examples", "mods")).Order(StringComparer.Ordinal)
                .Where(mod => Directory.Exists(Path.Combine(mod, "menudefs")))
                .Select(mod => (Path.GetFileName(mod), ModPack(mod), (string?)mod)),
        ];
        var probe = WriteStatsPanelProbe();
        runs.Add(("wolf3d + stats-panel probe", "wolf3d", probe));

        // Act
        var snapshot = new StringBuilder();
        int menus = 0;
        var workingFolder = Directory.GetCurrentDirectory();
        try
        {
            foreach (var run in runs)
            {
                snapshot.AppendLine($"=== {run.Title}");
                GameContent content;
                try
                {
                    content = GameContent.Load(folder!, run.Game, run.Mod != null ? [run.Mod] : []);
                }
                catch (Exception e)
                {
                    snapshot.AppendLine($"  doesn't load: {e.GetType().Name}");
                    continue;
                }
                snapshot.AppendLine($"  release {content.Game.ReleaseId}");
                menus += Write(snapshot, content, run.Title);
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(workingFolder);
            Directory.Delete(probe, recursive: true);
        }

        if (Environment.GetEnvironmentVariable("PFWOLF_MENU_SNAPSHOT") is { Length: > 0 } path)
            File.WriteAllText(path, snapshot.ToString());

        // Assert: each game has its twenty-odd menus, every one drawn, and the editor's pictures the game's
        Assert.That(menus, Is.GreaterThan(100));
        Assert.That(snapshot.ToString(), Does.Contain("main-menu"));
        Assert.That(snapshot.ToString(), Does.Not.Contain("[").And.Not.Contain("!EDITOR"), snapshot.ToString());
    }

    // A mod giving the new-episode and new-level menus stats panels (Midnight Assault's, outside the repository, has them)
    private static string WriteStatsPanelProbe()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"pfwolf-menu-probe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(folder, "menudefs"));
        File.WriteAllText(Path.Combine(folder, "modinfo.yaml"), "name: Menu probe\nversion: \"1\"\ngame-packs: [ wolf3d ]\n");
        const string panel = """
            stats-panel:
              x: 200
              y: 24
              top: 6
              font: SmallFont
              title: "Best"
              lines:
                - { label: "Time:", value: time, value-x: 48 }
                - { label: "Kills:", value: kill }
                - { label: "Secrets:", value: secret }
                - { label: "Kilroys:", value: objective.kilroy, count: true, hide-empty: true }
                - { label: "Intel:", value: objective.intel, hide-empty: true }
            """;
        File.WriteAllText(Path.Combine(folder, "menudefs", "new-level.yaml"), panel);
        File.WriteAllText(Path.Combine(folder, "menudefs", "new-episode.yaml"), panel);
        return folder;
    }

    // The pack an example mod is for: the first of its modinfo game-packs, else Wolf3D
    private static string ModPack(string mod)
    {
        var info = Path.Combine(mod, "modinfo.yaml");
        var line = File.Exists(info) ? File.ReadLines(info).FirstOrDefault(l => l.StartsWith("game-packs:")) : null;
        var first = line?.Split(':', 2)[1].Trim(' ', '[', ']').Split(',')[0].Trim();
        return string.IsNullOrEmpty(first) ? "wolf3d" : first;
    }

    // Writes each menu of the game, screen by screen, and returns how many there are
    private static int Write(StringBuilder snapshot, GameContent content, string title)
    {
        var assets = content.Assets;
        var names = assets.NamesOf<MenuAsset>()
            .Select(name => name.Contains('/') ? name[(name.LastIndexOf('/') + 1)..] : name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToList();

        int count = 0;
        foreach (var name in names)
        {
            if (assets.GetMenu(name) == null)
                continue;
            count++;
            var line = new StringBuilder($"  {name}:");
            foreach (var screen in Screens)
                line.Append(' ').Append(Draw(content, title, name, screen.Width, screen.Height, screen.Scale, lastRow: false));
            line.Append(" last-row ").Append(Draw(content, title, name, 320, 200, 1, lastRow: true));
            snapshot.AppendLine(line.ToString());
        }
        return count;
    }

    // A hash of the screen with the menu drawn on it, or what went wrong
    private static string Draw(GameContent content, string title, string name, int width, int height, double scale, bool lastRow)
    {
        var assets = new Lazy<AssetManager>(content.Assets);
        var screen = Screen(content, width, height, scale, out var surface);
        try
        {
            var gameInfo = content.Assets.FindInGamePack<GameInfoAsset>("game-info");
            var engine = new GameEngineManager(screen, null!, null!, assets, null!, null!, null!, null!);
            typeof(GameEngineManager).GetProperty(nameof(GameEngineManager.Game))!.SetValue(engine, content.Game);
            var fonts = new FontManager(assets, screen);
            var graphics = new GraphicManager(screen, assets, fonts)
            {
                MenuBackdrop = gameInfo?.MenuBackdrop,
                MenuStripe = gameInfo?.MenuStripe ?? new(),
            };
            var ui = new UiText(graphics, fonts);
            var settings = gameInfo != null ? GameInfoSettings.Read(gameInfo) : new GameInfoSettings();
            var draw = new MenuDrawing(graphics, screen, fonts, engine, () => settings);
            var defs = new MenuDefs(content.Assets, engine, graphics, null!);

            var (items, info) = defs.LoadMenu(name);
            if (lastRow && items.Length > 0)
            {
                info.CurPos = (short)(items.Length - 1);
                info.ScrollTo(info.CurPos);
            }

            var menu = content.Assets.GetMenu(name)!;
            var language = content.Assets.GetText("en-us");
            Func<string, string> translate = text => text.ToLanguageText(language);

            defs.DrawComponents(name);   // DrawMenuComponents without starting the menu's music
            draw.DrawMenu(info, items);
            DrawSamples(draw.DrawMenuCheckbox, draw.DrawMenuSlider, draw.DrawMenuChoice, menu, info, items);
            if (info.Scrolls)
                draw.DrawMenuScrollMarks(info, items, info.ScrollMarkX ?? 300);
            if (info.Cursor != null && items.Length > 0)
                draw.DrawMenuBar(info, items, info.CurPos, on: true);
            else if (items.Length > 0)
                draw.DrawMenuGun(info);
            if (menu.StatsPanel != null)
                StatsPanel.Draw(menu.StatsPanel, SampleStats, graphics.Menus, translate);

            var pixels = Pixels(surface, width, height);
            if (Environment.GetEnvironmentVariable("PFWOLF_MENU_PICTURES") is { Length: > 0 } pictures && !lastRow)
                WriteBitmap(Path.Combine(pictures, $"{title.Replace(' ', '_')}-{name}-{width}x{height}.bmp"), pixels, width, height, content.Palette);

            // The editor's picture of it (PFWolf.Common alone) has to be the same, pixel for pixel
            var hash = Convert.ToHexString(SHA256.HashData(pixels))[..16];
            if (scale == 1)
            {
                var editor = DrawInEditor(content, gameInfo, settings, defs, menu, name, info, items, width, height, translate);
                int differ = Enumerable.Range(0, pixels.Length).Count(i => pixels[i] != editor[i]);
                if (differ > 0)
                {
                    hash += $"!EDITOR({differ}px)";
                    if (Environment.GetEnvironmentVariable("PFWOLF_MENU_PICTURES") is { Length: > 0 } folder)
                        WriteBitmap(Path.Combine(folder, $"{title.Replace(' ', '_')}-{name}-{width}x{height}-editor.bmp"), editor, width, height, content.Palette);
                }
            }
            return hash;
        }
        catch (Exception e)
        {
            return $"[{e.GetType().Name}: {e.Message}]";
        }
        finally
        {
            SDL.SDL_FreeSurface(surface);
        }
    }

    // A made-up best for the stats panels: an hour and a bit, most kills, some secrets, a Kilroy
    private static readonly LevelStats SampleStats = new(3723, 85, 40,
        new(StringComparer.OrdinalIgnoreCase) { ["kilroy"] = (50, 1, 2) });

    // The menu drawn by PFWolf.Common's MenuPainter on its IndexedMenuCanvas, as the editor draws
    // it: the same rows (the game's LoadMenu), fonts built as the game's FontManager builds them
    private static byte[] DrawInEditor(GameContent content, GameInfoAsset? gameInfo, GameInfoSettings settings, MenuDefs defs,
        MenuMetadata menu, string name, ControlPanelMenuInfo info, ControlPanelItem[] items, int width, int height, Func<string, string> translate)
    {
        var assets = content.Assets;
        var definitions = assets.Exists<FontDefinitionsAsset>($"{assets.GamePackId}/fonts") ? assets.FindInGamePack<FontDefinitionsAsset>("fonts") : null;
        IndexedMenuCanvas? canvas = null;
        var builder = new FontBuilder(source => assets.Find<FontAsset>(source),
            picture => assets.Exists<GraphicAsset>(picture) ? assets.Find<GraphicAsset>(picture) : null,
            color => canvas!.TextCanvas.ResolveColor(color), _ => { });
        var fonts = new Dictionary<string, Font>(StringComparer.OrdinalIgnoreCase);
        Font? FindFont(string font)
        {
            if (string.IsNullOrEmpty(font))
                return null;
            if (fonts.TryGetValue(font, out var built))
                return built;
            FontDefinition? definition = null;
            definitions?.Fonts.TryGetValue(font, out definition);
            if (builder.Build(font, definition) is { } made)
                fonts[font] = made;
            return fonts.GetValueOrDefault(font);
        }

        var theme = assets.Exists<ColorThemeAsset>($"{assets.GamePackId}/colors") ? assets.FindInGamePack<ColorThemeAsset>("colors") : null;
        canvas = new IndexedMenuCanvas(width, height, content.Palette, theme, FindFont, picture => assets.Find<GraphicAsset>(picture),
            gameInfo?.MenuBackdrop, gameInfo?.MenuStripe);
        var painter = new MenuPainter(canvas);
        painter.DrawComponents(menu, defs.FitOf(name), defs.InCurrentGamePack, translate);
        painter.DrawMenu(info, items);
        DrawSamples(painter.DrawMenuCheckbox, painter.DrawMenuSlider, painter.DrawMenuChoice, menu, info, items);
        if (info.Scrolls)
            painter.DrawMenuScrollMarks(info, items, info.ScrollMarkX ?? 300);
        if (info.Cursor != null && items.Length > 0)
            painter.DrawMenuBar(info, items, info.CurPos, on: true);
        else if (items.Length > 0)
            painter.DrawMenuGun(info, settings.MenuGun);
        if (menu.StatsPanel != null)
            StatsPanel.Draw(menu.StatsPanel, SampleStats, painter, translate);
        return canvas.Pixels;
    }

    // What a menu's code draws on its rows, with made-up values: every other checkbox on, sliders
    // at 4 of 9, choices "Sample"
    private static void DrawSamples(Action<ControlPanelMenuInfo, ControlPanelItem[], string, bool> checkbox,
        Action<ControlPanelMenuInfo, ControlPanelItem[], string, int, int> slider,
        Action<ControlPanelMenuInfo, ControlPanelItem[], string, string> choice,
        MenuMetadata menu, ControlPanelMenuInfo info, ControlPanelItem[] items)
    {
        int index = 0;
        foreach (var item in menu.MenuItems)
        {
            if (item.Id == null || !items.Any(row => row.Id == item.Id))
                continue;
            switch (item)
            {
                case ToggleMenuItem:
                    checkbox(info, items, item.Id, index++ % 2 == 0);
                    break;
                case SliderMenuItem:
                    slider(info, items, item.Id, 4, 9);
                    break;
                case ChoiceMenuItem:
                    choice(info, items, item.Id, "Sample");
                    break;
            }
        }
    }

    // A VideoManager drawing into an 8-bit surface of this size, in the game's palette and colors
    private static VideoManager Screen(GameContent content, int width, int height, double scale, out IntPtr surface)
    {
        var video = new VideoManager(new Lazy<AssetManager>(content.Assets));
        var property = typeof(VideoManager).GetProperty("GamePal", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var colorType = property.PropertyType.GetElementType()!;
        var palette = Array.CreateInstance(colorType, 256);
        for (int i = 0; i < 256; i++)
        {
            var c = content.Palette[Math.Min(i, content.Palette.Length - 1)];
            object color = Activator.CreateInstance(colorType)!;
            colorType.GetField("r")!.SetValue(color, c.Red);
            colorType.GetField("g")!.SetValue(color, c.Green);
            colorType.GetField("b")!.SetValue(color, c.Blue);
            colorType.GetField("a")!.SetValue(color, (byte)255);
            palette.SetValue(color, i);
        }
        property.SetValue(video, palette);

        var named = (Dictionary<string, byte>)typeof(VideoManager).GetField("_colorIndexCache", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(video)!;
        foreach (var (colorName, color) in content.Assets.FindInGamePack<ColorThemeAsset>("colors")?.Colors ?? [])
            named[colorName] = video.ParseColor(color);

        surface = SDL.SDL_CreateRGBSurface(0, width, height, 8, 0, 0, 0, 0);
        Assert.That(surface, Is.Not.EqualTo(IntPtr.Zero), SDL.SDL_GetError());
        var pitch = Marshal.PtrToStructure<SDL.SDL_Surface>(surface).pitch;
        video.ScreenBuffer = surface;
        video.ScreenWidth = (short)width;
        video.ScreenHeight = (short)height;
        video.BufferPitch = (uint)pitch;
        video.YLookup = Enumerable.Range(0, height).Select(y => (uint)(y * pitch)).ToArray();
        video.UiScale = scale;
        video.UseUiOrigin(UiAnchor.Center);
        return video;
    }

    private static byte[] Pixels(IntPtr surface, int width, int height)
    {
        var info = Marshal.PtrToStructure<SDL.SDL_Surface>(surface);
        var pixels = new byte[width * height];
        for (int y = 0; y < height; y++)
            Marshal.Copy(info.pixels + y * info.pitch, pixels, y * width, width);
        return pixels;
    }

    // An 8-bit Windows bitmap of the screen, to look at (PFWOLF_MENU_PICTURES: a folder)
    internal static void WriteBitmap(string path, byte[] pixels, int width, int height, PaletteColor[] palette)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        int stride = (width + 3) & ~3;
        using var file = new BinaryWriter(File.Create(path));
        int offset = 14 + 40 + 256 * 4;
        file.Write((ushort)0x4D42); file.Write(offset + stride * height); file.Write(0); file.Write(offset);
        file.Write(40); file.Write(width); file.Write(height); file.Write((ushort)1); file.Write((ushort)8);
        file.Write(0); file.Write(stride * height); file.Write(2835); file.Write(2835); file.Write(256); file.Write(0);
        for (int i = 0; i < 256; i++)
        {
            var c = i < palette.Length ? palette[i] : default;
            file.Write(c.Blue); file.Write(c.Green); file.Write(c.Red); file.Write((byte)0);
        }
        var row = new byte[stride];
        for (int y = height - 1; y >= 0; y--)
        {
            Array.Copy(pixels, y * width, row, 0, width);
            file.Write(row);
        }
    }
}
