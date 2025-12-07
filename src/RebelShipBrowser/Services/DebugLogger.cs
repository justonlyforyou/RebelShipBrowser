using System;
using System.Globalization;
using System.IO;

namespace RebelShipBrowser.Services
{
    /// <summary>
    /// Simple debug logger that writes to a log file
    /// </summary>
    public static class DebugLogger
    {
        private static readonly string LogPathValue = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RebelShipBrowser",
            "debug.log"
        );

        /// <summary>
        /// Gets the path to the debug log file
        /// </summary>
        public static string LogPath => LogPathValue;

        static DebugLogger()
        {
            try
            {
                var logDir = Path.GetDirectoryName(LogPathValue);
                if (logDir != null && !Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
            }
            catch
            {
                // Ignore directory creation errors
            }
        }

        public static void Log(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                var logMessage = $"[{timestamp}] {message}\n";
                File.AppendAllText(LogPathValue, logMessage);
            }
            catch
            {
                // Ignore logging errors
            }
        }

        public static void LogError(string message, Exception? ex = null)
        {
            var fullMessage = ex != null
                ? $"ERROR: {message} - Exception: {ex.GetType().Name}: {ex.Message}\n  StackTrace: {ex.StackTrace}"
                : $"ERROR: {message}";

            Log(fullMessage);
        }

        public static void ClearLog()
        {
            try
            {
                if (File.Exists(LogPathValue))
                {
                    File.Delete(LogPathValue);
                }
            }
            catch
            {
                // Ignore clear errors
            }
        }
    }
}
