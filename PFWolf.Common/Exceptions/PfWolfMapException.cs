namespace PFWolf.Exceptions;

public class PfWolfMapException : Exception
{
    public PfWolfMapException(string message, params string[] args) : base (string.Format(message, args))
    {
    }
}
