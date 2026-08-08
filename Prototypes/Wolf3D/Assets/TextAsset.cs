namespace Wolf3D.Assets;

internal record TextAsset : Asset
{
    public TextAsset(byte[] data)
    {
        RawData = data;
    }

    public string ToText()
    {
        return new string(System.Text.Encoding.ASCII.GetString(RawData).ToCharArray());
    }
}
