namespace Wolf3D.Exceptions;

internal class PfWolfFileLoaderException : Exception
{
    public PfWolfFileLoaderException(string message, params object[] args) : base (string.Format(message, args))
    {
    }
}
