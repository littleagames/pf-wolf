namespace PFWolf.Exceptions;

internal class PfWolfVideoException : Exception
{
    public PfWolfVideoException(string message, params string[] args) : base (string.Format(message, args))
    {
    }
}
