using System.Windows;

namespace RebelShipBrowser
{
    public enum LoginMethod
    {
        None,
        Steam,
        Browser
    }

    public partial class LoginMethodDialog : Window
    {
        public LoginMethod SelectedMethod { get; private set; } = LoginMethod.None;

        public LoginMethodDialog()
        {
            InitializeComponent();
        }

        private void SteamLoginButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedMethod = LoginMethod.Steam;
            DialogResult = true;
            Close();
        }

        private void BrowserLoginButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedMethod = LoginMethod.Browser;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedMethod = LoginMethod.None;
            DialogResult = false;
            Close();
        }
    }
}
