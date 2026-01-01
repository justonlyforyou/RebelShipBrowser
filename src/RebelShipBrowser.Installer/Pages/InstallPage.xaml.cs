using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Controls;
using Microsoft.Win32;

namespace RebelShipBrowser.Installer.Pages
{
    public partial class InstallPage : Page
    {
        public InstallPage()
        {
            InitializeComponent();
        }

        public async Task RunInstallationAsync()
        {
            await Task.Run(async () =>
            {
                // Step 1: Close running instances
                await UpdateProgressAsync("Checking for running instances...", 5);
                CloseRunningInstances();

                // Step 2: Extract payload
                await UpdateProgressAsync("Extracting files...", 10);
                ExtractPayload();

                // Step 3: Install default userscripts
                await UpdateProgressAsync("Installing default scripts...", 40);
                InstallDefaultUserScripts();

                // Step 4: Create shortcuts
                await UpdateProgressAsync("Creating shortcuts...", 60);
                CreateShortcuts();

                // Step 5: Register uninstaller
                await UpdateProgressAsync("Registering application...", 80);
                RegisterUninstaller();

                // Complete
                await UpdateProgressAsync("Installation complete!", 100);
            });
        }

        private async Task UpdateProgressAsync(string status, int progress)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                StatusText.Text = status;
                ProgressBar.Value = progress;
                ProgressText.Text = $"{progress}%";
            });

            // Small delay to show progress
            await Task.Delay(300);
        }

        private static void CloseRunningInstances()
        {
            try
            {
                var processes = Process.GetProcessesByName("RebelShipBrowser");
                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                    }
                    catch
                    {
                        // Ignore errors killing individual processes
                    }
                }

                // Wait a bit for file handles to be released
                if (processes.Length > 0)
                {
                    Thread.Sleep(1000);
                }
            }
            catch
            {
                // Ignore errors finding processes
            }
        }

        private static void ExtractPayload()
        {
            var installPath = InstallerSettings.InstallPath;

            // Create install directory
            Directory.CreateDirectory(installPath);

            // Clear old bundled scripts before extraction (renamed/deleted scripts cleanup)
            // User's enabled/disabled settings are preserved in UserScriptSettings.json
            var bundledPath = InstallerSettings.BundledScriptsPath;
            if (Directory.Exists(bundledPath))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(bundledPath, "*.js"))
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            // Try to extract embedded payload
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("app-payload.zip", StringComparison.OrdinalIgnoreCase));

            if (resourceName != null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                    foreach (var entry in archive.Entries)
                    {
                        // Sanitize path to prevent path traversal attacks (CA5389)
                        var sanitizedName = SanitizeEntryPath(entry.FullName);
                        if (string.IsNullOrEmpty(sanitizedName))
                        {
                            continue;
                        }

                        var destPath = Path.GetFullPath(Path.Combine(installPath, sanitizedName));

                        // Ensure destination is within install directory
                        if (!destPath.StartsWith(installPath, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var destDir = Path.GetDirectoryName(destPath);

                        if (!string.IsNullOrEmpty(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }

                        if (!string.IsNullOrEmpty(entry.Name))
                        {
                            entry.ExtractToFile(destPath, overwrite: true);
                        }
                    }
                }
            }
            else
            {
                // Development mode: Copy from build output
                var sourceDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "RebelShipBrowser", "bin", "Release", "net8.0-windows10.0.19041.0");
                if (Directory.Exists(sourceDir))
                {
                    CopyDirectory(sourceDir, installPath);
                }
            }
        }

        private static void InstallDefaultUserScripts()
        {
            // Create directories
            Directory.CreateDirectory(InstallerSettings.BundledScriptsPath);
            Directory.CreateDirectory(InstallerSettings.CustomScriptsPath);

            // Migrate existing scripts from flat structure to new structure
            MigrateExistingScripts();
        }

        // Known bundled script filenames (scripts we ship)
        private static readonly HashSet<string> KnownBundledScripts = new(StringComparer.OrdinalIgnoreCase)
        {
            "auto-depart.user.js",
            "bunker-price-display.user.js",
            "buy-vip-vessel.user.js",
            "depart-all-loop.user.js",
            "export-messages.user.js",
            "export-vessels-csv.user.js",
            "forecast-calendar.user.js",
            "map-unlock.user.js",
            "reputation-display.user.js",
            "save-vessel-history.user.js",
            "vessel-cart.user.js"
        };

        private static void MigrateExistingScripts()
        {
            var userScriptsPath = InstallerSettings.UserScriptsPath;

            // Check for scripts in the root userscripts folder (old structure)
            var rootScripts = Directory.GetFiles(userScriptsPath, "*.js", SearchOption.TopDirectoryOnly);

            foreach (var scriptPath in rootScripts)
            {
                var fileName = Path.GetFileName(scriptPath);
                var destFolder = KnownBundledScripts.Contains(fileName)
                    ? InstallerSettings.BundledScriptsPath
                    : InstallerSettings.CustomScriptsPath;

                var destPath = Path.Combine(destFolder, fileName);

                try
                {
                    // Move to appropriate folder
                    if (File.Exists(destPath))
                    {
                        File.Delete(destPath);
                    }
                    File.Move(scriptPath, destPath);
                }
                catch
                {
                    // Ignore migration errors for individual files
                }
            }
        }

        private static string? SanitizeEntryPath(string entryPath)
        {
            if (string.IsNullOrEmpty(entryPath))
            {
                return null;
            }

            // Remove any path traversal attempts
            var sanitized = entryPath
                .Replace("..", string.Empty, StringComparison.Ordinal)
                .Replace(":", string.Empty, StringComparison.Ordinal);

            // Normalize separators
            sanitized = sanitized.Replace('/', Path.DirectorySeparatorChar);

            // Remove leading separators
            sanitized = sanitized.TrimStart(Path.DirectorySeparatorChar);

            return string.IsNullOrEmpty(sanitized) ? null : sanitized;
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }

        private static void CreateShortcuts()
        {
            var exePath = InstallerSettings.ExePath;

            // Start Menu shortcut using PowerShell
            CreateShortcutViaPowerShell(InstallerSettings.StartMenuPath, exePath, "RebelShip Browser");

            // Desktop shortcut using PowerShell
            CreateShortcutViaPowerShell(InstallerSettings.DesktopPath, exePath, "RebelShip Browser");
        }

        private static void CreateShortcutViaPowerShell(string shortcutPath, string targetPath, string description)
        {
            try
            {
                var script = $@"
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut('{shortcutPath.Replace("'", "''", StringComparison.Ordinal)}')
$Shortcut.TargetPath = '{targetPath.Replace("'", "''", StringComparison.Ordinal)}'
$Shortcut.WorkingDirectory = '{Path.GetDirectoryName(targetPath)?.Replace("'", "''", StringComparison.Ordinal)}'
$Shortcut.Description = '{description}'
$Shortcut.Save()
";
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                process?.WaitForExit(5000);
            }
            catch
            {
                // Ignore shortcut creation errors
            }
        }

        private static string GetVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString(3) ?? "0.0.0";
        }

        private static void RegisterUninstaller()
        {
            try
            {
                var uninstallKey = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall\RebelShipBrowser"
                );

                if (uninstallKey != null)
                {
                    var exePath = InstallerSettings.ExePath;
                    var installPath = InstallerSettings.InstallPath;
                    var setupPath = Environment.ProcessPath ?? "";

                    uninstallKey.SetValue("DisplayName", "RebelShip Browser");
                    uninstallKey.SetValue("DisplayVersion", GetVersion());
                    uninstallKey.SetValue("Publisher", "justonlyforyou");
                    uninstallKey.SetValue("InstallLocation", installPath);
                    uninstallKey.SetValue("DisplayIcon", exePath);
                    uninstallKey.SetValue("UninstallString", $"\"{setupPath}\" /uninstall");
                    uninstallKey.SetValue("NoModify", 1);
                    uninstallKey.SetValue("NoRepair", 1);

                    uninstallKey.Close();
                }
            }
            catch
            {
                // Ignore registry errors
            }
        }
    }
}
