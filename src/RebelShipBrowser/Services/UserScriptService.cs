using System.IO;

namespace RebelShipBrowser.Services
{
    /// <summary>
    /// Manages userscripts: loading, saving, and providing scripts for injection
    /// </summary>
    public class UserScriptService
    {
        private static readonly string ScriptsDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RebelShipBrowser", "userscripts"
        );

        private readonly List<UserScript> _scripts = new();

        public IReadOnlyList<UserScript> Scripts => _scripts.AsReadOnly();

        /// <summary>
        /// Gets the path to the scripts directory
        /// </summary>
        public static string ScriptsDirectory => ScriptsDirectoryPath;

        public UserScriptService()
        {
            EnsureDirectoryExists();
            LoadAllScripts();
        }

        /// <summary>
        /// Ensures the userscripts directory exists
        /// </summary>
        private static void EnsureDirectoryExists()
        {
            if (!Directory.Exists(ScriptsDirectoryPath))
            {
                Directory.CreateDirectory(ScriptsDirectoryPath);
                DebugLogger.Log($"[UserScriptService] Created scripts directory: {ScriptsDirectoryPath}");
            }
        }

        /// <summary>
        /// Loads all scripts from the userscripts directory
        /// </summary>
        public void LoadAllScripts()
        {
            _scripts.Clear();

            if (!Directory.Exists(ScriptsDirectoryPath))
            {
                return;
            }

            foreach (var filePath in Directory.GetFiles(ScriptsDirectoryPath, "*.js"))
            {
                try
                {
                    var script = UserScript.Parse(filePath);
                    _scripts.Add(script);
                    DebugLogger.Log($"[UserScriptService] Loaded script: {script.Name} ({script.FileName})");
                }
                catch (Exception ex)
                {
                    DebugLogger.LogError($"[UserScriptService] Failed to load script {filePath}: {ex.Message}");
                }
            }

            DebugLogger.Log($"[UserScriptService] Loaded {_scripts.Count} script(s)");
        }

        /// <summary>
        /// Saves a script to file
        /// </summary>
        public void SaveScript(UserScript script)
        {
            ArgumentNullException.ThrowIfNull(script);

            EnsureDirectoryExists();

            // Generate full content with metadata
            var metaBlock = script.GenerateMetadataBlock();
            var codeWithoutMeta = script.GetCodeWithoutMetadata();
            var fullContent = metaBlock + Environment.NewLine + Environment.NewLine + codeWithoutMeta;

            File.WriteAllText(script.FilePath, fullContent);
            DebugLogger.Log($"[UserScriptService] Saved script: {script.Name}");

            // Reload to ensure consistency
            var index = _scripts.FindIndex(s => s.FilePath == script.FilePath);
            var reloadedScript = UserScript.Parse(script.FilePath);

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
        /// Creates a new script with default template
        /// </summary>
        public UserScript CreateNewScript()
        {
            EnsureDirectoryExists();

            // Generate unique filename
            var counter = 1;
            var fileName = "new-script.user.js";
            var filePath = Path.Combine(ScriptsDirectoryPath, fileName);

            while (File.Exists(filePath))
            {
                fileName = $"new-script-{counter}.user.js";
                filePath = Path.Combine(ScriptsDirectoryPath, fileName);
                counter++;
            }

            // Create file with template - metadata comes from code
            var code = GetDefaultTemplate();
            File.WriteAllText(filePath, code);

            // Parse and return
            var script = UserScript.Parse(filePath);
            _scripts.Add(script);
            return script;
        }

        /// <summary>
        /// Deletes a script
        /// </summary>
        public void DeleteScript(UserScript script)
        {
            ArgumentNullException.ThrowIfNull(script);

            if (File.Exists(script.FilePath))
            {
                File.Delete(script.FilePath);
                DebugLogger.Log($"[UserScriptService] Deleted script: {script.Name}");
            }

            _scripts.Remove(script);
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
            EnsureDirectoryExists();
            System.Diagnostics.Process.Start("explorer.exe", ScriptsDirectoryPath);
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
