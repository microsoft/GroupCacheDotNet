// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TestCacheMetadata.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCacheStubTest
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using GroupCache;
    using Xunit;

    public sealed class TestCacheMetadata
    {
        [Fact]
        public Task MemoryCacheRestoresMetadataOnCacheHitAsync()
        {
            return VerifyMetadataIsRestoredAsync(new MemoryCache());
        }

        [Fact]
        public Task DiskCacheRestoresMetadataOnCacheHitAsync()
        {
            return VerifyMetadataIsRestoredAsync(
                new DiskCache("c:\\MetadataCache", 10, new MockFileSystem()));
        }

        [Fact]
        public void ExistingCacheControlImplementationsReceiveHeaderStorage()
        {
            ICacheControl cacheControl = new LegacyCacheControl();

            cacheControl.CustomHeaders["X-GroupCache-Test"] = "metadata-value";

            Assert.Equal("metadata-value", cacheControl.CustomHeaders["X-GroupCache-Test"]);
        }

        [Fact]
        public async Task ConcurrentMemoryCacheWaitersReceiveMetadataAsync()
        {
            var cache = new MemoryCache();
            var fillStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFill = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstCacheControl = new CacheControl();
            var secondCacheControl = new CacheControl();

            Task<ICacheEntry> firstTask = cache.GetOrAddAsync(
                "key",
                async (key, sink, cacheControl) =>
                {
                    fillStarted.SetResult(true);
                    await releaseFill.Task;
                    cacheControl.CustomHeaders["X-GroupCache-Test"] = "metadata-value";
                    using var value = "value".StringToStream();
                    await value.CopyToAsync(sink);
                },
                firstCacheControl,
                CancellationToken.None);
            await fillStarted.Task;

            Task<ICacheEntry> secondTask = cache.GetOrAddAsync(
                "key",
                (key, sink, cacheControl) => throw new InvalidOperationException("Concurrent fill should be shared."),
                secondCacheControl,
                CancellationToken.None);
            releaseFill.SetResult(true);

            await using ICacheEntry firstEntry = await firstTask;
            await using ICacheEntry secondEntry = await secondTask;
            Assert.Equal("metadata-value", firstCacheControl.CustomHeaders["X-GroupCache-Test"]);
            Assert.Equal("metadata-value", secondCacheControl.CustomHeaders["X-GroupCache-Test"]);
        }

        [Fact]
        public async Task MemoryCachePreservesNoStoreOnReturnedEntryAsync()
        {
            var cache = new MemoryCache();
            var cacheControl = new CacheControl();

            await using ICacheEntry entry = await cache.GetOrAddAsync(
                "key",
                async (key, sink, control) =>
                {
                    control.NoStore = true;
                    using var value = "value".StringToStream();
                    await value.CopyToAsync(sink);
                },
                cacheControl,
                CancellationToken.None);

            Assert.True(cacheControl.NoStore);
            Assert.IsType<EmptyCacheEntry>(await cache.GetAsync("key", CancellationToken.None));
        }

        private static async Task VerifyMetadataIsRestoredAsync(ICache cache)
        {
            var initialCacheControl = new CacheControl();
            await using (ICacheEntry initialEntry = await cache.GetOrAddAsync(
                "key",
                async (key, sink, cacheControl) =>
                {
                    cacheControl.CustomHeaders["X-GroupCache-Test"] = "metadata-value";
                    using var value = "value".StringToStream();
                    await value.CopyToAsync(sink);
                },
                initialCacheControl,
                CancellationToken.None))
            {
                Assert.Equal("value", initialEntry.Value().StreamToString());
            }

            var hitCacheControl = new CacheControl { NoStore = true };
            hitCacheControl.CustomHeaders["X-Stale"] = "remove-me";
            await using ICacheEntry cachedEntry = await cache.GetOrAddAsync(
                "key",
                (key, sink, cacheControl) => throw new InvalidOperationException("Cache hit should not refill."),
                hitCacheControl,
                CancellationToken.None);

            Assert.Equal("value", cachedEntry.Value().StreamToString());
            Assert.False(hitCacheControl.NoStore);
            Assert.Equal("metadata-value", hitCacheControl.CustomHeaders["X-GroupCache-Test"]);
            Assert.False(hitCacheControl.CustomHeaders.ContainsKey("X-Stale"));
        }

        private sealed class LegacyCacheControl : ICacheControl
        {
            public bool NoStore { get; set; }
        }
    }
}
