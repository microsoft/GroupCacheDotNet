// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CacheControl.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Default <see cref="ICacheControl"/> implementation used when filling a cache entry.
    /// </summary>
    public class CacheControl : ICacheControl
    {
        /// <inheritdoc/>
        public bool NoStore { get; set; } = false;

        /// <inheritdoc/>
        public IDictionary<string, string> CustomHeaders { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
