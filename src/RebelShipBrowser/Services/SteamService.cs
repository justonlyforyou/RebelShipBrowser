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
                if (File.Exists(CookiePathNew)) return CookiePathNew;
                if (File.Exists(CookiePathOld)) return CookiePathOld;
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
                if (File.Exists(LocalStatePathNew)) return LocalStatePathNew;
                if (File.Exists(LocalStatePathOld)) return LocalStatePathOld;
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
        /// Stops Steam gracefully, with fallback to force kill
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>True if Steam was stopped or wasn't running</returns>
        public static async Task<bool> StopSteamAsync(CancellationToken ct = default)
        {
            if (!IsSteamRunning())
            {
                return true;
            }

            try
            {
                // Graceful shutdown via Steam protocol
                var startInfo = new ProcessStartInfo
                {
                    FileName = "steam://exit",
                    UseShellExecute = true
                };
                Process.Start(startInfo);

                // Wait up to 15 seconds for graceful exit
                for (int i = 0; i < 15; i++)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return false;
                    }

                    await Task.Delay(1000, ct);

                    if (!IsSteamRunning())
                    {
                        // Wait a bit more for file handles to be released
                        await Task.Delay(500, ct);
                        return true;
                    }
                }

                // Fallback: Force kill
                foreach (var process in Process.GetProcessesByName("steam"))
                {
                    try
                    {
                        process.Kill();
                        await process.WaitForExitAsync(ct);
                    }
                    catch
                    {
                        // Ignore kill errors
                    }
                }

                // Final wait for file handles
                await Task.Delay(500, ct);
                return !IsSteamRunning();
            }
            catch (Exception)
            {
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
            // Check if Steam files exist
            var cookiePath = CookiePath;
            var localStatePath = LocalStatePath;

            if (cookiePath == null || localStatePath == null)
            {
                return null;
            }

            // Get AES key
            var aesKey = CookieDecryptor.GetAesKey(localStatePath);
            if (aesKey == null)
            {
                return null;
            }

            // Try to read from database (may need to copy if locked)
            string dbPath = cookiePath;
            string? tempDbPath = null;

            try
            {
                // First try direct access
                var encryptedCookie = CookieDecryptor.GetEncryptedCookieFromDb(dbPath, TargetDomain, TargetCookieName);

                // If failed (likely locked), copy the database
                if (encryptedCookie == null && IsSteamRunning())
                {
                    tempDbPath = CookieDecryptor.CopyDatabaseIfLocked(dbPath);
                    if (tempDbPath != dbPath)
                    {
                        encryptedCookie = CookieDecryptor.GetEncryptedCookieFromDb(tempDbPath, TargetDomain, TargetCookieName);
                    }
                }

                if (encryptedCookie == null)
                {
                    return null;
                }

                // Decrypt the cookie
                return CookieDecryptor.DecryptCookieValue(encryptedCookie, aesKey);
            }
            finally
            {
                // Cleanup temp database if created
                if (tempDbPath != null && tempDbPath != dbPath)
                {
                    await Task.Run(() => CookieDecryptor.CleanupTempDatabase(tempDbPath));
                }
            }
        }

        /// <summary>
        /// Full extraction flow: Stop Steam -> Extract Cookie -> Restart Steam
        /// </summary>
        /// <param name="restartSteam">Whether to restart Steam after extraction</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Session cookie value or null</returns>
        public static async Task<string?> ExtractWithSteamManagementAsync(bool restartSteam = true, CancellationToken ct = default)
        {
            bool steamWasRunning = IsSteamRunning();

            try
            {
                // Stop Steam if running
                if (steamWasRunning)
                {
                    var stopped = await StopSteamAsync(ct);
                    if (!stopped)
                    {
                        return null;
                    }
                }

                // Extract cookie
                return await ExtractSessionCookieAsync();
            }
            finally
            {
                // Restart Steam if it was running and restart is requested
                if (restartSteam && steamWasRunning)
                {
                    RestartSteamMinimized();
                }
            }
        }
    }
}
