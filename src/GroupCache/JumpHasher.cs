// --------------------------------------------------------------------------------------------------------------------
// <copyright file="JumpHasher.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    /// <summary>
    /// A C# implementation of the jump consistent hash from Lamping and Veach.
    /// Paper: http://arxiv.org/ftp/arxiv/papers/1406/1406.2294.pdf.
    /// </summary>
    public class JumpHasher
    {
        private const long JUMP = 1L << 31;
        private const ulong HASHSeed = 2862933555777941757;
        private const int BitShiftAmount = 33;

        // Hash returns the integer hash for the given key.
        public int Hash(ulong key, int n)
        {
            long b = -1;
            long j = 0;
            while (j < n)
            {
                b = j;
                key = (key * HASHSeed) + 1;
                j = (long)((b + 1L) * (JUMP / (double)((key >> BitShiftAmount) + 1L)));
            }

            return (int)b;
        }
    }
}
