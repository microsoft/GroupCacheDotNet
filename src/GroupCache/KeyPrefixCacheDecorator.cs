// --------------------------------------------------------------------------------------------------------------------
// <copyright file="KeyPrefixCacheDecorator.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public class KeyPrefixCacheDecorator : ICache
    {
        private readonly ICache decorated;
        private readonly string prefix;

        public KeyPrefixCacheDecorator(string prefix, ICache cache)
        {
            this.prefix = prefix;
            this.decorated = cache;
            this.decorated.ItemOverCapacity += (key) => this.ItemOverCapacity?.Invoke(key);
        }

        /// <inheritdoc/>
        public event Action<string> ItemOverCapacity;

        /// <inheritdoc/>
        public Task<ICacheEntry> GetOrAddAsync(string key, Func<string, Stream, ICacheControl, Task> valueFactory, ICacheControl cacheControl, CancellationToken ct)
        {
            var newkey = this.prefix + key;
            return this.decorated.GetOrAddAsync(newkey, (str, stream, cc) => valueFactory(key, stream, cc), cacheControl, ct);
        }

        /// <inheritdoc/>
        public Task RemoveAsync(string key, CancellationToken ct)
        {
            var newkey = this.prefix + key;
            return this.decorated.RemoveAsync(newkey, ct);
        }
    }
}
