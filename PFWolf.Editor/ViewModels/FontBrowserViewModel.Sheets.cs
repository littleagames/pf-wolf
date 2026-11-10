using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Editor.Definitions;
using PFWolf.Editor.Editing;
using PFWolf.Loaders;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.ViewModels;

/// <summary>
/// The font window's ways in and out: a Wolf3D or WDC font file added to a mod, a vga font
/// exported as a PNG sheet, a sheet brought in as a sheet font or a vga font
/// </summary>
public sealed partial class FontBrowserViewModel
{
    /// <summary>Asks the window to show the sheet import dialog</summary>
    public event EventHandler<SheetImportViewModel>? SheetImportRequested;

    // Every name a new font or font file can't take
    private HashSet<string> TakenNames()
        => _content == null ? []
            : FontCatalog.FontAssetNames(_content).Concat(_all.Select(item => item.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A Wolf3D font file (.fnt) or WDC's (.wsf, made a .fnt) put in a mod's fonts/ folder, named
    /// as asked (a game font chunk's name replaces it); then printed in by that name
    /// </summary>
    public async Task AddFontFileAsync(string fileName, byte[] data)
    {
        if (_content == null || AskText == null)
            return;
        FontAsset font;
        try
        {
            font = FontAsset.FromFile(data);
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException)
        {
            Status = $"{Path.GetFileName(fileName)} isn't a font file: {e.Message}";
            return;
        }

        // A game font's name is fine: that's how a mod replaces it
        var chunks = FontCatalog.FontAssetNames(_content).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var defined = _all.Where(item => item.Entry.IsDefined).Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = await AskText("Add font file to mod", "Its name in the mod (fonts/NAME.fnt; a game font's name replaces that font):",
            Path.GetFileNameWithoutExtension(fileName),
            candidate => FontCatalog.NameProblem(candidate, taken => defined.Contains(taken) && !chunks.Contains(taken)));
        if (name == null)
            return;
        // Through the glyph editor's save, so a mod's own file of the name is the one replaced
        var document = FontDocument.From(name.Trim(), font, isNew: true);
        if (await SaveGlyphsAsync(document))
            Status = $"Added {Path.GetFileName(fileName)} as {document.Name} ({font.Height} high)";
    }

    /// <summary>The picked vga font as a PNG sheet, with the fonts.yaml entry that reads it; null for another kind</summary>
    public (string Name, byte[] Png, string Yaml)? ExportSheet()
    {
        if (_content == null || PickedGlyphs is not { } font || SelectedItem?.Entry.SourceName is not { } source)
            return null;
        var (png, yaml) = FontSheets.Export(font, source, _content.Palette);
        return (source, png, yaml);
    }

    /// <summary>A sheet of characters (PNG or BMP) to bring in, in the import dialog</summary>
    public void ImportSheet(string fileName, byte[] data)
    {
        if (_content is not { } content)
            return;
        RgbaImage image;
        try
        {
            image = ImageDecoder.Decode(data);
        }
        catch (Exception e) when (e is NotSupportedException or InvalidDataException or IndexOutOfRangeException or ArgumentException)
        {
            Status = $"{Path.GetFileName(fileName)} can't be read as a picture: {e.Message}";
            return;
        }
        var taken = TakenNames();
        var chunks = FontCatalog.FontAssetNames(content).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var defined = _all.Where(item => item.Entry.IsDefined).Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sheet = new SheetImportViewModel(Path.GetFileNameWithoutExtension(fileName), data, image, content.Palette, _theme,
            (name, asVga) => asVga
                // A vga font file of a game font's name replaces it
                ? FontCatalog.NameProblem(name, taken => defined.Contains(taken) && !chunks.Contains(taken))
                : FontCatalog.NameProblem(name, taken.Contains)
                  ?? (content.Assets.Exists<GraphicAsset>(name) ? $"The game has a picture {name} already: the sheet is saved as a picture named like the font" : null))
        {
            SaveSheet = SaveSheetFontAsync,
            OpenVga = OpenGlyphEditor,
        };
        SheetImportRequested?.Invoke(this, sheet);
    }

    /// <summary>
    /// A sheet font saved into a mod picked: its picture as graphics/NAME.png and its fonts.yaml
    /// entry; then the game loads again
    /// </summary>
    public async Task<bool> SaveSheetFontAsync(string name, byte[] picture, YamlMappingNode definition)
    {
        if (_fontSet is not { } set || PickMod == null || await PickMod($"Save the font {name} into") is not { } mod)
            return false;
        try
        {
            var image = PngFile.IsPng(picture) ? ("png", picture) : ("bmp", picture);
            Editing.ModFiles.Write(mod, ($"graphics/{name}.{image.Item1}", image.Item2));
            var places = DefinitionSaver.Save(set.PlanWrite([name], definition, mod));
            Status = $"Saved the font {name} in {string.Join(", ", places)}, its sheet as graphics/{name}.{image.Item1}";
            AddMod?.Invoke(mod);
            Saved?.Invoke(this, mod);
            return true;
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Status = $"{name} isn't saved: {e.Message}";
            return false;
        }
    }
}
