using System.Windows;
using System.Windows.Controls;
using ArcGIS.Desktop.Mapping;

namespace Rasid.Pages.Shared
{
	public partial class LayerPickerDialog : Window
	{
		public LayerPickerDialog()
		{
			InitializeComponent();
		}

		private void LayerList_SelectionChanged(
			object sender,
			SelectionChangedEventArgs e)
		{
			if (DataContext is LayerPickerViewModel viewModel &&
				LayerList.SelectedItem is FeatureLayer selectedLayer)
			{
				viewModel.SelectedLayer = selectedLayer;
			}
		}

		private void Ok_Click(object sender, RoutedEventArgs e)
		{
			if (DataContext is not LayerPickerViewModel viewModel ||
				LayerList.SelectedItem is not FeatureLayer selectedLayer)
			{
				return;
			}

			viewModel.SelectedLayer = selectedLayer;
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
