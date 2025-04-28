// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ExhaustedRetryException.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;

    public class ExhaustedRetryException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExhaustedRetryException"/> class.
        /// </summary>
        public ExhaustedRetryException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExhaustedRetryException"/> class.
        /// </summary>
        /// <param name="message"></param>
        public ExhaustedRetryException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExhaustedRetryException"/> class.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="inner"></param>
        public ExhaustedRetryException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}