using System;

namespace GroupCache
{
    public interface ILogger
    {
        /// <summary>Logs a message with severity Debug.</summary>
        void Debug(string message);

        /// <summary>Logs a formatted message with severity Debug.</summary>
        void Debug(string format, params object[] formatArgs);

        /// <summary>Logs a message with severity Info.</summary>
        void Info(string message);

        /// <summary>Logs a formatted message with severity Info.</summary>
        void Info(string format, params object[] formatArgs);

        /// <summary>Logs a message with severity Warning.</summary>
        void Warning(string message);

        /// <summary>Logs a formatted message with severity Warning.</summary>
        void Warning(string format, params object[] formatArgs);

        /// <summary>Logs a message and an associated exception with severity Warning.</summary>
        void Warning(Exception exception, string message);

        /// <summary>Logs a message with severity Error.</summary>
        void Error(string message);

        /// <summary>Logs a formatted message with severity Error.</summary>
        void Error(string format, params object[] formatArgs);

        /// <summary>Logs a message and an associated exception with severity Error.</summary>
        void Error(Exception exception, string message);
    }
}
