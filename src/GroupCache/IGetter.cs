// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IGetter.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ICacheControl
    {
        // Whether cache must not store any response.
        bool NoStore { get; set; }

        /// <summary>
        /// Gets response metadata that should be propagated between peers.
        /// </summary>
        IDictionary<string, string> CustomHeaders => CacheControlHeaderStore.Get(this);
    }

    public class CacheControl : ICacheControl
    {
        // Whether cache must not store any response.
        /// <inheritdoc/>
        public bool NoStore { get; set; } = false;

        /// <inheritdoc/>
        public IDictionary<string, string> CustomHeaders { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A Getter loads data for identified by key.
    ///
    /// The returned data must be unversioned.
    /// That is, key must uniquely describe the loaded data
    /// without an implicit current time, and without relying on cache expiration mechanisms.
    /// </summary>
    public interface IGetter
    {
        Task GetAsync(string key, Stream sink, ICacheControl cacheControl, CancellationToken ct);
    }

    public sealed class GetterFunc : IGetter
    {
        private readonly Func<string, Stream, ICacheControl, CancellationToken, Task> func;

        public GetterFunc(Func<string, Stream, ICacheControl, CancellationToken, Task> func)
        {
            this.func = func;
        }

        /// <inheritdoc/>
        public Task GetAsync(string key, Stream sink, ICacheControl cacheControl, CancellationToken ct)
        {
            return this.func(key, sink, cacheControl, ct);
        }
    }

    internal sealed class CacheEntryMetadata
    {
        internal static readonly CacheEntryMetadata Empty =
            new CacheEntryMetadata(
                noStore: false,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        internal readonly IReadOnlyDictionary<string, string> CustomHeaders;
        internal readonly bool NoStore;

        internal CacheEntryMetadata(bool noStore, IReadOnlyDictionary<string, string> customHeaders)
        {
            this.NoStore = noStore;
            this.CustomHeaders = customHeaders;
        }

        internal static CacheEntryMetadata Capture(ICacheControl cacheControl)
        {
            return new CacheEntryMetadata(
                cacheControl.NoStore,
                new Dictionary<string, string>(cacheControl.CustomHeaders, StringComparer.OrdinalIgnoreCase));
        }

        internal void Apply(ICacheControl cacheControl)
        {
            cacheControl.NoStore = this.NoStore;
            cacheControl.CustomHeaders.Clear();
            foreach (KeyValuePair<string, string> header in this.CustomHeaders)
            {
                cacheControl.CustomHeaders.Add(header.Key, header.Value);
            }
        }
    }

    internal static class CacheControlHeaderStore
    {
        internal static readonly ConditionalWeakTable<ICacheControl, HeaderCollection> Headers =
            new ConditionalWeakTable<ICacheControl, HeaderCollection>();

        internal static IDictionary<string, string> Get(ICacheControl cacheControl)
        {
            return Headers.GetOrCreateValue(cacheControl).Values;
        }

        internal sealed class HeaderCollection
        {
            public IDictionary<string, string> Values { get; } =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
