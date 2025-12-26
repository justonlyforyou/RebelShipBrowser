using System.Windows.Controls;
using Microsoft.Win32;

namespace RebelShipBrowser.Installer.Pages
{
    public partial class WelcomePage : Page
    {
        public WelcomePage()
        {
            InitializeComponent();
            InstallPathText.Text = InstallerSettings.InstallPath;
        }

        private void BrowseButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Installation Folder",
                InitialDirectory = InstallPathText.Text
            };

            if (dialog.ShowDialog() == true)
            {
                var selectedPath = dialog.FolderName;
                // Append RebelShipBrowser if user selected a generic folder
                if (!selectedPath.EndsWith("RebelShipBrowser", System.StringComparison.OrdinalIgnoreCase))
                {
                    selectedPath = System.IO.Path.Combine(selectedPath, "RebelShipBrowser");
                }
                InstallPathText.Text = selectedPath;
                InstallerSettings.InstallPath = selectedPath;
            }
        }

        private void InstallPathText_TextChanged(object sender, TextChangedEventArgs e)
        {
            InstallerSettings.InstallPath = InstallPathText.Text;
        }
    }
}
