using PFWolf.Assets;
using PFWolf.Assets.Sounds;
using PFWolf.Editor.Data;

namespace PFWolf.Editor.Definitions;

/// <summary>The names a definition's fields can take, from the loaded game: its textures, pictures, classes, sounds...</summary>
public sealed class DefinitionSuggestions
{
    private readonly GameContent? _content;
    private readonly Dictionary<FieldHint, IReadOnlyList<string>> _lists = [];

    public DefinitionSuggestions(GameContent? content) => _content = content;

    public IReadOnlyList<string> For(FieldHint hint)
    {
        if (_content == null || hint is FieldHint.None or FieldHint.Color)
            return [];
        if (!_lists.TryGetValue(hint, out var list))
            _lists[hint] = list = Build(_content, hint);
        return list;
    }

    private static IReadOnlyList<string> Build(GameContent content, FieldHint hint)
    {
        var assets = content.Assets;
        IEnumerable<string> names = hint switch
        {
            FieldHint.Texture => assets.AssetNames.Where(name => !name.Contains('/') && assets.Exists<TextureAsset>(name)).Select(name => name.ToUpperInvariant()),
            FieldHint.Picture => assets.AssetNames.Where(name => !name.Contains('/') && assets.Exists<GraphicAsset>(name)),
            FieldHint.Class => content.Actors.Keys,
            FieldHint.Flag => content.Actors.Values.SelectMany(actor => actor.Flags),
            // GARDA1 -> GARD: the name less its frame letter and view number
            FieldHint.Sprite => assets.AssetNames.Where(name => !name.Contains('/') && name.Length > 2 && char.IsDigit(name[^1]) && char.IsLetter(name[^2])
                                                                && assets.Exists<SpriteAsset>(name))
                .Select(name => name[..^2].ToUpperInvariant()),
            FieldHint.Art => assets.AssetNames.Where(name => !name.Contains('/')
                                                             && (assets.Exists<SpriteAsset>(name) || assets.Exists<TextureAsset>(name) || assets.Exists<GraphicAsset>(name)))
                .Select(name => name.ToUpperInvariant()),
            FieldHint.Music => content.MusicNames,
            FieldHint.Map => content.Maps.Select(map => map.Name.ToUpperInvariant()),
            FieldHint.Font => assets.AssetNames.Where(name => !name.Contains('/') && assets.Exists<FontAsset>(name)),
            FieldHint.Sound => new[] { "sound-seq", $"{content.PackId}/sound-seq" }
                .Where(assets.Exists<SoundSequenceAsset>)
                .SelectMany(name => assets.Find<SoundSequenceAsset>(name)!.SoundInfo.Keys),
            FieldHint.Weather => content.WeatherNames,
            FieldHint.Faction => content.FactionNames,
            FieldHint.Translation => content.TranslationNames,
            FieldHint.DamageType => content.DamageTypesDealt,
            _ => [],
        };
        return names.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
