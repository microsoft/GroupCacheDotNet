// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ICache.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ICache
    {
        Task<ICacheEntry> GetOrAddAsync(string key, Func<string, Stream, ICacheControl, Task> valueFactory, ICacheControl cacheControl, CancellationToken ct);

        event Action<string> ItemOverCapacity;

        Task RemoveAsync(string key, CancellationToken ct);
    }

    public interface ICacheEntry
    {
        Stream Value();

        void Ref();

        Task DisposeAsync();
    }

    public class EmptyCacheEntry : ICacheEntry
    {
        /// <inheritdoc/>
        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public void Ref()
        {
        }

        /// <inheritdoc/>
        public Stream Value()
        {
            throw new NotImplementedException("Empty entry have no stream");
        }
    }
}
