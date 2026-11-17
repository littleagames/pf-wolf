using CommunityToolkit.Mvvm.ComponentModel;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;
using PFWolf.Editor.Rendering;
using PFWolf.Menus;

namespace PFWolf.Editor.ViewModels;

/// <summary>
/// The menu window's extras: the menu as pfwolf.pk3 has it shown in place of the one changed (by a
/// mod, or here), to compare, and the picture saved as a PNG
/// </summary>
public sealed partial class MenuBrowserViewModel
{
    private MenuMetadata? _gameMenu;

    /// <summary>The menu picked as pfwolf.pk3's files make it, in place of the mods' and the fields' (to compare)</summary>
    [ObservableProperty] private bool _showGameMenu;

    /// <summary>pfwolf.pk3 has a menu by the picked one's name that a mod or the fields change</summary>
    [ObservableProperty] private bool _hasGameMenu;

    // pfwolf.pk3's own of the menu picked, to compare with
    private void LoadGameMenu(MenuListItem? item)
    {
        _gameMenu = null;
        if (item != null && _content != null && item.Entry.Layers.Any(layer => layer.IsBase))
            _gameMenu = MenuCatalog.Merge(_content, item.Entry.Layers.Where(layer => layer.IsBase)).Menu;
        HasGameMenu = _gameMenu != null && (item!.Entry.FromMod || item.HasEdits);
        if (!HasGameMenu)
            ShowGameMenu = false;
    }

    partial void OnShowGameMenuChanged(bool value)
    {
        if (SelectedItem != null && _edits.TryGetValue(SelectedItem.Name, out var edit))
            UpdateFields(edit);
        else if (SelectedItem != null)
            Preview.Show(_content, SelectedItem.Name, _build?.Menu);
    }

    // The menu the picture shows: pfwolf.pk3's when comparing (nothing dragged then), else the one edited
    private void ShowInPreview(string name, MenuMetadata? edited)
    {
        bool compare = ShowGameMenu && _gameMenu != null;
        Preview.Layout = compare ? null : this;
        Preview.Show(_content, name, compare ? _gameMenu : edited);
    }

    /// <summary>The picture as shown (at its zoom), as a PNG; null with none</summary>
    public byte[]? PicturePng()
    {
        if (Preview.Picture is not { } picture || _content == null)
            return null;
        int zoom = Math.Max(Preview.Zoom, 1);
        var source = picture.Picture;
        int width = source.Width * zoom, height = source.Height * zoom;
        var indices = new byte[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                indices[y * width + x] = source.Indices[y / zoom * source.Width + x / zoom];
        return PngFile.Write(new IndexedPicture(width, height, indices, null), _content.Palette);
    }

    /// <summary>A file name for the picture: the menu's, the game's and the zoom</summary>
    public string PictureFileName => $"{SelectedItem?.Name ?? "menu"}-{_content?.PackId ?? "game"}-{Preview.Zoom}x.png";
}
