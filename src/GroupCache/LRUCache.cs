// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LRUCache.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    /// <summary>
    /// Implements a Generic fixed-size thread safe LRU (Least Recently Used) cache.
    /// The object maintains a doubly-linked list of elements.
    /// When an element is accessed, it is promoted to the head of the list.
    /// When space is needed, the element at the tail of the list (the least recently used element) is evicted.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the cache.</typeparam>
    /// <typeparam name="TValue">The type of the values in the cache.</typeparam>
    public class LRUCache<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, IDisposable
    {
        private readonly ReaderWriterLockSlim @lock;
        private readonly LinkedList<Entry<TKey, TValue>> lruList;
        private readonly Dictionary<TKey, LinkedListNode<Entry<TKey, TValue>>> map;
        private readonly int maxEntries;
        private ulong capacity;
        private TimeSpan ttl = TimeSpan.MaxValue;
        private ulong usage = 0;

        public ulong Capacity
        {
            get
            {
                using (this.@lock.GetReaderLock())
                {
                    return this.capacity;
                }
            }

            set
            {
                using (this.@lock.GetWriterLock())
                {
                    this.capacity = value;
                }
            }
        }

        public event Action<KeyValuePair<TKey, TValue>> ItemEvicted;

        public event Action<KeyValuePair<TKey, TValue>> ItemOverCapacity;

        /// <summary>
        /// Gets returns the number of items in the cache.
        /// </summary>
        public int Count
        {
            get
            {
                using (this.@lock.GetReaderLock())
                {
                    return this.lruList.Count;
                }
            }
        }

        public ulong TotalCharge
        {
            get
            {
                using (this.@lock.GetReaderLock())
                {
                    return this.usage;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LRUCache{TKey, TValue}"/> class.
        /// Creates an LRU of the given size.
        /// maxEntries is zero, the cache has no limit and it's assumed that eviction is done by the caller.
        /// </summary>
        ///
        public LRUCache(int maxEntries, IEqualityComparer<TKey> comparer, ulong capacity, TimeSpan ttl)
        {
            this.maxEntries = maxEntries;
            this.@lock = new ReaderWriterLockSlim();
            this.lruList = new LinkedList<Entry<TKey, TValue>>();
            this.map = new Dictionary<TKey, LinkedListNode<Entry<TKey, TValue>>>(comparer);
            this.capacity = capacity;
            this.ttl = ttl;
        }

        public LRUCache(int maxEntries, TimeSpan ttl)
            : this(maxEntries, EqualityComparer<TKey>.Default, 0, ttl)
        {
        }

        public LRUCache(int maxEntries, IEqualityComparer<TKey> comparer)
            : this(maxEntries, comparer, 0, TimeSpan.MaxValue)
        {
        }

        public LRUCache(int maxEntries = 0, ulong capacity = 0)
            : this(maxEntries, EqualityComparer<TKey>.Default, capacity, TimeSpan.MaxValue)
        {
        }

        /// <summary>
        /// Adds a an element to the cache.
        /// </summary>
        /// <param name="key">The object to use as the key of the element to add.</param>
        /// <param name="value">The object to use as the value of the element to add.</param>
        /// <exception cref="System.ArgumentNullException">key is null.</exception>
        /// <returns>Returns true if an eviction occured.</returns>
        public bool Add(TKey key, TValue value, ulong charge = 0)
        {
            return this.Add(key, ref value, true, charge);
        }

        /// <summary>
        /// Removes the element with the specified key from the cache.
        /// </summary>
        /// <param name="key">
        /// true if the element is successfully removed; otherwise, false.
        /// This method also returns false if key was not found.
        /// </param>
        /// <returns>true if the item existed.</returns>
        public bool Remove(TKey key)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }

            using (this.@lock.GetWriterLock())
            {
                LinkedListNode<Entry<TKey, TValue>> node;
                if (this.map.TryGetValue(key, out node))
                {
                    this.RemoveElement(node);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Check if a key is in the cache, without updating the recent-ness or deleting it for being stale.
        /// </summary>
        /// <returns></returns>
        public bool ContainsKey(TKey key)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }

            using (this.@lock.GetReaderLock())
            {
                return this.map.ContainsKey(key);
            }
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key whose value to get.</param>
        /// <param name="value">
        /// The value associated with the specified key, if the key is found;
        /// otherwise, the default value for the type of the value parameter.
        /// </param>
        /// <returns>true if the item was found.</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }

            using (this.@lock.GetWriterLock())
            {
                LinkedListNode<Entry<TKey, TValue>> node;
                if (this.map.TryGetValue(key, out node))
                {
                    if (DateTimeOffset.Now.Subtract(node.Value.CreationTime) <= this.ttl)
                    {
                        this.lruList.MoveToFront(node);
                        value = node.Value.Value;
                        return true;
                    }
                    else
                    {
                        this.RemoveElement(node);
                    }
                }

                value = default(TValue);
                return false;
            }
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key whose value to get.</param>
        /// <param name="valueFactory">
        /// If the key is not found, this is the value that will be added and returned.
        /// </param>
        /// <returns>the value associated to the key.</returns>
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }

            if (valueFactory == null)
            {
                throw new ArgumentNullException("valueFactory");
            }

            TValue outValue;
            if (!this.TryGetValue(key, out outValue))
            {
                outValue = valueFactory(key);
                this.Add(key, ref outValue, false, 0);
            }

            return outValue;
        }

        public void Clear()
        {
            using (this.@lock.GetWriterLock())
            {
                this.lruList.Clear();
                this.map.Clear();
            }
        }

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            using (this.@lock.GetReaderLock())
            {
                return this.lruList.Select(e => new KeyValuePair<TKey, TValue>(e.Key, e.Value)).ToList().GetEnumerator();
            }
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.@lock.Dispose();
        }



        // note: must hold _lock
        private void RemoveOldest()
        {
            var oldestNode = this.lruList.Last;
            if (oldestNode != null)
            {
                this.RemoveElement(oldestNode);
                this.ItemEvicted?.Invoke(new KeyValuePair<TKey, TValue>(oldestNode.Value.Key, oldestNode.Value.Value));
            }
        }

        // note: must hold _lock
        private void RemoveElement(LinkedListNode<Entry<TKey, TValue>> element)
        {
            // Remove using LinkedListNode is an O(1) operation.
            this.lruList.Remove(element);
            this.map.Remove(element.Value.Key);
            this.usage -= element.Value.Charge;
        }

        /// <summary>
        /// Add or update an entry to the LRUCache and put it in the front of the lru list.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="replace">whether to replace the value if already exist. </param>
        /// <param name="charge">charge for capacity.</param>
        /// <returns></returns>
        private bool Add(TKey key, ref TValue value, bool replace = true, ulong charge = 0)
        {
            bool evicted = false;
            bool itemLargerThanCapacity = false;
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }

            using (this.@lock.GetWriterLock())
            {
                LinkedListNode<Entry<TKey, TValue>> node;

                // Check for existing item
                if (this.map.TryGetValue(key, out node))
                {
                    this.lruList.MoveToFront(node);
                    if (replace)
                    {
                        node.Value.Value = value;
                    }
                    else
                    {
                        value = node.Value.Value;
                    }

                    return false;
                }

                if (this.capacity == 0 || charge <= this.capacity)
                {
                    // Add new item
                    var entry = new Entry<TKey, TValue> { Key = key, Value = value, Charge = charge, CreationTime = DateTimeOffset.Now };
                    this.lruList.AddFirst(entry);
                    this.map[key] = this.lruList.First;
                    this.usage += charge;
                }
                else
                {
                    itemLargerThanCapacity = true;
                }

                // Verify size not exceeded
                while ((this.maxEntries != 0 && this.lruList.Count > this.maxEntries) || (this.capacity != 0 && this.usage > this.capacity))
                {
                    evicted = true;
                    this.RemoveOldest();
                }
            }

            if (itemLargerThanCapacity)
            {
                this.ItemOverCapacity?.Invoke(new KeyValuePair<TKey, TValue>(key, value));
            }

            return evicted;
        }

        /// <summary>
        /// Defines a key/value pair that can be set or retrieved.
        /// </summary>
        /// <typeparam name="TEKey">The type of the key.</typeparam>
        /// <typeparam name="TEValue">The type of the value.</typeparam>
        private class Entry<TEKey, TEValue>
        {
            public ulong Charge
            {
                get;
                set;
            }

            public DateTimeOffset CreationTime
            {
                get;
                set;
            }

            public TEKey Key
            {
                get;
                set;
            }

            public TEValue Value
            {
                get;
                set;
            }
        }
    }
}
