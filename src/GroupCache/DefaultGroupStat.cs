// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DefaultGroupStat.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;

    /// <summary>
    /// For unit-testing only.
    /// </summary>
    public class NullGroupStat : IGroupStat
    {
        /// <inheritdoc/>
        public void TraceCacheHits(string groupName)
        {
        }

        /// <inheritdoc/>
        public void TraceGets(string groupName)
        {
        }

        /// <inheritdoc/>
        public void TraceLoadsDeduped(string groupName)
        {
        }

        /// <inheritdoc/>
        public void TraceLocalLoads(string groupName)
        {
        }

        /// <inheritdoc/>
        public void TracePeerLoads(string groupName)
        {
        }

        /// <inheritdoc/>
        public void TraceServerRequests(string groupName)
        {
        }

        /// <inheritdoc/>
        public void TraceRoundtripLatency(string groupName, TimeSpan ts)
        {
        }

        /// <inheritdoc/>
        public void TraceRetry(string groupName)
        {
        }

        /// <inheritdoc/>
        public void TraceItemOverCapacity(string groupName)
        {
        }

        /// <inheritdoc/>
        public void TraceConcurrentServerRequests(int v)
        {
        }
    }
}
