﻿using System;

namespace KeyVault.Acmebot;

[Serializable]
public class RetriableOrchestratorException : Exception
{
    public RetriableOrchestratorException()
    {
    }

    public RetriableOrchestratorException(string message)
        : base(message)
    {
    }

    public RetriableOrchestratorException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
