// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IPool.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System.Collections.Generic;

    /// <summary>
    /// Provides peer clients and peer pickers for a transport-backed cache pool.
    /// </summary>
    public interface IPool : IGroupCacheClient
    {
        /// <summary>
        /// Gets a client for the specified peer.
        /// </summary>
        IGroupCacheClient GetClient(PeerEndpoint endpoint);

        /// <summary>
        /// Gets the peer picker for a cache group.
        /// </summary>
        IPeerPicker GetPicker(string groupName, IEqualityComparer<string> keyHasher = null);
    }
}
