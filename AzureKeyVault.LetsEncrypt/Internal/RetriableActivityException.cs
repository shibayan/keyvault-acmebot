using System;
using System.Runtime.Serialization;

namespace AzureKeyVault.LetsEncrypt.Internal
{
    [Serializable]
    public class RetriableActivityException : Exception
    {
        public RetriableActivityException()
        {
        }

        public RetriableActivityException(string message) : base(message)
        {
        }

        public RetriableActivityException(string message, Exception inner) : base(message, inner)
        {
        }

        protected RetriableActivityException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
