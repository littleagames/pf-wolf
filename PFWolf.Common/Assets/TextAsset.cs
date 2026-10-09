namespace PFWolf.Assets;

public record TextAsset : Asset
{
    public TextAsset(byte[] data)
    {
        RawData = data;
    }

    /// <summary>
    /// A pk3's texts/NAME.txt: as the data files hold texts, less the UTF-8 byte order mark a
    /// text editor may have put at its start (which would come before an article's ^P)
    /// </summary>
    public static TextAsset FromFile(byte[] data)
    {
        if (data is [0xEF, 0xBB, 0xBF, ..])
            data = data[3..];
        return new TextAsset(data);
    }

    public string ToText()
    {
        return new string(System.Text.Encoding.ASCII.GetString(RawData).ToCharArray());
    }
}
