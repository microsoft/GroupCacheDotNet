// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DiskCacheEntry.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    // Cache entries have an "in_cache" boolean indicating whether the cache has a
    // reference on the entry.
    //
    // The cache keeps two Dictionary of items in the cache.
    // All items in the cache are in one or the other, and never both.
    // Items still referenced by clients but erased from the cache are in neither list.
    public class DiskCacheEntry : ICacheEntry
    {
        private readonly CacheEntryMetadata metadata;
        private readonly DiskCache ownerCache;

        internal int Refs = 0; // References, including cache reference
        internal bool InCache = true; // Whether entry is in the cache.

        public DiskCacheEntry(string key, string entryPath, DiskCache owner)
            : this(key, entryPath, owner, CacheEntryMetadata.Empty)
        {
        }

        internal DiskCacheEntry(string key, string entryPath, DiskCache owner, CacheEntryMetadata metadata)
        {
            Debug.Assert(entryPath != string.Empty, "entryPath should not be empty");
            this.EntryPath = entryPath;
            this.ownerCache = owner;
            this.Key = key;
            this.metadata = metadata;
        }

        public string Key { get; private set; }

        // file path to the file storing this cache entry data
        public string EntryPath { get; internal set; }

        /// <summary>
        /// Read the file from disk each time this is called.
        /// </summary>
        /// <returns></returns>
        public Stream Value()
        {
            return this.ownerCache.ReadEntry(this);
        }

        /// <inheritdoc/>
        public void Ref()
        {
            Interlocked.Increment(ref this.Refs);
        }

        internal int Unref()
        {
            Debug.Assert(this.Refs > 0, "disk entry should have 1+ reference");
            return Interlocked.Decrement(ref this.Refs);
        }

        public void Delete()
        {
            Debug.Assert(this.Refs <= 0, "refcount must be 0");
            this.ownerCache.DeleteEntry(this);
        }

        /// <inheritdoc/>
        public Task DisposeAsync()
        {
            return this.ownerCache.ReleaseAsync(this);
        }

        internal void ApplyMetadata(ICacheControl cacheControl)
        {
            this.metadata.Apply(cacheControl);
        }
    }
}
