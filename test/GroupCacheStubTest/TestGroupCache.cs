// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TestGroupCache.cs" company="Microsoft Corporation">
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
        public string PeerName;
        public int CallCount = 0;

        public async Task GetAsync(string key, Stream sink, ICacheControl cacheControl, CancellationToken ct)
        {
            Interlocked.Increment(ref this.CallCount);
            using (var stream = key.StringToStream())
            {
                await stream.CopyToAsync(sink);
            }
        }
    }

    public class FibGetter : IGetter
    {
        public IGetter Getter;
        public int CallCount = 0;

        public async Task GetAsync(string key, Stream sink, ICacheControl cacheControl, CancellationToken ct)
        {
            Interlocked.Increment(ref this.CallCount);
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
                        await this.Getter.GetAsync((number - 1).ToString(), firstStream, new CacheControl(), CancellationToken.None).ConfigureAwait(false);
                        await this.Getter.GetAsync((number - 2).ToString(), secondStream, new CacheControl(), CancellationToken.None).ConfigureAwait(false);
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

    public class TestGroupCache : IDisposable
    {
        private readonly GroupCacheStub.OwinPool peer1Pool;
        private readonly GroupCacheStub.OwinPool peer2Pool;

        public TestGroupCache()
        {
            var peer1 = new PeerEndpoint { HostName = "localhost", Port = 60053 };
            var peer2 = new PeerEndpoint { HostName = "localhost", Port = 60054 };
            var peerList = new List<PeerEndpoint> { peer1, peer2 };

            // server listening on port 50053
            this.peer1Pool = new GroupCacheStub.OwinPool(peer1);
            this.peer1Pool.GetPicker("TestGroupForwarding").Add(peerList);
            this.peer1Pool.GetPicker("Fibonacci").Add(peerList);

            // server listening on port 50054
            this.peer2Pool = new GroupCacheStub.OwinPool(peer2);
            this.peer2Pool.GetPicker("TestGroupForwarding").Add(peerList);
            this.peer2Pool.GetPicker("Fibonacci").Add(peerList);
        }

        public void Dispose()
        {
            this.peer1Pool.Dispose();
            this.peer2Pool.Dispose();
        }

        [Fact]
        public void TestGroupForwarding()
        {
            var getter1 = new HelloGetter { PeerName = "peer1" };
            var getter2 = new HelloGetter { PeerName = "peer2" };
            var fooResponse = "foo";
            var barResponse = "bar";
            var peer1Group = GroupCache.NewGroup("TestGroupForwarding", getter1, this.peer1Pool.GetPicker("TestGroupForwarding"));
            var peer2Group = GroupCache.NewGroup("TestGroupForwarding", getter2, this.peer2Pool.GetPicker("TestGroupForwarding"));
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
                Xunit.Assert.Equal(fooResponse, result1aStr);
                var result1bStr = result1b.StreamToString();
                Xunit.Assert.Equal(barResponse, result1bStr);
                var result2aStr = result2a.StreamToString();
                Xunit.Assert.Equal(fooResponse, result2aStr);
                var result2bStr = result2b.StreamToString();
                Xunit.Assert.Equal(barResponse, result2bStr);
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

            Xunit.Assert.Equal(2, getter1.CallCount + getter2.CallCount);
            Xunit.Assert.Equal(2, group1Stat.LocalLoads + group2Stat.LocalLoads);
        }

        [Fact]
        public async Task TestFibonacciAsync()
        {
            var fibGetter1 = new FibGetter { CallCount = 0 };
            var fibGetter2 = new FibGetter { CallCount = 0 };
            var groupName = "Fibonacci";
            var peer1Group = GroupCache.NewGroup(groupName, fibGetter1, this.peer1Pool.GetPicker(groupName));
            var peer2Group = GroupCache.NewGroup(groupName, fibGetter2, this.peer2Pool.GetPicker(groupName));
            var group1Stat = new UnitTestGroupStat();
            var group2Stat = new UnitTestGroupStat();
            peer1Group.Stats = group1Stat;
            peer2Group.Stats = group2Stat;
            fibGetter1.Getter = peer1Group;
            fibGetter2.Getter = peer2Group;
            using (var result1a = new MemoryStream())
            {
                await peer1Group.GetAsync("90", result1a, new CacheControl(), CancellationToken.None);
                var result1aStr = result1a.StreamToString();
                Xunit.Assert.Equal("2880067194370816120", result1aStr);
            }

            Xunit.Assert.Equal(group1Stat.PeerLoads + group2Stat.PeerLoads, group1Stat.ServerRequests + group2Stat.ServerRequests);
            Xunit.Assert.Equal(91, group1Stat.LocalLoads + group2Stat.LocalLoads);
            Xunit.Assert.Equal(91, fibGetter1.CallCount + fibGetter2.CallCount);
            using (var result1b = new MemoryStream())
            {
                await peer2Group.GetAsync("90", result1b, new CacheControl(), CancellationToken.None);
                var result1bStr = result1b.StreamToString();
                Xunit.Assert.Equal("2880067194370816120", result1bStr);
            }

            Xunit.Assert.Equal(91, fibGetter1.CallCount + fibGetter2.CallCount);
        }
    }
}
