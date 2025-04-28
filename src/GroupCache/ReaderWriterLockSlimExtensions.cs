// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ReaderWriterLockSlimExtensions.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System.Threading;

    public static class ReaderWriterLockSlimExtensions
    {
        public static ReaderLockHolder GetReaderLock(this ReaderWriterLockSlim rwLock)
        {
            return new ReaderLockHolder(rwLock);
        }

        public static WriterLockHolder GetWriterLock(this ReaderWriterLockSlim rwLock)
        {
            return new WriterLockHolder(rwLock);
        }

        public static UpgradeableReaderLockHolder GetUpgradeableReaderLock(this ReaderWriterLockSlim rwLock)
        {
            return new UpgradeableReaderLockHolder(rwLock);
        }
    }
}