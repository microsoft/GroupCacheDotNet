// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WriterLockHolder.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.Threading;

    /// <summary>
    /// Helper class that makes it easier to ensure proper usage of a <see cref="ReaderWriterLockSlim"/>
    /// for writers by providing support for <see cref="IDisposable"/> and the using keyword.
    /// </summary>
    public sealed class WriterLockHolder : IDisposable
    {
        private readonly ReaderWriterLockSlim rwLock;

        // Track whether Dispose has been called.
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="WriterLockHolder"/> class.
        /// Acquires a reader lock on the rwLock received
        /// with no timeout specified.
        /// </summary>
        public WriterLockHolder(ReaderWriterLockSlim rwLock)
        {
            if (rwLock == null)
            {
                throw new ArgumentNullException("rwLock");
            }

            this.rwLock = rwLock;
            this.rwLock.EnterWriteLock();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing && !this.disposed)
            {
                if (this.rwLock.IsWriteLockHeld)
                {
                    this.rwLock.ExitWriteLock();
                }

                this.disposed = true;
            }
        }
    }
}