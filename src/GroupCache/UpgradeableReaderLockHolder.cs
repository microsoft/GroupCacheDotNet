// --------------------------------------------------------------------------------------------------------------------
// <copyright file="UpgradeableReaderLockHolder.cs" company="Microsoft Corporation">
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
    /// for readers by providing support for <see cref="IDisposable"/> and the using keyword.
    /// </summary>
    public sealed class UpgradeableReaderLockHolder : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UpgradeableReaderLockHolder"/> class.
        /// Acquires a reader lock on the rwLock received
        /// with no timeout specified.
        /// </summary>
        public UpgradeableReaderLockHolder(ReaderWriterLockSlim rwLock)
        {
            if (rwLock == null)
            {
                throw new ArgumentNullException("rwLock");
            }

            this.rwLock = rwLock;
            this.rwLock.EnterUpgradeableReadLock();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
        }

        public WriterLockHolder GetWriterLock()
        {
            return this.rwLock.GetWriterLock();
        }

        private void Dispose(bool disposing)
        {
            if (disposing && !this.disposed)
            {
                if (this.rwLock.IsUpgradeableReadLockHeld)
                {
                    this.rwLock.ExitUpgradeableReadLock();
                }

                this.disposed = true;
            }
        }

        private readonly ReaderWriterLockSlim rwLock;

        // Track whether Dispose has been called.
        private bool disposed = false;
    }
}
