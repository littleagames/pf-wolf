namespace PFWolf.Common.Assets;

public record Text : Asset
{
    public string Content { get; set; } = string.Empty;
    public Text()
    {
    }
}
