using System;
using System.Threading;

namespace GroupCache
{
    /// <summary>
    /// Helper class that makes it easier to ensure proper usage of a <see
    /// cref="ReaderWriterLockSlim"/> for readers by providing support for <see cref="IDisposable"/>
    /// and the using keyword.
    /// </summary>
    public sealed class ReaderLockHolder : IDisposable
    {
        private ReaderWriterLockSlim _rwLock;

        // Track whether Dispose has been called.
        private bool _disposed = false;

        /// <summary>
        /// Acquires a reader lock on the rwLock received with no timeout specified.
        /// </summary>
        public ReaderLockHolder(ReaderWriterLockSlim rwLock)
        {
            if (rwLock == null)
            {
                throw new ArgumentNullException("rwLock");
            }

            _rwLock = rwLock;
            _rwLock.EnterReadLock();
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                if (_rwLock.IsReadLockHeld)
                {
                    _rwLock.ExitReadLock();
                }

                _disposed = true;
            }
        }
    }
}