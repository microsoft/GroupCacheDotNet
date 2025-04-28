// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IGetter.cs" company="Microsoft Corporation">
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

    public interface ICacheControl
    {
        // Whether cache must not store any response.
        bool NoStore { get; set; }
    }

    public class CacheControl : ICacheControl
    {
        // Whether cache must not store any response.
        /// <inheritdoc/>
        public bool NoStore { get; set; } = false;
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
}
