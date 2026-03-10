namespace Wolf3D.Exceptions;

internal class PfWolfInputException : Exception
{
    public PfWolfInputException(string message, params string[] args) : base (string.Format(message, args))
    {
    }
}
