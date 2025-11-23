using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using RebelShipBrowser.Services;
using Forms = System.Windows.Forms;

namespace RebelShipBrowser
{
    public partial class MainWindow : Window
    {
        private const string TargetUrl = "https://shippingmanager.cc";
        private const string TargetDomain = ".shippingmanager.cc";
        private const string CookieName = "shipping_manager_session";

        private static readonly string WebViewUserDataFolder = Path.Combine(Path.GetTempPath(), "RebelShipBrowser_WebView2");

        private Forms.NotifyIcon? _trayIcon;
        private Forms.ContextMenuStrip? _trayMenu;
        private string? _sessionCookie;
        private bool _isClosingCompletely;
        private bool _webViewInitialized;
        private DispatcherTimer? _headerHideTimer;

        public MainWindow()
        {
            InitializeComponent();
            VersionText.Text = $"v{GetVersion()}";
            InitializeHeaderTimer();
        }

        private void InitializeHeaderTimer()
        {
            _headerHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _headerHideTimer.Tick += (s, e) =>
            {
                _headerHideTimer.Stop();
                HideHeader();
            };
        }

        private void StartHeaderTimer()
        {
            _headerHideTimer?.Stop();
            _headerHideTimer?.Start();
        }

        private void HideHeader()
        {
            HeaderFull.Visibility = Visibility.Collapsed;
            HeaderToggle.Visibility = Visibility.Visible;
        }

        private void ShowHeader()
        {
            HeaderFull.Visibility = Visibility.Visible;
            HeaderToggle.Visibility = Visibility.Collapsed;
            StartHeaderTimer();
        }

        private void HideHeaderButton_Click(object sender, RoutedEventArgs e)
        {
            _headerHideTimer?.Stop();
            HideHeader();
        }

        private void HeaderToggle_Click(object sender, MouseButtonEventArgs e)
        {
            ShowHeader();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Cleanup any traces from previous sessions
            PerformStartupCleanup();

            InitializeTrayIcon();
            await InitializeAndLoginAsync();
            StartHeaderTimer();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_isClosingCompletely)
            {
                e.Cancel = true;
                Hide();
                ShowTrayBalloon("RebelShip Browser", "Running in background. Double-click to restore.");
            }
            else
            {
                CleanupTrayIcon();
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                ShowTrayBalloon("RebelShip Browser", "Minimized to tray. Double-click to restore.");
            }
        }

        #region Tray Icon

        private void InitializeTrayIcon()
        {
            _trayMenu = new Forms.ContextMenuStrip();
            _trayMenu.Items.Add("Show Window", null, OnTrayShowClick);
            _trayMenu.Items.Add("Refresh Page", null, OnTrayRefreshClick);
            _trayMenu.Items.Add("Re-Login from Steam", null, OnTrayReLoginClick);
            _trayMenu.Items.Add("-");
            _trayMenu.Items.Add($"Version {GetVersion()}", null, null);
            _trayMenu.Items.Add("-");
            _trayMenu.Items.Add("Exit", null, OnTrayExitClick);

            _trayIcon = new Forms.NotifyIcon
            {
                Text = $"RebelShip Browser v{GetVersion()}",
                Visible = true,
                ContextMenuStrip = _trayMenu
            };

            LoadTrayIcon();

            _trayIcon.DoubleClick += OnTrayDoubleClick;
        }

