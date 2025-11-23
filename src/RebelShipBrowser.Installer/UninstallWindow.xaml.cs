using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace RebelShipBrowser.Installer
{
    public partial class UninstallWindow : Window
    {
        private static readonly string InstallPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RebelShipBrowser"
        );

        private static readonly string StartMenuPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            "RebelShip Browser.lnk"
        );

        private static readonly string DesktopPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "RebelShip Browser.lnk"
        );

        public UninstallWindow()
        {
            InitializeComponent();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            // Update UI
            TitleText.Text = "Uninstalling...";
            MessageText.Text = "Please wait while RebelShip Browser is being uninstalled.";
            ProgressBar.Visibility = Visibility.Visible;
            UninstallButton.IsEnabled = false;
            CancelButton.IsEnabled = false;

            try
            {
                await Task.Run(() =>
                {
                    // Kill running instances
                    KillRunningInstances();

                    // Delete shortcuts
                    DeleteShortcuts();

                    // Delete install directory
                    DeleteInstallDirectory();

                    // Remove registry entries
                    RemoveRegistryEntries();
                });

                // Show completion
                TitleText.Text = "Uninstall Complete";
                MessageText.Text = "RebelShip Browser has been successfully uninstalled.";
                ProgressBar.Visibility = Visibility.Collapsed;
                UninstallButton.Content = "Close";
                UninstallButton.IsEnabled = true;
                UninstallButton.Click -= UninstallButton_Click;
                UninstallButton.Click += (s, args) => Close();
                CancelButton.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Uninstall failed:\n\n{ex.Message}",
                    "Uninstall Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                Close();
            }
        }

        private static void KillRunningInstances()
        {
            try
            {
                foreach (var process in Process.GetProcessesByName("RebelShipBrowser"))
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
            }
            catch
            {
                // Ignore kill errors
            }
        }

        private static void DeleteShortcuts()
        {
            try
            {
                if (File.Exists(StartMenuPath))
                {
                    File.Delete(StartMenuPath);
                }

                if (File.Exists(DesktopPath))
                {
                    File.Delete(DesktopPath);
                }
            }
            catch
            {
                // Ignore delete errors
            }
        }

        private static void DeleteInstallDirectory()
        {
            try
            {
                if (Directory.Exists(InstallPath))
                {
                    Directory.Delete(InstallPath, recursive: true);
                }
            }
            catch
            {
                // Ignore delete errors - files may be in use
            }
        }

        private static void RemoveRegistryEntries()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall\RebelShipBrowser",
                    throwOnMissingSubKey: false
                );
            }
            catch
            {
                // Ignore registry errors
            }
        }
    }
}
