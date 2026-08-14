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
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Threading;

    public interface IFileSystem
    {
        Stream FileOpenRead(string path);

        void FileDelete(string sourcePath, string tmpPath);

        Task<string> WriteAtomicAsync(Func<Stream, CancellationToken, Task> copyAsync, string tmpPath, CancellationToken ct);

        string[] DirectoryGetFiles(string directoryPath);

        void DirectoryCreate(string directoryPath);

        void DirectoryReCreate(string directoryPath);
    }

    public class MockFileSystem : IFileSystem
    {
        private readonly Dictionary<string, Dictionary<string, byte[]>> dirs = new Dictionary<string, Dictionary<string, byte[]>>();
        private readonly object @lock = new object();

        /// <inheritdoc/>
        public void DirectoryCreate(string directoryPath)
        {
            lock (this.@lock)
            {
                if (!this.dirs.ContainsKey(directoryPath))
                {
                    this.dirs.Add(directoryPath, new Dictionary<string, byte[]>());
                }
            }
        }

        /// <inheritdoc/>
        public string[] DirectoryGetFiles(string directoryPath)
        {
            lock (this.@lock)
            {
                return this.dirs[directoryPath].Keys.ToArray();
            }
        }

        /// <inheritdoc/>
        public void DirectoryReCreate(string directoryPath)
        {
            lock (this.@lock)
            {
                this.dirs.Remove(directoryPath);
                this.dirs.Add(directoryPath, new Dictionary<string, byte[]>());
            }
        }

        /// <inheritdoc/>
        public void FileDelete(string sourcePath, string tmpPath)
        {
            lock (this.@lock)
            {
                var directory = Path.GetDirectoryName(sourcePath);
                var file = Path.GetFileName(sourcePath);
                var dir = this.dirs[directory];
                Debug.Assert(dir.ContainsKey(file));
                dir.Remove(file);
            }
        }

        /// <inheritdoc/>
        public Stream FileOpenRead(string path)
        {
            lock (this.@lock)
            {
                var srcFileName = Path.GetFileName(path);
                var srcDir = Path.GetDirectoryName(path);
                return new MemoryStream(this.dirs[srcDir][srcFileName]);
            }
        }

        /// <inheritdoc/>
        public async Task<string> WriteAtomicAsync(Func<Stream, CancellationToken, Task> copyAsync, string tmpPath, CancellationToken ct)
        {
            using (var memStream = new MemoryStream())
            {
                await copyAsync(memStream, ct).ConfigureAwait(false);
                var array = memStream.ToArray();
                lock (this.@lock)
                {
                    var randomPath = this.GenerateRandomFilePath(tmpPath);
                    var srcFileName = Path.GetFileName(randomPath);
                    var srcDir = Path.GetDirectoryName(randomPath);
                    this.dirs[srcDir].Add(srcFileName, array);
                    return randomPath;
                }
            }
        }

        private bool Exists(string path)
        {
            lock (this.@lock)
            {
                var srcFileName = Path.GetFileName(path);
                var srcDir = Path.GetDirectoryName(path);
                return this.dirs[srcDir].ContainsKey(srcFileName);
            }
        }

        private string GenerateRandomFilePath(string tmpPath)
        {
            string filePath;
            do
            {
                filePath = Path.Combine(tmpPath, Guid.NewGuid().ToString());
            }
            while (this.Exists(filePath));
            return filePath;
        }
    }

    public class FileSystem : IFileSystem
    {
        /// <inheritdoc/>
        public Stream FileOpenRead(string path)
        {
            return File.OpenRead(path);
        }

        /// <inheritdoc/>
        public void FileDelete(string sourcePath, string tmpPath)
        {
            File.Delete(sourcePath);
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        private string GenerateRandomFilePath(string tmpPath)
        {
            string filePath;
            do
            {
                filePath = Path.Combine(tmpPath, Path.GetRandomFileName());
            }
            while (File.Exists(filePath));
            return filePath;
        }

        /// <summary>
        /// 1- create a new file at tmpPath;
        /// 2- send data to the operating system kernel for writing to tmpPath;
        /// 3- close the file;
        /// 4- flush the writing of data to tmpPath;
        /// 5- rename tmpPath on top of path.
        /// </summary>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        public async Task<string> WriteAtomicAsync(Func<Stream, CancellationToken, Task> copyAsync, string tmpPath, CancellationToken ct)
        {
            string randomPath = this.GenerateRandomFilePath(tmpPath);
            bool created = false;
            try
            {
                await using (var fileStream = new FileStream(
                    randomPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    created = true;
                    await copyAsync(fileStream, ct).ConfigureAwait(false);
                    await fileStream.FlushAsync(ct).ConfigureAwait(false);
                }

                return randomPath;
            }
            catch
            {
                if (created)
                {
                    File.Delete(randomPath);
                }

                throw;
            }
        }

        /// <inheritdoc/>
        public string[] DirectoryGetFiles(string directoryPath)
        {
            return Directory.GetFiles(directoryPath);
        }

        /// <inheritdoc/>
        public void DirectoryCreate(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        /// <inheritdoc/>
        public void DirectoryReCreate(string directoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                // discard all entry
                Directory.Delete(directoryPath, true);
            }

            Directory.CreateDirectory(directoryPath);
        }
    }

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

        public string Key { get; private set; }

        // file path to the file storing this cache entry data
        public string EntryPath { get; internal set; }

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

        // Event trigger when an attempt was made to add an item to the cache
        // but the item was larger than the cache maximum size
        /// <inheritdoc/>
        public event Action<string> ItemOverCapacity;

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
