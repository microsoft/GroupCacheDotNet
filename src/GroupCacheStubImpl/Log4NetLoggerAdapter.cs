// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Log4NetLoggerAdapter.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCacheStub
{
    using System;

    public class Log4NetLoggerAdapter : GroupCache.ILogger
    {
        private readonly log4net.ILog innerLogger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Log4NetLoggerAdapter"/> class.
        /// </summary>
        /// <param name="log4NetLogger"></param>
        public Log4NetLoggerAdapter(log4net.ILog log4NetLogger)
        {
            this.innerLogger = log4NetLogger;
        }

        /// <inheritdoc/>
        public void Debug(string message)
        {
            this.innerLogger.Debug(message);
        }

        /// <inheritdoc/>
        public void Debug(string format, params object[] formatArgs)
        {
            this.innerLogger.DebugFormat(format, formatArgs);
        }

        /// <inheritdoc/>
        public void Error(string message)
        {
            this.innerLogger.Error(message);
        }

        /// <inheritdoc/>
        public void Error(string format, params object[] formatArgs)
        {
            this.innerLogger.ErrorFormat(format, formatArgs);
        }

        /// <inheritdoc/>
        public void Error(Exception exception, string message)
        {
            this.innerLogger.Error(message, exception);
        }

        /// <inheritdoc/>
        public void Info(string message)
        {
            this.innerLogger.Info(message);
        }

        /// <inheritdoc/>
        public void Info(string format, params object[] formatArgs)
        {
            this.innerLogger.InfoFormat(format, formatArgs);
        }

        /// <inheritdoc/>
        public void Warning(string message)
        {
            this.innerLogger.Warn(message);
        }

        /// <inheritdoc/>
        public void Warning(string format, params object[] formatArgs)
        {
            this.innerLogger.WarnFormat(format, formatArgs);
        }

        /// <inheritdoc/>
        public void Warning(Exception exception, string message)
        {
            this.innerLogger.Warn(message, exception);
        }
    }
}
