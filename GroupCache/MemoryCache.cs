using DSInfra.Collections;
using DSInfra.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GroupCache
{
    public sealed class MemoryCacheEntry : ICacheEntry
    {
        private ArraySegment<byte> _segment;

        public MemoryCacheEntry(ArraySegment<byte> segment)
        {
            _segment = segment;
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public void Ref() { }

        public Stream Value()
        {
            return new MemoryStream(_segment.Array, _segment.Offset, _segment.Count);
        }
    }

    public sealed class MemoryCache : ICache
    {
        private const int _DefaultCopyBufferSize = 81920;
        private SingleFlight<ICacheEntry> createEntry = new SingleFlight<ICacheEntry>();
        private LRUCache<string, ArraySegment<byte>> _cache;

        public MemoryCache(int maxItemCount, TimeSpan ttl)
        {
            _cache = new LRUCache<string, ArraySegment<byte>>(maxItemCount, ttl);
            _cache.ItemOverCapacity += TriggerItemCapacity;
        }

        public MemoryCache(int maxItemCount = 200, ulong capacity = 0)
        {
            _cache = new LRUCache<string, ArraySegment<byte>>(maxItemCount, capacity);
            _cache.ItemOverCapacity += TriggerItemCapacity;
        }

        public MemoryCache(int maxItemCount, ulong capacity, TimeSpan ttl)
        {
            _cache = new LRUCache<string, ArraySegment<byte>>(maxItemCount, EqualityComparer<string>.Default, capacity, ttl);
            _cache.ItemOverCapacity += TriggerItemCapacity;
        }

        public event Action<string> ItemOverCapacity;

        /// <summary>valueFactory
        /// Get item from cache of call valueFactory if missing
        /// </summary>
        /// <param name="key">The key of the value that need to be filled in</param>
        /// <param name="valueFactory">valueFactory should write result to the stream but not close it</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns></returns>
        public Task<ICacheEntry> GetOrAddAsync(string key, Func<string, Stream, ICacheControl, Task> valueFactory, ICacheControl cacheControl, CancellationToken ct)
        {
            return createEntry.DoAsync(key, async () =>
            {
                ICacheEntry found = await GetAsync(key, ct).ConfigureAwait(false);
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
                    _cache.Add(key, buffer, (ulong)buffer.Count);
                }
                return new MemoryCacheEntry(buffer);
            });
        }

        public Task<ICacheEntry> GetAsync(string key, CancellationToken ct)
        {
            var found = _cache.TryGetValue(key, out ArraySegment<byte> bytes);
            if (!found)
            {
                return Task.FromResult<ICacheEntry>(new EmptyCacheEntry());
            }
            return Task.FromResult<ICacheEntry>(new MemoryCacheEntry(bytes));
        }

        private void TriggerItemCapacity(KeyValuePair<string, ArraySegment<byte>> kv)
        {
            ItemOverCapacity?.Invoke(kv.Key);
        }

        public Task RemoveAsync(string key, CancellationToken ct)
        {
            _cache.Remove(key);
            return Task.CompletedTask;
        }
    }
}
