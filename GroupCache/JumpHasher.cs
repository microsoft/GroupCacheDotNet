using System;

namespace GroupCache
{
    /// <summary>
    /// A C# implementation of the jump consistent hash from Lamping and Veach.
    /// Paper: http://arxiv.org/ftp/arxiv/papers/1406/1406.2294.pdf
    /// </summary>
    public class JumpHasher
    {
        private const long JUMP = 1L << 31;
        private const ulong HASHSeed = 2862933555777941757;
        private const int bitShiftAmount = 33;
        // Hash returns the integer hash for the given key.
        public int Hash(UInt64 key, int n)
        {
            Int64 b = -1;
            Int64 j = 0;
            while (j < n)
            {
                b = j;
                key = key * HASHSeed + 1;
                j = (long)((b + 1L) * (JUMP / (Double)((key >> bitShiftAmount) + 1L)));
            }
            return (int)b;
        }
    }
}
