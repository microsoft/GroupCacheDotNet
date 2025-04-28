// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MemoryCache.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class MemoryCacheEntry : ICacheEntry
    {
        private ArraySegment<byte> segment;

        public MemoryCacheEntry(ArraySegment<byte> segment)
        {
            this.segment = segment;
        }

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
            return new MemoryStream(this.segment.Array, this.segment.Offset, this.segment.Count);
        }
    }

    public sealed class MemoryCache : ICache
    {
        private const int DefaultCopyBufferSize = 81920;
        private readonly SingleFlight<ICacheEntry> createEntry = new SingleFlight<ICacheEntry>();
        private readonly LRUCache<string, ArraySegment<byte>> cache;

        public MemoryCache(int maxItemCount, TimeSpan ttl)
        {
            this.cache = new LRUCache<string, ArraySegment<byte>>(maxItemCount, ttl);
            this.cache.ItemOverCapacity += this.TriggerItemCapacity;
        }

        public MemoryCache(int maxItemCount = 200, ulong capacity = 0)
        {
            this.cache = new LRUCache<string, ArraySegment<byte>>(maxItemCount, capacity);
            this.cache.ItemOverCapacity += this.TriggerItemCapacity;
        }

        public MemoryCache(int maxItemCount, ulong capacity, TimeSpan ttl)
        {
            this.cache = new LRUCache<string, ArraySegment<byte>>(maxItemCount, EqualityComparer<string>.Default, capacity, ttl);
            this.cache.ItemOverCapacity += this.TriggerItemCapacity;
        }

        /// <inheritdoc/>
        public event Action<string> ItemOverCapacity;

        /// <summary>valueFactory
        /// Get item from cache of call valueFactory if missing.
        /// </summary>
        /// <param name="key">The key of the value that need to be filled in.</param>
        /// <param name="valueFactory">valueFactory should write result to the stream but not close it.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        public Task<ICacheEntry> GetOrAddAsync(string key, Func<string, Stream, ICacheControl, Task> valueFactory, ICacheControl cacheControl, CancellationToken ct)
        {
            return this.createEntry.DoAsync(key, async () =>
            {
                ICacheEntry found = await this.GetAsync(key, ct).ConfigureAwait(false);
                if (!(found is EmptyCacheEntry))
                {
                    return found;
                }

                ArraySegment<byte> buffer;
                using (var memStream = new MemoryStream())
                {
                    await valueFactory(key, memStream, cacheControl).ConfigureAwait(false);
                    memStream.Position = 0;
                    buffer = new ArraySegment<byte>(memStream.ToArray());
                }

                if (!cacheControl.NoStore)
                {
                    this.cache.Add(key, buffer, (ulong)buffer.Count);
                }

                return new MemoryCacheEntry(buffer);
            });
        }

        public Task<ICacheEntry> GetAsync(string key, CancellationToken ct)
        {
            var found = this.cache.TryGetValue(key, out ArraySegment<byte> bytes);
            if (!found)
            {
                return Task.FromResult<ICacheEntry>(new EmptyCacheEntry());
            }

            return Task.FromResult<ICacheEntry>(new MemoryCacheEntry(bytes));
        }

        private void TriggerItemCapacity(KeyValuePair<string, ArraySegment<byte>> kv)
        {
            this.ItemOverCapacity?.Invoke(kv.Key);
        }

        /// <inheritdoc/>
        public Task RemoveAsync(string key, CancellationToken ct)
        {
            this.cache.Remove(key);
            return Task.CompletedTask;
        }
    }
}
