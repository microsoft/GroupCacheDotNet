// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RetryContext.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;

    /// <summary>
    /// Low-level access to ongoing retry operation.
    /// Normally not needed by clients, but can be used to alter the course of the retry,.
    /// </summary>
    public class RetryContext
    {
        private Exception exception;
        private int count = 0;
        private TimeSpan backOffPeriod;
        private readonly int maxAttempts;

        /// <summary>
        /// Gets the number of attempts before a retry becomes impossible.
        /// </summary>
        public int RetryCount
        {
            get { return this.count; }
        }

        /// <summary>
        /// Gets accessor for the exception object that caused the current retry.
        /// </summary>
        public Exception LastException
        {
            get { return this.exception; }
        }

        /// <summary>
        /// Gets accessor for the backoff period of the current policy.
        /// </summary>
        public TimeSpan BackOffPeriod
        {
            get { return this.backOffPeriod; }
        }

        /// <summary>
        /// Gets accessor for the max attempts of the current policy.
        /// </summary>
        public int MaxAttempts
        {
            get { return this.maxAttempts; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether signal to the framework that no more attempts should be made to try or retry the current RetryCallback.
        /// </summary>
        public bool IsExhausted { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RetryContext"/> class.
        /// </summary>
        /// <param name="maxAttempts"></param>
        /// <param name="backOffPeriod"></param>
        public RetryContext(int maxAttempts, TimeSpan backOffPeriod)
        {
            this.maxAttempts = maxAttempts;
            this.backOffPeriod = backOffPeriod;
            this.IsExhausted = false;
        }

        public void RegisterException(Exception ex)
        {
            this.exception = ex;
            if (ex != null)
            {
                this.count++;
            }
        }
    }
}