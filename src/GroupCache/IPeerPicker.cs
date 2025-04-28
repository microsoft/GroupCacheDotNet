// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IPeerPicker.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// PeerPicker is the interface that must be implemented to locate
    /// the peer that owns a specific key.
    /// </summary>
    public interface IPeerPicker
    {
        /// <summary>
        /// PickPeer returns the peer that owns the specific key.
        /// Property IsLocal on IGroupCacheClient will be false
        /// to indicate that a remote peer was nominated.
        /// </summary>
        /// <returns></returns>
        List<IGroupCacheClient> PickPeers(string key, int numPeers);

        /// <summary>
        /// Set the peer list this picker need to select from.
        /// </summary>
        /// <param name="peerEndpoints">List peer Endpoint struct.</param>
        void Set(List<PeerEndpoint> peerEndpoints);

        /// <summary>
        /// Set the peer list to the union of the existing list and the provided list.
        /// </summary>
        /// <param name="peerEndpoints">List of peer to add if not already present.</param>
        void Add(List<PeerEndpoint> peerEndpoints);

        /// <summary>
        /// Gets endpoint metadata of the local machine.
        /// </summary>
        PeerEndpoint Self { get; }

        /// <summary>
        /// Gets the total number of peer this PeerPicker select from.
        /// </summary>
        int Count { get; }
    }

    public struct PeerEndpoint : IEquatable<PeerEndpoint>, IComparable<PeerEndpoint>
    {
        private string hostName;

        public string HostName
        {
            get
            {
                return this.hostName;
            }

            set
            {
                this.hostName = value.ToLower();
            }
        }

        public int Port { get; set; }

        public override bool Equals(object obj)
        {
            return obj is PeerEndpoint && this.Equals((PeerEndpoint)obj);
        }

        public bool Equals(PeerEndpoint other)
        {
            return this.HostName.Equals(other.HostName, StringComparison.OrdinalIgnoreCase) &&
                   this.Port == other.Port;
        }

        public override int GetHashCode()
        {
            var hashCode = 1180852050;
            hashCode = (hashCode * -1521134295) + base.GetHashCode();
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.HostName);
            hashCode = (hashCode * -1521134295) + this.Port.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(PeerEndpoint endpoint1, PeerEndpoint endpoint2)
        {
            return endpoint1.Equals(endpoint2);
        }

        public static bool operator !=(PeerEndpoint endpoint1, PeerEndpoint endpoint2)
        {
            return !endpoint1.Equals(endpoint2);
        }

        public static bool operator <=(PeerEndpoint endpoint1, PeerEndpoint endpoint2)
        {
            return endpoint1.CompareTo(endpoint2) <= 0;
        }

        public static bool operator >=(PeerEndpoint endpoint1, PeerEndpoint endpoint2)
        {
            return endpoint1.CompareTo(endpoint2) >= 0;
        }

        public static bool operator <(PeerEndpoint endpoint1, PeerEndpoint endpoint2)
        {
            return endpoint1.CompareTo(endpoint2) < 0;
        }

        public static bool operator >(PeerEndpoint endpoint1, PeerEndpoint endpoint2)
        {
            return endpoint1.CompareTo(endpoint2) > 0;
        }

        public static bool TryCreate(string endPointStr, out PeerEndpoint endpoint)
        {
            bool canParse = Uri.TryCreate($"http://{endPointStr}", UriKind.Absolute, out Uri uri);
            if (!canParse)
            {
                endpoint = default(PeerEndpoint);
                return false;
            }

            endpoint = new PeerEndpoint { HostName = uri.Host, Port = uri.Port };
            return true;
        }

        public int CompareTo(PeerEndpoint other)
        {
            return this.HostName.CompareTo(other.HostName);
        }

        public override string ToString()
        {
            return $"{this.hostName}:{this.Port}";
        }
    }

    /// <summary>
    /// Key to uniquely identify a cache on local machine.
    /// this include the cache name and the endpoint metadata.
    /// </summary>
    public struct GroupKey : IEquatable<GroupKey>
    {
        public string GroupName;
        public PeerEndpoint Endpoint;

        public override bool Equals(object obj)
        {
            return obj is GroupKey && this.Equals((GroupKey)obj);
        }

        public bool Equals(GroupKey other)
        {
            return this.GroupName == other.GroupName &&
                   this.Endpoint.Equals(other.Endpoint);
        }

        public override int GetHashCode()
        {
            var hashCode = -1870784389;
            hashCode = (hashCode * -1521134295) + base.GetHashCode();
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.GroupName);
            hashCode = (hashCode * -1521134295) + EqualityComparer<PeerEndpoint>.Default.GetHashCode(this.Endpoint);
            return hashCode;
        }

        public static bool operator ==(GroupKey key1, GroupKey key2)
        {
            return key1.Equals(key2);
        }

        public static bool operator !=(GroupKey key1, GroupKey key2)
        {
            return !(key1 == key2);
        }
    }
}
