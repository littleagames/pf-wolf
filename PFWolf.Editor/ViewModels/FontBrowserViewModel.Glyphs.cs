using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets;
using PFWolf.Definitions;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;
using PFWolf.Fonts;
using PFWolf.Loaders;

namespace PFWolf.Editor.ViewModels;

/// <summary>
/// The font window's way to the glyphs: a vga font's in the glyph editor (saved into a mod as
/// fonts/NAME.fnt), a graphic or sheet font's pictures in the pixel editor
/// </summary>
public sealed partial class FontBrowserViewModel
{
    /// <summary>Asks the window to open the glyph editor</summary>
    public event EventHandler<GlyphEditorViewModel>? GlyphEditRequested;

    /// <summary>Asks the editor to open a picture in the pixel editor (a graphic or sheet font's)</summary>
    public event EventHandler<string>? PictureEditRequested;

    // The vga font asset the picked font's glyphs are, or null for a graphic or sheet font
    private FontAsset? PickedGlyphs => _content != null && SelectedItem?.Entry.SourceName is { } source ? _content.Find<FontAsset>(source) : null;

    private bool CanEditGlyphs => PickedGlyphs != null;

    /// <summary>The picked vga font's glyphs in the glyph editor (its font chunk's or file's, under that name)</summary>
    [RelayCommand(CanExecute = nameof(CanEditGlyphs))]
    private void EditGlyphs()
    {
        if (PickedGlyphs is not { } asset || SelectedItem?.Entry.SourceName is not { } source)
            return;
        // The asset's own spelling, as its file is named
        var name = FontCatalog.FontAssetNames(_content!).FirstOrDefault(n => n.Equals(source, StringComparison.OrdinalIgnoreCase)) ?? source;
        OpenGlyphEditor(FontDocument.From(name, asset));
    }

    /// <summary>
    /// A new font file (a vga font used by its name, or by fonts.yaml): a copy of the picked vga
    /// font's glyphs, or a blank one ten high
    /// </summary>
    [RelayCommand]
    private async Task NewFontFileAsync()
    {
        if (AskText == null || _content == null)
            return;
        var taken = FontCatalog.FontAssetNames(_content).Concat(_all.Select(item => item.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var picked = PickedGlyphs;
        var name = await AskText("New font file", picked != null
                ? $"Its name (a fonts/NAME.fnt file; it starts as a copy of {SelectedItem!.Entry.SourceName}'s letters):"
                : "Its name (a fonts/NAME.fnt file; it starts with just a space):", "",
            candidate => FontCatalog.NameProblem(candidate, taken.Contains));
        if (name == null)
            return;
        name = name.Trim();
        OpenGlyphEditor(picked != null ? FontDocument.From(name, picked, isNew: true) : FontDocument.Blank(name, 10, 4));
    }

    private void OpenGlyphEditor(FontDocument document)
    {
        if (_content is not { } content)
            return;
        var others = FontCatalog.FontAssetNames(content).Order(StringComparer.OrdinalIgnoreCase).ToList();
        var editor = new GlyphEditorViewModel(document, content.Palette, _theme, others, content.Find<FontAsset>, SaveGlyphsAsync);
        GlyphEditRequested?.Invoke(this, editor);
    }

    /// <summary>
    /// Saves a font's glyphs into a mod as fonts/NAME.fnt: the mod whose fonts/ file it is
    /// already, else one picked. A font named like a game font chunk replaces it; a .wsf of the
    /// name in that mod is taken out (the .fnt is it now).
    /// </summary>
    public async Task<bool> SaveGlyphsAsync(FontDocument document)
    {
        if (_content is not { } content)
            return false;
        byte[] data;
        try
        {
            data = document.ToFile();
        }
        catch (InvalidDataException e)
        {
            Status = $"{document.Name} isn't saved: {e.Message}";
            return false;
        }

        var origin = FontCatalog.FontOrigins(content, document.Name).LastOrDefault(origin => origin.Action != AssetOrigin.LeftOut);
        var owner = origin == null ? null : content.Assets.LoadedMods.FirstOrDefault(mod => mod.Source.Name.Equals(origin.Source, StringComparison.OrdinalIgnoreCase));
        var mod = owner?.FullPath ?? (PickMod == null ? null : await PickMod($"Save the font {document.Name} into"));
        if (mod == null)
            return false;

        var path = $"fonts/{document.Name}.fnt";
        var deletes = new List<string>();
        if (owner != null && origin!.Path.Replace('\\', '/').StartsWith("fonts/", StringComparison.OrdinalIgnoreCase))
        {
            if (origin.Path.EndsWith(".fnt", StringComparison.OrdinalIgnoreCase))
                path = origin.Path.Replace('\\', '/');       // the file it is, as it's spelled
            else
                deletes.Add(origin.Path);                     // a .wsf: the .fnt takes its place
        }

        try
        {
            var place = Editing.ModFiles.Change(mod, [(path, data)], deletes);
            Status = $"Saved {document.Name} in {place}";
            AddMod?.Invoke(mod);
            Saved?.Invoke(this, mod);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Status = $"{document.Name} isn't saved: {e.Message}";
            return false;
        }
    }

    // The picture the picked character is drawn with, in a graphic font; a sheet font's sheet
    private string? PictureToEdit
    {
        get
        {
            if (SelectedItem?.Entry is not { Definition: { } definition } entry)
                return null;
            if (entry.Type == FontSchema.Sheet)
                return string.IsNullOrEmpty(definition.Image) ? null : definition.Image;
            if (entry.Type != FontSchema.Graphic || PickedCharacter is not int code)
                return null;
            var pictures = FontBuilder.GlyphPictures(definition);
            var ch = definition.UpperCase ? char.ToUpperInvariant((char)code) : (char)code;
            return pictures.TryGetValue(ch, out var picture) && _content?.Assets.Exists<GraphicAsset>(picture) == true ? picture : null;
        }
    }

    private bool CanEditGlyphPicture => PictureToEdit != null;

    /// <summary>The picked character's picture (a graphic font's), or the sheet (a sheet font's), in the pixel editor</summary>
    [RelayCommand(CanExecute = nameof(CanEditGlyphPicture))]
    private void EditGlyphPicture()
    {
        if (PictureToEdit is { } picture)
            PictureEditRequested?.Invoke(this, picture);
    }
}
