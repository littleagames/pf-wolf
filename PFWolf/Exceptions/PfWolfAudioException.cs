namespace PFWolf.Exceptions;

internal class PfWolfAudioException : Exception
{
    public PfWolfAudioException(string message, params string[] args) : base (string.Format(message, args))
    {
    }
}
