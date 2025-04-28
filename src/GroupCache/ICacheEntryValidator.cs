// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ICacheEntryValidator.cs" company="Microsoft Corporation">
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

    public class CacheEntryValidationFailedException : Exception
    {
        public CacheEntryValidationFailedException()
            : base()
        {
        }

        public CacheEntryValidationFailedException(string message)
            : base(message)
        {
        }

        public CacheEntryValidationFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public abstract class ValidationStream : Stream
    {
        public abstract Task ValidateAsync(CancellationToken ct);
    }

    public interface ICacheEntryValidator
    {
        ValidationStream ValidateEntryPassThrough(string key, Stream streamPayload);
    }
}