namespace Wolf3D.Assets;

public abstract record Asset
{
    public byte[] RawData { get; set; } = [];

    public int Size => RawData.Length;

    public abstract void Merge(Asset other);
}
