using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using RebelShipBrowser.Services;

namespace RebelShipBrowser
{
    public partial class ScriptManagerDialog : Window
    {
        private readonly UserScriptService _scriptService;
        private Storyboard? _pulseStoryboard;

        /// <summary>
        /// Indicates if any scripts were enabled or disabled during this session
        /// </summary>
        public bool ScriptsChanged { get; private set; }

        public ScriptManagerDialog(UserScriptService scriptService)
        {
            InitializeComponent();
            _scriptService = scriptService;
            RefreshScriptList();

            // Check for updates in background
            Loaded += async (s, e) => await CheckForUpdatesAsync();
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                var updatesAvailable = await _scriptService.CheckForUpdatesAsync();
                RefreshScriptList();

                if (updatesAvailable > 0)
                {
                    ShowUpdateNotification(updatesAvailable);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"[ScriptManagerDialog] Failed to check for updates: {ex.Message}");
            }
        }

        private void ShowUpdateNotification(int count)
        {
            // Show badge with count
            UpdateBadge.Visibility = Visibility.Visible;
            UpdateBadgeText.Text = count.ToString(System.Globalization.CultureInfo.InvariantCulture);

            // Change button background to orange
            UpdateButton.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#f59e0b"));

            // Start pulsing animation
            StartPulseAnimation();
        }

        private void StartPulseAnimation()
        {
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 0.6,
                Duration = TimeSpan.FromMilliseconds(800),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            _pulseStoryboard = new Storyboard();
            _pulseStoryboard.Children.Add(animation);
            Storyboard.SetTarget(animation, UpdateGlow);
            Storyboard.SetTargetProperty(animation, new PropertyPath(OpacityProperty));
            _pulseStoryboard.Begin();
        }

        private void StopPulseAnimation()
        {
            _pulseStoryboard?.Stop();
            UpdateGlow.Opacity = 0;
            UpdateBadge.Visibility = Visibility.Collapsed;
            UpdateButton.Background = (System.Windows.Media.Brush)FindResource("PrimaryColor");
        }

        private void RefreshScriptList()
        {
            _scriptService.LoadAllScripts();
            ScriptList.ItemsSource = null;
            ScriptList.ItemsSource = _scriptService.Scripts;

            EmptyMessage.Visibility = _scriptService.Scripts.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void ScriptList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var script = ScriptList.SelectedItem as Services.UserScript;
            var isCustom = script != null && !script.IsBundled;

            // Edit and Delete only for custom scripts
            EditButton.IsEnabled = isCustom;
            DeleteButton.IsEnabled = isCustom;
        }

        private void EnableToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.DataContext is UserScript script)
            {
                script.Enabled = checkBox.IsChecked ?? true;
                _scriptService.SaveScript(script);
                ScriptsChanged = true;
            }
        }

        private void NewButton_Click(object sender, RoutedEventArgs e)
        {
            // Create script with default template - user edits @name in code
            var script = _scriptService.CreateNewScript();
            RefreshScriptList();

            // Open editor for the new script
            OpenScriptEditor(script);
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (ScriptList.SelectedItem is UserScript script)
            {
                OpenScriptEditor(script);
            }
        }

        private void OpenScriptEditor(UserScript script)
        {
            var editor = new ScriptEditorDialog(script, _scriptService)
            {
                Owner = this
            };

            editor.ShowDialog();
            RefreshScriptList();

            if (editor.ScriptSaved)
            {
                ScriptsChanged = true;
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ScriptList.SelectedItem is UserScript script)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete '{script.Name}'?\n\nThis action cannot be undone.",
                    "Delete Script",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (result == MessageBoxResult.Yes)
                {
                    _scriptService.DeleteScript(script);
                    RefreshScriptList();
                }
            }
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateButton.IsEnabled = false;
            StopPulseAnimation();
            UpdateButtonText.Text = "Updating...";

            try
            {
                var (updated, added, deleted, errors) = await _scriptService.UpdateScriptsFromGitHubAsync((current, total, fileName) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateButtonText.Text = $"Updating ({current}/{total})...";
                    });
                });

                RefreshScriptList();

                // Mark as changed if any scripts were updated/added/deleted
                if (updated.Count > 0 || added.Count > 0 || deleted.Count > 0)
                {
                    ScriptsChanged = true;
                }

                // Build result message
                var message = "Update complete!\n\n";
                if (added.Count > 0)
                {
                    message += $"New scripts ({added.Count}):\n";
                    foreach (var name in added)
                    {
                        message += $"  + {name}\n";
                    }
                    message += "\n";
                }
                if (updated.Count > 0)
                {
                    message += $"Updated scripts ({updated.Count}):\n";
                    foreach (var name in updated)
                    {
                        message += $"  * {name}\n";
                    }
                    message += "\n";
                }
                if (deleted.Count > 0)
                {
                    message += $"Removed scripts ({deleted.Count}):\n";
                    foreach (var name in deleted)
                    {
                        message += $"  - {name}\n";
                    }
                    message += "\n";
                }
                if (added.Count == 0 && updated.Count == 0 && deleted.Count == 0)
                {
                    message += "All scripts are up to date.\n";
                }
                if (errors.Count > 0)
                {
                    message += $"Errors ({errors.Count}):\n";
                    foreach (var error in errors.Take(5))
                    {
                        message += $"  ! {error}\n";
                    }
                    if (errors.Count > 5)
                    {
                        message += $"  ... and {errors.Count - 5} more errors";
                    }
                }

                System.Windows.MessageBox.Show(
                    message,
                    "Script Update",
                    MessageBoxButton.OK,
                    errors.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to update scripts:\n\n{ex.Message}",
                    "Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                UpdateButton.IsEnabled = true;
                UpdateButtonText.Text = "Update Scripts";
            }
        }

        private void FolderButton_Click(object sender, RoutedEventArgs e)
        {
            UserScriptService.OpenScriptsDirectory();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
