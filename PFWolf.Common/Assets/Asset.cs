namespace PFWolf.Assets;

public abstract record Asset
{
    public byte[] RawData { get; set; } = [];

    public int Size => RawData.Length;
}
