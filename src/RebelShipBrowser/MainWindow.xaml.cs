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

        private async void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                UpdateStatus($"Loaded: {WebView.Source}", StatusType.Success);

                // Inject premium map unlocks on map pages
                if (WebView.Source?.ToString().Contains("shippingmanager.cc", StringComparison.OrdinalIgnoreCase) == true)
                {
                    await InjectPremiumMapUnlockAsync();
                }
            }
            else
            {
                UpdateStatus($"Navigation failed: {e.WebErrorStatus}", StatusType.Error);
            }
        }

        private async Task InjectPremiumMapUnlockAsync()
        {
            // Direct tile replacement approach from cheatsheet - hijack menu clicks
            const string mapUnlockScript = @"
(function() {
    if (window._rebelShipMapUnlockActive) return;
    window._rebelShipMapUnlockActive = true;

    const TOKEN = 'sk.eyJ1Ijoic2hqb3J0aCIsImEiOiJjbGV0cHdodGwxaWZnM3NydnlvNHc4cG02In0.D5n6nIFb0JqhGA9lM_jRkw';

    const THEMES = {
        'Dark': { base: 'https://api.mapbox.com/styles/v1/mapbox/dark-v10/tiles', suffix: '' },
        'Light': { base: 'https://api.mapbox.com/styles/v1/mapbox/light-v10/tiles', suffix: '' },
        'Street': { base: 'https://api.mapbox.com/styles/v1/mapbox/streets-v11/tiles', suffix: '' },
        'Satellite': { base: 'https://api.mapbox.com/styles/v1/mapbox/satellite-v9/tiles', suffix: '' },
        'City': { base: 'https://api.mapbox.com/styles/v1/shjorth/ck6hrwoqh0uuy1iqvq5jmcch2/tiles/256', suffix: '@2x' },
        'Sky': { base: 'https://api.mapbox.com/styles/v1/shjorth/ck6hzf3qq11wg1ijsrtfaouxb/tiles/256', suffix: '@2x' }
    };

    let currentObserver = null;
    let currentTheme = null;

    function switchTheme(themeName) {
        const theme = THEMES[themeName];
        if (!theme) return;

        currentTheme = themeName;

        // Stop old observer
        if (currentObserver) {
            currentObserver.disconnect();
            currentObserver = null;
        }

        const tilePane = document.querySelector('.leaflet-tile-pane');
        if (!tilePane) return;

        // Replace all current tiles
        tilePane.querySelectorAll('img').forEach(img => {
            const match = img.src.match(/\/(\d+)\/(\d+)\/(\d+)/);
            if (match) {
                img.src = theme.base + '/' + match[1] + '/' + match[2] + '/' + match[3] + theme.suffix + '?access_token=' + TOKEN;
            }
        });

        // Watch for new tiles
        currentObserver = new MutationObserver(mutations => {
            mutations.forEach(m => {
                m.addedNodes.forEach(node => {
                    if (node.tagName === 'IMG') {
                        const match = node.src.match(/\/(\d+)\/(\d+)\/(\d+)/);
                        if (match) {
                            node.src = theme.base + '/' + match[1] + '/' + match[2] + '/' + match[3] + theme.suffix + '?access_token=' + TOKEN;
                        }
                    }
                });
            });
        });
        currentObserver.observe(tilePane, { childList: true, subtree: true });

        // Unlock zoom range and force tile refresh
        try {
            const app = document.querySelector('#app').__vue_app__;
            const pinia = app._context.provides.pinia || app.config.globalProperties.$pinia;
            const map = pinia._s.get('mapStore').map;
            map.setMinZoom(1);
            map.setMaxZoom(18);

            // Force tiles to reload by invalidating and doing a tiny pan
            map.invalidateSize();
            var center = map.getCenter();
            map.panTo([center.lat + 0.0001, center.lng], {animate: false});
            setTimeout(function() {
                map.panTo([center.lat, center.lng], {animate: false});
            }, 100);
        } catch(e) {}

        console.log('[RebelShip] Switched to', themeName, 'theme');
    }

    function init() {
        // Wait for map to be ready
        const tilePane = document.querySelector('.leaflet-tile-pane');
        if (!tilePane) {
            setTimeout(init, 500);
            return;
        }

        // Start with Dark theme
        switchTheme('Dark');
    }

    // Auto-accept cookies
    function acceptCookies() {
        const btn = document.querySelector('button.dark-green');
        if (btn && btn.textContent.includes('accept')) {
            btn.click();
            console.log('[RebelShip] Clicked cookie accept button');
            return true;
        }
        return false;
    }

    // Try to accept cookies periodically
    let cookieTries = 0;
    const cookieInterval = setInterval(() => {
        if (acceptCookies() || cookieTries++ > 20) {
            clearInterval(cookieInterval);
        }
    }, 500);

    // Unlock tanker ops and metropolis in UI by modifying Pinia user store
    function unlockFeatures() {
        try {
            var app = document.querySelector('#app');
            if (!app || !app.__vue_app__) return;

            var pinia = app.__vue_app__._context.provides.pinia || app.__vue_app__.config.globalProperties.$pinia;
            if (!pinia || !pinia._s) return;

            var userStore = pinia._s.get('user');
            if (!userStore || !userStore.user) return;

            var companyType = userStore.user.company_type;
            if (!companyType) return;

            // Check if already has tanker
            var hasTanker = Array.isArray(companyType)
                ? companyType.includes('tanker')
                : (typeof companyType === 'string' && companyType.indexOf('tanker') >= 0);

            var needsPatch = false;
            var newCompanyType = companyType;
            var newMetropolis = userStore.settings ? userStore.settings.metropolis : 0;

            // Unlock tanker if needed
            if (!hasTanker) {
                newCompanyType = Array.isArray(companyType)
                    ? companyType.slice().concat(['tanker'])
                    : [companyType, 'tanker'];
                needsPatch = true;
                console.log('[RebelShip] Will unlock tanker ops');
            }

            // Unlock metropolis if needed
            if (userStore.settings && !userStore.settings.metropolis) {
                newMetropolis = 1;
                needsPatch = true;
                console.log('[RebelShip] Will unlock metropolis');
            }

            if (needsPatch) {
                userStore.$patch(function(state) {
                    state.user.company_type = newCompanyType;
                    if (state.settings) {
                        state.settings.metropolis = newMetropolis;
                    }
                });
                console.log('[RebelShip] Unlocked! company_type:', userStore.user.company_type, 'metropolis:', userStore.settings.metropolis);
            }
        } catch(e) {
            // Silently fail - store not ready yet
        }
    }
    setInterval(unlockFeatures, 2000);
    setTimeout(unlockFeatures, 4000);

    var selectedTheme = 'Dark';

    // Only modify the base layers content, keep everything else
    function fixLayerControl() {
        var baseDiv = document.querySelector('.leaflet-control-layers-base');
        if (!baseDiv) return;
        if (baseDiv.dataset.fixed) return;
        baseDiv.dataset.fixed = 'true';

        // Remove locked labels, premium-span, and custom-separator
        baseDiv.querySelectorAll('label.locked').forEach(function(l) { l.remove(); });
        baseDiv.querySelectorAll('.premium-span').forEach(function(s) { s.remove(); });
        baseDiv.querySelectorAll('.custom-separator').forEach(function(s) { s.remove(); });

        // Add new premium label
        var premLabel = document.createElement('div');
        premLabel.style.cssText = 'color:#4ade80;font-size:11px;padding:4px 0;margin-top:4px;border-top:1px solid #444;';
        premLabel.textContent = 'Premium (unlocked)';
        baseDiv.appendChild(premLabel);

        // Add working theme options
        var themes = ['Dark', 'Light', 'Street', 'Satellite', 'City', 'Sky'];
        themes.forEach(function(name) {
            var label = document.createElement('label');
            var span1 = document.createElement('span');
            var radio = document.createElement('input');
            radio.type = 'radio';
            radio.className = 'leaflet-control-layers-selector';
            radio.name = 'leaflet-base-layers_678';
            var span2 = document.createElement('span');
            span2.textContent = ' ' + name;

            span1.appendChild(radio);
            span1.appendChild(span2);
            label.appendChild(span1);
            label.style.cursor = 'pointer';

            label.onclick = function(e) {
                e.preventDefault();
                e.stopPropagation();
                baseDiv.querySelectorAll('input[type=radio]').forEach(function(r) { r.checked = false; });
                radio.checked = true;
                selectedTheme = name;
                switchTheme(name);
            };

            baseDiv.appendChild(label);
        });

        console.log('[RebelShip] Layer control fixed!');
    }

    setTimeout(init, 2000);
    setTimeout(fixLayerControl, 2500);
    setInterval(fixLayerControl, 3000);
})();
";

            try
            {
                await WebView.CoreWebView2.ExecuteScriptAsync(mapUnlockScript);
            }
            catch
            {
                // Ignore injection errors
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
