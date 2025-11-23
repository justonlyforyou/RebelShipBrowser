using System;
using System.IO;
using System.Windows.Controls;

namespace RebelShipBrowser.Installer.Pages
{
    public partial class WelcomePage : Page
    {
        public static readonly string InstallPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RebelShipBrowser"
        );

        public WelcomePage()
        {
            InitializeComponent();
            InstallPathText.Text = InstallPath;
        }
    }
}
