using System.Threading;

namespace RebelShipBrowser
{
    public partial class App : System.Windows.Application
    {
        private static Mutex? _mutex;
        private const string MutexName = "RebelShipBrowser_SingleInstance_Mutex";

        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            // Check if another instance is already running
            _mutex = new Mutex(true, MutexName, out bool createdNew);

            if (!createdNew)
            {
                // Another instance is already running
                System.Windows.MessageBox.Show(
                    "RebelShip Browser is already running.\n\nCheck your system tray for the existing instance.",
                    "RebelShip Browser",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information
                );
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(System.Windows.ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
