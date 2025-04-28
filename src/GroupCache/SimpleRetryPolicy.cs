// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SimpleRetryPolicy.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class SimpleRetryPolicy
    {
        public const int DEFAULTMAXATTEMPTS = 3;
        public static readonly TimeSpan DEFAULTBACKOFFPERIOD = TimeSpan.FromMilliseconds(1000);
        private volatile int maxAttempts;
        private volatile int backOffPeriod;
        private readonly Type[] retryableExceptionTypes;

        public SimpleRetryPolicy()
            : this(DEFAULTMAXATTEMPTS, DEFAULTBACKOFFPERIOD)
        {
        }

        public SimpleRetryPolicy(int maxAttempts, TimeSpan backOffPeriod)
            : this(maxAttempts, backOffPeriod, new Type[] { typeof(Exception) })
        {
        }

        public SimpleRetryPolicy(int maxAttempts, TimeSpan backOffPeriod, Type[] retryableExceptions)
        {
            this.maxAttempts = maxAttempts;
            this.backOffPeriod = (int)backOffPeriod.TotalMilliseconds;
            this.retryableExceptionTypes = retryableExceptions;
        }

        private bool RetryForException(Exception classifiable)
        {
            if (classifiable == null)
            {
                return false;
            }

            var exceptionClass = classifiable.GetType();

            if (this.retryableExceptionTypes == null)
            {
                return false;
            }
            else
            {
                // Determines whether the exception is a subclass of any element of the retryable exception type.
                return this.retryableExceptionTypes.Any((retryableType) => { return retryableType.IsAssignableFrom(exceptionClass); });
            }
        }

        /// <summary>
        /// Gets or sets the maximum retry count.
        /// </summary>
        /// <value>The maximum retry count.</value>
        public int MaxAttempts
        {
            get { return this.maxAttempts; }
            set { this.maxAttempts = value; }
        }

        /// <summary>
        /// Gets or sets the delay time span to sleep after an exception is thrown and a rety is
        /// attempted.
        /// </summary>
        /// <value>The delay time span.</value>
        public TimeSpan BackOffPeriod
        {
            get { return TimeSpan.FromMilliseconds(this.backOffPeriod); }
            set { this.backOffPeriod = (int)value.TotalMilliseconds; }
        }

        public virtual bool CanRetry(RetryContext context)
        {
            Exception ex = context.LastException;

            // N.B. the contract is defined to include the initial attempt in the count.
            return (ex == null || this.RetryForException(ex)) && (context.RetryCount < this.maxAttempts);
        }

        public virtual void RegisterThrowable(RetryContext context, Exception exception)
        {
            context.RegisterException(exception);
        }

        public virtual void BackOff(RetryContext context)
        {
            Thread.Sleep(this.BackOffPeriod);
        }

        public virtual Task BackOffAsync(RetryContext context)
        {
            return Task.Delay(this.BackOffPeriod);
        }

        public virtual T HandleRetryExhausted<T>(RetryContext context)
        {
            throw new ExhaustedRetryException("Retry exhausted after last attempt with no recovery path.", context.LastException);
        }
    }
}