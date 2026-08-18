using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Rasid.Pages.Processes
{
	internal class ProcessCardViewModel : ArcGIS.Desktop.Framework.Contracts.PropertyChangedBase
	{
		public Models.ProcessItem Process { get; set; }

		private string _statusLabel;
		public string StatusLabel { get => _statusLabel; set => SetProperty(ref _statusLabel, value); }

		private string _statusColor;
		public string StatusColor { get => _statusColor; set => SetProperty(ref _statusColor, value); }

		private bool _isSelected;
		public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

		private BitmapImage _thumbnail;
		public BitmapImage Thumbnail { get => _thumbnail; set => SetProperty(ref _thumbnail, value); }

		public ICommand SelectCommand { get; set; }
		public ICommand HideCommand { get; set; }
	}
}
