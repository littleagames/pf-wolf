using PFWolf.Assets;
using PFWolf.Editor.Editing;
using PFWolf.Editor.Rendering;
using PFWolf.Loaders;

namespace PFWolf.Editor.Data;

/// <summary>Where a picture's file is in a mod (a folder or a pk3): "sprites/GARDA1.png"</summary>
public sealed record ArtFile(string ModPath, string EntryPath)
{
    public override string ToString() => $"{Path.GetFileName(ModPath.TrimEnd('\\', '/'))}: {EntryPath}";
}

/// <summary>
/// A change to a mod's pictures, worked out but not made yet: files written into it and taken out
/// of it, as one change to a pk3, and the picture to show once the game's loaded again
/// </summary>
public sealed record ArtChange(string ModPath, IReadOnlyList<(string EntryPath, byte[] Data)> Writes, IReadOnlyList<string> Deletes,
    ArtKind Kind, string ShowName, string Summary)
{
    /// <summary>Makes the change; returns where the first file written went</summary>
    public string Apply() => ModFiles.Change(ModPath, Writes, Deletes);
}

/// <summary>
/// Adding, replacing, renaming and taking out the pictures of mods: wall textures in textures/,
/// flats in flats/, sprites in sprites/ and pictures in graphics/, named by their file names. A
/// picture a mod has is changed where it is; one of the game's own (its data files or
/// pfwolf.pk3) is replaced by a file of the same name put in a mod.
/// </summary>
public static class ArtFiles
{
    /// <summary>The folder a mod keeps pictures of this kind in</summary>
    public static string Folder(ArtKind kind) => kind switch
    {
        ArtKind.Texture => "textures",
        ArtKind.Flat => "flats",
        ArtKind.Sprite => "sprites",
        _ => "graphics",
    };

    /// <summary>The asset type pictures of this kind load as</summary>
    public static string AssetType(ArtKind kind) => kind switch
    {
        ArtKind.Sprite => nameof(SpriteAsset),
        ArtKind.Picture => nameof(GraphicAsset),
        ArtKind.Movie => nameof(JamMovieAsset),
        _ => nameof(TextureAsset),
    };

    /// <summary>Whether the game has a picture of this kind and name (a texture and a flat are both textures)</summary>
    public static bool Exists(GameContent content, ArtKind kind, string name) => kind switch
    {
        ArtKind.Sprite => content.Assets.Exists<SpriteAsset>(name),
        ArtKind.Picture => content.Assets.Exists<GraphicAsset>(name),
        ArtKind.Movie => content.Assets.Exists<JamMovieAsset>(name),
        _ => content.Assets.Exists<TextureAsset>(name),
    };

    /// <summary>
    /// The file in a loaded mod the picture shown comes from, or null when it's the game's own
    /// (from its data files or pfwolf.pk3)
    /// </summary>
    public static ArtFile? InMod(GameContent content, ArtKind kind, string assetName)
    {
        var origin = ArtCatalog.Origins(content, assetName, AssetType(kind)).LastOrDefault(origin => origin.Action != AssetOrigin.LeftOut);
        if (origin == null)
            return null;
        var mod = content.Assets.LoadedMods.LastOrDefault(mod => mod.Source.Name.Equals(origin.Source, StringComparison.OrdinalIgnoreCase));
        return mod != null ? new ArtFile(mod.FullPath, origin.Path) : null;
    }

    /// <summary>What's wrong with a picture's name, or null when it will do: the file is named after it</summary>
    public static string? CheckName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "It needs a name.";
        if (name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-'))
            return "Use letters, digits, - and _ only: the name is the file's.";
        if (name.Length > 32)
            return "That's too long: 32 characters at most.";
        return null;
    }

    /// <summary>
    /// What a picture file has to be: a PNG or BMP the game reads. Throws (with why) when it isn't;
    /// returns its size and the extension to save it with.
    /// </summary>
    public static (int Width, int Height, string Extension) Check(byte[] data)
    {
        var image = ImageDecoder.Decode(data);
        return (image.Width, image.Height, PngFile.IsPng(data) ? ".png" : ".bmp");
    }

    /// <summary>New pictures (or ones in place of the game's of those names) put in a mod, all of one kind</summary>
    public static ArtChange Add(ArtKind kind, IReadOnlyList<(string Name, byte[] Data)> pictures, string modPath)
    {
        if (pictures.Count == 0)
            throw new InvalidOperationException("There's nothing to add");

        var writes = pictures.Select(picture =>
        {
            var (_, _, extension) = Check(picture.Data);
            return ($"{Folder(kind)}/{picture.Name.ToUpperInvariant()}{extension}", picture.Data);
        }).ToList();
        var first = pictures[0].Name.ToUpperInvariant();
        return new ArtChange(modPath, writes, [], kind, first, pictures.Count == 1 ? $"Added {first}" : $"Added {pictures.Count} pictures");
    }

    /// <summary>
    /// A picture file in place of a picture: written over the mod's own file, or, for one of the
    /// game's own, put in <paramref name="modPath"/>. A sprite keeps its offset when the new file
    /// gives none.
    /// </summary>
    public static ArtChange Replace(GameContent content, ArtKind kind, string assetName, byte[] data, string? modPath)
    {
        var (_, _, extension) = Check(data);
        if (kind == ArtKind.Sprite && PngFile.IsPng(data) && PngFile.ReadOffset(data) == null
            && content.Find<SpriteAsset>(assetName) is { HasOffset: true } sprite)
            data = PngFile.WithOffset(data, sprite.Offset.X, sprite.Offset.Y);

        var (mod, entryPath, deletes) = Target(content, kind, assetName, extension, modPath);
        return new ArtChange(mod, [(entryPath, data)], deletes, kind, assetName, $"Replaced {assetName.ToUpperInvariant()}");
    }

