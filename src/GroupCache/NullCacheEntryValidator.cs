// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NullCacheEntryValidator.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public class NullCacheEntryValidator : ICacheEntryValidator
    {
        /// <inheritdoc/>
        public ValidationStream ValidateEntryPassThrough(string key, Stream sink)
        {
            return new NullValidationStream(sink);
        }
    }

    public class NullValidationStream : ValidationStream
    {
        private readonly Stream sink;

        public NullValidationStream(Stream sink)
        {
            this.sink = sink;
        }

        /// <inheritdoc/>
        public override bool CanRead => this.sink.CanRead;

        /// <inheritdoc/>
        public override bool CanSeek => this.sink.CanSeek;

        /// <inheritdoc/>
        public override bool CanWrite => this.sink.CanWrite;

        /// <inheritdoc/>
        public override long Length => this.sink.Length;

        /// <inheritdoc/>
        public override long Position { get => this.sink.Position; set => this.sink.Position = value; }

        /// <inheritdoc/>
        public override void Flush()
        {
            this.sink.Flush();
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.sink.Read(buffer, offset, count);
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            return this.sink.Seek(offset, origin);
        }

        /// <inheritdoc/>
        public override void SetLength(long value)
        {
            this.sink.SetLength(value);
        }

        /// <inheritdoc/>
        public override Task ValidateAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            this.sink.Write(buffer, offset, count);
        }
    }
}