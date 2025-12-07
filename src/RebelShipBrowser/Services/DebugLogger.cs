using System;
using System.IO;

namespace RebelShipBrowser.Services
{
    /// <summary>
    /// Simple debug logger that writes to a log file
    /// </summary>
    public static class DebugLogger
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RebelShipBrowser",
            "debug.log"
        );

        static DebugLogger()
        {
            try
            {
                var logDir = Path.GetDirectoryName(LogPath);
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
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logMessage = $"[{timestamp}] {message}\n";
                File.AppendAllText(LogPath, logMessage);
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
                if (File.Exists(LogPath))
                {
                    File.Delete(LogPath);
                }
            }
            catch
            {
                // Ignore clear errors
            }
        }

        public static string GetLogPath() => LogPath;
    }
}
