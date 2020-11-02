using System;
using System.Threading.Tasks;

namespace GroupCache
{
    /// <summary>
    /// Template class that simplifies the execution of operations with retry semantics.
    /// This class is thread-safe and suitable for concurrent access when executing operations and when performing configuration changes.
    /// </summary>
    public class Retry
    {
        private SimpleRetryPolicy _retryPolicy;

        public Action<RetryContext> OnRetry
        {
            get;
            set;
        }

        public Retry(SimpleRetryPolicy retryPolicy)
        {
            OnRetry = null;
            _retryPolicy = retryPolicy;
        }

        /// <summary>
        /// Keep executing the callback until it eiether succeeds or the policy dictates that we stop,
        /// in which case the most recent exception thrown by the callback will be rethrown.
        /// </summary>
        /// <param name="action">lambda for an operation that can be retried</param>
        public void Execute(Action<RetryContext> action)
        {
            Execute((context) =>
            {
                action(context);
                return true;
            });
        }

        /// <summary>
        /// Keep executing the callback until it eiether succeeds or the policy dictates that we stop,
        /// in which case the most recent exception thrown by the callback will be rethrown.
        /// </summary>
        /// <param name="callback">Callback interface for an operation that can be retried</param>
        /// <returns>Return value of the callback</returns>
        /// <typeparam name="T">Return type</typeparam>
        public T Execute<T>(Func<RetryContext, T> callback)
        {
            Exception lastException = null;
            RetryContext context = new RetryContext(_retryPolicy.MaxAttempts, _retryPolicy.BackOffPeriod);
            while (_retryPolicy.CanRetry(context) && !context.IsExhausted)
            {
                try
                {
                    lastException = null;
                    return callback(context);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _retryPolicy.RegisterThrowable(context, ex);

                    if (!_retryPolicy.CanRetry(context))
                    {
                        // Rethrow last exception
                        throw;
                    }
                }

                var onRetryHandler = OnRetry;
                if (onRetryHandler != null)
                {
                    onRetryHandler(context);
                }

                _retryPolicy.BackOff(context);
            }

            return _retryPolicy.HandleRetryExhausted<T>(context);
        }

        /// <summary>
        /// Keep executing the callback until it eiether succeeds or the policy dictates that we stop,
        /// in which case the most recent exception thrown by the callback will be rethrown.
        /// </summary>
        /// <param name="action">lambda for an operation that can be retried</param>
        public Task ExecuteAsync(Func<RetryContext, Task> func)
        {
            return ExecuteAsync(async (context) =>
            {
                await func(context).ConfigureAwait(false);
                return true;
            });
        }

        /// <summary>
        /// Keep executing the callback until it eiether succeeds or the policy dictates that we stop,
        /// in which case the most recent exception thrown by the callback will be rethrown.
        /// </summary>
        /// <param name="callback">Callback interface for an operation that can be retried</param>
        /// <returns>Return value of the callback</returns>
        /// <typeparam name="T">Return type</typeparam>
        public async Task<T> ExecuteAsync<T>(Func<RetryContext, Task<T>> callback)
        {
            Exception lastException = null;
            RetryContext context = new RetryContext(_retryPolicy.MaxAttempts, _retryPolicy.BackOffPeriod);
            while (_retryPolicy.CanRetry(context) && !context.IsExhausted)
            {
                try
                {
                    lastException = null;
                    return await callback(context).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _retryPolicy.RegisterThrowable(context, ex);

                    if (!_retryPolicy.CanRetry(context))
                    {
                        // Rethrow last exception
                        throw;
                    }
                }

                var onRetryHandler = OnRetry;
                if (onRetryHandler != null)
                {
                    onRetryHandler(context);
                }

                await _retryPolicy.BackOffAsync(context).ConfigureAwait(false);
            }

            return _retryPolicy.HandleRetryExhausted<T>(context);
        }
    }
}