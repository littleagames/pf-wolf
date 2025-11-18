namespace PFWolf.Common.Components;

public record Graphic : RenderComponent
{
    public string AssetName { get; init; }

    public static Graphic Create(string asset, Transform transform)
    {
        return new Graphic(asset, transform);
    }

    private Graphic(string asset, Transform transform)
    {
        AssetName = asset;
        Transform = transform;
    }
}
