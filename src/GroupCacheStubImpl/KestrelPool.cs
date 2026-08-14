// --------------------------------------------------------------------------------------------------------------------
// <copyright file="KestrelPool.cs" company="Microsoft Corporation">
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
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Hosts the local cache endpoint and provides HTTP clients for remote peers.
    /// </summary>
    public sealed class KestrelPool : IPool
    {
        private readonly ConcurrentDictionary<PeerEndpoint, Lazy<IGroupCacheClient>> cacheClients =
            new ConcurrentDictionary<PeerEndpoint, Lazy<IGroupCacheClient>>();

        private readonly SemaphoreSlim concurrencyLimiter;
        private readonly int concurrencyLimit;
        private readonly ILogger<KestrelPool> logger;
        private readonly ConcurrentDictionary<string, Lazy<IPeerPicker>> peerPickers =
            new ConcurrentDictionary<string, Lazy<IPeerPicker>>(StringComparer.InvariantCultureIgnoreCase);

        private readonly KestrelServer server;
        private int disposed;
        private PeerEndpoint self;

        public KestrelPool(PeerEndpoint self, int concurrencyLimit = 24, ILoggerFactory loggerFactory = null)
        {
            if (concurrencyLimit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(concurrencyLimit));
            }

            this.self = self;
            this.concurrencyLimit = concurrencyLimit;
            this.concurrencyLimiter = new SemaphoreSlim(concurrencyLimit, concurrencyLimit);
            this.logger = loggerFactory?.CreateLogger<KestrelPool>();
            this.server = new KestrelServer(this, self.Port, self.HostName);
            this.server.Start();
            this.self.Port = this.server.Endpoint.Port;
            this.logger?.LogInformation("GroupCache Kestrel endpoint listening on {Endpoint}.", this.self);
        }

        /// <inheritdoc/>
        public PeerEndpoint Endpoint => this.self;

        /// <inheritdoc/>
        public bool IsLocal => true;

        /// <inheritdoc/>
        public async Task GetAsync(
            string groupName,
            string key,
            Stream sink,
            ICacheControl cacheControl,
            CancellationToken ct)
        {
            bool entered = await this.concurrencyLimiter.WaitAsync(TimeSpan.Zero, ct).ConfigureAwait(false);
            if (!entered)
            {
                throw new ServerBusyException("Too many concurrent requests.");
            }

            try
            {
                var groupKey = new GroupKey { GroupName = groupName, Endpoint = this.Endpoint };
                bool found = GroupCache.GetGroup(groupKey, out Group group);
                if (!found)
                {
                    throw new GroupNotFoundException($"No such group: {groupName}");
                }

                group.Stats.TraceConcurrentServerRequests(this.concurrencyLimit - this.concurrencyLimiter.CurrentCount);
                group.Stats.TraceServerRequests(groupName);

                // Always resolve peer requests locally to avoid forwarding loops between pools with different peer lists.
                await group.GetAsyncLocallyAsync(key, sink, cacheControl, ct).ConfigureAwait(false);
            }
            finally
            {
                this.concurrencyLimiter.Release();
            }
        }

        /// <inheritdoc/>
        public IGroupCacheClient GetClient(PeerEndpoint endpoint)
        {
            var lazyClient = new Lazy<IGroupCacheClient>(
                () => new CircuitBreakerClient(new GroupCacheHttpClient(endpoint)),
                LazyThreadSafetyMode.ExecutionAndPublication);

            return this.cacheClients.GetOrAdd(endpoint, lazyClient).Value;
        }

        /// <inheritdoc/>
        public IPeerPicker GetPicker(string groupName, IEqualityComparer<string> keyHasher = null)
        {
            var lazyPicker = new Lazy<IPeerPicker>(
                () =>
                {
                    var picker = new PoolPicker(groupName, this);
                    if (keyHasher != null)
                    {
                        picker.KeyHasher = keyHasher;
                    }

                    return picker;
                },
                LazyThreadSafetyMode.ExecutionAndPublication);

            return this.peerPickers.GetOrAdd(groupName, lazyPicker).Value;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }

            this.server.Dispose();
            foreach (Lazy<IGroupCacheClient> client in this.cacheClients.Values)
            {
                if (client.IsValueCreated)
                {
                    client.Value.Dispose();
                }
            }

            this.concurrencyLimiter.Dispose();
            this.logger?.LogInformation("GroupCache Kestrel endpoint {Endpoint} stopped.", this.self);
        }
    }
}
