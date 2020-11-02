using GroupCache;
using System;
using System.Threading;

namespace GroupCacheStubTest
{
    /// <summary>
    /// For unit-testing only
    /// </summary>
    public class UnitTestGroupStat : IGroupStat
    {
        public int CacheHits = 0;
        public int Gets = 0;
        public int LoadsDeduped = 0;
        public int LocalLoads = 0;
        public int PeerLoads = 0;
        public int ServerRequests = 0;

        public void TraceCacheHits(string groupName)
        {
            Interlocked.Increment(ref CacheHits);
        }

        public void TraceGets(string groupName)
        {
            Interlocked.Increment(ref Gets);
        }

        public void TraceLoadsDeduped(string groupName)
        {
            Interlocked.Increment(ref LoadsDeduped);
        }

        public void TraceLocalLoads(string groupName)
        {
            Interlocked.Increment(ref LocalLoads);
        }

        public void TracePeerLoads(string groupName)
        {
            Interlocked.Increment(ref PeerLoads);
        }

        public void TraceServerRequests(string groupName)
        {
            Interlocked.Increment(ref ServerRequests);
        }

        public void TraceRoundtripLatency(string groupName, TimeSpan ts) { }

        public void TraceRetry(string groupName) { }

        public void TraceItemOverCapacity(string groupName) { }

        public void TraceConcurrentServerRequests(int v) { }
    }
}
