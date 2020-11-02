using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using GroupCache;
using GroupCacheStub;
using System.Net.Http;

namespace GroupCacheStubTest
{
    public sealed class GroupCacheHandler : IGroupCacheClient
    {
        public void Dispose()
        {
        }

        public bool IsLocal { get { return true; }  }

        public PeerEndpoint Endpoint => throw new NotImplementedException();

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
        public void Dispose()
        {
        }

        public bool IsLocal { get { return true; } }

        public PeerEndpoint Endpoint => throw new NotImplementedException();

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
        public void Dispose()
        {
        }

        public bool IsLocal { get { return true; } }

        public PeerEndpoint Endpoint => throw new NotImplementedException();

        public Task GetAsync(string group, string key, Stream sink, ICacheControl cacheControl, CancellationToken ct)
        {
            return Task.FromException<Stream>(new InvalidOperationException("test"));
        }
    }

    [TestClass]
    public class TestStub
    {
        [TestMethod]
        public async Task TestHelloWorldAsync()
        {
            var host = "localhost";
            var port = 50051;
            var handler = new GroupCacheHandler();
            using (var server = new OwinServer(handler, port, host)){
                server.Start();
                var endpoint = new PeerEndpoint() { HostName = host, Port = port };
                using (var client = new OwinClient(endpoint))
                {
                    using (var stream = new MemoryStream())
                    {
                        var cacheControl = new CacheControl();
                        await client.GetAsync("groupA", "key1", stream, cacheControl, CancellationToken.None);
                        var str = stream.StreamToString();
                        Assert.AreEqual(str, "HelloWorld");
                        Assert.IsFalse(cacheControl.NoStore);
                    }
                }
            }
        }

        [TestMethod]
        public async Task TestHelloWorldNoStoreAsync()
        {
            var host = "localhost";
            var port = 50051;
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
                        Assert.AreEqual(str, "HelloWorld");
                        Assert.IsTrue(cacheControl.NoStore);
                    }
                }
            }
        }


        [TestMethod]
        [ExpectedException(typeof(TaskCanceledException))]
        public async Task TestCancellationAsync()
        {
            var host = "localhost";
            var port = 50051;
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
                            await client.GetAsync("groupA", "key1", stream, new CacheControl(), ct);
                            var str = stream.StreamToString();
                            Assert.Fail();
                        }
                    }
                }
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InternalServerErrorException))]
        public async Task TestServerSideExceptionAsync()
        {
            var host = "localhost";
            var port = 50051;
            var handler = new BadGroupCacheHandler();

            using (var server = new OwinServer(handler, port, host))
            {
                server.Start();
                var endpoint = new PeerEndpoint() { HostName = host, Port = port };
                using (var client = new OwinClient(endpoint))
                {
                    using (var stream = new MemoryStream())
                    {
                        await client.GetAsync("groupA", "key1", stream, new CacheControl(), CancellationToken.None);
                        var str = stream.StreamToString();
                        Assert.Fail();
                    }
                }
            }
            
        }
        
        
        [TestMethod]
        [ExpectedException(typeof(ConnectFailureException))]
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
                    await client.GetAsync("groupA", "key1", stream, new CacheControl(), CancellationToken.None);
                    var str = stream.StreamToString();
                    Assert.Fail();
                }
            }   
        }

    }
}
