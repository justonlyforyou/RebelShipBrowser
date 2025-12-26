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
    public partial class MainWindow : Window, IDisposable
    {
        private bool _disposed;
        private const string TargetUrl = "https://shippingmanager.cc";
        private const string TargetDomain = ".shippingmanager.cc";
        private const string CookieName = "shipping_manager_session";

        private static readonly string WebViewUserDataFolder = Path.Combine(Path.GetTempPath(), "RebelShipBrowser_WebView2");
        private static readonly string SettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RebelShipBrowser"
        );
        private static readonly string GpuSettingsFile = Path.Combine(SettingsFolder, "gpu_enabled.txt");

        private Forms.NotifyIcon? _trayIcon;
        private Forms.ContextMenuStrip? _trayMenu;
        private string? _sessionCookie;
        private bool _isClosingCompletely;
        private bool _webViewInitialized;
        private DispatcherTimer? _headerHideTimer;
        private readonly UserScriptService _userScriptService = new();

        private static bool IsGpuEnabled
        {
            get
            {
                if (!File.Exists(GpuSettingsFile))
                {
                    return false; // Default: GPU disabled for screenshot compatibility
                }
                return File.ReadAllText(GpuSettingsFile).Trim() == "1";
            }
            set
            {
                if (!Directory.Exists(SettingsFolder))
                {
                    Directory.CreateDirectory(SettingsFolder);
                }
                File.WriteAllText(GpuSettingsFile, value ? "1" : "0");
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            VersionText.Text = $"v{GetVersion()}";
            InitializeHeaderTimer();
            UpdateGpuButtonText();
        }

        private void UpdateGpuButtonText()
        {
            GpuToggleButton.Content = IsGpuEnabled ? "GPU: On" : "GPU: Off";
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
            // Clear debug log on startup
            DebugLogger.ClearLog();
            DebugLogger.Log("=== RebelShip Browser starting ===");

            // Cleanup any traces from previous sessions
            PerformStartupCleanup();

            InitializeTrayIcon();

            // Check for updates in background
            _ = CheckForUpdatesAsync();

            await InitializeWithLoginDialogAsync();
            StartHeaderTimer();
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                var updateAvailable = await UpdateService.CheckForUpdateAsync();
                if (updateAvailable)
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateButton.Content = $"Update to v{UpdateService.LatestVersion}";
                        UpdateButton.Visibility = Visibility.Visible;
                    });
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UpdateCheck] Error: {ex.Message}");
            }
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

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                RefreshPage();
                e.Handled = true;
            }
        }

        #region Tray Icon

        private void InitializeTrayIcon()
        {
            _trayMenu = new Forms.ContextMenuStrip();
            _trayMenu.Items.Add("Show Window", null, OnTrayShowClick);
            _trayMenu.Items.Add("Refresh Page", null, OnTrayRefreshClick);
            _trayMenu.Items.Add("Logout / Switch Account", null, OnTrayReLoginClick);
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
            if (_trayIcon == null)
            {
                return;
            }

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

        private async Task InitializeWithLoginDialogAsync()
        {
            try
            {
                // Always show login method dialog first
                LoadingOverlay.Visibility = Visibility.Collapsed;

                var dialog = new LoginMethodDialog
                {
                    Owner = this
                };

                var result = dialog.ShowDialog();
                if (result != true || dialog.SelectedMethod == LoginMethod.None)
                {
                    // User cancelled - exit app
                    ExitApplication();
                    return;
                }

                LoadingOverlay.Visibility = Visibility.Visible;

                if (dialog.SelectedMethod == LoginMethod.Steam)
                {
                    await LoginWithSteamAsync();
                }
                else
                {
                    await LoginWithBrowserAsync();
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}", StatusType.Error);
                LoadingOverlay.Visibility = Visibility.Collapsed;
                ShowError($"An error occurred during initialization:\n\n{ex.Message}");
            }
        }

        private async Task LoginWithSteamAsync()
        {
            UpdateStatus("Checking Steam installation...", StatusType.Warning);

            if (!SteamService.IsSteamInstalled())
            {
                UpdateStatus("Steam not installed", StatusType.Error);
                ShowError("Steam is not installed or has never visited shippingmanager.cc.\n\nPlease use Browser Login instead, or open Steam and login to shippingmanager.cc first.", "RebelShip Browser - Error");
                LoadingOverlay.Visibility = Visibility.Collapsed;

                // Show dialog again
                await InitializeWithLoginDialogAsync();
                return;
            }

            // Step 1: Stop Steam if running so we can read the cookie database
            bool steamWasRunning = SteamService.IsSteamRunning();
            if (steamWasRunning)
            {
                LoadingText.Text = "Stopping Steam to check session...";
                UpdateStatus("Stopping Steam to check session...", StatusType.Warning);
                await SteamService.ExitSteamGracefullyAsync();
                await Task.Delay(1000);
            }

            // Step 2: Check if valid cookie already exists
            LoadingText.Text = "Checking for existing session...";
            UpdateStatus("Checking for existing session...", StatusType.Warning);

            _sessionCookie = await SteamService.ExtractSessionCookieAsync();

            if (!string.IsNullOrEmpty(_sessionCookie))
            {
                LoadingText.Text = "Validating session...";
                UpdateStatus("Validating session...", StatusType.Warning);

                var isValid = await CookieStorage.ValidateCookieAsync(_sessionCookie);
                if (isValid)
                {
                    DebugLogger.Log("[LoginWithSteam] Existing cookie is valid, using it");

                    // Restart Steam if it was running
                    if (steamWasRunning)
                    {
                        SteamService.RestartSteamMinimized();
                    }

                    LoadingText.Text = "Initializing browser...";
                    await InitializeWebViewAsync();
                    InjectCookieAndNavigate(_sessionCookie);
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    UpdateStatus("Connected to ShippingManager", StatusType.Success);
                    return;
                }

                DebugLogger.Log("[LoginWithSteam] Existing cookie is invalid, need fresh login");
                _sessionCookie = null;
            }

            // Step 3: No valid cookie - ask user to start game manually
            ShowInfo("No valid Steam session found.\n\nPlease do the following:\n\n1. Start ShippingManager via Steam\n2. Login to the game\n3. Play for about 5 minutes\n4. Restart RebelShip Browser\n\nNote: Steam saves the session cookie with a delay.\nThis process ensures a valid session is available.", "RebelShip Browser - Steam Login Required");

            // Exit the application - user needs to restart after playing
            ExitApplication();
        }

        private async Task LoginWithBrowserAsync()
        {
            LoadingText.Text = "Checking for saved session...";
            UpdateStatus("Checking for saved browser session...", StatusType.Warning);

            // Check for saved cookie from previous browser login
            var savedCookie = await CookieStorage.LoadAndValidateCookieAsync();
            if (!string.IsNullOrEmpty(savedCookie))
            {
                LoadingText.Text = "Auto-login with saved session...";
                UpdateStatus("Auto-login with saved session...", StatusType.Warning);

                _sessionCookie = savedCookie;
                await InitializeWebViewAsync();
                InjectCookieAndNavigate(_sessionCookie);

                LoadingOverlay.Visibility = Visibility.Collapsed;
                UpdateStatus("Connected to ShippingManager (saved session)", StatusType.Success);
                return;
            }

            // No saved cookie - open browser for manual login
            LoadingText.Text = "Starting browser...";
            UpdateStatus("Starting browser for manual login...", StatusType.Warning);

            await InitializeWebViewAsync();

            // Enable cookie monitoring to capture login
            StartCookieMonitoring();

            WebView.Source = new Uri(TargetUrl);

            LoadingOverlay.Visibility = Visibility.Collapsed;
            UpdateStatus("Please login to ShippingManager", StatusType.Warning);
        }

        private DispatcherTimer? _cookieMonitorTimer;

        private void StartCookieMonitoring()
        {
            _cookieMonitorTimer?.Stop();
            _cookieMonitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _cookieMonitorTimer.Tick += async (s, e) => await CheckForLoginCookieAsync();
            _cookieMonitorTimer.Start();
        }

        private void StopCookieMonitoring()
        {
            _cookieMonitorTimer?.Stop();
            _cookieMonitorTimer = null;
        }

        private async Task CheckForLoginCookieAsync()
        {
            if (!_webViewInitialized || _sessionCookie != null)
            {
                return;
            }

            try
            {
                var cookies = await WebView.CoreWebView2.CookieManager.GetCookiesAsync(TargetUrl);
                foreach (var cookie in cookies)
                {
                    if (cookie.Name == CookieName && !string.IsNullOrEmpty(cookie.Value))
                    {
                        // Validate the cookie
                        var isValid = await CookieStorage.ValidateCookieAsync(cookie.Value);
                        if (isValid)
                        {
                            _sessionCookie = cookie.Value;
                            CookieStorage.SaveCookie(_sessionCookie);
                            StopCookieMonitoring();
                            UpdateStatus("Login successful! Session saved.", StatusType.Success);
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[CookieMonitor] Error: {ex.Message}");
            }
        }

        private async Task InitializeWebViewAsync()
        {
            if (_webViewInitialized)
            {
                return;
            }

            // GPU disabled by default for screenshot compatibility, can be toggled via button
            CoreWebView2Environment env;
            if (IsGpuEnabled)
            {
                env = await CoreWebView2Environment.CreateAsync(userDataFolder: WebViewUserDataFolder);
            }
            else
            {
                var options = new CoreWebView2EnvironmentOptions("--disable-gpu");
                env = await CoreWebView2Environment.CreateAsync(userDataFolder: WebViewUserDataFolder, options: options);
            }
            await WebView.EnsureCoreWebView2Async(env);

            WebView.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            WebView.ZoomFactor = 0.8;

            WebView.CoreWebView2.NavigationCompleted += WebView_NavigationCompleted;

            // Register document-start userscripts
            await RegisterDocumentStartScriptsAsync();

            _webViewInitialized = true;
        }

        private async Task RegisterDocumentStartScriptsAsync()
        {
            _userScriptService.LoadAllScripts();

            foreach (var script in _userScriptService.Scripts)
            {
                if (!script.Enabled || script.RunAt != RunAt.DocumentStart)
                {
                    continue;
                }

                var code = script.GetCodeWithoutMetadata();
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                try
                {
                    // Build URL pattern check - scripts run on all pages but check match patterns
                    var matchPatterns = script.Match.Select(p => $"'{p}'").ToList();
                    var matchCheck = matchPatterns.Count > 0
                        ? $"var patterns = [{string.Join(",", matchPatterns)}]; var url = window.location.href; var matches = patterns.some(function(p) {{ var regex = new RegExp(p.replace(/\\*/g, '.*')); return regex.test(url); }}); if (!matches) return;"
                        : "";

                    var wrappedCode = $"(function() {{ {matchCheck} {code} }})();";

                    await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(wrappedCode);
                    DebugLogger.Log($"[UserScript] Registered '{script.Name}' for document-start");
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[UserScript] Error registering '{script.Name}': {ex.Message}");
                }
            }
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

        private async void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                UpdateStatus($"Loaded: {WebView.Source}", StatusType.Success);

                // Auto-accept cookie consent
                await AutoAcceptCookieConsentAsync();

                // Inject userscripts
                await InjectUserScriptsAsync();
            }
            else
            {
                UpdateStatus($"Navigation failed: {e.WebErrorStatus}", StatusType.Error);
            }
        }

        private async Task AutoAcceptCookieConsentAsync()
        {
            if (!_webViewInitialized)
            {
                return;
            }

            try
            {
                // Click the "I accept" button if it exists
                var script = @"
                    (function() {
                        var buttons = document.querySelectorAll('button');
                        for (var i = 0; i < buttons.length; i++) {
                            if (buttons[i].textContent.trim() === 'I accept') {
                                buttons[i].click();
                                return true;
                            }
                        }
                        return false;
                    })();
                ";
                await WebView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AutoAccept] Error: {ex.Message}");
            }
        }

        private async Task InjectUserScriptsAsync()
        {
            if (!_webViewInitialized || WebView.Source == null)
            {
                return;
            }

            _userScriptService.LoadAllScripts();

            DebugLogger.Log($"[UserScript] Total scripts loaded: {_userScriptService.Scripts.Count}");
            foreach (var s in _userScriptService.Scripts)
            {
                DebugLogger.Log($"[UserScript] Script '{s.Name}' - Enabled: {s.Enabled}, RunAt: {s.RunAt}");
            }

            var matchingScripts = _userScriptService.GetScriptsForUrl(WebView.Source).ToList();
            DebugLogger.Log($"[UserScript] Matching scripts for {WebView.Source}: {matchingScripts.Count}");

            foreach (var script in matchingScripts)
            {
                try
                {
                    var code = script.GetCodeWithoutMetadata();
                    if (string.IsNullOrWhiteSpace(code))
                    {
                        continue;
                    }

                    // Wrap in IIFE to avoid polluting global scope
                    var wrappedCode = $"(function() {{ {code} }})();";

                    switch (script.RunAt)
                    {
                        case RunAt.DocumentEnd:
                            await WebView.CoreWebView2.ExecuteScriptAsync(wrappedCode);
                            DebugLogger.Log($"[UserScript] Injected '{script.Name}' at document-end");
                            break;

                        case RunAt.DocumentIdle:
                            // Run after a short delay to let the page settle
                            var idleScript = $"setTimeout(function() {{ {code} }}, 100);";
                            await WebView.CoreWebView2.ExecuteScriptAsync(idleScript);
                            DebugLogger.Log($"[UserScript] Injected '{script.Name}' at document-idle");
                            break;

                        case RunAt.DocumentStart:
                            // document-start scripts should be registered separately
                            // They run before the DOM is created, handled in RegisterDocumentStartScripts
                            break;
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[UserScript] Error injecting '{script.Name}': {ex.Message}");
                }
            }
        }

        #endregion

        #region Button Handlers

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshPage();
        }

        private void ScriptsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ScriptManagerDialog(_userScriptService)
            {
                Owner = this
            };
            dialog.ShowDialog();
        }

        private void GpuToggleButton_Click(object sender, RoutedEventArgs e)
        {
            var newValue = !IsGpuEnabled;
            IsGpuEnabled = newValue;
            UpdateGpuButtonText();

            var result = System.Windows.MessageBox.Show(
                $"GPU acceleration has been {(newValue ? "enabled" : "disabled")}.\n\n" +
                "The browser needs to restart for this change to take effect.\n\n" +
                "Restart now?",
                "Restart Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                // Restart the application
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    Process.Start(exePath);
                    _isClosingCompletely = true;
                    System.Windows.Application.Current.Shutdown();
                }
            }
        }

        private async void RestartLoginButton_Click(object sender, RoutedEventArgs e)
        {
            await ReLoginAsync();
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateButton.IsEnabled = false;
            UpdateButton.Content = "Downloading...";

            var success = await UpdateService.DownloadAndInstallUpdateAsync();
            if (success)
            {
                // Exit the app - installer will handle the rest
                ExitApplication();
            }
            else
            {
                UpdateButton.Content = "Update Failed";
                UpdateButton.IsEnabled = true;
                ShowError("Failed to download the update.\n\nPlease try again or download manually from GitHub.");
            }
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
            LoadingText.Text = "Logging out...";

            // Clear saved cookie
            CookieStorage.DeleteCookie();
            _sessionCookie = null;

            // Clear browser cookies
            if (_webViewInitialized)
            {
                WebView.CoreWebView2.CookieManager.DeleteAllCookies();
            }

            // Stop any ongoing cookie monitoring
            StopCookieMonitoring();

            // Show login dialog again
            await InitializeWithLoginDialogAsync();
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

        private static void ShowError(string message, string title = "RebelShip Browser - Error")
        {
            System.Windows.MessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }

        private static void ShowInfo(string message, string title = "RebelShip Browser")
        {
            System.Windows.MessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Information
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

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _trayIcon?.Dispose();
                _trayMenu?.Dispose();
                _headerHideTimer?.Stop();
                _cookieMonitorTimer?.Stop();
            }

            _disposed = true;
        }

        #endregion
    }
}
