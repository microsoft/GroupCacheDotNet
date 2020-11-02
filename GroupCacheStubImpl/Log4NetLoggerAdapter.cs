using System;

namespace GroupCacheStub
{
    public class Log4NetLoggerAdapter : GroupCache.ILogger
    {
        private log4net.ILog _innerLogger;

        public Log4NetLoggerAdapter(log4net.ILog log4NetLogger)
        {
            _innerLogger = log4NetLogger;
        }

        public void Debug(string message)
        {
            _innerLogger.Debug(message);
        }

        public void Debug(string format, params object[] formatArgs)
        {
            _innerLogger.DebugFormat(format, formatArgs);
        }

        public void Error(string message)
        {
            _innerLogger.Error(message);
        }

        public void Error(string format, params object[] formatArgs)
        {
            _innerLogger.ErrorFormat(format, formatArgs);
        }

        public void Error(Exception exception, string message)
        {
            _innerLogger.Error(message, exception);
        }

        public void Info(string message)
        {
            _innerLogger.Info(message);
        }

        public void Info(string format, params object[] formatArgs)
        {
            _innerLogger.InfoFormat(format, formatArgs);
        }

        public void Warning(string message)
        {
            _innerLogger.Warn(message);
        }

        public void Warning(string format, params object[] formatArgs)
        {
            _innerLogger.WarnFormat(format, formatArgs);
        }

        public void Warning(Exception exception, string message)
        {
            _innerLogger.Warn(message, exception);
        }
    }
}
