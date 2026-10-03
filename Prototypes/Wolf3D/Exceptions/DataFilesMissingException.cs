namespace Wolf3D.Exceptions;

/// <summary>
/// The running release's data files (VSWAP.WL6...) aren't in the game folder: the game can't
/// start, and says which are missing
/// </summary>
internal class DataFilesMissingException(string message) : Exception(message)
{
}
