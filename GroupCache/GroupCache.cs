using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GroupCache
{
    /// <summary>
    /// Groupcache provides a data loading mechanism with caching and de-duplication
    /// that works across a set of peer processes.
    ///
    /// Each data Get first consults its local cache, otherwise delegates to the requested key's canonical owner,
    /// which then checks its cache or finally gets the data.
    ///
    /// In the common case, many concurrent cache misses across
    /// a set of peers for the same key result in just one cache fill.
    ///
    /// In a nutshell, a groupcache lookup of Get("foo") looks like:
    /// (On machine #5 of a set of N machines running the same code)
    ///
    ///   1- Is the value of "foo" in local memory because peer #5 (the current peer) is the owner of it? If so, use it.
    ///
    ///   2- Amongst the peers in my set of N, am I the owner of the key "foo"? (e.g.does it  hash to 5?) If so, load it.
    ///      If other callers come in, via the same process or via RPC requests from peers, they block waiting for the load to finish and get the same answer.
    ///      If not, RPC to the peer that's the owner and get to the answer.
    ///      If the RPC fails, just try again next canonical owner for that key (still with local dup suppression).
    /// </summary>
    public static class GroupCache
    {
        private static ConcurrentDictionary<GroupKey, Group> GROUPS = new ConcurrentDictionary<GroupKey, Group>();

        /// <summary>
        /// returns the named group previously created with NewGroup,
        /// Used by server callback to forward received request to the correct Group instance.
        /// </summary>
        public static bool GetGroup(GroupKey key, out Group group)
        {
            return GROUPS.TryGetValue(key, out group);
        }

        public static Group NewGroup(string groupName, Func<string, Stream, ICacheControl, CancellationToken, Task> getterFunc, IPeerPicker peers)
        {
            Argument.NotNull(getterFunc, "getterFunc");
            var getter = new GetterFunc(getterFunc);
            return NewGroup(groupName, getter, peers);
        }

        public static Group NewGroup(string name, IGetter getter, ICache cache, IPeerPicker peers)
        {
            Argument.NotNull(name, "name");
            Argument.NotNull(getter, "getter");
            Argument.NotNull(cache, "cache");
            Argument.NotNull(peers, "peers");
            var groupKey = new GroupKey { GroupName = name, Endpoint = peers.Self };
            var group = new Group(name, getter, peers, cache);
            return GROUPS.GetOrAdd(groupKey, group);
        }

        public static Group NewGroup(string name, IGetter getter, IPeerPicker peers)
        {
            Argument.NotNull(name, "name");
            Argument.NotNull(getter, "getter");
            Argument.NotNull(peers, "peers");
            var groupKey = new GroupKey { GroupName = name, Endpoint = peers.Self };
            var group = new Group(name, getter, peers);
            return GROUPS.GetOrAdd(groupKey, group);
        }
    }

    /// <summary>
    /// A Group is a cache namespace and associated data loaded spread over a group of 1 or more machines.
    /// </summary>
    public sealed class Group : IGetter
    {
        private string _groupName;
        private IGetter _getter;
        private IPeerPicker _peers;
        private ICache _localCache;
        private IGroupStat _stats;
        private ILogger _logger;

        public IGroupStat Stats
        {
            get { return _stats; }
            set
            {
                Argument.NotNull(value, "Stats");
                _stats = value;
            }
        }

        public ILogger Logger
        {
            get { return _logger; }

            set
            {
                Argument.NotNull(value, "Logger");
                _logger = value;
            }
        }

        public int MaxRetry { get; set; } = 5;

        public ICacheEntryValidator CacheEntryValidator { get; set; }

        internal Group(string groupName, IGetter getter, IPeerPicker peers)
            : this(groupName, getter, peers, new MemoryCache())
        {
        }

        internal Group(string groupName, IGetter getter, IPeerPicker peers, ICache localCache)
        {
            this._groupName = groupName;
            this._getter = getter;
            this._peers = peers;
            this._localCache = localCache;
            this.Stats = new NullGroupStat();
            this.Logger = new NullLogger();
            this._localCache.ItemOverCapacity += ItemWasOverCapacity;
            this.CacheEntryValidator = new NullCacheEntryValidator();
        }

        private void ItemWasOverCapacity(string obj)
        {
            this.Stats.TraceItemOverCapacity(_groupName);
        }

        /// <summary>
        /// Read the data from Peer or local machine and dedup in flight call
        /// </summary>
        public async Task GetAsync(string key, Stream sink, ICacheControl cacheControl, CancellationToken ct)
        {
            if (cacheControl == null)
            {
                cacheControl = new CacheControl();
            }
            Stats.TraceGets(_groupName);
            var watch = Stopwatch.StartNew();
            await LoadFromGetterOrPeerAsync(key, sink, cacheControl, ct).ConfigureAwait(false);
            Stats.TraceRoundtripLatency(_groupName, watch.Elapsed);
        }

        /// <summary>
        /// Read the data from localMachine or from IGetter and dedup in flight call
        /// </summary>
        public async Task GetAsyncLocallyAsync(string key, Stream sink, ICacheControl cacheControl, CancellationToken ct)
        {
            Stats.TraceGets(_groupName);
            // Dipose validatingSink  but leave sink open, to release validatingSink ressources
            using (var validatingSink = CacheEntryValidator.ValidateEntryPassThrough(key, sink))
            {
                try
                {
                    await LoadLocallyAsync(key, validatingSink, cacheControl, ct).ConfigureAwait(false);
                    await validatingSink.ValidateAsync(ct).ConfigureAwait(false);
                }
                catch (CacheEntryValidationFailedException)
                {
                    await RemoveAsync(key, ct).ConfigureAwait(false);
                    throw;
                }
            }
        }

        public Task RemoveAsync(string key, CancellationToken ct)
        {
            return _localCache.RemoveAsync(key, ct);
        }

        private async Task LoadFromGetterOrPeerAsync(string key, Stream sink, ICacheControl cacheControl, CancellationToken ct)
        {
            Stats.TraceLoadsDeduped(_groupName);
            var replicasForKey = _peers.PickPeers(key, _peers.Count);
            var retryCount = Math.Min(MaxRetry, replicasForKey.Count);
            var retryableException = new Type[] { typeof(InternalServerErrorException), typeof(ServerBusyException), typeof(GroupNotFoundException), typeof(ConnectFailureException) };
            var retry = new Retry(new SimpleRetryPolicy(retryCount, TimeSpan.Zero, retryableException));
            try
            {
                await retry.ExecuteAsync(async (RetryContext ctx) =>
                {
                    if (ctx.RetryCount > 0)
                    {
                        Stats.TraceRetry(_groupName);
                    }
                    var peerClient = replicasForKey[ctx.RetryCount];
                    if (peerClient.IsLocal) // we need to read from peer
                    {
                        Logger.Debug(String.Format("Call to LoadLocally for Cache {0} key: {1} retryCount: {2}", _groupName, key, ctx.RetryCount));
                        await LoadLocallyAsync(key, sink, cacheControl, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        Logger.Debug(String.Format("Call to LoadFromPeer for Cache {0} key: {1} retryCount: {2}", _groupName, key, ctx.RetryCount));
                        await LoadFromPeerAsync(key, sink, cacheControl, peerClient, ct).ConfigureAwait(false);
                    }
                }
                ).ConfigureAwait(false);
            }
            catch
            {
                Logger.Info($"{_groupName}:{key} failed to download from peer. trying to download locally from source directly.");
                await LoadLocallyAsync(key, sink, cacheControl, ct).ConfigureAwait(false);
            }
        }

        private async Task LoadLocallyAsync(string key, Stream sink, ICacheControl cacheControl, CancellationToken ct)
        {
            async Task func(string cachekey, Stream factorySink, ICacheControl factoryCacheControl)
            {
                try
                {
                    Stats.TraceLocalLoads(_groupName);
                    await _getter.GetAsync(cachekey, factorySink, factoryCacheControl, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, String.Format("Call to LoadLocally for Cache {0} key: {1} failed ", _groupName, key));
                    throw;
                }
            }
            Stats.TraceCacheHits(_groupName);

            var cacheEntry = await _localCache.GetOrAddAsync(key, func, cacheControl, ct).ConfigureAwait(false);
            try
            {
                Stream cacheEntryStream = cacheEntry.Value();
                await cacheEntryStream.CopyToAsync(sink).ConfigureAwait(false);
            }
            finally
            {
                await cacheEntry.DisposeAsync().ConfigureAwait(false);
            }
        }

        private async Task LoadFromPeerAsync(string key, Stream sink, ICacheControl cacheControl, IGroupCacheClient peerClient, CancellationToken ct)
        {
            try
            {
                Stats.TracePeerLoads(_groupName);
                using (var validatingSink = CacheEntryValidator.ValidateEntryPassThrough(key, sink))
                {
                    await peerClient.GetAsync(_groupName, key, validatingSink, cacheControl, ct).ConfigureAwait(false);
                    await validatingSink.ValidateAsync(ct).ConfigureAwait(false);
                }
            }
            catch (CircuitBreakerOpenException)
            {
                throw; // Dont log CircuitBreakerOpenException
            }
            catch (InternalServerErrorException internalErrorEx)
            {
                Logger.Error(String.Format("Call to LoadFromPeer to {0} for Cache {1} key: {2} failed on InternalServerErrorException {3}", peerClient.Endpoint.ToString(), _groupName, key, internalErrorEx.Message));
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, String.Format("Call to LoadFromPeer to {0} for Cache {1} key: {2} failed ", peerClient.Endpoint.ToString(), _groupName, key));
                throw;
            }
        }
    }
}
