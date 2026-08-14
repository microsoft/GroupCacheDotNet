// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CacheControlHeaderStore.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Backing storage for <see cref="ICacheControl.CustomHeaders"/>.
    ///
    /// <see cref="ICacheControl.CustomHeaders"/> is a default interface member so that
    /// implementations written before it existed keep compiling. Those implementations declare no
    /// storage for it, so the headers are attached here and collected with the instance.
    /// </summary>
    internal static class CacheControlHeaderStore
    {
        private static readonly ConditionalWeakTable<ICacheControl, HeaderCollection> Headers =
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
