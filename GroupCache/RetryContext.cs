using System;

namespace GroupCache
{
    /// <summary>
    /// Low-level access to ongoing retry operation.
    /// Normally not needed by clients, but can be used to alter the course of the retry,
    /// </summary>
    public class RetryContext
    {
        private Exception _exception;
        private int _count = 0;
        private TimeSpan _backOffPeriod;
        private int _maxAttempts;

        /// <summary>
        /// the number of attempts before a retry becomes impossible.
        /// </summary>
        public int RetryCount
        {
            get { return _count; }
        }

        /// <summary>
        /// Accessor for the exception object that caused the current retry.
        /// </summary>
        public Exception LastException
        {
            get { return _exception; }
        }

        /// <summary>
        /// Accessor for the backoff period of the current policy
        /// </summary>
        public TimeSpan BackOffPeriod
        {
            get { return _backOffPeriod; }
        }

        /// <summary>
        /// Accessor for the max attempts of the current policy
        /// </summary>
        public int MaxAttempts
        {
            get { return _maxAttempts; }
        }

        /// <summary>
        /// Signal to the framework that no more attempts should be made to try or retry the current RetryCallback.
        /// </summary>
        public bool IsExhausted { get; set; }

        public RetryContext(int maxAttempts, TimeSpan backOffPeriod)
        {
            _maxAttempts = maxAttempts;
            _backOffPeriod = backOffPeriod;
            IsExhausted = false;
        }

        public void RegisterException(Exception ex)
        {
            _exception = ex;
            if (ex != null)
            {
                _count++;
            }
        }
    }
}