// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NullLogger.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;

    /// <summary>
    /// Logger which doesn't log any information anywhere.
    /// </summary>
    public sealed class NullLogger : ILogger
    {
        /// <summary>
        /// As with all logging calls on this logger, this method is a no-op.
        /// </summary>
        public void Debug(string message)
        {
        }

        /// <summary>
        /// As with all logging calls on this logger, this method is a no-op.
        /// </summary>
        public void Debug(string format, params object[] formatArgs)
        {
        }

        /// <summary>
        /// As with all logging calls on this logger, this method is a no-op.
        /// </summary>
        public void Error(string message)
        {
        }

        /// <summary>
        /// As with all logging calls on this logger, this method is a no-op.
        /// </summary>
        public void Error(Exception exception, string message)
        {
        }

        /// <summary>
        /// As with all logging calls on this logger, this method is a no-op.
        /// </summary>
        public void Error(string format, params object[] formatArgs)
        {
        }

        /// <summary>
        /// Returns a reference to the instance on which the method is called, as
        /// instances aren't associated with specific types.
        /// </summary>
        /// <returns></returns>
        public ILogger ForType<T>()
        {
            return this;
        }

        /// <summary>
        /// As with all logging calls on this logger, this method is a no-op.
        /// </summary>
        public void Info(string message)
        {
        }

        /// <summary>
        /// As with all logging calls on this logger, this method is a no-op.
        /// </summary>
        public void Info(string format, params object[] formatArgs)
        {
        }

        /// <summary>
        /// As with all logging calls on this logger, this method is a no-op.
        /// </summary>
        public void Warning(string message)
        {
        }

        /// <summary>
        /// As with all logging calls on this logger, this method is a no-op.
        /// </summary>
        public void Warning(Exception exception, string message)
        {
        }

        /// <summary>
        /// As with all logging calls on this logger, this method is a no-op.
        /// </summary>
        public void Warning(string format, params object[] formatArgs)
        {
        }
    }
}
