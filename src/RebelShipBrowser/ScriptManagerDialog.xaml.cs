using System.Windows;
using System.Windows.Controls;
using RebelShipBrowser.Services;

namespace RebelShipBrowser
{
    public partial class ScriptManagerDialog : Window
    {
        private readonly UserScriptService _scriptService;

        public ScriptManagerDialog(UserScriptService scriptService)
        {
            InitializeComponent();
            _scriptService = scriptService;
            RefreshScriptList();
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
            UpdateButton.Content = "Updating...";

            try
            {
                var (updated, added, errors) = await _scriptService.UpdateScriptsFromGitHubAsync((current, total, fileName) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateButton.Content = $"Updating ({current}/{total})...";
                    });
                });

                RefreshScriptList();

                // Build result message
                var message = $"Update complete!\n\n";
                if (added > 0)
                {
                    message += $"New scripts: {added}\n";
                }
                if (updated > 0)
                {
                    message += $"Updated scripts: {updated}\n";
                }
                if (added == 0 && updated == 0)
                {
                    message += "All scripts are up to date.\n";
                }
                if (errors.Count > 0)
                {
                    message += $"\nErrors ({errors.Count}):\n";
                    foreach (var error in errors.Take(5))
                    {
                        message += $"- {error}\n";
                    }
                    if (errors.Count > 5)
                    {
                        message += $"... and {errors.Count - 5} more errors";
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
                UpdateButton.Content = "Update Scripts";
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
