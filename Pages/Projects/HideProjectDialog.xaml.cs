using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace Rasid.Pages.Projects
{
    public partial class HideProjectDialog : Window
    {
        public string ProjectTitle { get; }

        public HideProjectDialog(string projectTitle)
        {
            ProjectTitle = projectTitle;
            InitializeComponent();
            DataContext = this;
        }

        private void HiddenItemsLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
            e.Handled = true;
        }

        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void No_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