    /// <summary>A picture drawn in the game palette, written as a PNG over the mod's file or into <paramref name="modPath"/></summary>
    public static ArtChange Save(GameContent content, ArtKind kind, string assetName, IndexedPicture picture, (int X, int Y)? offset, string? modPath)
    {
        var data = PngFile.Write(picture, content.LoadedPalette, offset);
        var (mod, entryPath, deletes) = Target(content, kind, assetName, ".png", modPath);
        return new ArtChange(mod, [(entryPath, data)], deletes, kind, assetName, $"Saved {assetName.ToUpperInvariant()}");
    }

    /// <summary>
    /// A sprite with another offset (its PNG's grAb chunk): the mod's PNG with it changed, or the
    /// sprite written as a PNG (a BMP, or one of the game's own, into <paramref name="modPath"/>)
    /// </summary>
    public static ArtChange SetOffset(GameContent content, string spriteName, int x, int y, string? modPath)
    {
        var file = InMod(content, ArtKind.Sprite, spriteName);
        var data = file != null ? ModFiles.ReadBytes(file.ModPath, file.EntryPath) : null;
        if (data != null && PngFile.IsPng(data))
            data = PngFile.WithOffset(data, x, y);
        else
        {
            var picture = ArtCache.Indexed(content, ArtKind.Sprite, spriteName)
                          ?? throw new InvalidOperationException($"There's no sprite {spriteName}");
            data = PngFile.Write(picture, content.LoadedPalette, (x, y));
        }

        var (mod, entryPath, deletes) = Target(content, ArtKind.Sprite, spriteName, ".png", modPath);
        return new ArtChange(mod, [(entryPath, data)], deletes, ArtKind.Sprite, spriteName, $"Set {spriteName.ToUpperInvariant()}'s offset to {x}, {y}");
    }

    /// <summary>
    /// A picture as a PNG to edit: the mod's own file when it's a PNG (as it was drawn, in its own
    /// colors), else the picture written in the game palette, with a sprite's offset
    /// </summary>
    public static byte[] ToPng(GameContent content, ArtKind kind, string assetName)
    {
        if (InMod(content, kind, assetName) is { } file && ModFiles.ReadBytes(file.ModPath, file.EntryPath) is { } data && PngFile.IsPng(data))
            return data;

        var picture = ArtCache.Indexed(content, kind, assetName)
                      ?? throw new InvalidOperationException($"There's no {assetName}");
        (int, int)? offset = kind == ArtKind.Sprite && content.Find<SpriteAsset>(assetName) is { HasOffset: true } sprite ? (sprite.Offset.X, sprite.Offset.Y) : null;
        return PngFile.Write(picture, content.LoadedPalette, offset);
    }

    /// <summary>A mod's picture under another name, in the same folder</summary>
    public static ArtChange Rename(ArtFile file, ArtKind kind, string newName)
    {
        var data = ModFiles.ReadBytes(file.ModPath, file.EntryPath)
                   ?? throw new FileNotFoundException($"{file} isn't there any more");
        var entryPath = InSameFolder(file.EntryPath, newName.ToUpperInvariant() + Path.GetExtension(file.EntryPath));
        return new ArtChange(file.ModPath, [(entryPath, data)], [file.EntryPath], kind, newName.ToUpperInvariant(),
            $"Renamed {Path.GetFileNameWithoutExtension(file.EntryPath)} to {newName.ToUpperInvariant()}");
    }

    /// <summary>A mod's picture taken out of it (the game's own of that name, if there is one, shows again)</summary>
    public static ArtChange Delete(ArtFile file, ArtKind kind, string name)
        => new(file.ModPath, [], [file.EntryPath], kind, name, $"Took {name.ToUpperInvariant()} out of {Path.GetFileName(file.ModPath.TrimEnd('\\', '/'))}");

    // Where a picture's new file goes: over the mod's own (with another extension, the old one
    // goes), or else in the mod given, in the kind's folder
    private static (string ModPath, string EntryPath, IReadOnlyList<string> Deletes) Target(GameContent content, ArtKind kind, string assetName,
        string extension, string? modPath)
    {
        if (InMod(content, kind, assetName) is { } file)
        {
            var entryPath = Path.ChangeExtension(file.EntryPath, extension).Replace('\\', '/');
            return (file.ModPath, entryPath, ModFiles.SamePath(entryPath, file.EntryPath) ? [] : [file.EntryPath]);
        }

        if (modPath == null)
            throw new InvalidOperationException($"{assetName} is the game's own: pick a mod to put the new one in");
        return (modPath, $"{Folder(kind)}/{assetName.ToUpperInvariant()}{extension}", []);
    }

    private static string InSameFolder(string entryPath, string fileName)
    {
        var slash = entryPath.Replace('\\', '/').LastIndexOf('/');
        return slash < 0 ? fileName : entryPath[..(slash + 1)].Replace('\\', '/') + fileName;
    }
}
