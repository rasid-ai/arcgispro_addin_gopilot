using System.Linq;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using Rasid.Commands;
using Rasid.Models;
using Rasid.Services;

namespace Rasid.Pages.Solutions
{
	internal class CreateProjectDialogViewModel : PropertyChangedBase
	{
		private readonly RasidApiClient _api;
		private readonly SolutionCardViewModel _solution;

		public string SolutionName => _solution.Name;
		public string DescriptionHtml => _solution.DescriptionHtml;
		public string ResultImageUrl => _solution.ResultImageUrl;

		private BitmapImage _resultPreview;
		public BitmapImage ResultPreview
		{
			get => _resultPreview;
			set => SetProperty(ref _resultPreview, value);
		}

		public string Title { get; set; }
		public string Tags { get; set; }
		public Models.ProjectItem CreatedProject { get; private set; }

		public ICommand CreateCommand { get; }
		public event System.Action<bool> RequestClose;

		public CreateProjectDialogViewModel(SolutionCardViewModel solution)
		{
			_solution = solution;
			_api = new RasidApiClient(ApiClient.Instance);
			CreateCommand = new AsyncRelayCommand(CreateAsync);
			_ = LoadResultPreviewAsync();
		}

		private async Task LoadResultPreviewAsync()
		{
			if (string.IsNullOrWhiteSpace(ResultImageUrl))
				return;

			ResultPreview = await ImageLoader.LoadAsync(
				ResultImageUrl,
				ApiClient.Instance.ApiHost,
				900,
				700);
		}

		private async System.Threading.Tasks.Task CreateAsync()
		{
			if (string.IsNullOrWhiteSpace(Title))
			{
				ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Please enter a project title.", "Missing Title");
				return;
			}
			var tagIds = (Tags ?? "")
				.Split(' ', System.StringSplitOptions.RemoveEmptyEntries)
				.Select(token => int.TryParse(token, out var id) ? (int?)id : null)
				.Where(id => id.HasValue)
				.Select(id => id.Value)
				.ToList();

			try
			{
				CreatedProject = await _api.CreateProjectAsync(
					_solution.Slug,
					Title.Trim(),
					tagIds.Count > 0 ? tagIds : null);
				ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
					$"Project '{CreatedProject.Title}' created successfully!", "Project Created");
				RequestClose?.Invoke(true);
			}
			catch (System.Exception ex)
			{
				ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"Failed to create project:\n{ex.Message}", "Error");
			}
		}
	}
}
