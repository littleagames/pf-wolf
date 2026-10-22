namespace PFWolf.Editor.Editing;

/// <summary>
/// Object-plane codes that keep their value in the tile after them (east), so the two go
/// together: Blake Stone's map-info codes, and the links some values make (Data.MapTiles)
/// </summary>
public interface IObjectPairs
{
    /// <summary>Whether the tile's object-plane value is the value of the code or link west of it</summary>
    bool IsValueTile(int x, int y);

    /// <summary>Whether the tile's code or link keeps its value in the tile east of it</summary>
    bool HasValueTile(int x, int y);
}
