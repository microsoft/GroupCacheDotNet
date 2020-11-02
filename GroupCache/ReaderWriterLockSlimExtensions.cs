using System;
using System.Threading;

namespace GroupCache
{

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