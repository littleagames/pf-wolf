namespace PFWolf.Common;

public abstract record Asset
{
    public string Name { get; init; } = null!;
}