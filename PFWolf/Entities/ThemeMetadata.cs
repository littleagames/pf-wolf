namespace PFWolf.Entities;

public record ThemeMetadata
{
    public Dictionary<string, Color> Colors { get; set; } = [];
}
