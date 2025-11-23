using System.Windows.Controls;

namespace RebelShipBrowser.Installer.Pages
{
    public partial class CompletePage : Page
    {
        public CheckBox LaunchCheckbox => (CheckBox)FindName("LaunchAppCheckbox");

        public CompletePage()
        {
            InitializeComponent();
        }
    }
}
