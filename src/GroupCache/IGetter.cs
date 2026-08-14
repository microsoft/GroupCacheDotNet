// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IGetter.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

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
}
