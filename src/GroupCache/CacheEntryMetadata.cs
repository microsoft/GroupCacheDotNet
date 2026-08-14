// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CacheEntryMetadata.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Immutable snapshot of the <see cref="ICacheControl"/> state produced by a cache fill.
    ///
    /// A cached entry is filled once but served many times, so the directives and headers the
    /// getter produced are captured here and replayed onto the caller supplied
    /// <see cref="ICacheControl"/> on every subsequent hit.
    /// </summary>
    internal sealed class CacheEntryMetadata
    {
        internal static readonly CacheEntryMetadata Empty =
            new CacheEntryMetadata(
                noStore: false,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        internal CacheEntryMetadata(bool noStore, IReadOnlyDictionary<string, string> customHeaders)
        {
            this.NoStore = noStore;
            this.CustomHeaders = customHeaders;
        }

        internal bool NoStore { get; }

        internal IReadOnlyDictionary<string, string> CustomHeaders { get; }

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
}
