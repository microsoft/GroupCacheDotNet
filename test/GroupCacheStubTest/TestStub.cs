// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TestStub.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCacheStubTest
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using GroupCache;
    using GroupCacheStub;
    using Xunit;

    public sealed class GroupCacheHandler : IGroupCacheClient
    {
        /// <inheritdoc/>
        public bool IsLocal => true;

        /// <inheritdoc/>
        public PeerEndpoint Endpoint => throw new NotImplementedException();

        /// <inheritdoc/>
        public void Dispose()
        {
        }

        /// <inheritdoc/>
        public async Task GetAsync(
            string group,
            string key,
            Stream sink,
            ICacheControl cacheControl,
            CancellationToken ct)
        {
            using Stream resultStream = "HelloWorld".StringToStream();
            await resultStream.CopyToAsync(sink, ct);
        }
    }

    public sealed class GroupCacheHandlerWithMetadata : IGroupCacheClient
    {
        /// <inheritdoc/>
        public bool IsLocal => true;

        /// <inheritdoc/>
        public PeerEndpoint Endpoint => throw new NotImplementedException();

        /// <inheritdoc/>
        public void Dispose()
        {
        }

        /// <inheritdoc/>
        public async Task GetAsync(
            string group,
            string key,
            Stream sink,
            ICacheControl cacheControl,
            CancellationToken ct)
        {
            cacheControl.NoStore = true;
            cacheControl.CustomHeaders["X-GroupCache-Test"] = "metadata-value";
            using Stream resultStream = "HelloWorld".StringToStream();
            await resultStream.CopyToAsync(sink, ct);
        }
    }

    public sealed class BadGroupCacheHandler : IGroupCacheClient
    {
        /// <inheritdoc/>
        public bool IsLocal => true;

        /// <inheritdoc/>
        public PeerEndpoint Endpoint => throw new NotImplementedException();

        /// <inheritdoc/>
        public void Dispose()
        {
        }

        /// <inheritdoc/>
        public Task GetAsync(
            string group,
            string key,
            Stream sink,
            ICacheControl cacheControl,
            CancellationToken ct)
        {
            return Task.FromException(new InvalidOperationException("test"));
        }
    }

    public class TestStub
    {
        [Fact]
        public async Task TestHelloWorldAsync()
        {
            var handler = new GroupCacheHandler();
            await using var server = new KestrelServer(handler, 0);
            await server.StartAsync();
            using var client = new GroupCacheHttpClient(server.Endpoint);
            using var stream = new MemoryStream();
            var cacheControl = new CacheControl();

            await client.GetAsync("groupA", "key1", stream, cacheControl, CancellationToken.None);

            Assert.Equal("HelloWorld", stream.StreamToString());
            Assert.False(cacheControl.NoStore);
        }

        [Fact]
        public async Task TestResponseMetadataAsync()
        {
            var handler = new GroupCacheHandlerWithMetadata();
            await using var server = new KestrelServer(handler, 0);
            await server.StartAsync();
            using var client = new GroupCacheHttpClient(server.Endpoint);
            using var stream = new MemoryStream();
            var cacheControl = new CacheControl();

            await client.GetAsync("groupA", "key1", stream, cacheControl, CancellationToken.None);

            Assert.Equal("HelloWorld", stream.StreamToString());
            Assert.True(cacheControl.NoStore);
            Assert.Equal("metadata-value", cacheControl.CustomHeaders["X-GroupCache-Test"]);

            await using var plainServer = new KestrelServer(new GroupCacheHandler(), 0);
            await plainServer.StartAsync();
            using var plainClient = new GroupCacheHttpClient(plainServer.Endpoint);
            using var plainStream = new MemoryStream();
            await plainClient.GetAsync("groupA", "key1", plainStream, cacheControl, CancellationToken.None);

            Assert.False(cacheControl.NoStore);
            Assert.Empty(cacheControl.CustomHeaders);
        }

        [Fact]
        public async Task TestCancellationAsync()
        {
            var handler = new GroupCacheHandler();
            await using var server = new KestrelServer(handler, 0);
            await server.StartAsync();
            using var client = new GroupCacheHttpClient(server.Endpoint);
            using var stream = new MemoryStream();
            using var tokenSource = new CancellationTokenSource();
            await tokenSource.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => client.GetAsync("groupA", "key1", stream, new CacheControl(), tokenSource.Token));
        }

        [Fact]
        public async Task TestServerSideExceptionAsync()
        {
            var handler = new BadGroupCacheHandler();
            await using var server = new KestrelServer(handler, 0);
            await server.StartAsync();
            using var client = new GroupCacheHttpClient(server.Endpoint);
            using var stream = new MemoryStream();

            await Assert.ThrowsAsync<InternalServerErrorException>(
                () => client.GetAsync(
                    "groupA",
                    "key1",
                    stream,
                    new CacheControl(),
                    CancellationToken.None));
        }

        [Fact]
        public async Task TestUnavailableAsync()
        {
            var handler = new GroupCacheHandler();
            PeerEndpoint stoppedEndpoint;
            await using (var server = new KestrelServer(handler, 0))
            {
                await server.StartAsync();
                stoppedEndpoint = server.Endpoint;
            }

            using var client = new GroupCacheHttpClient(stoppedEndpoint);
            using var stream = new MemoryStream();

            await Assert.ThrowsAsync<ConnectFailureException>(
                () => client.GetAsync(
                    "groupA",
                    "key1",
                    stream,
                    new CacheControl(),
                    CancellationToken.None));
        }

        [Fact]
        public async Task TestServerCannotStartAfterDisposalAsync()
        {
            var server = new KestrelServer(new GroupCacheHandler(), 0);

            await server.DisposeAsync();
            await server.DisposeAsync();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => server.StartAsync());
        }
    }
}
