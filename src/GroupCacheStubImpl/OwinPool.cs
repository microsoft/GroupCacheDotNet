// --------------------------------------------------------------------------------------------------------------------
// <copyright file="OwinPool.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCacheStub
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using GroupCache;

    /// <summary>
    /// OwinPool implements IPeerPicker for a pool of HTTP peers.
    /// </summary>
    public sealed class OwinPool : IGroupCacheClient, IDisposable
    {
        private readonly object @lock = new object();
        private readonly OwinServer server;
        private PeerEndpoint self;
        private readonly ConcurrentDictionary<string, Lazy<IPeerPicker>> peerPickers = new ConcurrentDictionary<string, Lazy<IPeerPicker>>(StringComparer.InvariantCultureIgnoreCase);
        private readonly ConcurrentDictionary<PeerEndpoint, Lazy<IGroupCacheClient>> cacheClients = new ConcurrentDictionary<PeerEndpoint, Lazy<IGroupCacheClient>>();
        private readonly SemaphoreSlim concurrencyLimiter = null;
        private readonly int concurrencyLimit;

        /// <summary>
        /// Initializes a new instance of the <see cref="OwinPool"/> class.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="concurrencyLimit"></param>
        public OwinPool(PeerEndpoint self, int concurrencyLimit = 24)
        {
            this.self = self;
            this.concurrencyLimit = concurrencyLimit;
            this.concurrencyLimiter = new SemaphoreSlim(initialCount: this.concurrencyLimit);
            this.server = new OwinServer(this, self.Port, self.HostName);
            this.server.Start();
        }

        /// <inheritdoc/>
        public PeerEndpoint Endpoint
        {
            get { return this.self; }
        }

        /// <inheritdoc/>
        public bool IsLocal
        {
            get { return true; }
        }

        /// <summary>
        ///  Handle request from Peers and forward to the correct local Group instance.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task GetAsync(string groupName, string key, Stream sink, ICacheControl cacheControl, CancellationToken ct)
        {
            SemaphoreHolder limiter;
            try
            {
                limiter = await this.concurrencyLimiter.AcquireAsync(TimeSpan.Zero).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new ServerBusyException("Too many concurrent connection");
            }

            using (limiter)
            {
                var groupKey = new GroupKey { GroupName = groupName, Endpoint = this.Endpoint };
                var found = GroupCache.GetGroup(groupKey, out Group group);
                if (!found)
                {
                    throw new GroupNotFoundException($"no such group: {groupName}");
                }

                group.Stats.TraceConcurrentServerRequests(this.concurrencyLimit - this.concurrencyLimiter.CurrentCount);
                group.Stats.TraceServerRequests(groupName);

                // We received a request from a peer, we need to download it locally and not forward to peer
                // because forwarding to peer would cause infinite loop if using different peer list
                await group.GetAsyncLocallyAsync(key, sink, cacheControl, ct);
            }
        }

        public IGroupCacheClient GetClient(PeerEndpoint endpoint)
        {
            var lazy = new Lazy<IGroupCacheClient>(
                () =>
                {
                    return new CircuitBreakerClient(new OwinClient(endpoint));
                },
                LazyThreadSafetyMode.ExecutionAndPublication);
            return this.cacheClients.GetOrAdd(endpoint, lazy).Value;
        }

        public IPeerPicker GetPicker(string groupName, IEqualityComparer<string> keyHasher = null)
        {
            var lazy = new Lazy<IPeerPicker>(
                () =>
                {
                    var hasher = keyHasher;
                    if (hasher == null)
                    {
                        hasher = EqualityComparer<string>.Default;
                    }

                    var pool = new OwinPoolPicker(groupName, this);
                    pool.KeyHasher = hasher;
                    return pool;
                },
                LazyThreadSafetyMode.ExecutionAndPublication);
            return this.peerPickers.GetOrAdd(groupName, lazy).Value;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.server.Dispose();
        }
    }
}
