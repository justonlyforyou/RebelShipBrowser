using System.Diagnostics;
using System.IO;

namespace RebelShipBrowser.Services
{
    /// <summary>
    /// Handles Steam process management and session cookie extraction
    /// </summary>
    public static class SteamService
    {
        private const string TargetDomain = "shippingmanager.cc";
        private const string TargetCookieName = "shipping_manager_session";

        static SteamService()
        {
            DebugLogger.Log("=== SteamService initialized ===");
        }

        private static readonly string HtmlCacheBase = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Steam", "htmlcache"
        );

        /// <summary>
        /// New path: Default/Network/Cookies
        /// </summary>
        private static string CookiePathNew => Path.Combine(HtmlCacheBase, "Default", "Network", "Cookies");

        /// <summary>
        /// Old path: Network/Cookies
        /// </summary>
        private static string CookiePathOld => Path.Combine(HtmlCacheBase, "Network", "Cookies");

        /// <summary>
        /// New path: Local State
        /// </summary>
        private static string LocalStatePathNew => Path.Combine(HtmlCacheBase, "Local State");

        /// <summary>
        /// Old path: LocalPrefs.json
        /// </summary>
        private static string LocalStatePathOld => Path.Combine(HtmlCacheBase, "LocalPrefs.json");

        /// <summary>
        /// Returns the cookie path that exists (new path preferred)
        /// </summary>
        public static string? CookiePath
        {
            get
            {
                if (File.Exists(CookiePathNew))
                {
                    return CookiePathNew;
                }
                if (File.Exists(CookiePathOld))
                {
                    return CookiePathOld;
                }
                return null;
            }
        }

        /// <summary>
        /// Returns the local state path that exists (new path preferred)
        /// </summary>
        public static string? LocalStatePath
        {
            get
            {
                if (File.Exists(LocalStatePathNew))
                {
                    return LocalStatePathNew;
                }
                if (File.Exists(LocalStatePathOld))
                {
                    return LocalStatePathOld;
                }
                return null;
            }
        }

        /// <summary>
        /// Path to Steam executable
        /// </summary>
        public static string SteamExePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam", "steam.exe"
        );

        /// <summary>
        /// Checks if Steam is currently running
        /// </summary>
        public static bool IsSteamRunning()
        {
            return Process.GetProcessesByName("steam").Length > 0;
        }

        /// <summary>
        /// Checks if Steam is installed and has visited the target domain
        /// </summary>
        public static bool IsSteamInstalled()
        {
            return CookiePath != null && LocalStatePath != null;
        }

        /// <summary>
        /// Exits Steam gracefully using steam://exit protocol (NO force kill!)
        /// This ensures Steam writes all data to disk properly.
        /// </summary>
        /// <returns>True if Steam exited</returns>
        public static async Task<bool> ExitSteamGracefullyAsync()
        {
            if (!IsSteamRunning())
            {
                DebugLogger.Log("[SteamService] Steam not running, nothing to exit");
                return true;
            }

            try
            {
                DebugLogger.Log("[SteamService] Sending steam://exit...");
                var startInfo = new ProcessStartInfo
                {
                    FileName = "steam://exit",
                    UseShellExecute = true
                };
                Process.Start(startInfo);

                // Wait up to 30 seconds for Steam to exit gracefully
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(1000);

                    if (!IsSteamRunning())
                    {
                        DebugLogger.Log("[SteamService] Steam exited gracefully");
                        return true;
                    }
                }

                DebugLogger.LogError("[SteamService] Steam did not exit after 30 seconds");
                return false;
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"[SteamService] Failed to exit Steam: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Restarts Steam in minimized/silent mode
        /// </summary>
        public static void RestartSteamMinimized()
        {
            if (IsSteamRunning())
            {
                return;
            }

            try
            {
                if (File.Exists(SteamExePath))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = SteamExePath,
                        Arguments = "-silent",
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                }
            }
            catch
            {
                // Ignore restart errors
            }
        }

        /// <summary>
        /// Extracts the session cookie from Steam's cache
        /// </summary>
        /// <returns>Session cookie value or null if extraction fails</returns>
        public static async Task<string?> ExtractSessionCookieAsync()
        {
            DebugLogger.Log("ExtractSessionCookieAsync: Starting cookie extraction");

            // Check if Steam files exist
            var cookiePath = CookiePath;
            var localStatePath = LocalStatePath;

            DebugLogger.Log($"Cookie path: {cookiePath ?? "NOT FOUND"}");
            DebugLogger.Log($"LocalState path: {localStatePath ?? "NOT FOUND"}");

            if (cookiePath == null || localStatePath == null)
            {
                DebugLogger.LogError("Cookie extraction failed: Steam files not found");
                return null;
            }

            // Get AES key
            DebugLogger.Log("Attempting to extract AES key...");
            var aesKey = CookieDecryptor.GetAesKey(localStatePath);
            if (aesKey == null)
            {
                DebugLogger.LogError("Failed to extract AES key from LocalState");
                return null;
            }
            DebugLogger.Log("AES key extracted successfully");

            // Try to read from database (may need to copy if locked)
            string dbPath = cookiePath;
            string? tempDbPath = null;

            try
            {
                DebugLogger.Log($"Reading cookie database: {dbPath}");

                // First try direct access
                var encryptedCookie = CookieDecryptor.GetEncryptedCookieFromDb(dbPath, TargetDomain, TargetCookieName);

                // If failed (likely locked), copy the database
                if (encryptedCookie == null && IsSteamRunning())
                {
                    DebugLogger.Log("Direct access failed (likely locked), copying database...");
                    tempDbPath = CookieDecryptor.CopyDatabaseIfLocked(dbPath);
                    if (tempDbPath != dbPath)
                    {
                        DebugLogger.Log($"Database copied to: {tempDbPath}");
                        encryptedCookie = CookieDecryptor.GetEncryptedCookieFromDb(tempDbPath, TargetDomain, TargetCookieName);
                    }
                }

                if (encryptedCookie == null)
                {
                    DebugLogger.LogError($"Cookie '{TargetCookieName}' not found in database for domain '{TargetDomain}'");
                    return null;
                }

                DebugLogger.Log("Encrypted cookie found, decrypting...");

                // Decrypt the cookie (DO NOT LOG THE DECRYPTED VALUE!)
                var decryptedCookie = CookieDecryptor.DecryptCookieValue(encryptedCookie, aesKey);

                if (decryptedCookie != null)
                {
                    DebugLogger.Log("Cookie decrypted successfully (length: " + decryptedCookie.Length + ")");
                }
                else
                {
                    DebugLogger.LogError("Failed to decrypt cookie");
                }

                return decryptedCookie;
            }
            finally
            {
                // Cleanup temp database if created
                if (tempDbPath != null && tempDbPath != dbPath)
                {
                    DebugLogger.Log("Cleaning up temporary database...");
                    await Task.Run(() => CookieDecryptor.CleanupTempDatabase(tempDbPath));
                }
            }
        }
    }
}
