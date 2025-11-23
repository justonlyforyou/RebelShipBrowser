using System.IO;
using System.Windows;
using RebelShipBrowser.Installer.Pages;

namespace RebelShipBrowser.Installer
{
    public partial class MainWindow : Window
    {
        private int _currentPage;
        private readonly WelcomePage _welcomePage;
        private readonly InstallPage _installPage;
        private readonly CompletePage _completePage;

        public MainWindow()
        {
            InitializeComponent();

            _welcomePage = new WelcomePage();
            _installPage = new InstallPage();
            _completePage = new CompletePage();

            NavigateToPage(0);
        }

        private void NavigateToPage(int pageIndex)
        {
            _currentPage = pageIndex;

            switch (pageIndex)
            {
                case 0:
                    ContentFrame.Content = _welcomePage;
                    BackButton.Visibility = Visibility.Collapsed;
                    NextButton.Content = "Install";
                    NextButton.IsEnabled = true;
                    CancelButton.IsEnabled = true;
                    break;

                case 1:
                    ContentFrame.Content = _installPage;
                    BackButton.Visibility = Visibility.Collapsed;
                    NextButton.Content = "Installing...";
                    NextButton.IsEnabled = false;
                    CancelButton.IsEnabled = false;
                    StartInstallation();
                    break;

                case 2:
                    ContentFrame.Content = _completePage;
                    BackButton.Visibility = Visibility.Collapsed;
                    NextButton.Content = "Finish";
                    NextButton.IsEnabled = true;
                    CancelButton.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private async void StartInstallation()
        {
            try
            {
                await _installPage.RunInstallationAsync();
                NavigateToPage(2);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Installation failed:\n\n{ex.Message}",
                    "Installation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                Application.Current.Shutdown();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to cancel the installation?",
                "Cancel Installation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 0)
            {
                NavigateToPage(_currentPage - 1);
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage == 2)
            {
                // Finish button - launch app and close
                if (_completePage.LaunchCheckbox?.IsChecked == true)
                {
                    LaunchApplication();
                }
                // Properly shutdown the application
                Application.Current.Shutdown();
            }
            else
            {
                NavigateToPage(_currentPage + 1);
            }
        }

        private static void LaunchApplication()
        {
            try
            {
                var installPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RebelShipBrowser",
                    "RebelShipBrowser.exe"
                );

                if (File.Exists(installPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = installPath,
                        UseShellExecute = true
                    });
                }
            }
            catch
            {
                // Ignore launch errors
            }
        }
    }
}
