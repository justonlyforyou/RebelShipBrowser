using System;
using System.IO;

namespace RebelShipBrowser.Installer
{
    public static class InstallerSettings
    {
        private static string _installPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RebelShipBrowser"
        );

        public static string InstallPath
        {
            get => _installPath;
            set => _installPath = value;
        }

        public static string UserScriptsPath => Path.Combine(InstallPath, "userscripts");

        public static string StartMenuPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            "RebelShip Browser.lnk"
        );

        public static string DesktopPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "RebelShip Browser.lnk"
        );

        public static string ExePath => Path.Combine(InstallPath, "RebelShipBrowser.exe");
    }
}
