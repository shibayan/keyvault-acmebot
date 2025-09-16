using System;

namespace KeyVault.Acmebot;

[Serializable]
public class RetriableActivityException : Exception
{
    public RetriableActivityException()
    {
    }

    public RetriableActivityException(string message)
        : base(message)
    {
    }

    public RetriableActivityException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
