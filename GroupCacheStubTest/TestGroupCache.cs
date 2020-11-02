using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GroupCache;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace GroupCacheStubTest
{
    public static class StreamHelper
    {
        public static string StreamToString(this Stream stream)
        {
            stream.Position = 0;
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        public static Stream StringToStream(this string src)
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(src);
            return new MemoryStream(byteArray);
        }
    }

    public class HelloGetter : IGetter
    {
        public string peerName;
        public int callCount = 0;
        public async Task GetAsync(string key, Stream sink, ICacheControl cacheControl, CancellationToken ct)
        {
            Interlocked.Increment(ref callCount);
            using (var stream = key.StringToStream())
            {
                await stream.CopyToAsync(sink);
            }
        }
    }
    
    public class FibGetter : IGetter
    {
        public IGetter getter;
        public int callCount = 0;
        public async Task GetAsync(string key, Stream sink, ICacheControl cacheControl, CancellationToken ct)
        {
            Interlocked.Increment(ref callCount);
            try
            {
                long number = long.Parse(key);
                long result;
                if (number == 0)
                {
                    result = 0;
                }
                else if (number == 1)
                {
                    result = 1;
                }
                else
                {
                    // Make to call to n-1 and n-2 in parallel
                    using (var firstStream = new MemoryStream())
                    using (var secondStream = new MemoryStream())
                    {
                        await getter.GetAsync((number - 1).ToString(), firstStream, new CacheControl(), CancellationToken.None).ConfigureAwait(false);
                        await getter.GetAsync((number - 2).ToString(), secondStream, new CacheControl(), CancellationToken.None).ConfigureAwait(false);
                        var firstStr = firstStream.StreamToString();
                        var secondStr = secondStream.StreamToString();
                        // return the sum
                        var firstLong = long.Parse(firstStr);
                        var secondLong = long.Parse(secondStr);
                        result = firstLong + secondLong;
                    }
                }
                var resultStream = result.ToString().StringToStream();
                resultStream.Position = 0;
                await resultStream.CopyToAsync(sink);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw;
            }
        }
    }


    [TestClass]
    public class TestGroupCache
    {
        private GroupCacheStub.OwinPool peer1Pool;
        private GroupCacheStub.OwinPool peer2Pool;

        [TestInitialize]
        public void Setup()
        {
            var peer1 = new PeerEndpoint { HostName = "localhost", Port = 50053 };
            var peer2 = new PeerEndpoint { HostName = "localhost", Port = 50054 };
            var peerList = new List<PeerEndpoint> { peer1, peer2 };

            // server listening on port 50053
            peer1Pool = new GroupCacheStub.OwinPool(peer1);
            peer1Pool.GetPicker("TestGroupForwarding").Add(peerList);
            peer1Pool.GetPicker("Fibonacci").Add(peerList);
            
            // server listening on port 50054
            peer2Pool = new GroupCacheStub.OwinPool(peer2);
            peer2Pool.GetPicker("TestGroupForwarding").Add(peerList);
            peer2Pool.GetPicker("Fibonacci").Add(peerList);
        }

        [TestCleanup]
        public void Teardown()
        {
            peer1Pool.Dispose();
            peer2Pool.Dispose();
        }

        [TestMethod]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "<Pending>")]
        public void TestGroupForwarding()
        {
            var getter1 = new HelloGetter { peerName = "peer1" };
            var getter2 = new HelloGetter { peerName = "peer2" };

            var fooResponse = "foo";
            var barResponse = "bar";

            // Creating groupcache client for "TestGroupForwarding" on peer1
            var peer1Group = GroupCache.GroupCache.NewGroup("TestGroupForwarding", getter1, peer1Pool.GetPicker("TestGroupForwarding"));
            // Creating groupcache client for "TestGroupForwarding" on peer2
            var peer2Group = GroupCache.GroupCache.NewGroup("TestGroupForwarding", getter2, peer2Pool.GetPicker("TestGroupForwarding"));

            // Inject Stat class that count number execution of each internal operation
            var group1Stat = new UnitTestGroupStat();
            var group2Stat = new UnitTestGroupStat();
            peer1Group.Stats = group1Stat;
            peer2Group.Stats = group2Stat;

            using (var result1a = new MemoryStream())
            using (var result1b = new MemoryStream())
            using (var result2a = new MemoryStream())
            using (var result2b = new MemoryStream())
            {
                peer1Group.GetAsync("foo", result1a, new CacheControl(), CancellationToken.None).Wait();

                peer1Group.GetAsync("bar", result1b, new CacheControl(), CancellationToken.None).Wait();

                peer2Group.GetAsync("foo", result2a, new CacheControl(), CancellationToken.None).Wait();

                peer2Group.GetAsync("bar", result2b, new CacheControl(), CancellationToken.None).Wait();

                var result1aStr = result1a.StreamToString();
                Assert.AreEqual(fooResponse, result1aStr);
                var result1bStr = result1b.StreamToString();
                Assert.AreEqual(barResponse, result1bStr);
                var result2aStr = result2a.StreamToString();
                Assert.AreEqual(fooResponse, result2aStr);
                var result2bStr = result2b.StreamToString();
                Assert.AreEqual(barResponse, result2bStr);
            }


            using (var result3a = new MemoryStream())
            using (var result3b = new MemoryStream())
            using (var result4a = new MemoryStream())
            using (var result4b = new MemoryStream())
            {
                peer1Group.GetAsync("foo", result3a, new CacheControl(), CancellationToken.None).Wait();
                result3a.Position = 0;
                peer1Group.GetAsync("bar", result3b, new CacheControl(), CancellationToken.None).Wait();
                result3b.Position = 0;
                peer2Group.GetAsync("foo", result4a, new CacheControl(), CancellationToken.None).Wait();
                result4a.Position = 0;
                peer2Group.GetAsync("bar", result4b, new CacheControl(), CancellationToken.None).Wait();
                result4b.Position = 0;
            }

            // Verify that we are indeed caching the result
            Assert.AreEqual(2, getter1.callCount + getter2.callCount);
            Assert.AreEqual(2, group1Stat.LocalLoads + group2Stat.LocalLoads);
        }

       
        [TestMethod]
        public async Task TestFibonacciAsync()
        {
            var fibGetter1 = new FibGetter { callCount = 0 };
            var fibGetter2 = new FibGetter { callCount = 0 };

            var groupName = "Fibonacci";
            // Creating groupcache client for "Fibonacci" on peer1
            var peer1Group = GroupCache.GroupCache.NewGroup(groupName, fibGetter1, peer1Pool.GetPicker(groupName));
            // Creating groupcache client for "Fibonacci" on peer1
            var peer2Group = GroupCache.GroupCache.NewGroup(groupName, fibGetter2, peer2Pool.GetPicker(groupName));

            // Inject Stat class that count number execution of each internal operation
            var group1Stat = new UnitTestGroupStat();
            var group2Stat = new UnitTestGroupStat();
            peer1Group.Stats = group1Stat;
            peer2Group.Stats = group2Stat;

            fibGetter1.getter = peer1Group; // Use the groupcache to do recursive to compute fibonacci
            fibGetter2.getter = peer2Group; // Use the groupcache to do recursive to compute fibonacci

            using (var result1a = new MemoryStream())
            {
                await peer1Group.GetAsync("90", result1a, new CacheControl(), CancellationToken.None);
                var result1aStr = result1a.StreamToString();
                Assert.AreEqual("2880067194370816120", result1aStr);
            }


            Assert.AreEqual(group1Stat.PeerLoads + group2Stat.PeerLoads, group1Stat.ServerRequests + group2Stat.ServerRequests);
            Assert.AreEqual(91, group1Stat.LocalLoads + group2Stat.LocalLoads);
            Assert.AreEqual(91, fibGetter1.callCount + fibGetter2.callCount);

            using (var result1b = new MemoryStream())
            {
                await peer2Group.GetAsync("90", result1b, new CacheControl(), CancellationToken.None);

                var result1bStr = result1b.StreamToString();
                Assert.AreEqual("2880067194370816120", result1bStr);
            }
            Assert.AreEqual(91, fibGetter1.callCount + fibGetter2.callCount);
        }
    }
}
