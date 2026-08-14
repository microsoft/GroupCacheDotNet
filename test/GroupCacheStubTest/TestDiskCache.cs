// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TestDiskCache.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCacheStubTest
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using GroupCache;
    using Xunit;

    public class TestDiskCache
    {
        [Fact]
        public async Task TestConcurrentCacheAccessAsync()
        {
            var mockFileSystem = new MockFileSystem();
            var diskCache = new DiskCache("c:\\CacheRoot", 10, mockFileSystem);
            long cacheFill = 0;
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < 24; i++)
            {
                var t = Task.Run(async () =>
                {
                    for (int k = 0; k < 100; k++)
                    {
                        for (int j = 0; j < 40; j++)
                        {
                            var keyString = j.ToString();
                            ICacheEntry cacheEntry = await diskCache.GetOrAddAsync(
                                keyString,
                                async (key, sink, cacheControl) =>
                                {
                                    Interlocked.Increment(ref cacheFill);
                                    using var keyStream = key.StringToStream();
                                    await keyStream.CopyToAsync(sink);
                                },
                                new CacheControl(),
                                CancellationToken.None);
                            try
                            {
                                var cacheEntryString = cacheEntry.Value().StreamToString();
                                Xunit.Assert.Equal(keyString, cacheEntryString);
                            }
                            finally
                            {
                                await cacheEntry.DisposeAsync();
                            }
                        }
                    }
                });
                tasks.Add(t);
            }

            await Task.WhenAll(tasks);
            Assert.Equal(10, mockFileSystem.DirectoryGetFiles("c:\\CacheRoot\\tmp").Length);
        }

        [Fact]
        public async Task NoStoreReturnsValueAndDeletesTemporaryFileOnReleaseAsync()
        {
            var fileSystem = new MockFileSystem();
            var diskCache = new DiskCache("c:\\NoStoreCache", 10, fileSystem);
            var cacheControl = new CacheControl { NoStore = true };

            ICacheEntry entry = await diskCache.GetOrAddAsync(
                "key",
                (key, sink, _) => "value".StringToStream().CopyToAsync(sink),
                cacheControl,
                CancellationToken.None);

            Assert.Equal("value", entry.Value().StreamToString());
            Assert.True(cacheControl.NoStore);
            Assert.Single(fileSystem.DirectoryGetFiles("c:\\NoStoreCache\\tmp"));

            await entry.DisposeAsync();

            Assert.Empty(fileSystem.DirectoryGetFiles("c:\\NoStoreCache\\tmp"));
            Assert.IsType<EmptyCacheEntry>(await diskCache.GetAsync("key", CancellationToken.None));
        }

        [Fact]
        public async Task RemoveInUseEntryMakesItImmediatelyUnavailableAsync()
        {
            var fileSystem = new MockFileSystem();
            var diskCache = new DiskCache("c:\\RemoveCache", 10, fileSystem);
            ICacheEntry entry = await diskCache.GetOrAddAsync(
                "key",
                (key, sink, _) => "value".StringToStream().CopyToAsync(sink),
                new CacheControl(),
                CancellationToken.None);

            await diskCache.RemoveAsync("key", CancellationToken.None);

            Assert.IsType<EmptyCacheEntry>(await diskCache.GetAsync("key", CancellationToken.None));
            Assert.Single(fileSystem.DirectoryGetFiles("c:\\RemoveCache\\tmp"));

            await entry.DisposeAsync();

            Assert.Empty(fileSystem.DirectoryGetFiles("c:\\RemoveCache\\tmp"));
        }

        [Fact]
        public async Task ReleasingRemovedEntryDoesNotRemoveReplacementAsync()
        {
            var fileSystem = new MockFileSystem();
            var diskCache = new DiskCache("c:\\ReplacementCache", 10, fileSystem);
            ICacheEntry removedEntry = await diskCache.GetOrAddAsync(
                "key",
                (key, sink, _) => "old".StringToStream().CopyToAsync(sink),
                new CacheControl(),
                CancellationToken.None);

            await diskCache.RemoveAsync("key", CancellationToken.None);
            ICacheEntry replacementEntry = await diskCache.GetOrAddAsync(
                "key",
                (key, sink, _) => "new".StringToStream().CopyToAsync(sink),
                new CacheControl(),
                CancellationToken.None);

            await removedEntry.DisposeAsync();
            await replacementEntry.DisposeAsync();
            await using ICacheEntry cachedEntry = await diskCache.GetAsync("key", CancellationToken.None);

            Assert.Equal("new", cachedEntry.Value().StreamToString());
            Assert.Single(fileSystem.DirectoryGetFiles("c:\\ReplacementCache\\tmp"));
        }

        [Fact]
        public async Task AtomicWritePropagatesFailureAndDeletesPartialFileAsync()
        {
            string directory = Path.Combine(Path.GetTempPath(), $"GroupCache-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                var fileSystem = new FileSystem();

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => fileSystem.WriteAtomicAsync(
                        async (sink, cancellationToken) =>
                        {
                            byte[] bytes = Encoding.UTF8.GetBytes("partial");
                            await sink.WriteAsync(bytes, cancellationToken);
                            throw new InvalidOperationException("write failed");
                        },
                        directory,
                        CancellationToken.None));

                Assert.Empty(Directory.GetFiles(directory));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
