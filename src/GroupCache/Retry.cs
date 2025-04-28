// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Retry.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Template class that simplifies the execution of operations with retry semantics.
    /// This class is thread-safe and suitable for concurrent access when executing operations and when performing configuration changes.
    /// </summary>
    public class Retry
    {
        private readonly SimpleRetryPolicy retryPolicy;

        public Action<RetryContext> OnRetry
        {
            get;
            set;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Retry"/> class.
        /// </summary>
        /// <param name="retryPolicy"></param>
        public Retry(SimpleRetryPolicy retryPolicy)
        {
            this.OnRetry = null;
            this.retryPolicy = retryPolicy;
        }

        /// <summary>
        /// Keep executing the callback until it eiether succeeds or the policy dictates that we stop,
        /// in which case the most recent exception thrown by the callback will be rethrown.
        /// </summary>
        /// <param name="action">lambda for an operation that can be retried.</param>
        public void Execute(Action<RetryContext> action)
        {
            this.Execute((context) =>
            {
                action(context);
                return true;
            });
        }

        /// <summary>
        /// Keep executing the callback until it eiether succeeds or the policy dictates that we stop,
        /// in which case the most recent exception thrown by the callback will be rethrown.
        /// </summary>
        /// <param name="callback">Callback interface for an operation that can be retried.</param>
        /// <returns>Return value of the callback.</returns>
        /// <typeparam name="T">Return type.</typeparam>
        public T Execute<T>(Func<RetryContext, T> callback)
        {
            Exception lastException = null;
            RetryContext context = new RetryContext(this.retryPolicy.MaxAttempts, this.retryPolicy.BackOffPeriod);
            while (this.retryPolicy.CanRetry(context) && !context.IsExhausted)
            {
                try
                {
                    lastException = null;
                    return callback(context);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    this.retryPolicy.RegisterThrowable(context, ex);

                    if (!this.retryPolicy.CanRetry(context))
                    {
                        // Rethrow last exception
                        throw;
                    }
                }

                var onRetryHandler = this.OnRetry;
                if (onRetryHandler != null)
                {
                    onRetryHandler(context);
                }

                this.retryPolicy.BackOff(context);
            }

            return this.retryPolicy.HandleRetryExhausted<T>(context);
        }

        /// <summary>
        /// Keep executing the callback until it eiether succeeds or the policy dictates that we stop,
        /// in which case the most recent exception thrown by the callback will be rethrown.
        /// </summary>
        /// <param name="action">lambda for an operation that can be retried.</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        public Task ExecuteAsync(Func<RetryContext, Task> func)
        {
            return this.ExecuteAsync(async (context) =>
            {
                await func(context).ConfigureAwait(false);
                return true;
            });
        }

        /// <summary>
        /// Keep executing the callback until it eiether succeeds or the policy dictates that we stop,
        /// in which case the most recent exception thrown by the callback will be rethrown.
        /// </summary>
        /// <param name="callback">Callback interface for an operation that can be retried.</param>
        /// <returns>Return value of the callback.</returns>
        /// <typeparam name="T">Return type.</typeparam>
        public async Task<T> ExecuteAsync<T>(Func<RetryContext, Task<T>> callback)
        {
            Exception lastException = null;
            RetryContext context = new RetryContext(this.retryPolicy.MaxAttempts, this.retryPolicy.BackOffPeriod);
            while (this.retryPolicy.CanRetry(context) && !context.IsExhausted)
            {
                try
                {
                    lastException = null;
                    return await callback(context).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    this.retryPolicy.RegisterThrowable(context, ex);

                    if (!this.retryPolicy.CanRetry(context))
                    {
                        // Rethrow last exception
                        throw;
                    }
                }

                var onRetryHandler = this.OnRetry;
                if (onRetryHandler != null)
                {
                    onRetryHandler(context);
                }

                await this.retryPolicy.BackOffAsync(context).ConfigureAwait(false);
            }

            return this.retryPolicy.HandleRetryExhausted<T>(context);
        }
    }
}