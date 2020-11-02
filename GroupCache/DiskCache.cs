using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GroupCache
{
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
        Dictionary<string, Dictionary<string, byte[]>> dirs = new Dictionary<string, Dictionary<string, byte[]>>();
        Object _lock = new Object();

        public void DirectoryCreate(string directoryPath)
        {
            lock (_lock)
            {
                dirs.Add(directoryPath, new Dictionary<string, byte[]>());
            }
        }

        public string[] DirectoryGetFiles(string directoryPath)
        {
            lock (_lock)
            {
                return dirs[directoryPath].Keys.ToArray();
            }
        }

        public void DirectoryReCreate(string directoryPath)
        {
            lock (_lock)
            {
                dirs.Remove(directoryPath);
                dirs.Add(directoryPath, new Dictionary<string, byte[]>());
            }
        }

        public void FileDelete(string sourcePath, string tmpPath)
        {
            lock (_lock)
            {
                var directory = Path.GetDirectoryName(sourcePath);
                var file = Path.GetFileName(sourcePath);
                var dir = dirs[directory];
                Debug.Assert(dir.ContainsKey(file));
                dir.Remove(file);
            }
        }

        public Stream FileOpenRead(string path)
        {
            lock (_lock)
            {
                var srcFileName = Path.GetFileName(path);
                var srcDir = Path.GetDirectoryName(path);
                return new MemoryStream(dirs[srcDir][srcFileName]);
            }
        }

        public async Task<string> WriteAtomicAsync(Func<Stream, CancellationToken, Task> copyAsync, string tmpPath, CancellationToken ct)
        {
            using (var memStream = new MemoryStream())
            {
                await copyAsync(memStream, ct).ConfigureAwait(false);
                var array = memStream.ToArray();
                lock (_lock)
                {
                    var randomPath = GenerateRandomFilePath(tmpPath);
                    var srcFileName = Path.GetFileName(randomPath);
                    var srcDir = Path.GetDirectoryName(randomPath);
                    dirs[srcDir].Add(srcFileName, array);
                    return randomPath;
                }
            }
        }

        private bool Exists(string path)
        {
            lock (_lock)
            {
                var srcFileName = Path.GetFileName(path);
                var srcDir = Path.GetDirectoryName(path);
                return dirs[srcDir].ContainsKey(srcFileName);
            }
        }

        private string GenerateRandomFilePath(string tmpPath)
        {
            string filePath;
            do
            {
                filePath = Path.Combine(tmpPath, Guid.NewGuid().ToString());
            } while (Exists(filePath));
            return filePath;
        }
    }

    public class FileSystem : IFileSystem
    {

        public Stream FileOpenRead(string path)
        {
            return File.OpenRead(path);
        }

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
            } while (File.Exists(filePath));
            return filePath;
        }

        /// <summary>
        /// 1- create a new file at tmpPath;
        /// 2- send data to the operating system kernel for writing to tmpPath;
        /// 3- close the file;
        /// 4- flush the writing of data to tmpPath;
        /// 5- rename tmpPath on top of path.
        /// </summary>
        public async Task<string> WriteAtomicAsync(Func<Stream, CancellationToken, Task> copyAsync, string tmpPath, CancellationToken ct)
        {
            while (true)
            {
                try
                {
                    var randomPath = GenerateRandomFilePath(tmpPath);
                    using (var fileStream = File.Create(randomPath))
                    {
                        await copyAsync(fileStream, ct).ConfigureAwait(false);
                    }
                    return randomPath;
                }
                catch { }
            }
        }

        public string[] DirectoryGetFiles(string directoryPath)
        {
            return Directory.GetFiles(directoryPath);
        }

        public void DirectoryCreate(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

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
        private DiskCache _ownerCache;
        public string Key { get; private set; }
        // file path to the file storing this cache entry data
        public string EntryPath { get; internal set; }
        internal int Refs = 0; // References, including cache reference
        internal bool InCache = true; // Whether entry is in the cache.

        public DiskCacheEntry(string key, string entryPath, DiskCache owner)
        {
            Debug.Assert(entryPath != "", "entryPath should not be empty");
            EntryPath = entryPath;
            _ownerCache = owner;
            Key = key;
        }

        /// <summary>
        /// Read the file from disk each time this is called.
        /// </summary>
        /// <returns></returns>
        public Stream Value()
        {
            return _ownerCache.ReadEntry(this);
        }

        public void Ref()
        {
            Interlocked.Increment(ref Refs);
        }

        internal int Unref()
        {
            Debug.Assert(Refs > 0, "disk entry should have 1+ reference");
            return Interlocked.Decrement(ref Refs);
        }

        public void Delete()
        {
            Debug.Assert(Refs <= 0, "refcount must be 0");
            _ownerCache.DeleteEntry(this);
            
        }

        public Task DisposeAsync()
        {
            return _ownerCache.ReleaseAsync(this);
        }
    }

    public class DiskCache : ICache
    {
        SingleFlight<DiskCacheEntry> concurrentRequest = new SingleFlight<DiskCacheEntry>();
        public static readonly ICacheEntry EMPTY = new EmptyCacheEntry();
        private AsyncReaderWriterLock _rwLock = new AsyncReaderWriterLock();

        // Store (string -> DiskCacheEntry) mapping for entry that are on disk and not currently referenced by clients, in LRU order
        // Entries have refs==1 and in_cache==true.
        private LRUCache<string, DiskCacheEntry> _lruCache;

        // Store (string -> DiskCacheEntry) mapping for entry that are on disk and currently referenced by clients.
        // Entries have refs >= 2 and in_cache==true.
        private ConcurrentDictionary<string, DiskCacheEntry> _inUse = new ConcurrentDictionary<string, DiskCacheEntry>();

        // Root cache folder
        private string _cacheRootPath;
        // Folder for entry that are being written to disk but are not in the cache yet
        private string _cacheTmpPath;

        private IFileSystem _fs;

        // Event trigger when an attempt was made to add an item to the cache
        // but the item was larger than the cache maximum size
        public event Action<string> ItemOverCapacity;

        public DiskCache(string cacheRootPath, int maxEntryCount, IFileSystem fs = null)
        {
            _cacheRootPath = cacheRootPath;
            _cacheTmpPath = Path.Combine(_cacheRootPath, "tmp");
            _fs = fs;
            if (_fs == null)
            {
                _fs = new FileSystem();
            }

            _fs.DirectoryCreate(_cacheRootPath);
            _fs.DirectoryReCreate(_cacheTmpPath);

            _lruCache = new LRUCache<string, DiskCacheEntry>(maxEntryCount);
            _lruCache.ItemEvicted += (KeyValuePair<string, DiskCacheEntry> pair) => FinishErase(pair.Key, pair.Value);
            _lruCache.ItemOverCapacity += (kv) => ItemOverCapacity?.Invoke(kv.Key);
        }

        public async Task ReleaseAsync(DiskCacheEntry entry)
        {
            int refCount;
            // If InCache == false this is called from FinishErase with _lock already held and cause deadlock
            if (Volatile.Read(ref entry.InCache))
            {
                using (await _rwLock.WriteLockAsync())
                {
                    // entry is in "_inUse" and refcount is between 1 .. N
                    // Call Unref inside the lock to avoid other thread incrementing concurrently
                    refCount = entry.Unref();
                    if (refCount == 1)
                    {
                        // Entry is still in cache but no longer in use.
                        if (_inUse.TryRemove(entry.Key, out _))
                        {
                            _lruCache.Add(entry.Key, entry);
                        }
                    }
                    if (refCount <= 0)
                    {
                        _inUse.TryRemove(entry.Key, out _);
                        entry.Delete();
                        return;
                    }
                }
            }
            else
            {
                // its safe to decrment unref because Entry is not in cache anymore and refcount can only decrease now
                if (entry.Refs > 0) 
                {
                    refCount = entry.Unref();
                    if (refCount == 0)
                    {
                        _inUse.TryRemove(entry.Key, out _);
                        entry.Delete();
                        return;
                    }
                } 

            }
        }

        public async Task<ICacheEntry> GetOrAddAsync(string key, Func<string, Stream, ICacheControl, Task> valueFactory, ICacheControl cacheControl, CancellationToken ct)
        {
            using (await _rwLock.ReadLockAsync())
            {
                ICacheEntry found = GetInternal(key);
                if (!(found is EmptyCacheEntry))
                {
                    found.Ref(); // this entry remain valid after releasing the lock
                    return found;
                }
            }

            using (await _rwLock.WriteLockAsync())
            {
                ICacheEntry found = GetInternal(key);
                if (!(found is EmptyCacheEntry))
                {
                    found.Ref(); // this entry remain valid after releasing the lock
                    return found;
                }

                var randomPath = await _fs.WriteAtomicAsync((sink, cancel) => valueFactory(key, sink, cacheControl), _cacheTmpPath, ct).ConfigureAwait(false);
                var tmpEntry = new DiskCacheEntry(key, randomPath, this);
                if (!cacheControl.NoStore)
                {
                    SetInternal(key, tmpEntry);
                }

                var entry = GetInternal(key);
                entry.Ref();
                return entry;
            }
        }

        // Calling thread need to be first lock "_lock"
        private ICacheEntry GetInternal(string key)
        {
            var inUse = _inUse.TryGetValue(key, out DiskCacheEntry entry);
            if (inUse)
            {
                return entry;
            }

            var found = _lruCache.TryGetValue(key, out entry);
            if (found)
            {
                // Move from _lruCache to _inUse
                _lruCache.Remove(key);
                _inUse.TryAdd(key, entry);
                return entry;
            }
            else
            {
                return new EmptyCacheEntry();
            }
        }

        public async Task<ICacheEntry> GetAsync(string key, CancellationToken ct)
        {
            using (await _rwLock.ReadLockAsync())
            {
                ICacheEntry entry = GetInternal(key);
                entry.Ref();
                return entry;
            }
        }

        public async Task RemoveAsync(string key, CancellationToken ct)
        {
            using (await _rwLock.WriteLockAsync())
            {
                var found = _lruCache.TryGetValue(key, out DiskCacheEntry entry);
                if (found)
                {
                    _lruCache.Remove(key);
                    FinishErase(key, entry);
                }

                var inUse = _inUse.TryGetValue(key, out entry);
                if (inUse)
                {
                    FinishErase(key, entry);
                }
            }
        }

        // Calling thread need to be first take writelock
        private bool SetInternal(string key, DiskCacheEntry diskEntry)
        {
            if (_inUse.ContainsKey(key))
            {
                return false;
            }

            if (_lruCache.ContainsKey(key))
            {
                return false;
            }
            Volatile.Write(ref diskEntry.InCache, true);

            diskEntry.Ref(); // for the LRU cache's reference.
            if (diskEntry.Refs == 1)
            {
                _lruCache.Add(key, diskEntry);
            }
            else if (diskEntry.Refs > 1)
            {
                _inUse.TryAdd(key, diskEntry);
            }
            Debug.Assert(diskEntry.Refs > 0, "disk entry should have at least 1 reference");

            return true;
        }


        // Entry  has already been removed from the hash table
        // Finish removing from cache
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "<Pending>")]
        void FinishErase(string key, DiskCacheEntry entry)
        {
            if (entry != null)
            {
                Volatile.Write(ref entry.InCache, false);
                entry.DisposeAsync().Wait(); // not inside LRU cache anymore
            }
        }

        internal void DeleteEntry(DiskCacheEntry diskCacheEntry)
        {
            var refs = diskCacheEntry.Refs;
            Debug.Assert(refs == 0, "disk entry should have 0 reference");
            _fs.FileDelete(diskCacheEntry.EntryPath, _cacheTmpPath);
            diskCacheEntry.EntryPath = "";
        }

        internal Stream ReadEntry(DiskCacheEntry diskCacheEntry)
        {
            Debug.Assert(diskCacheEntry.Refs > 0, "disk entry should have at least 1 reference");
            Debug.Assert(diskCacheEntry.EntryPath != "", "disk entry have been deleted");
            return _fs.FileOpenRead(diskCacheEntry.EntryPath);
        }
    }
}
