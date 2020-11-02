using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GroupCache
{
    /// <summary>
    /// Provides a duplicate function call suppression mechanism.
    /// </summary>
    /// <typeparam name="T"> Return type of the callback</typeparam>
    public sealed class SingleFlight<T>
    {
        private ConcurrentDictionary<string, TaskCompletionSource<T>> _flights = new ConcurrentDictionary<string, TaskCompletionSource<T>>();

        public SingleFlight()
        {

        }

        public async Task<T> DoAsync(string key, Func<Task<T>> valueFactory)
        {
            TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();
            var result = _flights.GetOrAdd(key, tcs);
            if (result != tcs)
            {
                return await result.Task.ConfigureAwait(false);
            }

            try
            {
                var value = await valueFactory().ConfigureAwait(false);
                tcs.TrySetResult(value);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }

            _flights.TryRemove(key, out _);
            return await tcs.Task.ConfigureAwait(false);
        }
    }
}
