using DSInfra.Threading;
using GroupCache;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GroupCacheStub
{
    /// <summary>
    /// OwinPool implements IPeerPicker for a pool of HTTP peers.
    /// </summary>
    public sealed class OwinPool : IGroupCacheClient, IDisposable
    {
        private object _lock = new Object();
        private OwinServer _server;
        private PeerEndpoint _self;
        private ConcurrentDictionary<string, Lazy<IPeerPicker>> _peerPickers = new ConcurrentDictionary<string, Lazy<IPeerPicker>>(StringComparer.InvariantCultureIgnoreCase);
        private ConcurrentDictionary<PeerEndpoint, Lazy<IGroupCacheClient>> _cacheClients = new ConcurrentDictionary<PeerEndpoint, Lazy<IGroupCacheClient>>();
        private SemaphoreSlim _concurrencyLimiter = null;
        private int _concurrencyLimit;

        public OwinPool(PeerEndpoint self, int concurrencyLimit = 24)
        {
            _self = self;
            _concurrencyLimit = concurrencyLimit;
            _concurrencyLimiter = new SemaphoreSlim(initialCount: _concurrencyLimit);
            _server = new OwinServer(this, self.Port, self.HostName);
            _server.Start();
        }

        public PeerEndpoint Endpoint { get { return _self; } }

        public bool IsLocal { get { return true; } }

        /// <summary>
        ///  Handle request from Peers and forward to the correct local Group instance
        /// </summary>
        public async Task GetAsync(string groupName, string key, Stream sink, ICacheControl cacheControl, CancellationToken ct)
        {
            SemaphoreHolder limiter;
            try
            {
                limiter = await _concurrencyLimiter.AcquireAsync(TimeSpan.Zero).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new ServerBusyException("Too many concurrent connection");
            }

            using (limiter)
            {
                var groupKey = new GroupKey { GroupName = groupName, Endpoint = this.Endpoint };
                var found = GroupCache.GroupCache.GetGroup(groupKey, out Group group);
                if (!found)
                {
                    throw new GroupNotFoundException($"no such group: {groupName}");
                }
                group.Stats.TraceConcurrentServerRequests(_concurrencyLimit - _concurrencyLimiter.CurrentCount);
                group.Stats.TraceServerRequests(groupName);

                // We received a request from a peer, we need to download it locally and not forward to peer
                // because forwarding to peer would cause infinite loop if using different peer list
                await group.GetAsyncLocallyAsync(key, sink, cacheControl, ct);
            } 
        }

        public IGroupCacheClient GetClient(PeerEndpoint endpoint)
        {
            var lazy = new Lazy<IGroupCacheClient>(
                () => {
                    return new CircuitBreakerClient(new OwinClient(endpoint));
                }
                , LazyThreadSafetyMode.ExecutionAndPublication);
            return _cacheClients.GetOrAdd(endpoint, lazy).Value;
        }

        public IPeerPicker GetPicker(string groupName, IEqualityComparer<string> keyHasher = null)
        {
            var lazy = new Lazy<IPeerPicker>(
                () => {
                        var hasher = keyHasher;
                        if (hasher == null)
                        {
                            hasher = EqualityComparer<string>.Default;
                        }
                        var pool = new OwinPoolPicker(groupName, this);
                        pool.KeyHasher = hasher;
                        return pool;
                    }
                , LazyThreadSafetyMode.ExecutionAndPublication);
            return _peerPickers.GetOrAdd(groupName, lazy).Value;
        }

        public void Dispose()
        {
            _server.Dispose();
        }
    }
}
