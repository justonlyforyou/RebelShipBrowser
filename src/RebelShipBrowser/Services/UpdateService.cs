using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace RebelShipBrowser.Services
{
    public static class UpdateService
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private const string GitHubReleasesUrl = "https://api.github.com/repos/justonlyforyou/RebelShipBrowser/releases/latest";

        public static string CurrentVersion
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return version?.ToString(3) ?? "0.0.0";
            }
        }

        public static string? LatestVersion { get; private set; }
        public static Uri? DownloadUrl { get; private set; }

        /// <summary>
        /// Checks GitHub for the latest release and returns true if an update is available
        /// </summary>
        public static async Task<bool> CheckForUpdateAsync()
        {
            try
            {
                DebugLogger.Log("[UpdateService] Checking for updates...");

                using var request = new HttpRequestMessage(HttpMethod.Get, GitHubReleasesUrl);
                request.Headers.Add("User-Agent", "RebelShipBrowser");
                request.Headers.Add("Accept", "application/vnd.github.v3+json");

                using var response = await HttpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    DebugLogger.Log($"[UpdateService] GitHub API returned {(int)response.StatusCode}");
                    return false;
                }

                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                // Get tag_name (version)
                if (!root.TryGetProperty("tag_name", out var tagProp))
                {
                    DebugLogger.Log("[UpdateService] No tag_name in response");
                    return false;
                }

                var tagName = tagProp.GetString();
                if (string.IsNullOrEmpty(tagName))
                {
                    return false;
                }

                // Remove 'v' prefix if present
                LatestVersion = tagName.TrimStart('v', 'V');

                // Find the setup exe in assets
                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        if (asset.TryGetProperty("name", out var nameProp))
                        {
                            var name = nameProp.GetString();
                            if (name != null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                                name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                            {
                                if (asset.TryGetProperty("browser_download_url", out var urlProp))
                                {
                                    var urlString = urlProp.GetString();
                                    if (!string.IsNullOrEmpty(urlString))
                                    {
                                        DownloadUrl = new Uri(urlString);
                                        DebugLogger.Log($"[UpdateService] Found setup: {name}");
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }

                DebugLogger.Log($"[UpdateService] Current: {CurrentVersion}, Latest: {LatestVersion}");

                // Compare versions
                if (Version.TryParse(CurrentVersion, out var current) &&
                    Version.TryParse(LatestVersion, out var latest))
                {
                    var updateAvailable = latest > current;
                    DebugLogger.Log($"[UpdateService] Update available: {updateAvailable}");
                    return updateAvailable;
                }

                return false;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UpdateService] Error checking for updates: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Downloads the setup file and runs it
        /// </summary>
        public static async Task<bool> DownloadAndInstallUpdateAsync()
        {
            if (DownloadUrl == null)
            {
                DebugLogger.Log("[UpdateService] No download URL available");
                return false;
            }

            try
            {
                DebugLogger.Log($"[UpdateService] Downloading update from: {DownloadUrl.AbsoluteUri}");

                // Download to temp folder
                var tempPath = Path.Combine(Path.GetTempPath(), "RebelShipBrowser_Setup.exe");

                using var response = await HttpClient.GetAsync(DownloadUrl);
                response.EnsureSuccessStatusCode();

                await using var fileStream = new FileStream(tempPath, FileMode.Create);
                await response.Content.CopyToAsync(fileStream);

                DebugLogger.Log($"[UpdateService] Downloaded to: {tempPath}");

                // Run the setup
                var startInfo = new ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                };
                Process.Start(startInfo);

                DebugLogger.Log("[UpdateService] Setup started, exiting app...");
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UpdateService] Error downloading update: {ex.Message}");
                return false;
            }
        }
    }
}
