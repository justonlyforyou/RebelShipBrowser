using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace RebelShipBrowser.Services
{
    /// <summary>
    /// Manages userscripts: loading, saving, and providing scripts for injection
    /// </summary>
    public class UserScriptService
    {
        private static readonly string BaseDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RebelShipBrowser", "userscripts"
        );

        private static readonly string BundledDirectoryPath = Path.Combine(BaseDirectoryPath, "bundled");
        private static readonly string CustomDirectoryPath = Path.Combine(BaseDirectoryPath, "custom");

        // Legacy path for migration
        private static readonly string ScriptsDirectoryPath = BaseDirectoryPath;

        private readonly List<UserScript> _scripts = new();
        private readonly UserScriptSettings _settings;

        public IReadOnlyList<UserScript> Scripts => _scripts.AsReadOnly();

        /// <summary>
        /// Gets the path to the bundled scripts directory
        /// </summary>
        public static string BundledDirectory => BundledDirectoryPath;

        /// <summary>
        /// Gets the path to the custom scripts directory
        /// </summary>
        public static string CustomDirectory => CustomDirectoryPath;

        /// <summary>
        /// Gets the path to the scripts directory (legacy, points to base)
        /// </summary>
        public static string ScriptsDirectory => ScriptsDirectoryPath;

        public UserScriptService()
        {
            EnsureDirectoriesExist();
            _settings = UserScriptSettings.Load();
            LoadAllScripts();
        }

        /// <summary>
        /// Ensures the userscripts directories exist
        /// </summary>
        private static void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(BundledDirectoryPath);
            Directory.CreateDirectory(CustomDirectoryPath);
            DebugLogger.Log($"[UserScriptService] Ensured directories: {BundledDirectoryPath}, {CustomDirectoryPath}");
        }

        /// <summary>
        /// Loads all scripts from bundled and custom directories
        /// </summary>
        public void LoadAllScripts()
        {
            _scripts.Clear();

            // Load bundled scripts
            if (Directory.Exists(BundledDirectoryPath))
            {
                foreach (var filePath in Directory.GetFiles(BundledDirectoryPath, "*.js"))
                {
                    try
                    {
                        var script = UserScript.Parse(filePath);
                        script.IsBundled = true;
                        // Apply saved enabled state from settings (survives updates)
                        script.Enabled = _settings.IsEnabled(script.FileName);
                        _scripts.Add(script);
                        DebugLogger.Log($"[UserScriptService] Loaded bundled script: {script.Name} ({script.FileName}), enabled: {script.Enabled}");
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogError($"[UserScriptService] Failed to load bundled script {filePath}: {ex.Message}");
                    }
                }
            }

            // Load custom scripts
            if (Directory.Exists(CustomDirectoryPath))
            {
                foreach (var filePath in Directory.GetFiles(CustomDirectoryPath, "*.js"))
                {
                    try
                    {
                        var script = UserScript.Parse(filePath);
                        script.IsBundled = false;
                        // Apply saved enabled state from settings
                        script.Enabled = _settings.IsEnabled(script.FileName);
                        _scripts.Add(script);
                        DebugLogger.Log($"[UserScriptService] Loaded custom script: {script.Name} ({script.FileName}), enabled: {script.Enabled}");
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogError($"[UserScriptService] Failed to load custom script {filePath}: {ex.Message}");
                    }
                }
            }

            // Sort by Order (ascending), then by Name
            _scripts.Sort((a, b) =>
            {
                var orderCompare = a.Order.CompareTo(b.Order);
                return orderCompare != 0 ? orderCompare : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            DebugLogger.Log($"[UserScriptService] Loaded {_scripts.Count} script(s) ({_scripts.Count(s => s.IsBundled)} bundled, {_scripts.Count(s => !s.IsBundled)} custom)");
        }

        /// <summary>
        /// Saves a script to file (for custom scripts, saves full content; for bundled, only updates enabled state)
        /// </summary>
        public void SaveScript(UserScript script)
        {
            ArgumentNullException.ThrowIfNull(script);

            EnsureDirectoriesExist();

            // Save enabled state to settings file (persists across updates)
            _settings.SetEnabled(script.FileName, script.Enabled);
            _settings.Save();

            // Generate full content with metadata
            var metaBlock = script.GenerateMetadataBlock();
            var codeWithoutMeta = script.GetCodeWithoutMetadata();
            var fullContent = metaBlock + Environment.NewLine + Environment.NewLine + codeWithoutMeta;

            File.WriteAllText(script.FilePath, fullContent);
            DebugLogger.Log($"[UserScriptService] Saved script: {script.Name} (bundled: {script.IsBundled}, enabled: {script.Enabled})");

            // Reload to ensure consistency
            var index = _scripts.FindIndex(s => s.FilePath == script.FilePath);
            var reloadedScript = UserScript.Parse(script.FilePath);
            reloadedScript.IsBundled = script.IsBundled; // Preserve bundled flag
            reloadedScript.Enabled = script.Enabled; // Preserve enabled state

            if (index >= 0)
            {
                _scripts[index] = reloadedScript;
            }
            else
            {
                _scripts.Add(reloadedScript);
            }
        }

        /// <summary>
        /// Creates a new script with default template in the custom directory
        /// </summary>
        public UserScript CreateNewScript()
        {
            EnsureDirectoriesExist();

            // Generate unique filename in custom directory
            var counter = 1;
            var fileName = "new-script.user.js";
            var filePath = Path.Combine(CustomDirectoryPath, fileName);

            while (File.Exists(filePath))
            {
                fileName = $"new-script-{counter}.user.js";
                filePath = Path.Combine(CustomDirectoryPath, fileName);
                counter++;
            }

            // Create file with template - metadata comes from code
            var code = GetDefaultTemplate();
            File.WriteAllText(filePath, code);

            // Parse and return
            var script = UserScript.Parse(filePath);
            script.IsBundled = false;
            _scripts.Add(script);
            return script;
        }

        /// <summary>
        /// Deletes a script (only custom scripts can be deleted)
        /// </summary>
        public bool DeleteScript(UserScript script)
        {
            ArgumentNullException.ThrowIfNull(script);

            if (script.IsBundled)
            {
                DebugLogger.Log($"[UserScriptService] Cannot delete bundled script: {script.Name}");
                return false;
            }

            if (File.Exists(script.FilePath))
            {
                File.Delete(script.FilePath);
                DebugLogger.Log($"[UserScriptService] Deleted script: {script.Name}");
            }

            _scripts.Remove(script);
            return true;
        }

        /// <summary>
        /// Gets all enabled scripts that should run on the given URL
        /// </summary>
        public IEnumerable<UserScript> GetScriptsForUrl(Uri url)
        {
            ArgumentNullException.ThrowIfNull(url);
            return _scripts.Where(s => s.Enabled && s.ShouldRunOnUrl(url));
        }

        /// <summary>
        /// Gets all enabled scripts that should run on the given URL string
        /// </summary>
        internal IEnumerable<UserScript> GetScriptsForUrlString(string url)
        {
            return _scripts.Where(s => s.Enabled && s.ShouldRunOnUrlString(url));
        }

        /// <summary>
        /// Opens the scripts directory in Explorer
        /// </summary>
        public static void OpenScriptsDirectory()
        {
            Directory.CreateDirectory(BaseDirectoryPath);
            System.Diagnostics.Process.Start("explorer.exe", BaseDirectoryPath);
        }

        private static string GetDefaultTemplate()
        {
            return @"// ==UserScript==
// @name        New Script
// @description My custom script
// @version     1.0
// @match       https://shippingmanager.cc/*
// @run-at      document-end
// ==/UserScript==

(function() {
    'use strict';

    // Your code here
    console.log('Script loaded!');

})();
";
        }

        /// <summary>
        /// Checks for script updates from GitHub without downloading
        /// </summary>
        /// <returns>Number of scripts with available updates</returns>
        public async Task<int> CheckForUpdatesAsync()
        {
            const string rawBaseUrl = "https://raw.githubusercontent.com/justonlyforyou/shippingmanager_user_scripts/main/";
            int updatesAvailable = 0;

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "RebelShipBrowser");
            httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache, no-store");
            httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");

            foreach (var script in _scripts.Where(s => s.IsBundled))
            {
                try
                {
                    // Use cache-busting query parameter to bypass GitHub CDN cache
                    var cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    var scriptUrl = new Uri($"{rawBaseUrl}{script.FileName}?cb={cacheBuster}");
                    var remoteContent = await httpClient.GetStringAsync(scriptUrl);

                    // Extract version from remote content
                    var versionMatch = System.Text.RegularExpressions.Regex.Match(
                        remoteContent,
                        @"//\s*@version\s+(.+)$",
                        System.Text.RegularExpressions.RegexOptions.Multiline
                    );

                    if (versionMatch.Success)
                    {
                        var remoteVersion = versionMatch.Groups[1].Value.Trim();
                        script.RemoteVersion = remoteVersion;

                        if (IsNewerVersion(remoteVersion, script.Version))
                        {
                            script.HasUpdate = true;
                            updatesAvailable++;
                            DebugLogger.Log($"[UserScriptService] Update available for {script.FileName}: {script.Version} -> {remoteVersion}");
                        }
                        else
                        {
                            script.HasUpdate = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.LogError($"[UserScriptService] Failed to check update for {script.FileName}: {ex.Message}");
                }
            }

            DebugLogger.Log($"[UserScriptService] Update check complete: {updatesAvailable} updates available");
            return updatesAvailable;
        }

        /// <summary>
        /// Compares version strings (e.g., "1.2" vs "1.3")
        /// </summary>
        private static bool IsNewerVersion(string remoteVersion, string? localVersion)
        {
            if (string.IsNullOrEmpty(localVersion))
            {
                return true;
            }

            try
            {
                var remoteParts = remoteVersion.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
                var localParts = localVersion.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();

                var maxLength = Math.Max(remoteParts.Length, localParts.Length);

                for (int i = 0; i < maxLength; i++)
                {
                    var remote = i < remoteParts.Length ? remoteParts[i] : 0;
                    var local = i < localParts.Length ? localParts[i] : 0;

                    if (remote > local)
                    {
                        return true;
                    }
                    if (remote < local)
                    {
                        return false;
                    }
                }

                return false;
            }
            catch
            {
                return !string.Equals(remoteVersion, localVersion, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Updates bundled scripts from the GitHub repository
        /// </summary>
        /// <param name="progress">Optional progress callback (current, total, filename)</param>
        /// <returns>Lists of updated, added, deleted script names and errors</returns>
        public async Task<(List<string> updated, List<string> added, List<string> deleted, List<string> errors)> UpdateScriptsFromGitHubAsync(Action<int, int, string>? progress = null)
        {
            const string repoApiUrl = "https://api.github.com/repos/justonlyforyou/shippingmanager_user_scripts/contents";
            const string rawBaseUrl = "https://raw.githubusercontent.com/justonlyforyou/shippingmanager_user_scripts/main/";

            var errors = new List<string>();
            var updated = new List<string>();
            var added = new List<string>();
            var deleted = new List<string>();

            EnsureDirectoriesExist();

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "RebelShipBrowser");
            httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache, no-store");
            httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");

            try
            {
                // Get list of files from GitHub API
                DebugLogger.Log("[UserScriptService] Fetching script list from GitHub...");
                var response = await httpClient.GetStringAsync(new Uri(repoApiUrl));
                var files = JsonSerializer.Deserialize<JsonElement[]>(response);

                if (files == null)
                {
                    errors.Add("Failed to parse GitHub API response");
                    return (updated, added, deleted, errors);
                }

                // Filter for .user.js files
                var scriptFiles = files.Where(f =>
                    f.TryGetProperty("name", out var name) &&
                    name.GetString()?.EndsWith(".user.js", StringComparison.OrdinalIgnoreCase) == true
                ).ToList();

                var remoteFileNames = scriptFiles
                    .Select(f => f.GetProperty("name").GetString()!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                DebugLogger.Log($"[UserScriptService] Found {scriptFiles.Count} userscripts on GitHub");

                // Delete local bundled scripts that no longer exist on GitHub
                if (Directory.Exists(BundledDirectoryPath))
                {
                    foreach (var localFile in Directory.GetFiles(BundledDirectoryPath, "*.js"))
                    {
                        var localFileName = Path.GetFileName(localFile);
                        if (!remoteFileNames.Contains(localFileName))
                        {
                            try
                            {
                                File.Delete(localFile);
                                deleted.Add(localFileName);
                                DebugLogger.Log($"[UserScriptService] Deleted obsolete script: {localFileName}");
                            }
                            catch (Exception ex)
                            {
                                DebugLogger.LogError($"[UserScriptService] Failed to delete {localFileName}: {ex.Message}");
                            }
                        }
                    }
                }

                int current = 0;
                foreach (var file in scriptFiles)
                {
                    current++;
                    var fileName = file.GetProperty("name").GetString()!;
                    progress?.Invoke(current, scriptFiles.Count, fileName);

                    try
                    {
                        // Download raw script content with cache-busting query parameter
                        var cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        var scriptUrl = new Uri($"{rawBaseUrl}{fileName}?cb={cacheBuster}");
                        DebugLogger.Log($"[UserScriptService] Downloading: {scriptUrl}");
                        var scriptContent = await httpClient.GetStringAsync(scriptUrl);

                        // Check if file already exists
                        var localPath = Path.Combine(BundledDirectoryPath, fileName);
                        bool isNew = !File.Exists(localPath);

                        if (isNew)
                        {
                            // New script - just save it
                            await File.WriteAllTextAsync(localPath, scriptContent);
                            added.Add(fileName);
                            DebugLogger.Log($"[UserScriptService] Added new script: {fileName}");
                        }
                        else
                        {
                            // Existing script - check version before updating
                            var remoteVersionMatch = System.Text.RegularExpressions.Regex.Match(
                                scriptContent,
                                @"//\s*@version\s+(.+)$",
                                System.Text.RegularExpressions.RegexOptions.Multiline
                            );

                            var localContent = await File.ReadAllTextAsync(localPath);
                            var localVersionMatch = System.Text.RegularExpressions.Regex.Match(
                                localContent,
                                @"//\s*@version\s+(.+)$",
                                System.Text.RegularExpressions.RegexOptions.Multiline
                            );

                            var remoteVersion = remoteVersionMatch.Success ? remoteVersionMatch.Groups[1].Value.Trim() : "0";
                            var localVersion = localVersionMatch.Success ? localVersionMatch.Groups[1].Value.Trim() : "0";

                            DebugLogger.Log($"[UserScriptService] Comparing {fileName}: local='{localVersion}' remote='{remoteVersion}' isNewer={IsNewerVersion(remoteVersion, localVersion)}");

                            if (IsNewerVersion(remoteVersion, localVersion))
                            {
                                await File.WriteAllTextAsync(localPath, scriptContent);
                                updated.Add($"{fileName} ({localVersion} -> {remoteVersion})");
                                DebugLogger.Log($"[UserScriptService] Updated script: {fileName} ({localVersion} -> {remoteVersion})");
                            }
                            else
                            {
                                DebugLogger.Log($"[UserScriptService] Script up to date: {fileName} (v{localVersion})");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var error = $"Failed to download {fileName}: {ex.Message}";
                        errors.Add(error);
                        DebugLogger.LogError($"[UserScriptService] {error}");
                    }
                }

                // Reload scripts to pick up changes (preserves enabled state via settings)
                LoadAllScripts();

                DebugLogger.Log($"[UserScriptService] Update complete: {updated.Count} updated, {added.Count} new, {deleted.Count} deleted, {errors.Count} errors");
            }
            catch (Exception ex)
            {
                var error = $"Failed to fetch script list: {ex.Message}";
                errors.Add(error);
                DebugLogger.LogError($"[UserScriptService] {error}");
            }

            return (updated, added, deleted, errors);
        }
    }
}
