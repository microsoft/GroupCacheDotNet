// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DiskCache.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Threading;

    public class DiskCache : ICache
    {
        public static readonly ICacheEntry EMPTY = new EmptyCacheEntry();
        private readonly AsyncReaderWriterLock rwLock = new AsyncReaderWriterLock();

        // Store (string -> DiskCacheEntry) mapping for entry that are on disk and not currently referenced by clients, in LRU order
        // Entries have refs==1 and in_cache==true.
        private readonly LRUCache<string, DiskCacheEntry> lruCache;

        // Store (string -> DiskCacheEntry) mapping for entry that are on disk and currently referenced by clients.
        // Entries have refs >= 2 and in_cache==true.
        private readonly ConcurrentDictionary<string, DiskCacheEntry> inUse = new ConcurrentDictionary<string, DiskCacheEntry>();

        // Root cache folder
        private readonly string cacheRootPath;

        // Folder for entry that are being written to disk but are not in the cache yet
        private readonly string cacheTmpPath;

        private readonly IFileSystem fs;

        public DiskCache(string cacheRootPath, int maxEntryCount, IFileSystem fs = null)
        {
            if (maxEntryCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEntryCount));
            }

            this.cacheRootPath = cacheRootPath;
            this.cacheTmpPath = Path.Combine(this.cacheRootPath, "tmp");
            this.fs = fs;
            if (this.fs == null)
            {
                this.fs = new FileSystem();
            }

            this.fs.DirectoryCreate(this.cacheRootPath);
            this.fs.DirectoryReCreate(this.cacheTmpPath);

            this.lruCache = new LRUCache<string, DiskCacheEntry>(maxEntryCount);
            this.lruCache.ItemEvicted += (KeyValuePair<string, DiskCacheEntry> pair) => this.FinishErase(pair.Key, pair.Value);
            this.lruCache.ItemOverCapacity += (kv) => this.ItemOverCapacity?.Invoke(kv.Key);
        }

        // Event trigger when an attempt was made to add an item to the cache
        // but the item was larger than the cache maximum size
        /// <inheritdoc/>
        public event Action<string> ItemOverCapacity;

        public async Task ReleaseAsync(DiskCacheEntry entry)
        {
            if (Volatile.Read(ref entry.InCache))
            {
                using (await this.rwLock.WriteLockAsync())
                {
                    if (Volatile.Read(ref entry.InCache))
                    {
                        // Call Unref inside the lock so a cache lookup cannot increment the entry concurrently.
                        int refCount = entry.Unref();
                        if (refCount == 1)
                        {
                            if (this.inUse.TryRemove(entry.Key, out _))
                            {
                                this.lruCache.Add(entry.Key, entry);
                            }
                        }

                        if (refCount <= 0)
                        {
                            this.inUse.TryRemove(entry.Key, out _);
                            this.lruCache.Remove(entry.Key);
                            entry.Delete();
                        }

                        return;
                    }
                }
            }

            // Once an entry leaves the cache, no new references can be acquired and releases only decrement.
            int erasedEntryRefCount = entry.Unref();
            if (erasedEntryRefCount == 0)
            {
                entry.Delete();
            }
        }

        /// <inheritdoc/>
        public async Task<ICacheEntry> GetOrAddAsync(string key, Func<string, Stream, ICacheControl, Task> valueFactory, ICacheControl cacheControl, CancellationToken ct)
        {
            using (await this.rwLock.WriteLockAsync())
            {
                ICacheEntry found = this.GetInternal(key);
                if (!(found is EmptyCacheEntry))
                {
                    ((DiskCacheEntry)found).ApplyMetadata(cacheControl);
                    found.Ref(); // this entry remain valid after releasing the lock
                    return found;
                }
            }

            using (await this.rwLock.WriteLockAsync())
            {
                ICacheEntry found = this.GetInternal(key);
                if (!(found is EmptyCacheEntry))
                {
                    ((DiskCacheEntry)found).ApplyMetadata(cacheControl);
                    found.Ref(); // this entry remain valid after releasing the lock
                    return found;
                }

                var randomPath = await this.fs.WriteAtomicAsync((sink, cancel) => valueFactory(key, sink, cacheControl), this.cacheTmpPath, ct).ConfigureAwait(false);
                var tmpEntry = new DiskCacheEntry(key, randomPath, this, CacheEntryMetadata.Capture(cacheControl));
                if (cacheControl.NoStore)
                {
                    Volatile.Write(ref tmpEntry.InCache, false);
                    tmpEntry.Ref();
                    return tmpEntry;
                }

                this.SetInternal(key, tmpEntry);
                var entry = this.GetInternal(key);
                entry.Ref();
                return entry;
            }
        }

        // Calling thread must hold the write lock because this method moves entries between collections.
        private ICacheEntry GetInternal(string key)
        {
            var inUse = this.inUse.TryGetValue(key, out DiskCacheEntry entry);
            if (inUse)
            {
                return entry;
            }

            var found = this.lruCache.TryGetValue(key, out entry);
            if (found)
            {
                // Move from _lruCache to _inUse
                this.lruCache.Remove(key);
                this.inUse.TryAdd(key, entry);
                return entry;
            }
            else
            {
                return new EmptyCacheEntry();
            }
        }

        public async Task<ICacheEntry> GetAsync(string key, CancellationToken ct)
        {
            using (await this.rwLock.WriteLockAsync())
            {
                ICacheEntry entry = this.GetInternal(key);
                entry.Ref();
                return entry;
            }
        }

        /// <inheritdoc/>
        public async Task RemoveAsync(string key, CancellationToken ct)
        {
            using (await this.rwLock.WriteLockAsync())
            {
                var found = this.lruCache.TryGetValue(key, out DiskCacheEntry entry);
                if (found)
                {
                    this.lruCache.Remove(key);
                    this.FinishErase(key, entry);
                }

                if (this.inUse.TryRemove(key, out entry))
                {
                    this.FinishErase(key, entry);
                }
            }
        }

        // Calling thread need to be first take writelock
        private bool SetInternal(string key, DiskCacheEntry diskEntry)
        {
            if (this.inUse.ContainsKey(key))
            {
                return false;
            }

            if (this.lruCache.ContainsKey(key))
            {
                return false;
            }

            Volatile.Write(ref diskEntry.InCache, true);

            diskEntry.Ref(); // for the LRU cache's reference.
            if (diskEntry.Refs == 1)
            {
                this.lruCache.Add(key, diskEntry);
            }
            else if (diskEntry.Refs > 1)
            {
                this.inUse.TryAdd(key, diskEntry);
            }

            Debug.Assert(diskEntry.Refs > 0, "disk entry should have at least 1 reference");

            return true;
        }

        // Entry  has already been removed from the hash table
        // Finish removing from cache
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Usage",
            "VSTHRD002:Avoid problematic synchronous waits",
            Justification = "Eviction is synchronous and InCache=false keeps ReleaseAsync on its non-blocking path.")]
        private void FinishErase(string key, DiskCacheEntry entry)
        {
            if (entry != null)
            {
                Volatile.Write(ref entry.InCache, false);
                entry.DisposeAsync().GetAwaiter().GetResult();
            }
        }

        internal void DeleteEntry(DiskCacheEntry diskCacheEntry)
        {
            var refs = diskCacheEntry.Refs;
            Debug.Assert(refs == 0, "disk entry should have 0 reference");
            this.fs.FileDelete(diskCacheEntry.EntryPath, this.cacheTmpPath);
            diskCacheEntry.EntryPath = string.Empty;
        }

        internal Stream ReadEntry(DiskCacheEntry diskCacheEntry)
        {
            Debug.Assert(diskCacheEntry.Refs > 0, "disk entry should have at least 1 reference");
            Debug.Assert(diskCacheEntry.EntryPath != string.Empty, "disk entry have been deleted");
            return this.fs.FileOpenRead(diskCacheEntry.EntryPath);
        }
    }
}
