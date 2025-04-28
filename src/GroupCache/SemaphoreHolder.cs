// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SemaphoreHolder.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public static class SemaphoreExtensions
    {
        private const string TimeOutError = "Failed to Aquire lock within timeout";

        public static SemaphoreHolder Acquire(this SemaphoreSlim semaphore)
        {
            semaphore.Wait();
            return new SemaphoreHolder(semaphore);
        }

        public static SemaphoreHolder Acquire(this SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            semaphore.Wait(cancellationToken);
            return new SemaphoreHolder(semaphore);
        }

        public static SemaphoreHolder Acquire(this SemaphoreSlim semaphore, TimeSpan timeout)
        {
            if (!semaphore.Wait(timeout))
            {
                throw new TimeoutException(TimeOutError);
            }

            return new SemaphoreHolder(semaphore);
        }

        public static SemaphoreHolder Acquire(this SemaphoreSlim semaphore, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (!semaphore.Wait(timeout, cancellationToken))
            {
                throw new TimeoutException(TimeOutError);
            }

            return new SemaphoreHolder(semaphore);
        }

        public static async Task<SemaphoreHolder> AcquireAsync(this SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
            return new SemaphoreHolder(semaphore);
        }

        public static async Task<SemaphoreHolder> AcquireAsync(this SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new SemaphoreHolder(semaphore);
        }

        public static async Task<SemaphoreHolder> AcquireAsync(this SemaphoreSlim semaphore, TimeSpan timeout)
        {
            if (!await semaphore.WaitAsync(timeout).ConfigureAwait(false))
            {
                throw new TimeoutException(TimeOutError);
            }

            return new SemaphoreHolder(semaphore);
        }

        public static async Task<SemaphoreHolder> AcquireAsync(this SemaphoreSlim semaphore, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (!await semaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false))
            {
                throw new TimeoutException(TimeOutError);
            }

            return new SemaphoreHolder(semaphore);
        }
    }

    /// <summary>
    /// Helper class that makes it easier to ensure proper usage of a <see cref="Semaphore"/> for
    /// readers by providing support for <see cref="IDisposable"/> and the using keyword.
    /// </summary>
    public sealed class SemaphoreHolder : IDisposable
    {
        private readonly SemaphoreSlim semaphore;

        // Track whether Dispose has been called.
        private bool disposed = false;

        public SemaphoreHolder(SemaphoreSlim semaphore)
        {
            Argument.NotNull(semaphore, "semaphore");
            this.semaphore = semaphore;
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing && !this.disposed)
            {
                this.semaphore.Release();
                this.disposed = true;
            }
        }
    }
}
