namespace Wolf3D.Exceptions;

/// <summary>
/// The running release's data files (VSWAP.WL6...) aren't in the game folder, or are another
/// version's: the game can't start, and says which files
/// </summary>
internal class DataFilesException(string message) : Exception(message)
{
}
