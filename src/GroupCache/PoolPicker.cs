// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PoolPicker.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// Selects cache peers using highest-random-weight hashing.
    /// </summary>
    public class PoolPicker : IPeerPicker
    {
        private const ulong PeerHashMultiplier = 2862933555777941757UL;
        private const ulong ScoreMultiplier = 2685821657736338717UL;

        private readonly IPool localHandler;
        private PeerSnapshot snapshot = PeerSnapshot.Empty;

        public PoolPicker(string groupName, IPool localHandler)
            : this(groupName, localHandler, new List<PeerEndpoint>())
        {
        }

        public PoolPicker(string groupName, IPool localHandler, List<PeerEndpoint> peerEndpoints)
        {
            this.localHandler = localHandler;
            this.Set(peerEndpoints);
        }

        public IEqualityComparer<string> KeyHasher { get; set; } = StableOrdinalStringComparer.Instance;

        /// <inheritdoc/>
        public PeerEndpoint Self => this.localHandler.Endpoint;

        /// <inheritdoc/>
        public int Count => Volatile.Read(ref this.snapshot).PeerEndpoints.Length;

        /// <inheritdoc/>
        public List<IGroupCacheClient> PickPeers(string key, int numPeers)
        {
            if (numPeers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(numPeers));
            }

            PeerSnapshot currentSnapshot = Volatile.Read(ref this.snapshot);
            int peersToReturn = Math.Min(numPeers, currentSnapshot.PeerEndpoints.Length);
            var scoredClients = new List<ScoredClient>(currentSnapshot.PeerEndpoints.Length);
            ulong keyHash = unchecked((ulong)this.KeyHasher.GetHashCode(key));

            for (int i = 0; i < currentSnapshot.PeerEndpoints.Length; i++)
            {
                scoredClients.Add(new ScoredClient(currentSnapshot.Clients[i], ScorePeer(keyHash, i), i));
            }

            scoredClients.Sort(CompareScoredClients);

            var selectedClients = new List<IGroupCacheClient>(peersToReturn);
            for (int i = 0; i < peersToReturn; i++)
            {
                selectedClients.Add(scoredClients[i].Client);
            }

            return selectedClients;
        }

        /// <inheritdoc/>
        public void Set(List<PeerEndpoint> peerEndpoints)
        {
            PeerSnapshot newSnapshot = this.CreateSnapshot(peerEndpoints);
            Interlocked.Exchange(ref this.snapshot, newSnapshot);
        }

        /// <inheritdoc/>
        public void Add(List<PeerEndpoint> peerEndpoints)
        {
            if (peerEndpoints == null || peerEndpoints.Count == 0)
            {
                return;
            }

            while (true)
            {
                PeerSnapshot currentSnapshot = Volatile.Read(ref this.snapshot);
                var combinedEndpoints = new List<PeerEndpoint>(currentSnapshot.PeerEndpoints);
                combinedEndpoints.AddRange(peerEndpoints);
                PeerSnapshot newSnapshot = this.CreateSnapshot(combinedEndpoints);

                if (ReferenceEquals(
                    Interlocked.CompareExchange(ref this.snapshot, newSnapshot, currentSnapshot),
                    currentSnapshot))
                {
                    return;
                }
            }
        }

        private static int CompareEndpoints(PeerEndpoint left, PeerEndpoint right)
        {
            int hostComparison = StringComparer.Ordinal.Compare(left.HostName, right.HostName);
            return hostComparison != 0 ? hostComparison : left.Port.CompareTo(right.Port);
        }

        private static int CompareScoredClients(ScoredClient left, ScoredClient right)
        {
            int scoreComparison = right.Score.CompareTo(left.Score);
            return scoreComparison != 0 ? scoreComparison : left.SortedIndex.CompareTo(right.SortedIndex);
        }

        private static ulong ScorePeer(ulong keyHash, int sortedIndex)
        {
            unchecked
            {
                ulong combined = keyHash ^ ((ulong)sortedIndex * PeerHashMultiplier);
                combined ^= combined >> 12;
                combined ^= combined << 25;
                combined ^= combined >> 27;
                return combined * ScoreMultiplier;
            }
        }

        private PeerSnapshot CreateSnapshot(List<PeerEndpoint> peerEndpoints)
        {
            if (peerEndpoints == null || peerEndpoints.Count == 0)
            {
                return PeerSnapshot.Empty;
            }

            var uniqueEndpoints = new HashSet<PeerEndpoint>(peerEndpoints);
            var sortedEndpoints = new List<PeerEndpoint>(uniqueEndpoints);
            sortedEndpoints.Sort(CompareEndpoints);

            var clients = new IGroupCacheClient[sortedEndpoints.Count];
            for (int i = 0; i < sortedEndpoints.Count; i++)
            {
                PeerEndpoint endpoint = sortedEndpoints[i];
                clients[i] = endpoint == this.Self
                    ? this.localHandler
                    : this.localHandler.GetClient(endpoint);
            }

            return new PeerSnapshot(sortedEndpoints.ToArray(), clients);
        }

        private readonly struct ScoredClient
        {
            public ScoredClient(IGroupCacheClient client, ulong score, int sortedIndex)
            {
                this.Client = client;
                this.Score = score;
                this.SortedIndex = sortedIndex;
            }

            public IGroupCacheClient Client { get; }

            public ulong Score { get; }

            public int SortedIndex { get; }
        }

        private sealed class PeerSnapshot
        {
            public static readonly PeerSnapshot Empty =
                new PeerSnapshot(Array.Empty<PeerEndpoint>(), Array.Empty<IGroupCacheClient>());

            public PeerSnapshot(PeerEndpoint[] peerEndpoints, IGroupCacheClient[] clients)
            {
                this.PeerEndpoints = peerEndpoints;
                this.Clients = clients;
            }

            public PeerEndpoint[] PeerEndpoints { get; }

            public IGroupCacheClient[] Clients { get; }
        }

        private sealed class StableOrdinalStringComparer : IEqualityComparer<string>
        {
            private const uint FnvOffsetBasis = 2166136261U;
            private const uint FnvPrime = 16777619U;

            internal static readonly StableOrdinalStringComparer Instance = new StableOrdinalStringComparer();

            public bool Equals(string left, string right)
            {
                return StringComparer.Ordinal.Equals(left, right);
            }

            public int GetHashCode(string value)
            {
                if (value == null)
                {
                    return 0;
                }

                // string.GetHashCode is randomized per process; peer selection must be identical on every node.
                uint hash = FnvOffsetBasis;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= FnvPrime;
                }

                return unchecked((int)hash);
            }
        }
    }
}
