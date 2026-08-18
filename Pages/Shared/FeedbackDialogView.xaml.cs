using System.Windows;

namespace Rasid.Pages.Shared
{
    public partial class FeedbackDialogView : Window
    {
        public FeedbackDialogView()
        {
            InitializeComponent();
        }

		private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
