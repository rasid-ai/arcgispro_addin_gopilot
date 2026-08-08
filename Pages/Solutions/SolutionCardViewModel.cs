using System.Windows.Input;
using System.Windows.Media.Imaging;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace Rasid.Pages.Solutions
{
	internal class SolutionCardViewModel : ViewModelBase
	{
		public int Id { get; set; }

		public string Slug { get; set; }//internal key for solution, used in API calls

		public string Name { get; set; }

		public string DescriptionHtml { get; set; }

		public string ImageUrl { get; set; }

		public string ResultImageUrl { get; set; }

		public double EuroPerKm2 { get; set; }

		public string Status { get; set; }

		private BitmapImage _thumbnail;

		public BitmapImage Thumbnail
		{
			get => _thumbnail;
			set
			{
				_thumbnail = value;
				NotifyPropertyChanged();
			}
		}

		public string StatusLabel => Status switch
		{
			"prod" => "Available",
			"beta" => "Beta",
			"dev" => "Coming Soon",
			_ => Status
		};

		public bool IsAvailable => Status == "prod" || Status == "beta";

		public string PriceText => EuroPerKm2 > 0 
			? $"€{EuroPerKm2:F2} per km²" 
			: "Free";

		private ICommand _createProjectCommand;
		public ICommand CreateProjectCommand
		{
			get => _createProjectCommand;
			set => SetProperty(ref _createProjectCommand, value);
		}

	}
}
