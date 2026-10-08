namespace PFWolf.Exceptions;

public class PfWolfGraphicException : Exception
{
    public PfWolfGraphicException(string message, params string[] args) : base (string.Format(message, args))
    {
    }
}
