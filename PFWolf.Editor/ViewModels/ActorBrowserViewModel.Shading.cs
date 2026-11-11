using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PFWolf.Editor.Data;
using PFWolf.Editor.Rendering;
using PFWolf.Entities.Actors;
using PFWolf.Shading;

namespace PFWolf.Editor.ViewModels;

// "Seen from the player": the preview's frame shaded as the game shades it at a distance
// (docs/dev/lighting-in-common-plan.md phase 1), with the game's default-map shading
public sealed partial class ActorBrowserViewModel
{
    private readonly ShadePreview _shadePreview = new();
    private readonly LightenLevelTable _lightenLevels = new();
    private readonly List<string> _lightenWarnings = [];
    private readonly Dictionary<(string Name, byte[]? Row, int Offset), Bitmap?> _shadedBitmaps = [];

    /// <summary>The preview shows the frame shaded as the player would see it, at <see cref="Distance"/></summary>
    [ObservableProperty] private bool _seenFromPlayer;

    /// <summary>How far away (tiles, to its middle along the view) the frame is seen from</summary>
    [ObservableProperty] private double _distance = 8;

    [ObservableProperty] private string _shadeText = "";

    public const double NearestDistance = 0.75, FarthestDistance = 32;

    partial void OnSeenFromPlayerChanged(bool value) => DrawFrame();

    partial void OnDistanceChanged(double value) => DrawFrame();

    // The game's named lighten levels, unknown names said in the frame text rather than the console
    private void LoadShading(GameContent content)
    {
        _lightenLevels.Warn = _lightenWarnings.Add;
        _lightenLevels.Set(content.GameInfo?.LightenLevels ?? []);
        DisposeShaded();
    }

    private void DisposeShaded()
    {
        foreach (var bitmap in _shadedBitmaps.Values)
            bitmap?.Dispose();
        _shadedBitmaps.Clear();
    }

    /// <summary>
    /// How the frame is shaded seen from the player: the default map's shading, the class's
    /// `lighten` and bright flag with the frame's modifiers (ActorShadingRules, as the game's
    /// Lighting.IsBright and Lighten); a weapon's frames other than its Spawn are in hand
    /// </summary>
    internal FrameShade? FrameShading(ActorStateFrame frame)
    {
        if (_content == null || Shown is not { } entry)
            return null;
        var data = entry.Merged ?? entry.Own;
        bool hasOwn = data.Properties.TryGetValue("lighten", out var own);
        bool flag = data.Flags.Any(f => f.Equals("bright", StringComparison.OrdinalIgnoreCase));
        _lightenWarnings.Clear();
        bool bright = ActorShadingRules.IsBright(frame.Bright, flag, hasOwn, own?.ToString());
        int lighten = ActorShadingRules.Lighten(hasOwn, own?.ToString(), frame.LightenLevel, _lightenLevels);
        bool inHand = entry.Kind == ActorKind.Weapon && !frame.StateName.Equals("Spawn", StringComparison.OrdinalIgnoreCase);

        var settings = ShadingResolver.ResolveShading(null, _content.DefaultMapInfo);
        var shade = _shadePreview.Shade(_content.Palette, settings, Math.Clamp(Distance, NearestDistance, FarthestDistance), inHand, bright, lighten);
        var notes = new List<string> { shade.Text };
        if (settings == null)
            notes.Add("the game's default map has no shading (a level's own comes in a later phase)");
        if (frame.Light != null)
            notes.Add("its own light isn't shown");
        notes.AddRange(_lightenWarnings);
        return shade with { Text = string.Join("; ", notes) };
    }

    // The sprite through the shade's light row, made once per row and step
    private Bitmap? ShadedPicture(string name, FrameShade shade)
    {
        if (shade.Row == null)
            return Picture(name);
        var key = (name, shade.Row, shade.Offset);
        if (_shadedBitmaps.TryGetValue(key, out var bitmap))
            return bitmap;
        if (_shadedBitmaps.Count > 512)
            DisposeShaded();
        if (Avalonia.Application.Current != null && _content != null && ArtCache.Indexed(_content, ArtKind.Sprite, name) is { } picture)
            bitmap = ArtCache.Render(ShadePreview.Remap(picture, shade.Row, shade.Offset), _content.Palette);
        return _shadedBitmaps[key] = bitmap;
    }
}
