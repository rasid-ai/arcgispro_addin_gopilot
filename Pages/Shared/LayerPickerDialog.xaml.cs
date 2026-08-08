using System.Windows;

namespace Rasid.Pages.Shared
{
	public partial class LayerPickerDialog : Window
	{
		public LayerPickerDialog()
		{
			InitializeComponent();
		}

		private void Ok_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
			Close();
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}
	}
}
