// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MicrosoftLoggerAdapter.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCacheStub
{
    using System;
    using System.Globalization;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Adapts Microsoft.Extensions.Logging to the GroupCache logging contract.
    /// </summary>
    public sealed class MicrosoftLoggerAdapter : GroupCache.ILogger
    {
        private readonly Microsoft.Extensions.Logging.ILogger logger;

        public MicrosoftLoggerAdapter(Microsoft.Extensions.Logging.ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public void Debug(string message)
        {
            this.Log(LogLevel.Debug, null, message);
        }

        /// <inheritdoc/>
        public void Debug(string format, params object[] formatArgs)
        {
            this.Debug(string.Format(CultureInfo.CurrentCulture, format, formatArgs));
        }

        /// <inheritdoc/>
        public void Error(string message)
        {
            this.Log(LogLevel.Error, null, message);
        }

        /// <inheritdoc/>
        public void Error(string format, params object[] formatArgs)
        {
            this.Error(string.Format(CultureInfo.CurrentCulture, format, formatArgs));
        }

        /// <inheritdoc/>
        public void Error(Exception exception, string message)
        {
            this.Log(LogLevel.Error, exception, message);
        }

        /// <inheritdoc/>
        public void Info(string message)
        {
            this.Log(LogLevel.Information, null, message);
        }

        /// <inheritdoc/>
        public void Info(string format, params object[] formatArgs)
        {
            this.Info(string.Format(CultureInfo.CurrentCulture, format, formatArgs));
        }

        /// <inheritdoc/>
        public void Warning(string message)
        {
            this.Log(LogLevel.Warning, null, message);
        }

        /// <inheritdoc/>
        public void Warning(string format, params object[] formatArgs)
        {
            this.Warning(string.Format(CultureInfo.CurrentCulture, format, formatArgs));
        }

        /// <inheritdoc/>
        public void Warning(Exception exception, string message)
        {
            this.Log(LogLevel.Warning, exception, message);
        }

        private void Log(LogLevel level, Exception exception, string message)
        {
            this.logger.Log(
                level,
                default,
                message,
                exception,
                static (state, _) => state);
        }
    }
}
