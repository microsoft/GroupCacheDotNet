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
        private readonly MemoryCacheValue value;

        public MemoryCacheEntry(ArraySegment<byte> segment)
            : this(new MemoryCacheValue(segment, CacheEntryMetadata.Empty))
        {
        }

        internal MemoryCacheEntry(MemoryCacheValue value)
        {
            this.value = value;
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
            ArraySegment<byte> segment = this.value.Bytes;
            return new MemoryStream(segment.Array, segment.Offset, segment.Count);
        }

        internal void ApplyMetadata(ICacheControl cacheControl)
        {
            this.value.Metadata.Apply(cacheControl);
        }
    }

    public sealed class MemoryCache : ICache
    {
        private const int DefaultCopyBufferSize = 81920;
        private readonly SingleFlight<ICacheEntry> createEntry = new SingleFlight<ICacheEntry>();
        private readonly LRUCache<string, MemoryCacheValue> cache;

        public MemoryCache(int maxItemCount, TimeSpan ttl)
        {
            this.cache = new LRUCache<string, MemoryCacheValue>(maxItemCount, ttl);
            this.cache.ItemOverCapacity += this.TriggerItemCapacity;
        }

        public MemoryCache(int maxItemCount = 200, ulong capacity = 0)
        {
            this.cache = new LRUCache<string, MemoryCacheValue>(maxItemCount, capacity);
            this.cache.ItemOverCapacity += this.TriggerItemCapacity;
        }

        public MemoryCache(int maxItemCount, ulong capacity, TimeSpan ttl)
        {
            this.cache = new LRUCache<string, MemoryCacheValue>(maxItemCount, EqualityComparer<string>.Default, capacity, ttl);
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
        public async Task<ICacheEntry> GetOrAddAsync(
            string key,
            Func<string, Stream, ICacheControl, Task> valueFactory,
            ICacheControl cacheControl,
            CancellationToken ct)
        {
            ICacheEntry entry = await this.createEntry.DoAsync(key, async () =>
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

                var value = new MemoryCacheValue(buffer, CacheEntryMetadata.Capture(cacheControl));
                if (!cacheControl.NoStore)
                {
                    this.cache.Add(key, value, (ulong)buffer.Count);
                }

                return new MemoryCacheEntry(value);
            }).ConfigureAwait(false);

            ((MemoryCacheEntry)entry).ApplyMetadata(cacheControl);
            return entry;
        }

        public Task<ICacheEntry> GetAsync(string key, CancellationToken ct)
        {
            var found = this.cache.TryGetValue(key, out MemoryCacheValue value);
            if (!found)
            {
                return Task.FromResult<ICacheEntry>(new EmptyCacheEntry());
            }

            return Task.FromResult<ICacheEntry>(new MemoryCacheEntry(value));
        }

        /// <inheritdoc/>
        public Task RemoveAsync(string key, CancellationToken ct)
        {
            this.cache.Remove(key);
            return Task.CompletedTask;
        }

        private void TriggerItemCapacity(KeyValuePair<string, MemoryCacheValue> kv)
        {
            this.ItemOverCapacity?.Invoke(kv.Key);
        }
    }

    internal sealed class MemoryCacheValue
    {
        public MemoryCacheValue(ArraySegment<byte> bytes, CacheEntryMetadata metadata)
        {
            this.Bytes = bytes;
            this.Metadata = metadata;
        }

        public ArraySegment<byte> Bytes { get; }

        public CacheEntryMetadata Metadata { get; }
    }
}
