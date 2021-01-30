using System;
using System.Runtime.Serialization;

namespace KeyVault.Acmebot
{
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

        protected PreconditionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
