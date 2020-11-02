using System;

namespace GroupCache
{
    public class ExhaustedRetryException : Exception
    {
        public ExhaustedRetryException()
        {
        }

        public ExhaustedRetryException(string message)
            : base(message)
        {
        }

        public ExhaustedRetryException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}