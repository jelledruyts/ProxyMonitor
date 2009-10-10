using System;
using System.Diagnostics;
using System.Globalization;

namespace ProxyMonitor
{
    /// <summary>
    /// Provides logging for the application.
    /// </summary>
    internal static class Logger
    {
        #region Fields

        /// <summary>
        /// The trace source to log to.
        /// </summary>
        private static TraceSource trace = new TraceSource("ProxyMonitor", SourceLevels.Warning);

        #endregion

        #region LogMessage

        /// <summary>
        /// Logs a message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="level">The log level.</param>
        public static void LogMessage(string message, TraceEventType level)
        {
            var logMessage = string.Format(CultureInfo.CurrentCulture, "[{0}] {1}", DateTime.Now.ToString(), message);
            trace.TraceEvent(level, 0, logMessage);
            Debug.WriteLine(logMessage);
        }

        #endregion

        #region LogException

        /// <summary>
        /// Logs an exception.
        /// </summary>
        /// <param name="exception">The exception to log.</param>
        public static void LogException(Exception exception)
        {
            LogMessage(exception.ToString(), TraceEventType.Error);
        }

        #endregion
    }
}