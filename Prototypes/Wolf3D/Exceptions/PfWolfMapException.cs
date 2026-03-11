namespace Wolf3D.Exceptions;

internal class PfWolfMapException : Exception
{
    public PfWolfMapException(string message, params string[] args) : base (string.Format(message, args))
    {
    }
}
