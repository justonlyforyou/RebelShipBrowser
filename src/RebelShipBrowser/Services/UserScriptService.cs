using System.IO;

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
                        _scripts.Add(script);
                        DebugLogger.Log($"[UserScriptService] Loaded bundled script: {script.Name} ({script.FileName})");
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
                        _scripts.Add(script);
                        DebugLogger.Log($"[UserScriptService] Loaded custom script: {script.Name} ({script.FileName})");
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.LogError($"[UserScriptService] Failed to load custom script {filePath}: {ex.Message}");
                    }
                }
            }

            DebugLogger.Log($"[UserScriptService] Loaded {_scripts.Count} script(s) ({_scripts.Count(s => s.IsBundled)} bundled, {_scripts.Count(s => !s.IsBundled)} custom)");
        }

        /// <summary>
        /// Saves a script to file (for custom scripts, saves full content; for bundled, only updates enabled state)
        /// </summary>
        public void SaveScript(UserScript script)
        {
            ArgumentNullException.ThrowIfNull(script);

            EnsureDirectoriesExist();

            // Generate full content with metadata
            var metaBlock = script.GenerateMetadataBlock();
            var codeWithoutMeta = script.GetCodeWithoutMetadata();
            var fullContent = metaBlock + Environment.NewLine + Environment.NewLine + codeWithoutMeta;

            File.WriteAllText(script.FilePath, fullContent);
            DebugLogger.Log($"[UserScriptService] Saved script: {script.Name} (bundled: {script.IsBundled})");

            // Reload to ensure consistency
            var index = _scripts.FindIndex(s => s.FilePath == script.FilePath);
            var reloadedScript = UserScript.Parse(script.FilePath);
            reloadedScript.IsBundled = script.IsBundled; // Preserve bundled flag

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
    }
}
