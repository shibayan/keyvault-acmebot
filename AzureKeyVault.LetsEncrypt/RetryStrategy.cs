using System;

using AzureKeyVault.LetsEncrypt.Internal;

namespace AzureKeyVault.LetsEncrypt
{
    public static class RetryStrategy
    {
        public static bool RetriableException(Exception exception)
        {
            return exception.InnerException is RetriableActivityException;
        }
    }
}
