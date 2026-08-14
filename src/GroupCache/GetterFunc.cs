// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GetterFunc.cs" company="Microsoft Corporation">
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

    /// <summary>
    /// Adapts a delegate to the <see cref="IGetter"/> interface.
    /// </summary>
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
