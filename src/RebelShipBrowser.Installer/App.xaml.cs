using System.Windows;

namespace RebelShipBrowser.Installer
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);

            base.OnStartup(e);

            // Check if started in uninstall mode
            if (e.Args.Length > 0 && e.Args[0] == "/uninstall")
            {
                var uninstallWindow = new UninstallWindow();
                uninstallWindow.Show();
            }
            else
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
        }
    }
}
