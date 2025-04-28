// --------------------------------------------------------------------------------------------------------------------
// <copyright file="OwinPoolPicker.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCacheStub
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GroupCache;

    public class OwinPoolPicker : IPeerPicker
    {
        private readonly object @lock = new object();
        private readonly OwinPool locaHandler;
        private readonly string groupName;
        private readonly JumpHasher consistentHasher = new JumpHasher();
        private List<PeerEndpoint> peerEndpoints;
        private Dictionary<PeerEndpoint, IGroupCacheClient> clients;

        public IEqualityComparer<string> KeyHasher { get; set; } = EqualityComparer<string>.Default;

        public OwinPoolPicker(string groupName, OwinPool locaHandler)
            : this(groupName, locaHandler, new List<PeerEndpoint>())
        {
        }

        public OwinPoolPicker(string groupName, OwinPool localHandler, List<PeerEndpoint> peerEndpoints)
        {
            this.groupName = groupName;
            this.locaHandler = localHandler;
            this.Set(peerEndpoints);
        }

        /// <summary>
        /// Gets endpoint metadata of the local machine.
        /// </summary>
        public PeerEndpoint Self
        {
            get
            {
                return this.locaHandler.Endpoint;
            }
        }

        /// <inheritdoc/>
        public List<IGroupCacheClient> PickPeers(string key, int numPeers)
        {
            lock (this.@lock)
            {
                numPeers = Math.Min(numPeers, this.peerEndpoints.Count);
                List<IGroupCacheClient> replica = new List<IGroupCacheClient>(numPeers);
                List<PeerEndpoint> buckets = new List<PeerEndpoint>(this.peerEndpoints);

                for (int i = 0; i < numPeers; i++)
                {
                    var keyHash = this.KeyHasher.GetHashCode(key);
                    var index = this.consistentHasher.Hash((ulong)keyHash, buckets.Count);
                    replica.Add(this.clients[buckets[index]]);

                    // Remove this node from buckets to guarantee its note added twice to replica list
                    buckets.RemoveAt(index);
                }

                return replica;
            }
        }

        /// <inheritdoc/>
        public void Set(List<PeerEndpoint> peerEndpoints)
        {
            if (peerEndpoints == null)
            {
                return;
            }

            lock (this.@lock)
            {
                this.peerEndpoints = new List<PeerEndpoint>(peerEndpoints);
                this.peerEndpoints.Sort();
                var peerClients = new Dictionary<PeerEndpoint, IGroupCacheClient>();
                foreach (var peerEndpoint in this.peerEndpoints)
                {
                    if (this.Self == peerEndpoint)
                    {
                        peerClients.Add(peerEndpoint, this.locaHandler);
                    }
                    else
                    {
                        var client = this.locaHandler.GetClient(peerEndpoint);
                        peerClients.Add(peerEndpoint, client);
                    }
                }

                this.clients = peerClients;
            }
        }

        /// <inheritdoc/>
        public void Add(List<PeerEndpoint> peerEndpoints)
        {
            lock (this.@lock)
            {
                HashSet<PeerEndpoint> peerSet = new HashSet<PeerEndpoint>(this.peerEndpoints);
                peerSet.UnionWith(peerEndpoints);
                var newList = peerSet.ToList();
                newList.Sort();
                this.peerEndpoints = newList;
                var peerClients = new Dictionary<PeerEndpoint, IGroupCacheClient>();
                foreach (var peerEndpoint in this.peerEndpoints)
                {
                    bool isSelf = this.Self == peerEndpoint;
                    if (isSelf)
                    {
                        peerClients.Add(peerEndpoint, this.locaHandler);
                    }
                    else
                    {
                        var client = this.locaHandler.GetClient(peerEndpoint);
                        peerClients.Add(peerEndpoint, client);
                    }
                }

                this.clients = peerClients;
            }
        }

        /// <inheritdoc/>
        public int Count
        {
            get
            {
                lock (this.@lock)
                {
                    return this.peerEndpoints.Count;
                }
            }
        }
    }
}
