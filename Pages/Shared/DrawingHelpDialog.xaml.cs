using System.Windows;

namespace Rasid.Pages.Shared
{
	public partial class DrawingHelpDialog : Window
	{
		public DrawingHelpDialog()
		{
			InitializeComponent();
		}

		public bool DontShowAgain =>
			DontShowAgainCheckBox.IsChecked == true;

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
		}

		private void Draw_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
		}
	}
}
