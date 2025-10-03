namespace PFWolf.Common.Assets;

public record GamePackAssetReference : Asset
{
    public GamePackAssetReference()
    {
    }

    #region VSWAP
    public List<string> Walls { get; init; } = [];
    public List<string> Sprites { get; init; } = [];
    public List<string> DigitizedAudio { get; init; } = [];
    #endregion

    #region AUDIOT/HED
    public List<string> Audio { get; init; } = [];
    #endregion

    #region VGADICT/HEAD/GRAPH
    public List<string> Graphics { get; init; } = [];
    #endregion

    #region Maps
    public List<string> Maps { get; init; } = [];
    #endregion
}
