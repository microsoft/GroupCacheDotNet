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
        public void Dispose()
        {
        }

        /// <inheritdoc/>
        public bool IsLocal
        {
            get { return true; }
        }

        /// <inheritdoc/>
        public PeerEndpoint Endpoint => throw new NotImplementedException();

        /// <inheritdoc/>
        public async Task GetAsync(string group, string key, Stream sink, ICacheControl cacheControl, CancellationToken ct)
        {
            using (var resultStream = "HelloWorld".StringToStream())
            {
                await resultStream.CopyToAsync(sink);
            }

            return;
        }
    }

    public sealed class GroupCacheHandlerNoStore : IGroupCacheClient
    {
        /// <inheritdoc/>
        public void Dispose()
        {
        }

        /// <inheritdoc/>
        public bool IsLocal
        {
            get { return true; }
        }

        /// <inheritdoc/>
        public PeerEndpoint Endpoint => throw new NotImplementedException();

        /// <inheritdoc/>
        public async Task GetAsync(string group, string key, Stream sink, ICacheControl cacheControl, CancellationToken ct)
        {
            cacheControl.NoStore = true;
            using (var resultStream = "HelloWorld".StringToStream())
            {
                await resultStream.CopyToAsync(sink);
            }
        }
    }

    public sealed class BadGroupCacheHandler : IGroupCacheClient
    {
        /// <inheritdoc/>
        public void Dispose()
        {
        }

        /// <inheritdoc/>
        public bool IsLocal
        {
            get { return true; }
        }

        /// <inheritdoc/>
        public PeerEndpoint Endpoint => throw new NotImplementedException();

        /// <inheritdoc/>
        public Task GetAsync(string group, string key, Stream sink, ICacheControl cacheControl, CancellationToken ct)
        {
            return Task.FromException<Stream>(new InvalidOperationException("test"));
        }
    }

    public class TestStub
    {
        [Fact]
        public async Task TestHelloWorldAsync()
        {
            var host = "localhost";
            var port = 60051;
            var handler = new GroupCacheHandler();
            using (var server = new OwinServer(handler, port, host))
            {
                server.Start();
                var endpoint = new PeerEndpoint() { HostName = host, Port = port };
                using (var client = new OwinClient(endpoint))
                {
                    using (var stream = new MemoryStream())
                    {
                        var cacheControl = new CacheControl();
                        await client.GetAsync("groupA", "key1", stream, cacheControl, CancellationToken.None);
                        var str = stream.StreamToString();
                        Xunit.Assert.Equal("HelloWorld", str);
                        Xunit.Assert.False(cacheControl.NoStore);
                    }
                }
            }
        }

        [Fact]
        public async Task TestHelloWorldNoStoreAsync()
        {
            var host = "localhost";
            var port = 60051;
            var handler = new GroupCacheHandlerNoStore();
            using (var server = new OwinServer(handler, port, host))
            {
                server.Start();
                var endpoint = new PeerEndpoint() { HostName = host, Port = port };
                using (var client = new OwinClient(endpoint))
                {
                    using (var stream = new MemoryStream())
                    {
                        var cacheControl = new CacheControl();
                        await client.GetAsync("groupA", "key1", stream, cacheControl, CancellationToken.None);
                        var str = stream.StreamToString();
                        Xunit.Assert.Equal("HelloWorld", str);
                        Xunit.Assert.True(cacheControl.NoStore);
                    }
                }
            }
        }

        [Fact]
        public async Task TestCancellationAsync()
        {
            var host = "localhost";
            var port = 60051;
            var handler = new GroupCacheHandler();

            using (var tokenSource = new CancellationTokenSource())
            {
                tokenSource.Cancel();
                var ct = tokenSource.Token;
                using (var server = new OwinServer(handler, port, host))
                {
                    server.Start();
                    var endpoint = new PeerEndpoint() { HostName = host, Port = port };
                    using (var client = new OwinClient(endpoint))
                    {
                        using (var stream = new MemoryStream())
                        {
                            await Xunit.Assert.ThrowsAsync<TaskCanceledException>(async () =>
                            {
                                await client.GetAsync("groupA", "key1", stream, new CacheControl(), ct);
                                var str = stream.StreamToString();
                                Xunit.Assert.Fail("Should have thrown");
                            });
                        }
                    }
                }
            }
        }

        [Fact]
        public async Task TestServerSideExceptionAsync()
        {
            var host = "localhost";
            var port = 60051;
            var handler = new BadGroupCacheHandler();

            using (var server = new OwinServer(handler, port, host))
            {
                server.Start();
                var endpoint = new PeerEndpoint() { HostName = host, Port = port };
                using (var client = new OwinClient(endpoint))
                {
                    using (var stream = new MemoryStream())
                    {
                        await Xunit.Assert.ThrowsAsync<InternalServerErrorException>(async () =>
                        {
                            await client.GetAsync("groupA", "key1", stream, new CacheControl(), CancellationToken.None);
                            var str = stream.StreamToString();
                            Xunit.Assert.Fail("Should have thrown");
                        });
                    }
                }
            }
        }

        [Fact]
        public async Task TestUnavailableAsync()
        {
            var host = "localhost";
            var badPort = 666;
            var handler = new GroupCacheHandler();
            var endpoint = new PeerEndpoint() { HostName = host, Port = badPort };
            using (var client = new OwinClient(endpoint))
            {
                using (var stream = new MemoryStream())
                {
                    await Xunit.Assert.ThrowsAsync<ConnectFailureException>(async () =>
                    {
                        await client.GetAsync("groupA", "key1", stream, new CacheControl(), CancellationToken.None);
                        var str = stream.StreamToString();
                        Xunit.Assert.Fail("Should have thrown");
                    });
                }
            }
        }
    }
}
