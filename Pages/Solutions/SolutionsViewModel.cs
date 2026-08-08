using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

using Rasid.Models;
using Rasid.Pages.Base;
using Rasid.Services;

namespace Rasid.Pages.Solutions
{
	internal class SolutionsViewModel : PageViewModelBase
	{
		private readonly RasidApiClient _api;
		public ObservableCollection<SolutionCardViewModel> Solutions { get; } = new();

		private string _statusText = "Loading solutions...";
		public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

		public event System.Action<Models.ProjectItem> ProjectCreated;

		public SolutionsViewModel()
		{
			_api = new RasidApiClient(ApiClient.Instance);
			_ = LoadSolutionsAsync();
		}

		public async Task LoadSolutionsAsync()//download and display solutions from the API
		{
			try
			{
				var responseUrl = ApiClient.Instance.BaseUrl + "solutions/";

				var rawJson = await ApiClient.Instance.Raw.GetStringAsync(responseUrl);


				var solutions = await _api.GetSolutionsAsync();
				var sorted = solutions.OrderBy(s => s.Status != "prod").ThenBy(s => s.Name);

				Solutions.Clear();
				foreach (var solution in sorted)
				{
					var card = new SolutionCardViewModel
					{
						Id = solution.Id,
						Slug = solution.Slug,
						Name = solution.Name,
						DescriptionHtml = solution.DescriptionHtml,
						ImageUrl = solution.ImageUrl,
						ResultImageUrl = solution.ResultImageUrl,
						EuroPerKm2 = solution.EuroPerKm2,
						Status = solution.Status
					};

					card.Thumbnail = await ImageLoader.LoadAsync(
						solution.ImageUrl,
						ApiClient.Instance.ApiHost,
						500,
						260);

					card.CreateProjectCommand = new RelayCommand(
						() => OpenCreateProjectDialog(card),
						() => card.IsAvailable);

					Solutions.Add(card);
				}
				StatusText = Solutions.Count == 0 ? "No solutions found." : null;
			}
			catch (System.Exception ex)
			{
				StatusText = $"Failed to load solutions: {ex.Message}";
			}
		}
		private void OpenCreateProjectDialog(SolutionCardViewModel solution)
		{
			var viewmodel = new CreateProjectDialogViewModel(solution);
			var dialog = new CreateProjectDialogView
			{
				DataContext = viewmodel,
				Owner = FrameworkApplication.Current.MainWindow
			};
			viewmodel.RequestClose += result => dialog.DialogResult = result;
			if (dialog.ShowDialog() == true && viewmodel.CreatedProject != null)
			{
				ProjectCreated?.Invoke(viewmodel.CreatedProject);
			}
		}
	}
}
