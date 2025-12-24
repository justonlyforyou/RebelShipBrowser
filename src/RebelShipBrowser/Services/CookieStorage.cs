using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RebelShipBrowser.Services
{
    /// <summary>
    /// Stores and retrieves session cookies securely using Windows DPAPI.
    /// Cookies are encrypted and stored in the user's AppData folder.
    /// </summary>
    public static class CookieStorage
    {
        private static readonly string StorageFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RebelShipBrowser"
        );

        private static readonly string CookieFile = Path.Combine(StorageFolder, "session.dat");
        private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

        /// <summary>
        /// Saves a session cookie securely using DPAPI encryption.
        /// </summary>
        public static void SaveCookie(string cookie)
        {
            ArgumentNullException.ThrowIfNull(cookie);

            try
            {
                if (!Directory.Exists(StorageFolder))
                {
                    Directory.CreateDirectory(StorageFolder);
                }

                // Encrypt using DPAPI (Windows Data Protection API)
                var cookieBytes = Encoding.UTF8.GetBytes(cookie);
                var encryptedBytes = ProtectedData.Protect(cookieBytes, null, DataProtectionScope.CurrentUser);
                var base64 = Convert.ToBase64String(encryptedBytes);

                File.WriteAllText(CookieFile, base64);
                DebugLogger.Log($"[CookieStorage] Cookie saved ({cookie.Length} chars)");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[CookieStorage] Failed to save cookie: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads and decrypts the saved session cookie.
        /// Returns null if no cookie is saved or decryption fails.
        /// </summary>
        public static string? LoadCookie()
        {
            try
            {
                if (!File.Exists(CookieFile))
                {
                    DebugLogger.Log("[CookieStorage] No saved cookie found");
                    return null;
                }

                var base64 = File.ReadAllText(CookieFile);
                var encryptedBytes = Convert.FromBase64String(base64);
                var cookieBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                var cookie = Encoding.UTF8.GetString(cookieBytes);

                DebugLogger.Log($"[CookieStorage] Cookie loaded ({cookie.Length} chars)");
                return cookie;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[CookieStorage] Failed to load cookie: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deletes the saved session cookie.
        /// </summary>
        public static void DeleteCookie()
        {
            try
            {
                if (File.Exists(CookieFile))
                {
                    File.Delete(CookieFile);
                    DebugLogger.Log("[CookieStorage] Cookie deleted");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[CookieStorage] Failed to delete cookie: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates a session cookie against the game API.
        /// Returns true if the cookie is valid and can be used for authentication.
        /// </summary>
        public static async Task<bool> ValidateCookieAsync(string cookie)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://shippingmanager.cc/api/user/get-user-settings");
                request.Headers.Add("Cookie", $"shipping_manager_session={cookie}");
                request.Headers.Add("Accept", "application/json");

                using var response = await HttpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    DebugLogger.Log($"[CookieStorage] Cookie validation failed: HTTP {(int)response.StatusCode}");
                    return false;
                }

                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                if (!root.TryGetProperty("user", out var user))
                {
                    DebugLogger.Log("[CookieStorage] Cookie validation failed: No user in response");
                    return false;
                }

                var userId = user.TryGetProperty("id", out var idProp) ? idProp.ToString() : null;
                var companyName = user.TryGetProperty("company_name", out var companyProp)
                    ? companyProp.GetString()
                    : (user.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null);

                if (!string.IsNullOrEmpty(userId))
                {
                    DebugLogger.Log($"[CookieStorage] Cookie valid: {companyName} (ID: {userId})");
                    return true;
                }

                DebugLogger.Log("[CookieStorage] Cookie validation failed: No user ID");
                return false;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[CookieStorage] Cookie validation error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads the saved cookie and validates it.
        /// Returns the cookie if valid, null otherwise.
        /// </summary>
        public static async Task<string?> LoadAndValidateCookieAsync()
        {
            var cookie = LoadCookie();
            if (string.IsNullOrEmpty(cookie))
            {
                return null;
            }

            var isValid = await ValidateCookieAsync(cookie);
            if (isValid)
            {
                return cookie;
            }

            // Cookie is invalid, delete it
            DeleteCookie();
            return null;
        }
    }
}