        private void LoadTrayIcon()
        {
            if (_trayIcon == null) return;

            try
            {
                // Try to load from file first (for runtime)
                var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "icon.ico");
                if (File.Exists(iconPath))
                {
                    _trayIcon.Icon = new System.Drawing.Icon(iconPath);
                    return;
                }

                // Try embedded resource
                var assembly = Assembly.GetExecutingAssembly();
                var resourceNames = assembly.GetManifestResourceNames();
                var iconResourceName = resourceNames.FirstOrDefault(n => n.EndsWith("icon.ico", StringComparison.OrdinalIgnoreCase));

                if (iconResourceName != null)
                {
                    using var stream = assembly.GetManifestResourceStream(iconResourceName);
                    if (stream != null)
                    {
                        _trayIcon.Icon = new System.Drawing.Icon(stream);
                        return;
                    }
                }

                // Fallback to system icon
                _trayIcon.Icon = System.Drawing.SystemIcons.Application;
            }
            catch
            {
                _trayIcon.Icon = System.Drawing.SystemIcons.Application;
            }
        }

        private void CleanupTrayIcon()
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            _trayMenu?.Dispose();
            _trayMenu = null;
        }

        private void ShowTrayBalloon(string title, string text)
        {
            _trayIcon?.ShowBalloonTip(2000, title, text, Forms.ToolTipIcon.Info);
        }

        private void OnTrayDoubleClick(object? sender, EventArgs e)
        {
            ShowWindow();
        }

        private void OnTrayShowClick(object? sender, EventArgs e)
        {
            ShowWindow();
        }

        private void OnTrayRefreshClick(object? sender, EventArgs e)
        {
            ShowWindow();
            RefreshPage();
        }

        private async void OnTrayReLoginClick(object? sender, EventArgs e)
        {
            ShowWindow();
            await ReLoginAsync();
        }

        private void OnTrayExitClick(object? sender, EventArgs e)
        {
            ExitApplication();
        }

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            Focus();
            ShowHeader();
        }

        #endregion

        #region WebView and Login

        private async Task InitializeAndLoginAsync()
        {
            try
            {
                UpdateStatus("Checking Steam installation...", StatusType.Warning);

                if (!SteamService.IsSteamInstalled())
                {
                    UpdateStatus("Steam not installed or never visited shippingmanager.cc", StatusType.Error);
                    ShowError("Steam is not installed or has never visited shippingmanager.cc.\n\nPlease open Steam and login to shippingmanager.cc at least once.", "RebelShip Browser - Error");
                    return;
                }

                UpdateStatus("Extracting session from Steam...", StatusType.Warning);
                LoadingText.Text = "Extracting session from Steam...";

                bool steamWasRunning = SteamService.IsSteamRunning();

                if (steamWasRunning)
                {
                    LoadingText.Text = "Stopping Steam temporarily...";
                    UpdateStatus("Stopping Steam temporarily...", StatusType.Warning);
                }

                _sessionCookie = await SteamService.ExtractWithSteamManagementAsync(restartSteam: true);

                if (string.IsNullOrEmpty(_sessionCookie))
                {
                    UpdateStatus("Failed to extract session cookie", StatusType.Error);
                    ShowError("Could not extract session cookie from Steam.\n\nPlease ensure you are logged into shippingmanager.cc in Steam's browser.");
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    return;
                }

                LoadingText.Text = "Initializing browser...";
                UpdateStatus("Initializing browser...", StatusType.Warning);

                await InitializeWebViewAsync();

                LoadingText.Text = "Logging in...";
                UpdateStatus("Injecting session cookie...", StatusType.Warning);

                InjectCookieAndNavigate(_sessionCookie);

                LoadingOverlay.Visibility = Visibility.Collapsed;
                UpdateStatus("Connected to ShippingManager", StatusType.Success);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}", StatusType.Error);
                LoadingOverlay.Visibility = Visibility.Collapsed;
                ShowError($"An error occurred during initialization:\n\n{ex.Message}");
            }
        }

        private async Task InitializeWebViewAsync()
        {
            if (_webViewInitialized)
            {
                return;
            }

            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: WebViewUserDataFolder);
            await WebView.EnsureCoreWebView2Async(env);

            WebView.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            WebView.CoreWebView2.NavigationCompleted += WebView_NavigationCompleted;

            _webViewInitialized = true;
        }

        private void InjectCookieAndNavigate(string sessionCookie)
        {
            var cookieManager = WebView.CoreWebView2.CookieManager;
            var cookie = cookieManager.CreateCookie(CookieName, sessionCookie, TargetDomain, "/");
            cookie.IsSecure = true;
            cookie.IsHttpOnly = true;

            cookieManager.AddOrUpdateCookie(cookie);

            WebView.Source = new Uri(TargetUrl);
        }

        private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                UpdateStatus($"Loaded: {WebView.Source}", StatusType.Success);
            }
            else
            {
                UpdateStatus($"Navigation failed: {e.WebErrorStatus}", StatusType.Error);
            }
        }

        #endregion

        #region Button Handlers

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshPage();
        }

        private async void RestartLoginButton_Click(object sender, RoutedEventArgs e)
        {
            await ReLoginAsync();
        }

        private void RefreshPage()
        {
            if (_webViewInitialized)
            {
                UpdateStatus("Refreshing page...", StatusType.Warning);
                WebView.CoreWebView2.Reload();
            }
        }

        private async Task ReLoginAsync()
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingText.Text = "Re-extracting session from Steam...";

            if (_webViewInitialized)
            {
                WebView.CoreWebView2.CookieManager.DeleteAllCookies();
            }

            await InitializeAndLoginAsync();
        }

        #endregion

        #region Helpers

        private enum StatusType
        {
            Success,
            Warning,
            Error
        }

        private void UpdateStatus(string message, StatusType type)
        {
            StatusText.Text = message;

            var brush = type switch
            {
                StatusType.Success => (SolidColorBrush)FindResource("SuccessColor"),
                StatusType.Warning => (SolidColorBrush)FindResource("WarningColor"),
                StatusType.Error => (SolidColorBrush)FindResource("ErrorColor"),
                _ => (SolidColorBrush)FindResource("TextSecondary")
            };

            StatusIndicator.Fill = brush;
        }

        private void ShowError(string message, string title = "RebelShip Browser - Error")
        {
            System.Windows.MessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }

        private void ExitApplication()
        {
            _isClosingCompletely = true;

            // Cleanup traces before exit
            CleanupTraces();

            Close();
            System.Windows.Application.Current.Shutdown();
        }

        private void CleanupTraces()
        {
            try
            {
                // Dispose WebView2 to release file handles
                if (_webViewInitialized)
                {
                    WebView.Dispose();
                    _webViewInitialized = false;
                }

                // Wait a moment for file handles to be released
                System.Threading.Thread.Sleep(500);

                // Delete WebView2 user data folder
                if (Directory.Exists(WebViewUserDataFolder))
                {
                    Directory.Delete(WebViewUserDataFolder, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup - ignore errors
                // Schedule cleanup on next startup as fallback
                ScheduleCleanupOnStartup();
            }
        }

        private static void ScheduleCleanupOnStartup()
        {
            try
            {
                // Write a marker file to indicate cleanup needed on next startup
                var markerPath = Path.Combine(Path.GetTempPath(), "RebelShipBrowser_cleanup.marker");
                File.WriteAllText(markerPath, WebViewUserDataFolder);
            }
            catch
            {
                // Ignore
            }
        }

        private static void PerformStartupCleanup()
        {
            try
            {
                var markerPath = Path.Combine(Path.GetTempPath(), "RebelShipBrowser_cleanup.marker");
                if (File.Exists(markerPath))
                {
                    var folderToDelete = File.ReadAllText(markerPath);
                    if (Directory.Exists(folderToDelete))
                    {
                        Directory.Delete(folderToDelete, recursive: true);
                    }
                    File.Delete(markerPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private static string GetVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString(3) ?? "0.0.0";
        }

        #endregion
    }
}
