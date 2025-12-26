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
