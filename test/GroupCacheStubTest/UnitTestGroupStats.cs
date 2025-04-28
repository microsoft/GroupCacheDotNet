// --------------------------------------------------------------------------------------------------------------------
// <copyright file="UnitTestGroupStats.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCacheStubTest
{
    using System;
    using System.Threading;
    using GroupCache;

    /// <summary>
    /// For unit-testing only.
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
            Interlocked.Increment(ref this.CacheHits);
        }

        public void TraceGets(string groupName)
        {
            Interlocked.Increment(ref this.Gets);
        }

        public void TraceLoadsDeduped(string groupName)
        {
            Interlocked.Increment(ref this.LoadsDeduped);
        }

        public void TraceLocalLoads(string groupName)
        {
            Interlocked.Increment(ref this.LocalLoads);
        }

        public void TracePeerLoads(string groupName)
        {
            Interlocked.Increment(ref this.PeerLoads);
        }

        public void TraceServerRequests(string groupName)
        {
            Interlocked.Increment(ref this.ServerRequests);
        }

        public void TraceRoundtripLatency(string groupName, TimeSpan ts)
        {
        }

        public void TraceRetry(string groupName)
        {
        }

        public void TraceItemOverCapacity(string groupName)
        {
        }

        public void TraceConcurrentServerRequests(int v)
        {
        }
    }
}
