using System;

namespace GroupCache
{
    /// <summary>
    /// For unit-testing only
    /// </summary>
    public class NullGroupStat : IGroupStat
    {
        public void TraceCacheHits(string groupName) { }

        public void TraceGets(string groupName) { }

        public void TraceLoadsDeduped(string groupName) { }

        public void TraceLocalLoads(string groupName) { }

        public void TracePeerLoads(string groupName) { }

        public void TraceServerRequests(string groupName) { }

        public void TraceRoundtripLatency(string groupName, TimeSpan ts) { }

        public void TraceRetry(string groupName) { }

        public void TraceItemOverCapacity(string groupName) { }

        public void TraceConcurrentServerRequests(int v) { }
    }
}
