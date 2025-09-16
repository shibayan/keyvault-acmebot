﻿using System;

namespace KeyVault.Acmebot;

[Serializable]
public class PreconditionException : Exception
{
    public PreconditionException()
    {
    }

    public PreconditionException(string message)
        : base(message)
    {
    }

    public PreconditionException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
