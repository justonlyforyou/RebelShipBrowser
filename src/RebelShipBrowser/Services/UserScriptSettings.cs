using System.IO;
using System.Text.Json;

namespace RebelShipBrowser.Services
{
    /// <summary>
    /// Stores and persists userscript settings (enabled state) separately from script files.
    /// This allows settings to survive updates that overwrite bundled scripts.
    /// </summary>
    public class UserScriptSettings
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RebelShipBrowser", "userscript-settings.json"
        );

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        /// <summary>
        /// Dictionary of script filename -> enabled state
        /// </summary>
        public Dictionary<string, bool> EnabledScripts { get; init; } = new();

        /// <summary>
        /// Loads settings from disk
        /// </summary>
        public static UserScriptSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<UserScriptSettings>(json);
                    if (settings != null)
                    {
                        DebugLogger.Log($"[UserScriptSettings] Loaded {settings.EnabledScripts.Count} script settings");
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"[UserScriptSettings] Failed to load settings: {ex.Message}");
            }

            return new UserScriptSettings();
        }

        /// <summary>
        /// Saves settings to disk
        /// </summary>
        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(this, JsonOptions);
                File.WriteAllText(SettingsFilePath, json);
                DebugLogger.Log($"[UserScriptSettings] Saved {EnabledScripts.Count} script settings");
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"[UserScriptSettings] Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the enabled state for a script, defaulting to false if not found
        /// </summary>
        public bool IsEnabled(string fileName)
        {
            return EnabledScripts.TryGetValue(fileName, out var enabled) && enabled;
        }

        /// <summary>
        /// Sets the enabled state for a script
        /// </summary>
        public void SetEnabled(string fileName, bool enabled)
        {
            EnabledScripts[fileName] = enabled;
        }
    }
}
