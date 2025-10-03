namespace PFWolf.Common.Assets;

public record PicData : Asset
{
    public int NumFonts { get; set; }
    public int NumPics { get; set; }
    public List<Dimension> Dimensions { get; set; } = [];
}
