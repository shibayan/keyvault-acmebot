﻿using System;

using KeyVault.Acmebot.Internal;

namespace KeyVault.Acmebot.Contracts
{
    public static class RetryStrategy
    {
        public static bool RetriableException(Exception exception)
        {
            return exception.InnerException is RetriableActivityException;
        }
    }
}
