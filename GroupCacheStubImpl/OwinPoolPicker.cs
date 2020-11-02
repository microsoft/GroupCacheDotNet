using GroupCache;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GroupCacheStub
{
    public class OwinPoolPicker : IPeerPicker
    {
        private object _lock = new Object();
        private OwinPool _locaHandler;
        private string _groupName;
        private JumpHasher _consistentHasher = new JumpHasher();
        private List<PeerEndpoint> _peerEndpoints;
        private Dictionary<PeerEndpoint, IGroupCacheClient> _clients;
        public IEqualityComparer<string> KeyHasher { get; set; } = EqualityComparer<string>.Default;

        public OwinPoolPicker(string groupName, OwinPool locaHandler) :
            this(groupName, locaHandler, new List<PeerEndpoint>())
        {
        }


        public OwinPoolPicker(string groupName, OwinPool localHandler, List<PeerEndpoint> peerEndpoints)
        {
            _groupName = groupName;
            _locaHandler = localHandler;
            Set(peerEndpoints);
        }

        /// <summary>
        /// Endpoint metadata of the local machine
        /// </summary>
        public PeerEndpoint Self
        {
            get
            {
                return _locaHandler.Endpoint;
            }
        }

        public List<IGroupCacheClient> PickPeers(string key, int numPeers)
        {
            lock (_lock)
            {
                numPeers = Math.Min(numPeers, _peerEndpoints.Count);
                List<IGroupCacheClient> replica = new List<IGroupCacheClient>(numPeers);
                List<PeerEndpoint> buckets = new List<PeerEndpoint>(_peerEndpoints);

                for (int i = 0; i < numPeers; i++)
                {
                    var keyHash = KeyHasher.GetHashCode(key);
                    var index = _consistentHasher.Hash((ulong)keyHash, buckets.Count);
                    replica.Add(_clients[buckets[index]]);
                    // Remove this node from buckets to guarantee its note added twice to replica list
                    buckets.RemoveAt(index);
                }
                return replica;
            }
        }

        public void Set(List<PeerEndpoint> peerEndpoints)
        {
            if (peerEndpoints == null)
            {
                return;
            }
            lock (_lock)
            {
                _peerEndpoints = new List<PeerEndpoint>(peerEndpoints);
                _peerEndpoints.Sort();
                var peerClients = new Dictionary<PeerEndpoint, IGroupCacheClient>();
                foreach (var peerEndpoint in _peerEndpoints)
                {
                    if (Self == peerEndpoint)
                    {
                        peerClients.Add(peerEndpoint, _locaHandler);
                    }
                    else
                    {
                        var client = _locaHandler.GetClient(peerEndpoint);
                        peerClients.Add(peerEndpoint, client);
                    }
                }
                _clients = peerClients;
            }
        }

        public void Add(List<PeerEndpoint> peerEndpoints)
        {
            lock (_lock)
            {
                HashSet<PeerEndpoint> peerSet = new HashSet<PeerEndpoint>(_peerEndpoints);
                peerSet.UnionWith(peerEndpoints);
                var newList = peerSet.ToList();
                newList.Sort();
                _peerEndpoints = newList;
                var peerClients = new Dictionary<PeerEndpoint, IGroupCacheClient>();
                foreach (var peerEndpoint in _peerEndpoints)
                {
                    bool isSelf = Self == peerEndpoint;
                    if (isSelf)
                    {
                        peerClients.Add(peerEndpoint, _locaHandler);
                    }
                    else
                    {
                        var client = _locaHandler.GetClient(peerEndpoint);
                        peerClients.Add(peerEndpoint, client);
                    }
                }
                _clients = peerClients;
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _peerEndpoints.Count;
                }
            }
        }
    }
}
