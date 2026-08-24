namespace Wolf3D.Assets;

internal record MapObjectTranslationAsset : Asset
{
    public Dictionary<int, MapActorAsset> Things { get; internal set; } = new();

    public override void Merge(Asset other)
    {
        if (other is MapObjectTranslationAsset otherAsset)
        {
            foreach (var item in otherAsset.Things)
            {
                this.Things[item.Key] = item.Value;
            }
        }
    }
}
