// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TestPoolPicker.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCacheStubTest
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using GroupCache;
    using Xunit;

    public sealed class TestPoolPicker
    {
        [Fact]
        public void PickPeersUsesDeterministicHrwOrder()
        {
            var alpha = new PeerEndpoint { HostName = "alpha.example", Port = 80 };
            var bravo = new PeerEndpoint { HostName = "bravo.example", Port = 80 };
            var charlie = new PeerEndpoint { HostName = "charlie.example", Port = 80 };
            var delta = new PeerEndpoint { HostName = "delta.example", Port = 80 };
            using var pool = new TestPool();
            var picker = new PoolPicker(
                "group",
                pool,
                new List<PeerEndpoint> { delta, bravo, alpha, charlie })
            {
                KeyHasher = new FixedStringHasher(123456789),
            };

            List<PeerEndpoint> selectedEndpoints = picker
                .PickPeers("key", 4)
                .Select(client => client.Endpoint)
                .ToList();

            Assert.Equal(new[] { alpha, delta, charlie, bravo }, selectedEndpoints);
        }

        [Fact]
        public void DefaultHashingHasStableKnownOrder()
        {
            List<PeerEndpoint> peers = CreatePeerList();
            using var pool = new TestPool();
            var picker = new PoolPicker("group", pool, peers);

            List<PeerEndpoint> selectedEndpoints = picker
                .PickPeers("stable-key", peers.Count)
                .Select(client => client.Endpoint)
                .ToList();

            Assert.Equal(new[] { peers[1], peers[2], peers[0] }, selectedEndpoints);
        }

        [Fact]
        public void PickPeersReturnsUniquePeersAndCapsRequestedCount()
        {
            List<PeerEndpoint> peers = CreatePeerList();
            peers.Add(peers[0]);
            using var pool = new TestPool();
            var picker = new PoolPicker("group", pool, peers)
            {
                KeyHasher = new FixedStringHasher(42),
            };

            const int distinctPeerCount = 3;
            List<IGroupCacheClient> selectedClients = picker.PickPeers("key", peers.Count + 10);
            List<PeerEndpoint> selectedEndpoints = selectedClients.Select(client => client.Endpoint).ToList();

            Assert.Equal(distinctPeerCount, selectedClients.Count);
            Assert.Equal(distinctPeerCount, new HashSet<PeerEndpoint>(selectedEndpoints).Count);
        }

        [Fact]
        public void AddPublishesTheUnionOfExistingAndNewPeers()
        {
            List<PeerEndpoint> peers = CreatePeerList();
            using var pool = new TestPool();
            var picker = new PoolPicker("group", pool, new List<PeerEndpoint> { peers[0] });

            picker.Add(new List<PeerEndpoint> { peers[0], peers[1], peers[2] });

            Assert.Equal(3, picker.Count);
            Assert.Equal(3, picker.PickPeers("key", 3).Count);
        }

        [Fact]
        public void SetNullPublishesAnEmptySnapshot()
        {
            using var pool = new TestPool();
            var picker = new PoolPicker("group", pool, CreatePeerList());

            picker.Set(null);
            picker.Add(null);

            Assert.Equal(0, picker.Count);
            Assert.Empty(picker.PickPeers("key", 1));
        }

        private static List<PeerEndpoint> CreatePeerList()
        {
            return new List<PeerEndpoint>
            {
                new PeerEndpoint { HostName = "alpha.example", Port = 80 },
                new PeerEndpoint { HostName = "bravo.example", Port = 80 },
                new PeerEndpoint { HostName = "charlie.example", Port = 80 },
            };
        }

        private sealed class FixedStringHasher : IEqualityComparer<string>
        {
            private readonly int hashCode;

            public FixedStringHasher(int hashCode)
            {
                this.hashCode = hashCode;
            }

            public bool Equals(string left, string right)
            {
                return StringComparer.Ordinal.Equals(left, right);
            }

            public int GetHashCode(string value)
            {
                return this.hashCode;
            }
        }

        private sealed class TestPool : IPool
        {
            private readonly Dictionary<PeerEndpoint, IGroupCacheClient> clients =
                new Dictionary<PeerEndpoint, IGroupCacheClient>();

            public TestPool()
            {
                this.Endpoint = new PeerEndpoint { HostName = "local.example", Port = 80 };
            }

            public PeerEndpoint Endpoint { get; }

            public bool IsLocal => true;

            public void Dispose()
            {
                foreach (IGroupCacheClient client in this.clients.Values)
                {
                    client.Dispose();
                }
            }

            public IGroupCacheClient GetClient(PeerEndpoint endpoint)
            {
                if (!this.clients.TryGetValue(endpoint, out IGroupCacheClient client))
                {
                    client = new TestClient(endpoint);
                    this.clients.Add(endpoint, client);
                }

                return client;
            }

            public Task GetAsync(
                string group,
                string key,
                Stream sink,
                ICacheControl cacheControl,
                CancellationToken ct)
            {
                throw new NotSupportedException();
            }

            public IPeerPicker GetPicker(string groupName, IEqualityComparer<string> keyHasher = null)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class TestClient : IGroupCacheClient
        {
            public TestClient(PeerEndpoint endpoint)
            {
                this.Endpoint = endpoint;
            }

            public PeerEndpoint Endpoint { get; }

            public bool IsLocal => false;

            public void Dispose()
            {
            }

            public Task GetAsync(
                string group,
                string key,
                Stream sink,
                ICacheControl cacheControl,
                CancellationToken ct)
            {
                throw new NotSupportedException();
            }
        }
    }
}
