// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SingleFlight.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a duplicate function call suppression mechanism.
    /// </summary>
    /// <typeparam name="T"> Return type of the callback.</typeparam>
    public sealed class SingleFlight<T>
    {
        private readonly ConcurrentDictionary<string, TaskCompletionSource<T>> flights = new ConcurrentDictionary<string, TaskCompletionSource<T>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="SingleFlight{T}"/> class.
        /// </summary>
        public SingleFlight()
        {
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="key"></param>
        /// <param name="valueFactory"></param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        public async Task<T> DoAsync(string key, Func<Task<T>> valueFactory)
        {
            TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();
            var result = this.flights.GetOrAdd(key, tcs);
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

            this.flights.TryRemove(key, out _);
            return await tcs.Task.ConfigureAwait(false);
        }
    }
}
