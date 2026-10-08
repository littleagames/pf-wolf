namespace PFWolf.Exceptions;

public class PfWolfFileLoaderException : Exception
{
    public PfWolfFileLoaderException(string message, params object[] args) : base (string.Format(message, args))
    {
    }
}
