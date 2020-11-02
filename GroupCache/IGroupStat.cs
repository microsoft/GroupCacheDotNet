using System;

namespace GroupCache
{
    /// <summary>
    /// Interface used to observe GroupCache internal operations
    /// This would be used to collect counters for monitoring
    /// </summary>
    public interface IGroupStat
    {
        void TraceGets(string groupName); // any Get request, including from peers
        void TraceCacheHits(string groupName); // either cache was good
        void TracePeerLoads(string groupName); // either remote load or remote cache hit (not an error)
        void TraceLoadsDeduped(string groupName); // after singleflight
        void TraceLocalLoads(string groupName); // total good local loads
        void TraceServerRequests(string groupName); // gets that came over the network from peers
        void TraceRoundtripLatency(string groupName, TimeSpan ts);
        void TraceRetry(string groupName);
        void TraceItemOverCapacity(string groupName);
        void TraceConcurrentServerRequests(int v);
    }
}
