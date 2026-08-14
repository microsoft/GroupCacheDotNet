// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ICacheControl.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System.Collections.Generic;

    /// <summary>
    /// Carries per-response caching directives and metadata across a cache fill.
    /// </summary>
    public interface ICacheControl
    {
        /// <summary>
        /// Gets or sets a value indicating whether the response must not be stored by any cache.
        /// </summary>
        bool NoStore { get; set; }

        /// <summary>
        /// Gets response metadata that should be propagated between peers.
        /// </summary>
        IDictionary<string, string> CustomHeaders => CacheControlHeaderStore.Get(this);
    }
}
