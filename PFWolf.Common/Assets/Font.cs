namespace PFWolf.Common.Assets;

public record Font : Asset
{
    public Font(List<FontCharacter> fontCharacters)
    {
        FontCharacters = fontCharacters;
    }

    public List<FontCharacter> FontCharacters { get; } = new List<FontCharacter>();
}

public record FontCharacter
{

    public short Height { get; set; }
    public byte Width { get; set; }
    public byte[] Data { get; set; } = [];
}
